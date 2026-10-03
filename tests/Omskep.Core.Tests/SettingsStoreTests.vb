Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Settings

Namespace Settings

    <TestClass>
    Public Class SettingsStoreTests

        Private Shared ReadOnly FixedNow As New DateTime(2026, 10, 2, 8, 30, 0, DateTimeKind.Utc)

        Private Shared Function NewStore(folder As String) As SettingsStore
            Return New SettingsStore(folder, Function() FixedNow)
        End Function

        Private Shared Sub MakeStore(folder As String)
            Dim unused = New SettingsStore(folder)
        End Sub

        Private Shared Sub WriteRaw(store As SettingsStore, content As String)
            File.WriteAllText(store.SettingsPath, content)
        End Sub

        ' ---- first run ----

        <TestMethod>
        Public Sub Load_NoFile_IsNotFound_WithSouthAfricaNorthDefaults()
            Using t As New Support.TempDir()
                Dim r = NewStore(t.FolderPath).Load()
                Assert.AreEqual(SettingsLoadStatus.NotFound, r.Status)
                Assert.AreEqual("southafricanorth", r.Value.Azure.Region)
                Assert.IsNull(r.Value.Azure.KeyBlob)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_MissingFolder_IsNotFound()
            Using t As New Support.TempDir()
                Dim r = NewStore(Path.Combine(t.FolderPath, "not", "yet")).Load()
                Assert.AreEqual(SettingsLoadStatus.NotFound, r.Status)
            End Using
        End Sub

        ' ---- round trip ----

        <TestMethod>
        Public Sub SaveThenLoad_RoundTripsEveryField()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.Region = "westeurope"
                s.Azure.KeyBlob = "QUJDREVG"
                s.DefaultVoices("af-ZA") = "af-ZA-WillemNeural"
                s.SpeakingRatePercent("he-IL") = -10
                store.Save(s)

                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Loaded, r.Status)
                Assert.AreEqual(AppSettings.CurrentSchemaVersion, r.Value.SchemaVersion)
                Assert.AreEqual("westeurope", r.Value.Azure.Region)
                Assert.AreEqual("QUJDREVG", r.Value.Azure.KeyBlob)
                Assert.AreEqual("af-ZA-WillemNeural", r.Value.DefaultVoices("af-ZA"))
                Assert.AreEqual(-10, r.Value.SpeakingRatePercent("he-IL"))
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_TwiceInARow_SecondWins_NoTempLeft()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.Region = "first"
                store.Save(s)
                s.Azure.Region = "second"
                store.Save(s)
                Assert.AreEqual("second", store.Load().Value.Azure.Region)
                Assert.IsFalse(File.Exists(store.SettingsPath & ".tmp"))
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_NullSettings_Throws()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Assert.ThrowsExactly(Of ArgumentNullException)(Sub() store.Save(Nothing))
            End Using
        End Sub

        <TestMethod>
        Public Sub Save_ConcurrentCalls_NeverCorruptTheFile()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Parallel.For(0, 40, Sub(i As Integer)
                                        Dim s As New AppSettings()
                                        s.Azure.Region = "region" & i.ToString()
                                        store.Save(s)
                                    End Sub)
                Assert.AreEqual(SettingsLoadStatus.Loaded, store.Load().Status)
            End Using
        End Sub

        ' ---- atomic write: failure paths ----

        <TestMethod>
        Public Sub Save_WhenWriteFails_ThrowsAndKeepsOldSettings()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.Region = "westeurope"
                s.Azure.KeyBlob = "OLDBLOB"
                store.Save(s)

                Directory.CreateDirectory(store.SettingsPath & ".tmp")   ' forces the temp write to fail
                s.Azure.KeyBlob = "NEWBLOB"
                Dim threw As Boolean = False
                Try
                    store.Save(s)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    threw = True
                End Try

                Assert.IsTrue(threw)
                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Loaded, r.Status)
                Assert.AreEqual("OLDBLOB", r.Value.Azure.KeyBlob)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_LeftoverTempFile_IsIgnored_MainFileWins()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.Region = "westeurope"
                store.Save(s)
                File.WriteAllText(store.SettingsPath & ".tmp", "{ half written")

                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Loaded, r.Status)
                Assert.AreEqual("westeurope", r.Value.Azure.Region)
            End Using
        End Sub

        ' ---- damaged / unreadable ----

        <TestMethod>
        Public Sub Load_GarbageFile_IsDamaged_SetAsideNotDeleted()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                WriteRaw(store, "not json {{{")

                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Damaged, r.Status)
                Assert.AreEqual("southafricanorth", r.Value.Azure.Region)
                Assert.IsFalse(File.Exists(store.SettingsPath))

                Dim aside = Directory.GetFiles(t.FolderPath, "settings.json.damaged-*")
                Assert.HasCount(1, aside)
                Assert.AreEqual("not json {{{", File.ReadAllText(aside(0)))
                Assert.IsTrue(aside(0).EndsWith("20261002T083000Z", StringComparison.Ordinal))

                Assert.AreEqual(SettingsLoadStatus.NotFound, store.Load().Status)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_EmptyFile_And_JsonNull_And_WrongShape_AreDamaged()
            For Each raw In {"", "null", "[1,2,3]", "{""azure"": 5}"}
                Using t As New Support.TempDir()
                    Dim store = NewStore(t.FolderPath)
                    WriteRaw(store, raw)
                    Assert.AreEqual(SettingsLoadStatus.Damaged, store.Load().Status, "input: " & raw)
                End Using
            Next
        End Sub

        <TestMethod>
        Public Sub Load_FileLockedByAnotherHandle_IsUnreadable_AndLeftAlone()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.KeyBlob = "BLOB"
                store.Save(s)

                Using locked As New FileStream(store.SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                    Dim r = store.Load()
                    Assert.AreEqual(SettingsLoadStatus.Unreadable, r.Status)
                End Using

                Assert.IsEmpty(Directory.GetFiles(t.FolderPath, "*.damaged-*"))
                Assert.AreEqual("BLOB", store.Load().Value.Azure.KeyBlob)
            End Using
        End Sub

        ' ---- hand-editing tolerance ----

        <TestMethod>
        Public Sub Load_Utf8Bom_IsAccepted()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim body = Encoding.UTF8.GetBytes("{""schemaVersion"":1,""azure"":{""region"":""westeurope""}}")
                Dim withBom(body.Length + 2) As Byte
                withBom(0) = &HEF : withBom(1) = &HBB : withBom(2) = &HBF
                Array.Copy(body, 0, withBom, 3, body.Length)
                File.WriteAllBytes(store.SettingsPath, withBom)

                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Loaded, r.Status)
                Assert.AreEqual("westeurope", r.Value.Azure.Region)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_TrailingCommasAndComments_AreAccepted()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                WriteRaw(store, "{ // note" & vbLf & """azure"": {""region"": ""westeurope"",},}")
                Assert.AreEqual("westeurope", store.Load().Value.Azure.Region)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_NullSections_AreRepairedToDefaults()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                WriteRaw(store, "{""schemaVersion"":1,""azure"":null,""defaultVoices"":null,""speakingRatePercent"":null}")
                Dim r = store.Load()
                Assert.AreEqual(SettingsLoadStatus.Loaded, r.Status)
                Assert.IsNotNull(r.Value.Azure)
                Assert.AreEqual("southafricanorth", r.Value.Azure.Region)
                Assert.IsEmpty(r.Value.DefaultVoices)
                Assert.IsEmpty(r.Value.SpeakingRatePercent)
            End Using
        End Sub

        <TestMethod>
        Public Sub Load_BlankRegion_FallsBackToDefault()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                WriteRaw(store, "{""azure"":{""region"":""  ""}}")
                Assert.AreEqual("southafricanorth", store.Load().Value.Azure.Region)
            End Using
        End Sub

        ' ---- forward compatibility ----

        <TestMethod>
        Public Sub UnknownFields_SurviveLoadAndResave()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                WriteRaw(store, "{""schemaVersion"":1,""azure"":{""region"":""westeurope"",""futureAzureThing"":7},""albumName"":""Study"",""futureBlock"":{""a"":1}}")

                Dim r = store.Load()
                store.Save(r.Value)

                Dim fileText = File.ReadAllText(store.SettingsPath)
                Assert.IsTrue(fileText.Contains("albumName", StringComparison.Ordinal))
                Assert.IsTrue(fileText.Contains("futureBlock", StringComparison.Ordinal))
                Assert.IsTrue(fileText.Contains("futureAzureThing", StringComparison.Ordinal))
            End Using
        End Sub

        <TestMethod>
        Public Sub SavedFile_ContainsOnlyTheBlob_NotAnyPlaintextKeyField()
            Using t As New Support.TempDir()
                Dim store = NewStore(t.FolderPath)
                Dim s As New AppSettings()
                s.Azure.KeyBlob = "QkxPQg=="
                store.Save(s)
                Dim fileText = File.ReadAllText(store.SettingsPath)
                Assert.IsTrue(fileText.Contains("keyBlob", StringComparison.Ordinal))
                Assert.IsFalse(fileText.Contains("""key""", StringComparison.OrdinalIgnoreCase))
                Assert.IsFalse(fileText.Contains("subscription", StringComparison.OrdinalIgnoreCase))
            End Using
        End Sub

        <TestMethod>
        Public Sub Constructor_BlankFolder_Throws()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() MakeStore(" "))
        End Sub

    End Class

End Namespace