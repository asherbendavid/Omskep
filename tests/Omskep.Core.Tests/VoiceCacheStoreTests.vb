Imports System.IO
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Voices

Namespace Voices

    <TestClass>
    Public Class VoiceCacheStoreTests

        Private Shared Function Sample(region As String, count As Integer) As VoiceCache
            Dim c As New VoiceCache()
            c.Region = region
            c.FetchedUtc = New DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc)
            For i = 0 To count - 1
                c.Voices.Add(New VoiceInfo() With {
                    .ShortName = "xx-XX-Voice" & i.ToString() & "Neural",
                    .Locale = "xx-XX",
                    .DisplayName = "Voice" & i.ToString(),
                    .Gender = "Female",
                    .VoiceType = "Neural"})
            Next
            Return c
        End Function

        <TestMethod>
        Public Sub Load_NoFile_IsNotFound_AndEmpty()
            Using t As New Support.TempDir()
                Dim r = New VoiceCacheStore(t.FolderPath).Load()
                Assert.AreEqual(VoiceCacheLoadStatus.NotFound, r.Status)
                Assert.IsEmpty(r.Cache.Voices)
                Assert.AreEqual(CacheStatus.Missing, r.Cache.StatusFor("southafricanorth"))
            End Using
        End Sub

        <TestMethod>
        Public Sub SaveThenLoad_RoundTrips_LargeList()
            Using t As New Support.TempDir()
                Dim store As New VoiceCacheStore(t.FolderPath)
                store.Save(Sample("southafricanorth", 600))

                Dim r = store.Load()
                Assert.AreEqual(VoiceCacheLoadStatus.Loaded, r.Status)
                Assert.AreEqual("southafricanorth", r.Cache.Region)
                Assert.AreEqual(New DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc), r.Cache.FetchedUtc)
                Assert.HasCount(600, r.Cache.Voices)
                Assert.AreEqual("xx-XX-Voice599Neural", r.Cache.Voices(599).ShortName)
                Assert.AreEqual("Neural", r.Cache.Voices(0).VoiceType)
            End Using
        End Sub

        <TestMethod>
        Public Sub StatusFor_FollowsRegion_AndEmptiness()
            Dim c = Sample("southafricanorth", 3)
            Assert.AreEqual(CacheStatus.Present, c.StatusFor("southafricanorth"))
            Assert.AreEqual(CacheStatus.Present, c.StatusFor("SouthAfricaNorth"))
            Assert.AreEqual(CacheStatus.Missing, c.StatusFor("westeurope"))
            Assert.AreEqual(CacheStatus.Missing, Sample("southafricanorth", 0).StatusFor("southafricanorth"))
        End Sub

        <TestMethod>
        Public Sub Load_GarbageOrWrongShape_IsUnusable_AndFileLeftInPlace()
            For Each raw In {"", "not json", "null", "{""voices"": 5}"}
                Using t As New Support.TempDir()
                    Dim store As New VoiceCacheStore(t.FolderPath)
                    File.WriteAllText(store.CachePath, raw)
                    Dim r = store.Load()
                    Assert.AreEqual(VoiceCacheLoadStatus.Unusable, r.Status, "input: " & raw)
                    Assert.IsEmpty(r.Cache.Voices)
                    Assert.AreEqual(CacheStatus.Missing, r.Cache.StatusFor("southafricanorth"))
                End Using
            Next
        End Sub

        <TestMethod>
        Public Sub Load_NullVoicesAndNullEntries_AreRepaired()
            Using t As New Support.TempDir()
                Dim store As New VoiceCacheStore(t.FolderPath)
                File.WriteAllText(store.CachePath, "{""region"":""r"",""voices"":[null,{""shortName"":""a""},null]}")
                Dim r = store.Load()
                Assert.AreEqual(VoiceCacheLoadStatus.Loaded, r.Status)
                Assert.HasCount(1, r.Cache.Voices)

                File.WriteAllText(store.CachePath, "{""region"":""r"",""voices"":null}")
                Assert.IsEmpty(store.Load().Cache.Voices)
            End Using
        End Sub

        <TestMethod>
        Public Sub Clear_RemovesCache_AndIsSafeWhenNoneExists()
            Using t As New Support.TempDir()
                Dim store As New VoiceCacheStore(t.FolderPath)
                store.Clear()
                store.Save(Sample("southafricanorth", 2))
                store.Clear()
                Assert.AreEqual(VoiceCacheLoadStatus.NotFound, store.Load().Status)
                store.Clear()
            End Using
        End Sub

        <TestMethod>
        Public Sub CacheFile_IsSeparateFrom_SettingsFile()
            Using t As New Support.TempDir()
                Dim store As New VoiceCacheStore(t.FolderPath)
                Assert.AreNotEqual("settings.json", Path.GetFileName(store.CachePath))
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_NullCache_Throws()
            Using t As New Support.TempDir()
                Dim store As New VoiceCacheStore(t.FolderPath)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() store.Save(Nothing))
            End Using
        End Sub

    End Class

End Namespace