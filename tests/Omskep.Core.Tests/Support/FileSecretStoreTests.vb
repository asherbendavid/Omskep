Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Secrets
Imports Omskep.Core.Settings

Namespace Secrets

    <TestClass>
    Public Class FileSecretStoreTests

        Private Const SampleKey As String = "0123456789abcdef0123456789ABCDEF"

        Private Shared Function NewSettings(folder As String) As SettingsStore
            Return New SettingsStore(folder, Nothing, Nothing, New Integer() {0})   ' no waiting in tests
        End Function

        Private Shared Sub Build(settings As SettingsStore, protector As ISecretProtector)
            Dim unused = New FileSecretStore(settings, protector)
        End Sub

        Private Shared Function LockedSettings(folder As String) As SettingsStore
            Dim reader As Func(Of String, Byte()) =
                Function(p As String) As Byte()
                    Throw New IOException("locked")
                End Function
            Return New SettingsStore(folder, Nothing, reader, New Integer() {0, 0})
        End Function

        ' ---- round trip ----

        <TestMethod>
        Public Sub NoSettingsFile_IsAbsent()
            Using t As New Support.TempDir()
                Dim r = New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector()).Load()
                Assert.AreEqual(KeyState.Absent, r.Status)
                Assert.IsNull(r.Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub SaveThenLoad_RoundTripsTheKey()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Save(SampleKey)
                Dim r = store.Load()
                Assert.AreEqual(KeyState.Present, r.Status)
                Assert.AreEqual(SampleKey, r.Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub RoundTrip_LongKey_And_FreshStoreInstance_ModelsAnAppRestart()
            Using t As New Support.TempDir()
                Dim longKey = New String("k"c, 84)
                Dim protector As New Support.FakeSecretProtector()
                Dim first As New FileSecretStore(NewSettings(t.FolderPath), protector)
                first.Save(longKey)

                Dim second As New FileSecretStore(NewSettings(t.FolderPath), protector)
                Assert.AreEqual(longKey, second.Load().Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub SavedFile_NeverContainsThePlaintextKey()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                Dim store As New FileSecretStore(settings, New Support.FakeSecretProtector())
                store.Save(SampleKey)
                Dim fileText = File.ReadAllText(settings.SettingsPath)
                Assert.IsTrue(fileText.Contains("keyBlob", StringComparison.Ordinal))
                Assert.IsFalse(fileText.Contains(SampleKey, StringComparison.OrdinalIgnoreCase))
                Assert.IsFalse(fileText.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes(SampleKey)), StringComparison.Ordinal))
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_TrimsPastedWhitespace()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Save("  " & SampleKey & vbCrLf)
                Assert.AreEqual(SampleKey, store.Load().Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_Replace_KeepsOnlyTheNewKey()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Save("firstKey")
                store.Save("secondKey")
                Assert.AreEqual("secondKey", store.Load().Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub SaveClearSave_Sequence_Works()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Save("one")
                store.Clear()
                Assert.AreEqual(KeyState.Absent, store.Load().Status)
                store.Save("two")
                Assert.AreEqual("two", store.Load().Key)
            End Using
        End Sub

        ' ---- clear ----

        <TestMethod>
        Public Sub Clear_RemovesKey_ButKeepsOtherSettings()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s)
                                    s.Azure.Region = "westeurope"
                                    s.DefaultVoices("af-ZA") = "af-ZA-WillemNeural"
                                    s.SpeakingRatePercent("he-IL") = -10
                                End Sub)
                Dim store As New FileSecretStore(settings, New Support.FakeSecretProtector())
                store.Save(SampleKey)

                store.Clear()

                Assert.AreEqual(KeyState.Absent, store.Load().Status)
                Dim after = settings.Load().Value
                Assert.IsNull(after.Azure.KeyBlob)
                Assert.AreEqual("westeurope", after.Azure.Region)
                Assert.AreEqual("af-ZA-WillemNeural", after.DefaultVoices("af-ZA"))
                Assert.AreEqual(-10, after.SpeakingRatePercent("he-IL"))
            End Using
        End Sub

        <TestMethod>
        Public Sub Clear_WhenNothingSaved_IsSafe()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Clear()
                Assert.AreEqual(KeyState.Absent, store.Load().Status)
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_KeepsRegionAndOtherSettings()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s) s.Azure.Region = "westeurope")
                Dim store As New FileSecretStore(settings, New Support.FakeSecretProtector())
                store.Save(SampleKey)
                Assert.AreEqual("westeurope", settings.Load().Value.Azure.Region)
            End Using
        End Sub

        ' ---- must never: bad input, crash, or key leakage ----

        <TestMethod>
        Public Sub Save_BlankOrSpacedKey_Throws_KeepsExistingKey_AndNeverEchoesTheKey()
            Using t As New Support.TempDir()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), New Support.FakeSecretProtector())
                store.Save("goodKey")

                For Each bad In {Nothing, "", "   ", "abc def", "abc" & vbLf & "def"}
                    Dim message As String = Nothing
                    Try
                        store.Save(bad)
                    Catch ex As ArgumentException
                        message = ex.Message
                    End Try
                    Assert.IsNotNull(message, "should have thrown for: " & If(bad, "(Nothing)"))
                    If bad IsNot Nothing AndAlso bad.Trim().Length > 0 Then
                        Assert.IsFalse(message.Contains(bad.Trim(), StringComparison.Ordinal))
                    End If
                Next

                Assert.AreEqual("goodKey", store.Load().Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_WipesThePlaintextBuffer()
            Using t As New Support.TempDir()
                Dim protector As New Support.FakeSecretProtector()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), protector)
                store.Save(SampleKey)
                Assert.IsNotNull(protector.LastPlaintext)
                For Each b In protector.LastPlaintext
                    Assert.AreEqual(CByte(0), b)
                Next
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_WhenProtectorFails_Throws_AndKeepsExistingKey()
            Using t As New Support.TempDir()
                Dim protector As New Support.FakeSecretProtector()
                Dim store As New FileSecretStore(NewSettings(t.FolderPath), protector)
                store.Save("goodKey")

                protector.FailProtectWith = New CryptographicException("DPAPI unavailable")
                Assert.ThrowsExactly(Of CryptographicException)(Sub() store.Save("newKey"))

                protector.FailProtectWith = Nothing
                Assert.AreEqual("goodKey", store.Load().Key)
            End Using
        End Sub

        ' ---- corrupted blob: treated as no key, never a crash, nothing overwritten ----

        <TestMethod>
        Public Sub Load_ProtectorThrowsCryptographicException_IsBlobUnreadable()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                Dim protector As New Support.FakeSecretProtector()
                Dim store As New FileSecretStore(settings, protector)
                settings.Update(Sub(s) s.Azure.Region = "westeurope")
                store.Save(SampleKey)
                Dim before = File.ReadAllText(settings.SettingsPath)

                protector.FailUnprotectWith = New CryptographicException("wrong user")
                Dim r = store.Load()

                Assert.AreEqual(KeyState.BlobUnreadable, r.Status)
                Assert.IsNull(r.Key)
                Assert.AreEqual(before, File.ReadAllText(settings.SettingsPath))   ' Load never writes
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_HandCorruptedBlob_ValidBase64ButGarbage_IsBlobUnreadable()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s) s.Azure.KeyBlob = Convert.ToBase64String(New Byte() {1, 2, 3, 4, 5}))
                Dim r = New FileSecretStore(settings, New Support.FakeSecretProtector()).Load()
                Assert.AreEqual(KeyState.BlobUnreadable, r.Status)
                Assert.IsNull(r.Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_BlobThatIsNotBase64_IsBlobUnreadable()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s) s.Azure.KeyBlob = "!!! not base64 !!!")
                Assert.AreEqual(KeyState.BlobUnreadable, New FileSecretStore(settings, New Support.FakeSecretProtector()).Load().Status)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_DecryptsToEmptyOrInvalidUtf8_IsBlobUnreadable()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                Dim protector As New Support.FakeSecretProtector()
                Dim store As New FileSecretStore(settings, protector)
                store.Save(SampleKey)

                protector.UnprotectResultOverride = New Byte() {}
                Assert.AreEqual(KeyState.BlobUnreadable, store.Load().Status)

                protector.UnprotectResultOverride = New Byte() {&HFF, &HFE, &HFD}
                Assert.AreEqual(KeyState.BlobUnreadable, store.Load().Status)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_EmptyBlobField_IsAbsent()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s) s.Azure.KeyBlob = "")
                Assert.AreEqual(KeyState.Absent, New FileSecretStore(settings, New Support.FakeSecretProtector()).Load().Status)
            End Using
        End Sub

        <TestMethod>
        Public Sub CorruptedBlob_ThenSavingANewKey_Recovers()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                settings.Update(Sub(s) s.Azure.KeyBlob = "AAAA")
                Dim store As New FileSecretStore(settings, New Support.FakeSecretProtector())
                Assert.AreEqual(KeyState.BlobUnreadable, store.Load().Status)
                store.Save(SampleKey)
                Assert.AreEqual(SampleKey, store.Load().Key)
            End Using
        End Sub

        ' ---- damaged and locked settings file ----

        <TestMethod>
        Public Sub Load_DamagedSettingsFile_IsReported_ThenSavingWorks()
            Using t As New Support.TempDir()
                Dim settings = NewSettings(t.FolderPath)
                File.WriteAllText(settings.SettingsPath, "{{ garbage")
                Dim store As New FileSecretStore(settings, New Support.FakeSecretProtector())

                Assert.AreEqual(KeyState.SettingsFileDamaged, store.Load().Status)
                store.Save(SampleKey)
                Assert.AreEqual(SampleKey, store.Load().Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_SettingsFileLocked_IsSettingsUnreadable_NotAbsent()
            Using t As New Support.TempDir()
                Dim r = New FileSecretStore(LockedSettings(t.FolderPath), New Support.FakeSecretProtector()).Load()
                Assert.AreEqual(KeyState.SettingsUnreadable, r.Status)
                Assert.IsNull(r.Key)
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_And_Clear_WhenSettingsFileLocked_Throw_AndOverwriteNothing()
            Using t As New Support.TempDir()
                Dim good = NewSettings(t.FolderPath)
                Dim real As New FileSecretStore(good, New Support.FakeSecretProtector())
                real.Save(SampleKey)
                Dim before = File.ReadAllText(good.SettingsPath)

                Dim locked As New FileSecretStore(LockedSettings(t.FolderPath), New Support.FakeSecretProtector())
                Assert.ThrowsExactly(Of IOException)(Sub() locked.Save("otherKey"))
                Assert.ThrowsExactly(Of IOException)(Sub() locked.Clear())

                Assert.AreEqual(before, File.ReadAllText(good.SettingsPath))
                Assert.AreEqual(SampleKey, real.Load().Key)
            End Using
        End Sub

        ' ---- construction and the fake ----

        <TestMethod>
        Public Sub Constructor_NullArguments_Throw()
            Using t As New Support.TempDir()
                Dim s = NewSettings(t.FolderPath)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing, New Support.FakeSecretProtector()))
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(s, Nothing))
            End Using
        End Sub

        <TestMethod>
        Public Sub InMemoryFake_FollowsTheSameContract()
            Dim store As ISecretStore = New Support.InMemorySecretStore()
            Assert.AreEqual(KeyState.Absent, store.Load().Status)
            store.Save("  abc  ")
            Assert.AreEqual("abc", store.Load().Key)
            store.Clear()
            Assert.AreEqual(KeyState.Absent, store.Load().Status)
            Assert.ThrowsExactly(Of ArgumentException)(Sub() store.Save(" "))
        End Sub

    End Class

End Namespace
