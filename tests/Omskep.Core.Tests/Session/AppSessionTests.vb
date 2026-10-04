Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Settings
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Session

    <TestClass>
    Public Class AppSessionTests

        Private Const SaRegion As String = "southafricanorth"
        Private Const GoodKey As String = "GOODKEY0123456789abcdef"
        Private Shared ReadOnly NoToken As CancellationToken = CancellationToken.None

        ' ---- test rig: real stores on a temp folder, fake Azure ----

        Private NotInheritable Class Rig
            Implements IDisposable

            Public ReadOnly Property Dir As Support.TempDir
            Public ReadOnly Property Protector As New Support.FakeSecretProtector()
            Public Property SettingsLocked As Boolean
            Public ReadOnly Property Store As SettingsStore
            Public ReadOnly Property KeyStore As FileSecretStore
            Public ReadOnly Property CacheFile As VoiceCacheStore
            Public Property Monitor As ConnectionMonitor
            Public Property Client As Support.FakeSpeechClient
            Public Property VoiceSvc As VoiceService
            Public Property App As AppSession

            Public Sub New(client As Support.FakeSpeechClient)
                Dir = New Support.TempDir()
                Dim reader As Func(Of String, Byte()) =
                    Function(p As String) As Byte()
                        If SettingsLocked Then Throw New IOException("locked")
                        Return File.ReadAllBytes(p)
                    End Function
                Store = New SettingsStore(Dir.FolderPath, Nothing, reader, New Integer() {0})
                KeyStore = New FileSecretStore(Store, Protector)
                CacheFile = New VoiceCacheStore(Dir.FolderPath)
                Restart(client)
            End Sub

            ''' <summary>Models closing and reopening the app: fresh in-memory state, same files.</summary>
            Public Sub Restart(client As Support.FakeSpeechClient)
                Me.Client = client
                Monitor = New ConnectionMonitor()
                VoiceSvc = New VoiceService(client, CacheFile, Monitor)
                App = New AppSession(Store, KeyStore, VoiceSvc, Monitor)
            End Sub

            Public Sub Dispose() Implements IDisposable.Dispose
                Dir.Dispose()
            End Sub
        End Class

        Private Shared Function Always(result As VoiceListResult) As Support.FakeSpeechClient
            Return Support.FakeSpeechClient.Returning(result)
        End Function

        ''' <summary>Accepts only GoodKey (any region); every other key is a 401.</summary>
        Private Shared Function KeyedClient(Optional voices As Integer = 5) As Support.FakeSpeechClient
            Return New Support.FakeSpeechClient(
                Function(r, k, ct) Task.FromResult(If(k = GoodKey, Support.FakeSpeechClient.Ok(voices), Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401))))
        End Function

        ''' <summary>First-time setup done properly: key saved, region saved, cache saved, fully unlocked.</summary>
        Private Shared Async Function WorkingAsync(rig As Rig) As Task
            Await rig.App.StartupAsync(NoToken)
            Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)
            Assert.IsTrue(r.Success, r.Message)
        End Function

        Private Shared Async Function RestartWithAsync(rig As Rig, client As Support.FakeSpeechClient) As Task
            rig.Restart(client)
            Await rig.App.StartupAsync(NoToken)
        End Function

        Private Shared Sub AssertLocked(app As AppSession, reason As AccessReason)
            Dim a = app.Access
            Assert.AreEqual(AccessMode.Locked, a.Mode)
            Assert.AreEqual(reason, a.Reason)
            Assert.IsFalse(a.CanEdit)
            Assert.IsFalse(a.CanExport)
        End Sub

        ' ================= startup =================

        <TestMethod>
        Public Async Function Startup_NoSettings_IsLockedNoKey_AndTouchesNoNetwork() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                Await rig.App.StartupAsync(NoToken)
                AssertLocked(rig.App, AccessReason.NoKey)
                Assert.IsFalse(rig.App.KeyIsSaved)
                Assert.IsFalse(rig.App.CanRetry)
                Assert.AreEqual(SaRegion, rig.App.Region)
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_KeyAndCache_IsFull_WithoutTouchingTheNetwork() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)

                Dim second = Always(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0))
                Await RestartWithAsync(rig, second)

                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
                Assert.AreEqual(5, rig.App.VoiceCount)
                Assert.IsTrue(rig.App.KeyIsSaved)
                Assert.IsEmpty(second.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_KeyButNoCache_FetchesOnce_WithStoredKeyAndRegion() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                rig.KeyStore.Save(GoodKey)
                rig.Store.Update(Sub(s) s.Azure.Region = "westeurope")

                Await rig.App.StartupAsync(NoToken)

                Assert.HasCount(1, rig.Client.Calls)
                Assert.AreEqual("westeurope", rig.Client.Calls(0)(0))
                Assert.AreEqual(GoodKey, rig.Client.Calls(0)(1))
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
                Assert.IsTrue(File.Exists(rig.CacheFile.CachePath))
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_KeyNoCache_Offline_IsLockedWithRetry_KeyKept_ThenRetrySucceeds() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0)))
                rig.KeyStore.Save(GoodKey)
                Await rig.App.StartupAsync(NoToken)

                AssertLocked(rig.App, AccessReason.Offline)
                Assert.IsTrue(rig.App.CanRetry)
                Assert.IsTrue(rig.App.KeyIsSaved)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)

                rig.Restart(Always(Support.FakeSpeechClient.Ok(5)))
                Await rig.App.RetryAsync(NoToken)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_KeyNoCache_Rejected_IsLockedWithoutRetry_KeyKept() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                rig.KeyStore.Save(GoodKey)
                Await rig.App.StartupAsync(NoToken)

                AssertLocked(rig.App, AccessReason.KeyRejected)
                Assert.AreEqual("Azure rejected the key or region. Check both.", rig.App.Access.Message)
                Assert.IsFalse(rig.App.CanRetry)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_KeyNoCache_429_IsLockedWithRetry_NeverCalledRejected() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Transient, 429)))
                rig.KeyStore.Save(GoodKey)
                Await rig.App.StartupAsync(NoToken)
                AssertLocked(rig.App, AccessReason.AzureTemporarilyUnavailable)
                Assert.IsTrue(rig.App.CanRetry)
                Assert.IsFalse(rig.App.Access.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_UnreadableBlob_IsLockedKeyUnreadable_AndLeavesTheFileAlone() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                rig.Store.Update(Sub(s) s.Azure.KeyBlob = "AAAA")
                Await rig.App.StartupAsync(NoToken)

                AssertLocked(rig.App, AccessReason.KeyUnreadable)
                Assert.IsFalse(rig.App.CanRetry)
                Assert.IsEmpty(rig.Client.Calls)
                Assert.AreEqual("AAAA", rig.Store.Load().Value.Azure.KeyBlob)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_DamagedSettingsFile_IsLockedWithPlainMessage() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                File.WriteAllText(rig.Store.SettingsPath, "{{ garbage")
                Await rig.App.StartupAsync(NoToken)
                AssertLocked(rig.App, AccessReason.SettingsFileDamaged)
                Assert.IsFalse(rig.App.CanRetry)
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_SettingsFileLocked_IsLockedWithRetry_ThenRecovers() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)

                rig.Restart(Always(Support.FakeSpeechClient.Ok(5)))
                rig.SettingsLocked = True
                Await rig.App.StartupAsync(NoToken)
                AssertLocked(rig.App, AccessReason.SettingsUnreadable)
                Assert.IsTrue(rig.App.CanRetry)
                Assert.IsEmpty(rig.Client.Calls)

                rig.SettingsLocked = False
                Await rig.App.RetryAsync(NoToken)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
                Assert.IsTrue(rig.App.KeyIsSaved)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_CacheForAnotherRegion_IsIgnored_AndRefetched() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(3)))
                rig.KeyStore.Save(GoodKey)
                rig.CacheFile.Save(New VoiceCache() With {.Region = "westeurope", .FetchedUtc = DateTime.UtcNow, .Voices = Support.FakeSpeechClient.Voices(9)})

                Await rig.App.StartupAsync(NoToken)

                Assert.HasCount(1, rig.Client.Calls)
                Assert.AreEqual(3, rig.App.VoiceCount)
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_WhileAFetchIsRunning_DoesNotStartASecondOne() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Hanging())
                rig.KeyStore.Save(GoodKey)
                Using cts As New CancellationTokenSource()
                    Dim first = rig.App.StartupAsync(cts.Token)
                    Assert.IsTrue(rig.App.IsBusy)

                    Await rig.App.StartupAsync(NoToken)
                    Assert.HasCount(1, rig.Client.Calls)

                    cts.Cancel()
                    Try
                        Await first
                    Catch ex As OperationCanceledException
                        ' expected
                    End Try
                    Assert.IsFalse(rig.App.IsBusy)
                End Using
            End Using
        End Function

        <TestMethod>
        Public Async Function Startup_RaisesStateChanged_AndBusyIsVisibleDuringTheFetch() As Task
            Dim gate As New TaskCompletionSource(Of Boolean)()
            Dim held As New Support.FakeSpeechClient(
                Async Function(region, key, ct) As Task(Of VoiceListResult)
                    Await gate.Task   ' a real request takes time: hold it open until the test releases it
                    Return Support.FakeSpeechClient.Ok(5)
                End Function)
            Using rig As New Rig(held)
                rig.KeyStore.Save(GoodKey)
                Dim busySeen As New List(Of Boolean)()
                AddHandler rig.App.StateChanged, Sub(s, e) busySeen.Add(rig.App.IsBusy)

                Dim startup = rig.App.StartupAsync(NoToken)
                Assert.Contains(True, busySeen, "busy should be visible while the request is open")
                Assert.IsTrue(rig.App.IsBusy)

                gate.SetResult(True)
                Await startup

                Assert.IsFalse(busySeen(busySeen.Count - 1))
                Assert.IsFalse(rig.App.IsBusy)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
            End Using
        End Function

        ' ================= test connection: validation =================

        <TestMethod>
        Public Async Function Test_InvalidRegion_Fails_AndSendsNothing() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                For Each bad In {"", "evil.com/", "a b", "x#"}
                    Dim r = Await rig.App.TestConnectionAsync(bad, GoodKey, NoToken)
                    Assert.IsFalse(r.Success)
                    Assert.AreEqual(ConnectionMessages.InvalidRegion, r.Message)
                Next
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_KeyWithSpaces_Fails_AndSendsNothing() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "abc def", NoToken)
                Assert.IsFalse(r.Success)
                Assert.AreEqual("A key cannot contain spaces or line breaks.", r.Message)
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_NothingTyped_AndNoSavedKey_AsksForTheKey() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "", NoToken)
                Assert.IsFalse(r.Success)
                Assert.AreEqual(ConnectionMessages.NeedKey, r.Message)
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        ' ================= test connection: the save policy =================

        <TestMethod>
        Public Async Function Test_FirstRun_GoodKey_SavesKeyRegionAndCache_AndUnlocks() As Task
            Using rig As New Rig(KeyedClient())
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync("  SouthAfricaNorth ", "  " & GoodKey & vbCrLf, NoToken)

                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual("Connected. 5 voices in 1 locale.", r.Message)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.AreEqual(SaRegion, rig.App.Region)
                Assert.IsTrue(rig.App.KeyIsSaved)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
                Assert.IsTrue(File.Exists(rig.CacheFile.CachePath))
                Assert.AreEqual(SaRegion, rig.Client.Calls(0)(0))
                Assert.AreEqual(GoodKey, rig.Client.Calls(0)(1))
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_GoodKey_WithNewRegion_SavesBoth() As Task
            Using rig As New Rig(KeyedClient())
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync("westeurope", GoodKey, NoToken)

                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual("westeurope", rig.App.Region)
                Assert.AreEqual("westeurope", rig.Store.Load().Value.Azure.Region)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.AreEqual("westeurope", rig.CacheFile.Load().Cache.Region)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_RejectedCandidate_ChangesNothing_AndNeverBlocksExportForTheWorkingKey() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)

                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "typo-key", NoToken)

                Assert.IsFalse(r.Success)
                Assert.IsTrue(r.Message.StartsWith("Azure rejected the key or region. Check both.", StringComparison.Ordinal))
                Assert.IsTrue(r.Message.EndsWith("Nothing was changed.", StringComparison.Ordinal))
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.IsFalse(rig.Monitor.Snapshot.KeyRejected)
                Assert.IsTrue(rig.App.Access.CanExport)
                Assert.AreEqual(5, rig.App.VoiceCount)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_Candidate_Offline_IsNotSaved_FirstRun() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)

                Assert.IsFalse(r.Success)
                Assert.IsTrue(r.Message.Contains("firewall", StringComparison.Ordinal))
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
                Assert.IsFalse(rig.App.KeyIsSaved)
                AssertLocked(rig.App, AccessReason.NoKey)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_Candidate_Offline_DoesNotReplaceTheWorkingKey() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0)))

                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "another-key", NoToken)

                Assert.IsFalse(r.Success)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_Candidate_429_IsNotSaved_AndSaysTemporary() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Transient, 429)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)

                Assert.IsFalse(r.Success)
                Assert.IsTrue(r.Message.Contains("temporarily", StringComparison.Ordinal))
                Assert.IsFalse(r.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_Candidate_UnexpectedResponse_IsNotSaved_AndShowsTheStatus() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Fail(AzureOutcome.Failed, 403)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)

                Assert.IsFalse(r.Success)
                Assert.IsTrue(r.Message.Contains("HTTP 403", StringComparison.Ordinal))
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_Candidate_OkButNoVoices_IsNotSaved() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(0)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)

                Assert.IsFalse(r.Success)
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
                Assert.IsFalse(File.Exists(rig.CacheFile.CachePath))
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_RegionOnlyChange_Success_SavesRegion_KeepsKey_AndRefreshesCache() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)

                Dim r = Await rig.App.TestConnectionAsync("westeurope", "", NoToken)

                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual("westeurope", rig.App.Region)
                Assert.AreEqual("westeurope", rig.Store.Load().Value.Azure.Region)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.AreEqual("westeurope", rig.CacheFile.Load().Cache.Region)
                Assert.AreEqual(GoodKey, rig.Client.Calls(rig.Client.Calls.Count - 1)(1))
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_RegionOnlyChange_Failure_LeavesTheRegionAlone() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))

                Dim r = Await rig.App.TestConnectionAsync("westeurope", "", NoToken)

                Assert.IsFalse(r.Success)
                Assert.AreEqual(SaRegion, rig.App.Region)
                Assert.AreEqual(SaRegion, rig.Store.Load().Value.Azure.Region)
                Assert.IsTrue(rig.App.Access.CanExport)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_NothingTyped_SameRegion_ReChecksTheStoredKey_AndUpdatesTheCache() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, KeyedClient(8))

                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "", NoToken)

                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual(8, rig.App.VoiceCount)
                Assert.AreEqual(GoodKey, rig.Client.Calls(0)(1))
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_NothingTyped_SameRegion_Rejected_BlocksExport_ButNotEditing() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))

                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "", NoToken)

                Assert.IsFalse(r.Success)
                Dim a = rig.App.Access
                Assert.AreEqual(AccessMode.EditOnly, a.Mode)
                Assert.IsTrue(a.CanEdit)
                Assert.IsFalse(a.CanExport)
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
            End Using
        End Function

        <TestMethod>
        Public Async Function Test_WhenSavingFails_NothingIsAdopted_AndTheOldKeyStays() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Ok(9)))
                rig.Protector.FailProtectWith = New CryptographicException("DPAPI unavailable")

                Dim r = Await rig.App.TestConnectionAsync(SaRegion, "NEWKEY0123456789", NoToken)

                Assert.IsFalse(r.Success)
                Assert.AreEqual(ConnectionMessages.SaveFailed, r.Message)
                rig.Protector.FailProtectWith = Nothing
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
                Assert.AreEqual(5, rig.App.VoiceCount)
                Assert.HasCount(5, rig.CacheFile.Load().Cache.Voices)
            End Using
        End Function

        ' ================= cancellation and concurrency =================

        <TestMethod>
        Public Async Function Test_Cancelled_PropagatesAndChangesNothing() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Hanging())
                Await rig.App.StartupAsync(NoToken)
                Using cts As New CancellationTokenSource()
                    Dim pending = rig.App.TestConnectionAsync(SaRegion, GoodKey, cts.Token)
                    Assert.IsTrue(rig.App.IsBusy)
                    cts.Cancel()
                    Dim cancelled As Boolean = False
                    Try
                        Await pending
                    Catch ex As OperationCanceledException
                        cancelled = True
                    End Try
                    Assert.IsTrue(cancelled)
                End Using
                Assert.IsFalse(rig.App.IsBusy)
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
                Assert.IsFalse(File.Exists(rig.CacheFile.CachePath))
            End Using
        End Function

        <TestMethod>
        Public Async Function SecondRequest_WhileBusy_IsRefusedPolitely_NotThrown() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Hanging())
                rig.KeyStore.Save(GoodKey)
                Using cts As New CancellationTokenSource()
                    Dim first = rig.App.StartupAsync(cts.Token)

                    Dim viaTest = Await rig.App.TestConnectionAsync(SaRegion, "", NoToken)
                    Dim viaRegion = Await rig.App.TestConnectionAsync("westeurope", "", NoToken)
                    Dim viaRefresh = Await rig.App.RefreshVoicesAsync(NoToken)

                    Assert.AreEqual(ConnectionMessages.Busy, viaTest.Message)
                    Assert.AreEqual(ConnectionMessages.Busy, viaRegion.Message)
                    Assert.AreEqual(ConnectionMessages.Busy, viaRefresh.Message)

                    cts.Cancel()
                    Try
                        Await first
                    Catch ex As OperationCanceledException
                        ' expected
                    End Try
                End Using
            End Using
        End Function

        ' ================= must never: leak the key =================

        <TestMethod>
        Public Async Function Messages_NeverContainTheTypedKey_OnAnyOutcome() As Task
            Dim results As VoiceListResult() = {
                Support.FakeSpeechClient.Ok(5), Support.FakeSpeechClient.Ok(0),
                Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401), Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0),
                Support.FakeSpeechClient.Fail(AzureOutcome.Transient, 429), Support.FakeSpeechClient.Fail(AzureOutcome.Failed, 403)}
            For Each res In results
                Using rig As New Rig(Always(res))
                    Await rig.App.StartupAsync(NoToken)
                    Dim r = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)
                    Assert.IsFalse(r.Message.Contains(GoodKey, StringComparison.Ordinal), res.Outcome.ToString())
                    Assert.IsFalse(rig.App.Access.Message.Contains(GoodKey, StringComparison.Ordinal))
                End Using
            Next
        End Function

        ' ================= refresh voices =================

        <TestMethod>
        Public Async Function RefreshVoices_WithoutKey_AsksForTheKey() As Task
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(5)))
                Await rig.App.StartupAsync(NoToken)
                Dim r = Await rig.App.RefreshVoicesAsync(NoToken)
                Assert.IsFalse(r.Success)
                Assert.AreEqual(ConnectionMessages.NeedKey, r.Message)
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Function

        <TestMethod>
        Public Async Function RefreshVoices_Success_UpdatesTheListAndSaysSo() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, KeyedClient(7))

                Dim r = Await rig.App.RefreshVoicesAsync(NoToken)

                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual("Voice list updated. 7 voices in 1 locale.", r.Message)
                Assert.AreEqual(7, rig.App.VoiceCount)
            End Using
        End Function

        <TestMethod>
        Public Async Function RefreshVoices_Offline_KeepsTheOldList_AndStaysUnlocked() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0)))

                Dim r = Await rig.App.RefreshVoicesAsync(NoToken)

                Assert.IsFalse(r.Success)
                Assert.AreEqual(5, rig.App.VoiceCount)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
            End Using
        End Function

        ' ================= clear key =================

        <TestMethod>
        Public Async Function ClearKey_RemovesTheKey_Locks_AndKeepsRegionDefaultsAndCache() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                rig.Store.Update(Sub(s) s.DefaultVoices("af-ZA") = "af-ZA-WillemNeural")

                Dim r = rig.App.ClearKey()

                Assert.IsTrue(r.Success)
                Assert.AreEqual(ConnectionMessages.KeyRemoved, r.Message)
                Assert.IsFalse(rig.App.KeyIsSaved)
                AssertLocked(rig.App, AccessReason.NoKey)
                Assert.AreEqual(KeyState.Absent, rig.KeyStore.Load().Status)
                Dim after = rig.Store.Load().Value
                Assert.AreEqual(SaRegion, after.Azure.Region)
                Assert.AreEqual("af-ZA-WillemNeural", after.DefaultVoices("af-ZA"))
                Assert.IsTrue(File.Exists(rig.CacheFile.CachePath))

                Dim again = Await rig.App.TestConnectionAsync(SaRegion, GoodKey, NoToken)
                Assert.IsTrue(again.Success, again.Message)
                Assert.AreEqual(AccessMode.Full, rig.App.Access.Mode)
            End Using
        End Function

        <TestMethod>
        Public Async Function ClearKey_WhenSettingsFileIsLocked_Fails_AndKeepsTheKey() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                rig.SettingsLocked = True

                Dim r = rig.App.ClearKey()

                Assert.IsFalse(r.Success)
                Assert.AreEqual(ConnectionMessages.ClearFailed, r.Message)
                Assert.IsTrue(rig.App.KeyIsSaved)
                rig.SettingsLocked = False
                Assert.AreEqual(GoodKey, rig.KeyStore.Load().Key)
            End Using
        End Function

        <TestMethod>
        Public Async Function ClearKey_AlsoClearsAStandingRejection() As Task
            Using rig As New Rig(KeyedClient())
                Await WorkingAsync(rig)
                Await RestartWithAsync(rig, Always(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                Await rig.App.RefreshVoicesAsync(NoToken)
                Assert.IsTrue(rig.Monitor.Snapshot.KeyRejected)

                rig.App.ClearKey()

                Assert.IsFalse(rig.Monitor.Snapshot.KeyRejected)
            End Using
        End Function

        ' ================= wiring with the real client =================

        <TestMethod>
        Public Async Function Create_EndToEnd_WithRealClientAndFakeHttp_SurvivesARestartWithoutTheNetwork() As Task
            Using dir As New Support.TempDir()
                Dim protector As New Support.FakeSecretProtector()
                Const json As String = "[{""ShortName"":""af-ZA-WillemNeural"",""Locale"":""af-ZA"",""DisplayName"":""Willem""},{""ShortName"":""af-ZA-AdriNeural"",""Locale"":""af-ZA"",""DisplayName"":""Adri""},{""ShortName"":""he-IL-HilaNeural"",""Locale"":""he-IL"",""DisplayName"":""Hila""}]"

                Dim firstHandler = Support.FakeHttpMessageHandler.Responding(200, json)
                Dim first = AppSession.Create(dir.FolderPath, protector, New HttpClient(firstHandler))
                Await first.StartupAsync(NoToken)
                Dim r = Await first.TestConnectionAsync(SaRegion, GoodKey, NoToken)
                Assert.IsTrue(r.Success, r.Message)
                Assert.AreEqual("Connected. 3 voices in 2 locales.", r.Message)
                Assert.AreEqual(GoodKey, firstHandler.Requests(0).KeyHeader)

                Dim settingsText = File.ReadAllText(Path.Combine(dir.FolderPath, "settings.json"))
                Assert.IsFalse(settingsText.Contains(GoodKey, StringComparison.Ordinal))

                Dim secondHandler = Support.FakeHttpMessageHandler.Responding(401, "")
                Dim second = AppSession.Create(dir.FolderPath, protector, New HttpClient(secondHandler))
                Await second.StartupAsync(NoToken)

                Assert.AreEqual(AccessMode.Full, second.Access.Mode)
                Assert.AreEqual(3, second.VoiceCount)
                Assert.HasCount(2, second.Catalog.Locales)
                Assert.IsEmpty(secondHandler.Requests)
            End Using
        End Function

        <TestMethod>
        Public Sub Constructor_NullArguments_Throw()
            Using rig As New Rig(Always(Support.FakeSpeechClient.Ok(1)))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing, rig.KeyStore, rig.VoiceSvc, rig.Monitor))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(rig.Store, Nothing, rig.VoiceSvc, rig.Monitor))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(rig.Store, rig.KeyStore, Nothing, rig.Monitor))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(rig.Store, rig.KeyStore, rig.VoiceSvc, Nothing))
            End Using
        End Sub

        Private Shared Sub Build(s As SettingsStore, k As ISecretStore, v As VoiceService, m As ConnectionMonitor)
            Dim unused = New AppSession(s, k, v, m)
        End Sub

    End Class

End Namespace
