Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Linq
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Export
Imports Omskep.Core.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Settings
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Export

    ''' <summary>
    ''' Hostile-input coverage for ExportEngine and (indirectly, through the fake) SynthesizeAsync. Closes
    ''' the test gap flagged at the end of phase 3a: 401/429/5xx/offline/cut-off/cancel/resume/retry
    ''' exhaustion, no key in any message, join exactness, and the write-failure distinction.
    '''
    ''' Two open findings from writing these, not fixed here (see the phase 3b handoff):
    ''' - Cancellation is only ever returned as a clean ExportStatus.Cancelled when it is observed between
    '''   chunks. Cancellation while a chunk is in flight (mid-request or mid-retry-wait) throws
    '''   OperationCanceledException out of ExportAsync instead -- see
    '''   Export_CancelledDuringRetryWait_ThrowsOperationCanceledException below.
    ''' - "every chunk succeeded, only the write failed" is still plain ExportStatus.FailedResumable,
    '''   distinguishable only by every chunk being Done plus the message text -- see
    '''   Export_AllChunksSucceed_ButWriteFails_... below. A dedicated status is hardening item 2.
    ''' </summary>
    <TestClass>
    Public Class ExportEngineTests

        Private Const EngineKey As String = "EXPORTKEY0123456789abcdef"
        Private Shared ReadOnly NoToken As CancellationToken = CancellationToken.None
        Private Shared ReadOnly FastDelay As TimeSpan = TimeSpan.FromMilliseconds(10)

        ' ---- test rig: a real, fully-working AppSession (CanExport = True) on a temp folder ----
        ' Adapted from AppSessionTests.Rig. ExportEngine's own ISecretStore is deliberately a *separate*
        ' InMemorySecretStore per test, not the session's internal one: the session's key only has to be
        ' good enough to make Access.CanExport true once, up front; the engine's own key is what each test
        ' controls to simulate it vanishing mid-export, being rejected, etc.

        Private NotInheritable Class Rig
            Implements IDisposable

            Private Const RigRegion As String = "southafricanorth"
            Private Const RigKey As String = "RIGKEY0123456789abcdef"

            Public ReadOnly Property Dir As Support.TempDir
            Public ReadOnly Property Monitor As New ConnectionMonitor()
            Public ReadOnly Property App As AppSession

            Public Sub New()
                Dir = New Support.TempDir()
                Dim settings As New SettingsStore(Dir.FolderPath, Nothing, Nothing, New Integer() {0})
                Dim keyStore As New FileSecretStore(settings, New Support.FakeSecretProtector())
                Dim cacheFile As New VoiceCacheStore(Dir.FolderPath)
                Dim voiceClient = Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1))
                Dim voiceSvc As New VoiceService(voiceClient, cacheFile, Monitor)
                App = New AppSession(settings, keyStore, voiceSvc, Monitor, voiceClient)
            End Sub

            ''' <summary>Drives the session to Access.CanExport = True: key saved, region tested, voices cached.</summary>
            Public Async Function MakeWorkingAsync() As Task
                Await App.StartupAsync(NoToken)
                Dim r = Await App.TestConnectionAsync(RigRegion, RigKey, NoToken)
                If Not r.Success Then Throw New InvalidOperationException("Rig setup failed: " & r.Message)
            End Function

            Public Sub Dispose() Implements IDisposable.Dispose
                Dir.Dispose()
            End Sub
        End Class

        ' ---- small helpers ----

        Private Shared Function Chunks(count As Integer) As List(Of DocumentChunk)
            Dim list As New List(Of DocumentChunk)
            For i = 0 To count - 1
                list.Add(New DocumentChunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='af-ZA-WillemNeural'>Chunk " & i.ToString() & ".</voice></speak>", 1.0))
            Next
            Return list
        End Function

        Private Shared Function Audio(fill As Byte, Optional length As Integer = ExportEngine.MinPlausibleAudioBytes + 100) As Byte()
            Dim b(length - 1) As Byte
            For i = 0 To length - 1
                b(i) = fill
            Next
            Return b
        End Function

        Private Shared Function NewSecrets() As Support.InMemorySecretStore
            Dim s As New Support.InMemorySecretStore()
            s.Save(EngineKey)
            Return s
        End Function

        ''' <summary>A synthesis responder that returns each given result in order, repeating the last one for any further calls.</summary>
        Private Shared Function Sequenced(ParamArray results As SynthesisResult()) As Support.FakeSpeechClient
            Dim callIndex As Integer = 0
            Return New Support.FakeSpeechClient(
                Function(ct) Task.FromResult(Support.FakeSpeechClient.Ok(1)),
                Function(ssml, region, key, ct)
                    Dim r = results(Math.Min(callIndex, results.Length - 1))
                    callIndex += 1
                    Return Task.FromResult(r)
                End Function)
        End Function

        ' ================= happy path =================

        <TestMethod>
        Public Async Function Export_AllChunksSucceed_JoinsBytesExactly_WritesFile_ReturnsComplete() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim a1 = Audio(&H11)
                    Dim a2 = Audio(&H22)
                    Dim client = Sequenced(Support.FakeSpeechClient.OkSynthesis(a1), Support.FakeSpeechClient.OkSynthesis(a2))
                    Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor)

                    Dim result = Await engine.ExportAsync(Chunks(2), testpath, NoToken)

                    Assert.AreEqual(ExportStatus.Complete, result.Status)
                    Assert.AreEqual(testpath, result.OutputPath)
                    Assert.HasCount(2, result.Chunks)
                    Assert.IsTrue(result.Chunks.All(Function(c) c.Status = ChunkExportStatus.Done))
                    CollectionAssert.AreEqual(a1.Concat(a2).ToArray(), File.ReadAllBytes(testpath))
                End Using
            End Using
        End Function

        ' ================= 401: rejected, no retry, sticky lockout =================

        <TestMethod>
        Public Async Function Export_KeyRejected401_FailsWithoutRetrying_AndLocksFurtherExports() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim client = Sequenced(Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Rejected, 401))
                    Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor)

                    Dim result = Await engine.ExportAsync(Chunks(1), testpath, NoToken)

                    Assert.AreEqual(ExportStatus.FailedResumable, result.Status)
                    Assert.HasCount(1, client.SynthesisCalls)
                    Assert.AreEqual(ChunkExportStatus.Failed, result.Chunks(0).Status)
                    Assert.AreEqual(0, result.Chunks(0).RetriesUsed)
                    Assert.IsFalse(File.Exists(testpath))
                    Assert.IsFalse(rig.App.Access.CanExport)
                End Using
            End Using
        End Function

        ' ================= 429 / offline / 5xx / timeout: retry, then recover =================

        <TestMethod>
        Public Async Function Export_TransientFailures_RetryThenSucceed_MarksPossiblyBilledTwice() As Task
            Dim scenarios As (Outcome As AzureOutcome, Status As Integer)() = {
                (AzureOutcome.Transient, 429),
                (AzureOutcome.Offline, 0),
                (AzureOutcome.Transient, 500),
                (AzureOutcome.Transient, 408)
            }
            For Each scenario In scenarios
                Using rig As New Rig()
                    Await rig.MakeWorkingAsync()
                    Using outDir As New Support.TempDir()
                        Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                        Dim testaudio = Audio(&H33)
                        Dim client = Sequenced(
                            Support.FakeSpeechClient.FailSynthesis(scenario.Outcome, scenario.Status),
                            Support.FakeSpeechClient.OkSynthesis(testaudio))
                        Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor, FastDelay)

                        Dim result = Await engine.ExportAsync(Chunks(1), testpath, NoToken)

                        Assert.AreEqual(ExportStatus.Complete, result.Status, scenario.ToString())
                        Assert.AreEqual(1, result.Chunks(0).RetriesUsed, scenario.ToString())
                        Assert.IsTrue(result.Chunks(0).PossiblyBilledTwice, scenario.ToString())
                        Assert.HasCount(2, client.SynthesisCalls, scenario.ToString())
                    End Using
                End Using
            Next
        End Function

        ' ================= retry exhaustion =================

        <TestMethod>
        Public Async Function Export_PersistentServerError_FailsAfterMaxRetries_NamesWhichChunkFailed_LeavesRestPending() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim client = Sequenced(Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Failed, 500))
                    Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor, FastDelay)

                    Dim result = Await engine.ExportAsync(Chunks(2), testpath, NoToken)

                    Assert.AreEqual(ExportStatus.FailedResumable, result.Status)
                    Assert.AreEqual(ExportEngine.MaxRetriesPerChunk, result.Chunks(0).RetriesUsed)
                    Assert.AreEqual(ChunkExportStatus.Failed, result.Chunks(0).Status)
                    Assert.AreEqual(ChunkExportStatus.Pending, result.Chunks(1).Status)
                    Assert.IsTrue(result.Message.Contains("Chunk 1 of 2", StringComparison.Ordinal))
                    Assert.IsFalse(File.Exists(testpath))
                End Using
            End Using
        End Function

        ' ================= cut-off / implausibly short response =================

        <TestMethod>
        Public Async Function Export_ImplausiblyShortAudio_RetriesThenFails_NeverWritesAFile() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim tooShort = Audio(&H44, 500)   ' under MinPlausibleAudioBytes
                    Dim client = Sequenced(Support.FakeSpeechClient.OkSynthesis(tooShort))
                    Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor, FastDelay)

                    Dim result = Await engine.ExportAsync(Chunks(1), testpath, NoToken)

                    Assert.AreEqual(ExportStatus.FailedResumable, result.Status)
                    Assert.AreEqual(ExportEngine.MaxRetriesPerChunk, result.Chunks(0).RetriesUsed)
                    Assert.IsTrue(result.Chunks(0).FailureReason.Contains("truncated", StringComparison.Ordinal))
                    Assert.IsFalse(File.Exists(testpath))
                End Using
            End Using
        End Function

        ' ================= cancellation =================

        <TestMethod>
        Public Async Function Export_CancelledBetweenChunks_ReturnsCancelled_RemainingChunksPending() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Using cts As New CancellationTokenSource()
                        Dim client = New Support.FakeSpeechClient(
                            Function(ct) Task.FromResult(Support.FakeSpeechClient.Ok(1)),
                            Function(ssml, region, key, ct)
                                cts.Cancel()   ' request cancellation once the first chunk's call has completed
                                Return Task.FromResult(Support.FakeSpeechClient.OkSynthesis(Audio(&H66)))
                            End Function)
                        Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor)

                        Dim result = Await engine.ExportAsync(Chunks(2), testpath, cts.Token)

                        Assert.AreEqual(ExportStatus.Cancelled, result.Status)
                        Assert.AreEqual(ChunkExportStatus.Done, result.Chunks(0).Status)
                        Assert.AreEqual(ChunkExportStatus.Pending, result.Chunks(1).Status)
                        Assert.IsFalse(File.Exists(testpath))
                    End Using
                End Using
            End Using
        End Function

        ''' <summary>Documents the open finding in the class remarks: cancellation while a chunk is in
        ''' flight (here, during a retry wait) is NOT a clean Cancelled result -- it throws.</summary>
        <TestMethod>
        Public Async Function Export_CancelledDuringRetryWait_ThrowsOperationCanceledException() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Using cts As New CancellationTokenSource()
                        Dim client = Sequenced(Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Transient, 429))
                        Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor, TimeSpan.FromSeconds(5))

                        Dim exportTask = engine.ExportAsync(Chunks(1), testpath, cts.Token)
                        cts.CancelAfter(TimeSpan.FromMilliseconds(50))   ' cancel mid retry-wait

                        Await Assert.ThrowsAsync(Of OperationCanceledException)(Function() exportTask)
                    End Using
                End Using
            End Using
        End Function

        ' ================= resume =================

        <TestMethod>
        Public Async Function Export_Resume_SkipsAlreadyDoneChunks_OnlySendsTheRest() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim a1 = Audio(&H77)
                    Dim a2 = Audio(&H88)

                    ' First attempt: chunk 1 succeeds, chunk 2 fails for good after exhausting retries (NOT a 401 --
                    ' that sets a sticky rejection on the shared session/monitor and would correctly block attempt two).
                    Dim firstClient = Sequenced(Support.FakeSpeechClient.OkSynthesis(a1), Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Failed, 500))
                    Dim firstEngine As New ExportEngine(firstClient, NewSecrets(), rig.App, rig.Monitor, FastDelay)
                    Dim firstResult = Await firstEngine.ExportAsync(Chunks(2), testpath, NoToken)
                    Assert.AreEqual(ExportStatus.FailedResumable, firstResult.Status)
                    Assert.AreEqual(ChunkExportStatus.Done, firstResult.Chunks(0).Status)

                    ' A fresh engine/client models the next export attempt, in a later session.
                    Dim secondClient = Sequenced(Support.FakeSpeechClient.OkSynthesis(a2))
                    Dim secondEngine As New ExportEngine(secondClient, NewSecrets(), rig.App, rig.Monitor)
                    Dim secondResult = Await secondEngine.ExportAsync(Chunks(2), testpath, NoToken, priorAttempt:=firstResult)

                    Assert.AreEqual(ExportStatus.Complete, secondResult.Status)
                    Assert.HasCount(1, secondClient.SynthesisCalls)   ' chunk 1 was never re-sent
                    CollectionAssert.AreEqual(a1.Concat(a2).ToArray(), File.ReadAllBytes(testpath))
                End Using
            End Using
        End Function

        ' ================= key vanishes mid-export =================

        <TestMethod>
        Public Async Function Export_KeyVanishesMidExport_FailsTheNextChunk_WithoutReachingAzureAgain() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim secrets = NewSecrets()
                    Dim client = New Support.FakeSpeechClient(
                        Function(ct) Task.FromResult(Support.FakeSpeechClient.Ok(1)),
                        Function(ssml, region, key, ct)
                            secrets.Clear()   ' key removed right after chunk 1 is sent
                            Return Task.FromResult(Support.FakeSpeechClient.OkSynthesis(Audio(&H99)))
                        End Function)
                    Dim engine As New ExportEngine(client, secrets, rig.App, rig.Monitor)

                    Dim result = Await engine.ExportAsync(Chunks(2), testpath, NoToken)

                    Assert.AreEqual(ExportStatus.FailedResumable, result.Status)
                    Assert.AreEqual(ChunkExportStatus.Done, result.Chunks(0).Status)
                    Assert.AreEqual(ChunkExportStatus.Failed, result.Chunks(1).Status)
                    Assert.IsTrue(result.Chunks(1).FailureReason.Contains("No Azure key", StringComparison.Ordinal))
                    Assert.HasCount(1, client.SynthesisCalls)   ' chunk 2 never reached Azure at all
                End Using
            End Using
        End Function

        ' ================= write succeeds for every chunk, but the final write fails =================

        <TestMethod>
        Public Async Function Export_AllChunksSucceed_ButWriteFails_KeepsChunksDone_DoesNotResynthesizeOnRetry() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    ' An ancestor path component that is itself an existing file reliably fails the write
                    ' (Directory.CreateDirectory / the temp-file write throws IOException) without needing
                    ' AtomicFile's own swap seam, which ExportEngine does not expose.
                    Dim blocker = Path.Combine(outDir.FolderPath, "blocker.txt")
                    File.WriteAllText(blocker, "not a folder")
                    Dim badPath = Path.Combine(blocker, "study.mp3")

                    Dim testaudio = Audio(&HAA)
                    Dim client = Sequenced(Support.FakeSpeechClient.OkSynthesis(testaudio))
                    Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor)

                    Dim result = Await engine.ExportAsync(Chunks(1), badPath, NoToken)

                    Assert.AreEqual(ExportStatus.FailedWriteOnly, result.Status)
                    Assert.AreEqual(ChunkExportStatus.Done, result.Chunks(0).Status)   ' distinct from a chunk failure
                    Assert.IsTrue(result.Message.Contains("could not be written", StringComparison.OrdinalIgnoreCase))

                    ' Retrying at a good path should only retry the write, not re-synthesize the finished chunk.
                    Dim goodPath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim result2 = Await engine.ExportAsync(Chunks(1), goodPath, NoToken, priorAttempt:=result)
                    Assert.AreEqual(ExportStatus.Complete, result2.Status)
                    Assert.HasCount(1, client.SynthesisCalls)
                    CollectionAssert.AreEqual(testaudio, File.ReadAllBytes(goodPath))
                End Using
            End Using
        End Function

        ' ================= must never: the key leaks into a message =================

        <TestMethod>
        Public Async Function Messages_NeverContainTheEngineKey_OnAnyFailure() As Task
            Dim scenarios As SynthesisResult() = {
                Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Rejected, 401),
                Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Offline, 0),
                Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Transient, 429),
                Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Failed, 403)
            }
            For Each scenario In scenarios
                Using rig As New Rig()
                    Await rig.MakeWorkingAsync()
                    Using outDir As New Support.TempDir()
                        Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                        Dim client = Sequenced(scenario)
                        Dim engine As New ExportEngine(client, NewSecrets(), rig.App, rig.Monitor, FastDelay)

                        Dim result = Await engine.ExportAsync(Chunks(1), testpath, NoToken)

                        Assert.IsFalse(result.Message.Contains(EngineKey, StringComparison.Ordinal), scenario.Outcome.ToString())
                        Assert.IsFalse(result.Chunks(0).FailureReason.Contains(EngineKey, StringComparison.Ordinal), scenario.Outcome.ToString())
                    End Using
                End Using
            Next
        End Function

        ' ================= construction =================

        <TestMethod>
        Public Sub Constructor_NullArguments_Throw()
            Using rig As New Rig()
                Dim secrets = NewSecrets()
                Dim client = Support.FakeSpeechClient.ReturningSynthesis(Support.FakeSpeechClient.OkSynthesis(Audio(&H1)))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub()
                                                                   Dim unused = New ExportEngine(Nothing, secrets, rig.App, rig.Monitor)
                                                               End Sub)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub()
                                                                   Dim unused = New ExportEngine(client, Nothing, rig.App, rig.Monitor)
                                                               End Sub)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub()
                                                                   Dim unused = New ExportEngine(client, secrets, Nothing, rig.Monitor)
                                                               End Sub)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub()
                                                                   Dim unused = New ExportEngine(client, secrets, rig.App, Nothing)
                                                               End Sub)
            End Using
        End Sub

        <TestMethod>
        Public Async Function Export_Resume_WithEditedChunk_ReSynthesizesInsteadOfReusingStaleAudio() As Task
            Using rig As New Rig()
                Await rig.MakeWorkingAsync()
                Using outDir As New Support.TempDir()
                    Dim testpath = Path.Combine(outDir.FolderPath, "study.mp3")
                    Dim a1 = Audio(&HBB)
                    Dim a2 = Audio(&HCC)

                    Dim firstClient = Sequenced(Support.FakeSpeechClient.OkSynthesis(a1), Support.FakeSpeechClient.FailSynthesis(AzureOutcome.Failed, 500))
                    Dim firstEngine As New ExportEngine(firstClient, NewSecrets(), rig.App, rig.Monitor, FastDelay)
                    Dim originalChunks = Chunks(2)
                    Dim firstResult = Await firstEngine.ExportAsync(originalChunks, testpath, NoToken)
                    Assert.AreEqual(ExportStatus.FailedResumable, firstResult.Status)
                    Assert.AreEqual(ChunkExportStatus.Done, firstResult.Chunks(0).Status)

                    ' The document is edited after the failed attempt: chunk 0's text changes before the retry.
                    Dim editedChunks As New List(Of DocumentChunk) From {
                New DocumentChunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='af-ZA-WillemNeural'>Edited chunk 0.</voice></speak>", 1.0),
                originalChunks(1)
            }

                    Dim secondAudio0 = Audio(&HDD)
                    Dim secondClient = Sequenced(Support.FakeSpeechClient.OkSynthesis(secondAudio0), Support.FakeSpeechClient.OkSynthesis(a2))
                    Dim secondEngine As New ExportEngine(secondClient, NewSecrets(), rig.App, rig.Monitor)
                    Dim secondResult = Await secondEngine.ExportAsync(editedChunks, testpath, NoToken, priorAttempt:=firstResult)

                    Assert.AreEqual(ExportStatus.Complete, secondResult.Status)
                    Assert.HasCount(2, secondClient.SynthesisCalls)   ' chunk 0 was re-sent, not reused -- its text changed
                    CollectionAssert.AreEqual(secondAudio0.Concat(a2).ToArray(), File.ReadAllBytes(testpath))
                End Using
            End Using
        End Function

    End Class

End Namespace
