Imports System.IO
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Voices

    <TestClass>
    Public Class VoiceServiceTests

        Private Const Region As String = "southafricanorth"
        Private Const Key As String = "SECRETKEY0123456789abcdef"
        Private Shared ReadOnly FixedNow As New DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc)

        Private NotInheritable Class Rig
            Implements IDisposable

            Public ReadOnly Property Folder As Support.TempDir
            Public ReadOnly Property Cache As VoiceCacheStore
            Public ReadOnly Property Monitor As ConnectionMonitor
            Public ReadOnly Property Client As Support.FakeSpeechClient
            Public ReadOnly Property Service As VoiceService

            Public Sub New(client As Support.FakeSpeechClient)
                Folder = New Support.TempDir()
                Cache = New VoiceCacheStore(Folder.FolderPath)
                Monitor = New ConnectionMonitor()
                Me.Client = client
                Service = New VoiceService(client, Cache, Monitor, Function() FixedNow)
            End Sub

            Public Sub Dispose() Implements IDisposable.Dispose
                Folder.Dispose()
            End Sub
        End Class

        Private Shared Function Seed(rig As Rig, region As String, count As Integer) As VoiceCache
            Dim c As New VoiceCache() With {.Region = region, .FetchedUtc = FixedNow, .Voices = Support.FakeSpeechClient.Voices(count)}
            rig.Cache.Save(c)
            Return c
        End Function

        Private Shared Function StateNow(rig As Rig, key As KeyState, region As String) As AccessState
            Return AccessEvaluator.Evaluate(key, rig.Service.CacheStatusFor(region), rig.Monitor.Snapshot)
        End Function

        ' ---- startup: loading the cache ----

        <TestMethod>
        Public Sub LoadCache_NoFile_IsMissing_AndCatalogEmpty()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Assert.AreEqual(CacheStatus.Missing, rig.Service.LoadCache(Region))
                Assert.AreEqual(0, rig.Service.Catalog.Count)
                Assert.AreEqual(CacheStatus.Missing, rig.Service.CacheStatusFor(Region))
            End Using
        End Sub

        <TestMethod>
        Public Sub LoadCache_ValidCacheForThisRegion_IsPresent_AndNothingIsFetched()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Seed(rig, Region, 5)
                Assert.AreEqual(CacheStatus.Present, rig.Service.LoadCache(Region))
                Assert.AreEqual(5, rig.Service.Catalog.Count)
                Assert.AreEqual(CacheStatus.Present, rig.Service.CacheStatusFor("SouthAfricaNorth"))
                Assert.IsEmpty(rig.Client.Calls)
            End Using
        End Sub

        <TestMethod>
        Public Sub LoadCache_ForAnotherRegion_IsMissing_AndFileIsLeftAlone()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Seed(rig, "westeurope", 5)
                Assert.AreEqual(CacheStatus.Missing, rig.Service.LoadCache(Region))
                Assert.AreEqual(0, rig.Service.Catalog.Count)
                Assert.IsTrue(File.Exists(rig.Cache.CachePath))
            End Using
        End Sub

        <TestMethod>
        Public Sub LoadCache_GarbageFile_IsMissing()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                File.WriteAllText(rig.Cache.CachePath, "not json")
                Assert.AreEqual(CacheStatus.Missing, rig.Service.LoadCache(Region))
            End Using
        End Sub

        <TestMethod>
        Public Sub LoadCache_EmptyVoiceList_IsMissing()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Seed(rig, Region, 0)
                Assert.AreEqual(CacheStatus.Missing, rig.Service.LoadCache(Region))
            End Using
        End Sub

        ' ---- TestAsync: a candidate key must change nothing ----

        <TestMethod>
        Public Async Function TestAsync_Success_ReturnsVoices_ButWritesAndReportsNothing() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(3)))
                Dim r = Await rig.Service.TestAsync(Region, Key, CancellationToken.None)

                Assert.AreEqual(AzureOutcome.Ok, r.Outcome)
                Assert.HasCount(3, r.Voices)
                Assert.IsFalse(File.Exists(rig.Cache.CachePath))
                Assert.AreEqual(0, rig.Service.Catalog.Count)
                Assert.AreEqual(AzureOutcome.None, rig.Monitor.Snapshot.LastOutcome)
            End Using
        End Function

        <TestMethod>
        Public Async Function TestAsync_RejectedCandidate_NeverMarksTheWorkingKeyRejected() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                Seed(rig, Region, 4)
                rig.Service.LoadCache(Region)

                Dim r = Await rig.Service.TestAsync(Region, "typo-key", CancellationToken.None)

                Assert.AreEqual(AzureOutcome.Rejected, r.Outcome)
                Assert.IsFalse(rig.Monitor.Snapshot.KeyRejected)
                Dim state = StateNow(rig, KeyState.Present, Region)
                Assert.AreEqual(AccessMode.Full, state.Mode)
                Assert.IsTrue(state.CanExport)
                Assert.AreEqual(4, rig.Service.Catalog.Count)
            End Using
        End Function

        ' ---- Adopt ----

        <TestMethod>
        Public Sub Adopt_SetsCatalog_SavesCacheWithRegionAndTime_AndReportsOk()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Dim saved = rig.Service.Adopt(Support.FakeSpeechClient.Ok(6), "SouthAfricaNorth")

                Assert.IsTrue(saved)
                Assert.AreEqual(6, rig.Service.Catalog.Count)
                Assert.AreEqual(AzureOutcome.Ok, rig.Monitor.Snapshot.LastOutcome)
                Dim onDisk = rig.Cache.Load().Cache
                Assert.AreEqual("southafricanorth", onDisk.Region)
                Assert.AreEqual(FixedNow, onDisk.FetchedUtc)
                Assert.HasCount(6, onDisk.Voices)
            End Using
        End Sub

        <TestMethod>
        Public Sub Adopt_ThenFreshService_ModelsAnAppRestart_WithoutRefetching()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                rig.Service.Adopt(Support.FakeSpeechClient.Ok(6), Region)

                Dim secondClient = Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1))
                Dim restarted As New VoiceService(secondClient, rig.Cache, New ConnectionMonitor())
                Assert.AreEqual(CacheStatus.Present, restarted.LoadCache(Region))
                Assert.AreEqual(6, restarted.Catalog.Count)
                Assert.IsEmpty(secondClient.Calls)
            End Using
        End Sub

        <TestMethod>
        Public Sub Adopt_ForAnotherRegion_MakesTheOldRegionMissing()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                rig.Service.Adopt(Support.FakeSpeechClient.Ok(3), "westeurope")
                Assert.AreEqual(CacheStatus.Present, rig.Service.CacheStatusFor("westeurope"))
                Assert.AreEqual(CacheStatus.Missing, rig.Service.CacheStatusFor(Region))
            End Using
        End Sub

        <TestMethod>
        Public Sub Adopt_WhenCacheCannotBeWritten_ReturnsFalse_ButVoicesStillWorkThisSession()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Directory.CreateDirectory(rig.Cache.CachePath & ".tmp")   ' makes the cache write fail

                Dim saved = rig.Service.Adopt(Support.FakeSpeechClient.Ok(4), Region)

                Assert.IsFalse(saved)
                Assert.AreEqual(4, rig.Service.Catalog.Count)
                Assert.AreEqual(CacheStatus.Present, rig.Service.CacheStatusFor(Region))
                Assert.IsTrue(StateNow(rig, KeyState.Present, Region).CanExport)
            End Using
        End Sub

        <TestMethod>
        Public Sub Adopt_RejectsAnythingThatIsNotASuccessfulNonEmptyList()
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1)))
                Seed(rig, Region, 2)
                rig.Service.LoadCache(Region)

                Assert.ThrowsExactly(Of ArgumentException)(Sub() rig.Service.Adopt(Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0), Region))
                Assert.ThrowsExactly(Of ArgumentException)(Sub() rig.Service.Adopt(Support.FakeSpeechClient.Ok(0), Region))
                Assert.ThrowsExactly(Of ArgumentException)(Sub() rig.Service.Adopt(Support.FakeSpeechClient.Ok(2), "evil.com/"))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() rig.Service.Adopt(Nothing, Region))

                Assert.AreEqual(2, rig.Service.Catalog.Count)
                Assert.HasCount(2, rig.Cache.Load().Cache.Voices)
            End Using
        End Sub

        ' ---- RefreshAsync: stored key ----

        <TestMethod>
        Public Async Function Refresh_Success_ReplacesCache_Catalog_AndReportsOk() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(7)))
                Seed(rig, Region, 2)
                rig.Service.LoadCache(Region)

                Dim r = Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)

                Assert.IsTrue(r.CacheSaved)
                Assert.AreEqual(7, rig.Service.Catalog.Count)
                Assert.HasCount(7, rig.Cache.Load().Cache.Voices)
                Assert.AreEqual(AzureOutcome.Ok, rig.Monitor.Snapshot.LastOutcome)
                Assert.HasCount(1, rig.Client.Calls)
                Assert.AreEqual(Region, rig.Client.Calls(0)(0))
                Assert.AreEqual(Key, rig.Client.Calls(0)(1))
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_Rejected_KeepsOldCache_BlocksExportOnly() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                Seed(rig, Region, 4)
                rig.Service.LoadCache(Region)

                Dim r = Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)

                Assert.IsFalse(r.CacheSaved)
                Assert.AreEqual(4, rig.Service.Catalog.Count)
                Assert.HasCount(4, rig.Cache.Load().Cache.Voices)
                Dim state = StateNow(rig, KeyState.Present, Region)
                Assert.AreEqual(AccessMode.EditOnly, state.Mode)
                Assert.IsTrue(state.CanEdit)
                Assert.IsFalse(state.CanExport)
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_Offline_And_429_KeepCache_AndStayUnlocked() As Task
            For Each failure In {Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0), Support.FakeSpeechClient.Fail(AzureOutcome.Transient, 429), Support.FakeSpeechClient.Fail(AzureOutcome.Failed, 403)}
                Using rig As New Rig(Support.FakeSpeechClient.Returning(failure))
                    Seed(rig, Region, 4)
                    rig.Service.LoadCache(Region)

                    Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)

                    Assert.AreEqual(4, rig.Service.Catalog.Count)
                    Assert.IsFalse(rig.Monitor.Snapshot.KeyRejected)
                    Assert.AreEqual(AccessMode.Full, StateNow(rig, KeyState.Present, Region).Mode, failure.Outcome.ToString())
                End Using
            Next
        End Function

        <TestMethod>
        Public Async Function Refresh_NoCache_Offline_IsLockedWithConnectionMessage_ThenSuccessUnlocks() As Task
            Dim online As Boolean = False
            Dim client As New Support.FakeSpeechClient(
                Function(ct) Task.FromResult(If(online, Support.FakeSpeechClient.Ok(5), Support.FakeSpeechClient.Fail(AzureOutcome.Offline, 0))))
            Using rig As New Rig(client)
                Assert.AreEqual(CacheStatus.Missing, rig.Service.LoadCache(Region))

                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Dim locked = StateNow(rig, KeyState.Present, Region)
                Assert.AreEqual(AccessMode.Locked, locked.Mode)
                Assert.AreEqual(AccessReason.Offline, locked.Reason)

                online = True
                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Assert.AreEqual(AccessMode.Full, StateNow(rig, KeyState.Present, Region).Mode)
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_NoCache_Rejected_IsLockedWithCombinedMessage() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Dim state = StateNow(rig, KeyState.Present, Region)
                Assert.AreEqual(AccessMode.Locked, state.Mode)
                Assert.AreEqual("Azure rejected the key or region. Check both.", state.Message)
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_OkButNoVoices_DoesNotOverwriteTheCache() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(0)))
                Seed(rig, Region, 4)
                rig.Service.LoadCache(Region)

                Dim r = Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)

                Assert.IsFalse(r.CacheSaved)
                Assert.AreEqual(4, rig.Service.Catalog.Count)
                Assert.HasCount(4, rig.Cache.Load().Cache.Voices)
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_OkButNoVoices_WithNoCache_IsLockedAsNoVoices() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(0)))
                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Assert.AreEqual(AccessReason.NoVoicesAvailable, StateNow(rig, KeyState.Present, Region).Reason)
            End Using
        End Function

        <TestMethod>
        Public Async Function Refresh_AfterRejection_ThenSuccess_ClearsTheBlock() As Task
            Dim ok As Boolean = False
            Dim client As New Support.FakeSpeechClient(
                Function(ct) Task.FromResult(If(ok, Support.FakeSpeechClient.Ok(3), Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401))))
            Using rig As New Rig(client)
                Seed(rig, Region, 2)
                rig.Service.LoadCache(Region)

                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Assert.IsFalse(StateNow(rig, KeyState.Present, Region).CanExport)

                ok = True
                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Assert.IsTrue(StateNow(rig, KeyState.Present, Region).CanExport)
            End Using
        End Function

        ' ---- credentials changed ----

        <TestMethod>
        Public Async Function CredentialsChanged_ClearsAStandingRejection() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Fail(AzureOutcome.Rejected, 401)))
                Seed(rig, Region, 2)
                rig.Service.LoadCache(Region)
                Await rig.Service.RefreshAsync(Region, Key, CancellationToken.None)
                Assert.IsFalse(StateNow(rig, KeyState.Present, Region).CanExport)

                rig.Service.CredentialsChanged()

                Assert.IsTrue(StateNow(rig, KeyState.Present, Region).CanExport)
                Assert.AreEqual(AzureOutcome.None, rig.Monitor.Snapshot.LastOutcome)
            End Using
        End Function

        ' ---- single flight and cancellation ----

        <TestMethod>
        Public Async Function SecondRequest_WhileOneIsRunning_Throws_AndTheFirstCanStillBeCancelled() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Hanging())
                Using cts As New CancellationTokenSource()
                    Dim first = rig.Service.RefreshAsync(Region, Key, cts.Token)
                    Assert.IsTrue(rig.Service.IsBusy)

                    Dim threw As Boolean = False
                    Try
                        Await rig.Service.TestAsync(Region, Key, CancellationToken.None)
                    Catch ex As InvalidOperationException
                        threw = True
                    End Try
                    Assert.IsTrue(threw)

                    cts.Cancel()
                    Dim cancelled As Boolean = False
                    Try
                        Await first
                    Catch ex As OperationCanceledException
                        cancelled = True
                    End Try
                    Assert.IsTrue(cancelled)
                    Assert.IsFalse(rig.Service.IsBusy)
                End Using
            End Using
        End Function

        <TestMethod>
        Public Async Function Cancelled_Refresh_ChangesNothing_AndReleasesTheGuard() As Task
            Using rig As New Rig(Support.FakeSpeechClient.Hanging())
                Seed(rig, Region, 3)
                rig.Service.LoadCache(Region)
                Using cts As New CancellationTokenSource()
                    Dim pending = rig.Service.RefreshAsync(Region, Key, cts.Token)
                    cts.Cancel()
                    Try
                        Await pending
                    Catch ex As OperationCanceledException
                        ' expected
                    End Try
                End Using

                Assert.IsFalse(rig.Service.IsBusy)
                Assert.AreEqual(AzureOutcome.None, rig.Monitor.Snapshot.LastOutcome)
                Assert.AreEqual(3, rig.Service.Catalog.Count)
                Assert.HasCount(3, rig.Cache.Load().Cache.Voices)
            End Using
        End Function

        <TestMethod>
        Public Async Function ClientThatThrows_ReleasesTheGuard() As Task
            Dim client As New Support.FakeSpeechClient(
                Function(ct) As Task(Of VoiceListResult)
                    Throw New ArgumentException("bad region")
                End Function)
            Using rig As New Rig(client)
                Dim threw As Boolean = False
                Try
                    Await rig.Service.RefreshAsync("evil.com/", Key, CancellationToken.None)
                Catch ex As ArgumentException
                    threw = True
                End Try
                Assert.IsTrue(threw)
                Assert.IsFalse(rig.Service.IsBusy)
            End Using
        End Function

        ' ---- construction ----

        <TestMethod>
        Public Sub Constructor_NullArguments_Throw()
            Using t As New Support.TempDir()
                Dim cache As New VoiceCacheStore(t.FolderPath)
                Dim client = Support.FakeSpeechClient.Returning(Support.FakeSpeechClient.Ok(1))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing, cache, New ConnectionMonitor()))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(client, Nothing, New ConnectionMonitor()))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(client, cache, Nothing))
            End Using
        End Sub

        Private Shared Sub Build(client As ISpeechClient, cache As VoiceCacheStore, monitor As ConnectionMonitor)
            Dim unused = New VoiceService(client, cache, monitor)
        End Sub

    End Class

End Namespace
