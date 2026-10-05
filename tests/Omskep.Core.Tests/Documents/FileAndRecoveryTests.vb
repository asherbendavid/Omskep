Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    ''' <summary>Gives each test its own scratch folder (with an apostrophe in the name) and removes it afterwards.</summary>
    Public MustInherit Class ScratchFolderTests

        Private ReadOnly _folders As New List(Of String)()

        Protected Function NewFolder() As String
            Dim folder As String = Path.Combine(Path.GetTempPath(), "Omskep O'Brien tests", Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(folder)
            _folders.Add(folder)
            Return folder
        End Function

        <TestCleanup>
        Public Sub RemoveScratchFolders()
            For Each folder In _folders
                Try
                    Directory.Delete(folder, True)
                Catch ex As IOException
                    ' Best effort; the OS temp folder is cleaned up eventually.
                End Try
            Next
        End Sub

    End Class

    <TestClass>
    Public Class DocumentFileTests
        Inherits ScratchFolderTests

        Private Shared ReadOnly LF As String = ChrW(10)
        Private Const Sample As String = "<speak>Dit is ë, ê, ô en ’n Moses & Aaron עִברִית</speak>"

        <TestMethod>
        Public Sub Text_survives_a_save_and_load_unchanged()
            Dim file As String = Path.Combine(NewFolder(), "studie.ssml")
            DocumentFile.Save(file, Sample)
            Assert.AreEqual(Sample, DocumentFile.Load(file))
        End Sub

        <TestMethod>
        Public Sub Save_writes_utf8_without_a_BOM_and_LF_line_endings()
            Dim file As String = Path.Combine(NewFolder(), "a.ssml")
            DocumentFile.Save(file, "een" & vbCrLf & "twee" & vbCr & "drie" & LF & "ë")
            Dim bytes As Byte() = System.IO.File.ReadAllBytes(file)
            Assert.AreNotEqual(CByte(&HEF), bytes(0))
            Assert.AreEqual("een" & LF & "twee" & LF & "drie" & LF & "ë", Encoding.UTF8.GetString(bytes))
        End Sub

        <TestMethod>
        Public Sub Save_creates_missing_folders_and_replaces_an_existing_file()
            Dim file As String = Path.Combine(NewFolder(), "deeper", "still", "a.ssml")
            DocumentFile.Save(file, "eerste")
            DocumentFile.Save(file, "tweede")
            Assert.AreEqual("tweede", DocumentFile.Load(file))
            Assert.IsFalse(System.IO.File.Exists(file & ".tmp"))
        End Sub

        <TestMethod>
        Public Sub A_failed_save_reports_a_clear_error_and_leaves_the_old_file_alone()
            Dim folder As String = NewFolder()
            Dim good As String = Path.Combine(folder, "good.ssml")
            DocumentFile.Save(good, "oorspronklik")
            ' A "folder" that is really a file makes the save impossible.
            Dim blocked As String = Path.Combine(good, "x.ssml")
            Dim ex As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Save(blocked, "nuut"))
            Assert.Contains("still in the editor", ex.Message)
            Assert.AreEqual("oorspronklik", DocumentFile.Load(good))
        End Sub

        <TestMethod>
        Public Sub Text_with_an_unpaired_surrogate_is_refused_not_silently_altered()
            Dim file As String = Path.Combine(NewFolder(), "a.ssml")
            Dim ex As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Save(file, "a" & ChrW(&HD800) & "b"))
            Assert.Contains("unpaired surrogate", ex.Message)
            Assert.IsFalse(System.IO.File.Exists(file))
        End Sub

        <TestMethod>
        Public Sub Load_strips_a_UTF8_BOM_and_normalises_line_endings()
            Dim file As String = Path.Combine(NewFolder(), "bom.ssml")
            Dim body As Byte() = New UTF8Encoding(False).GetBytes("een" & vbCrLf & "twee" & vbCr & "drie")
            Dim withBom As Byte() = (New Byte() {&HEF, &HBB, &HBF}).Concat(body).ToArray()
            System.IO.File.WriteAllBytes(file, withBom)
            Assert.AreEqual("een" & LF & "twee" & LF & "drie", DocumentFile.Load(file))
        End Sub

        <TestMethod>
        Public Sub Load_reads_UTF16_files_with_a_BOM()
            Dim folder As String = NewFolder()
            Dim le As String = Path.Combine(folder, "le.txt")
            Dim be As String = Path.Combine(folder, "be.txt")
            System.IO.File.WriteAllBytes(le, New UnicodeEncoding(False, True).GetPreamble().Concat(New UnicodeEncoding(False, False).GetBytes("Dié")).ToArray())
            System.IO.File.WriteAllBytes(be, New UnicodeEncoding(True, True).GetPreamble().Concat(New UnicodeEncoding(True, False).GetBytes("Dié")).ToArray())
            Assert.AreEqual("Dié", DocumentFile.Load(le))
            Assert.AreEqual("Dié", DocumentFile.Load(be))
        End Sub

        <TestMethod>
        Public Sub Load_never_edits_markup_it_only_normalises_line_endings()
            Dim file As String = Path.Combine(NewFolder(), "wip.ssml")
            Dim broken As String = "<speak><voice>half & <typed" & LF & "</speak>"
            System.IO.File.WriteAllText(file, broken, New UTF8Encoding(False))
            Assert.AreEqual(broken, DocumentFile.Load(file))
        End Sub

        <TestMethod>
        Public Sub A_file_that_is_not_utf8_gets_a_clear_message()
            Dim file As String = Path.Combine(NewFolder(), "ansi.txt")
            System.IO.File.WriteAllBytes(file, New Byte() {&H44, &H69, &HE9, &H20, &H68, &HEB, &H6C}) ' Windows-1252
            Dim ex As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(file))
            Assert.Contains("not UTF-8", ex.Message)
        End Sub

        <TestMethod>
        Public Sub Binary_files_are_refused()
            Dim folder As String = NewFolder()
            Dim withNul As String = Path.Combine(folder, "nul.ssml")
            System.IO.File.WriteAllBytes(withNul, New Byte() {&H61, &H0, &H62})
            Dim ex1 As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(withNul))
            Assert.Contains("binary", ex1.Message)
            Dim pdf As String = Path.Combine(folder, "fake.ssml")
            System.IO.File.WriteAllBytes(pdf, New Byte() {&H25, &H50, &H44, &H46, &H2D, &H31, &H2E, &H37, &HA, &H25, &HE2, &HE3, &HCF, &HD3})
            Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(pdf))
        End Sub

        <TestMethod>
        Public Sub A_missing_or_oversized_file_gets_a_clear_message()
            Dim folder As String = NewFolder()
            Dim missing As String = Path.Combine(folder, "nope.ssml")
            Dim ex As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(missing))
            Assert.Contains("could not be opened", ex.Message)
            Dim big As String = Path.Combine(folder, "big.ssml")
            DocumentFile.Save(big, New String("a"c, 2000))
            Dim tooBig As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(big, 1000))
            Assert.Contains("too large", tooBig.Message)
        End Sub

        <TestMethod>
        Public Sub A_locked_file_gets_a_clear_message_not_a_raw_exception()
            Dim file As String = Path.Combine(NewFolder(), "locked.ssml")
            DocumentFile.Save(file, "inhoud")
            Using held As New FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)
                Dim ex As DocumentFileException = Assert.ThrowsExactly(Of DocumentFileException)(Sub() DocumentFile.Load(file))
                Assert.Contains("could not be opened", ex.Message)
            End Using
        End Sub

        <TestMethod>
        Public Sub Blank_paths_and_null_text_are_caller_bugs()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() DocumentFile.Load(" "))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() DocumentFile.Save(Nothing, "x"))
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() DocumentFile.Save("x.ssml", Nothing))
        End Sub

        <TestMethod>
        Public Sub An_eighty_thousand_character_document_round_trips()
            Dim file As String = Path.Combine(NewFolder(), "groot.ssml")
            Dim text As String = String.Join(LF, Enumerable.Range(1, 1300).Select(Function(n) "Reël " & n & " met ë en ’n woord & meer."))
            DocumentFile.Save(file, text)
            Assert.AreEqual(text, DocumentFile.Load(file))
        End Sub

    End Class

    <TestClass>
    Public Class RecoveryStoreTests
        Inherits ScratchFolderTests

        Private Shared ReadOnly LF As String = ChrW(10)

        Private Shared ReadOnly NobodyAlive As Func(Of Integer, Date, Boolean) = Function(pid As Integer, started As Date) False
        Private Shared ReadOnly EveryoneAlive As Func(Of Integer, Date, Boolean) = Function(pid As Integer, started As Date) True

        <TestMethod>
        Public Sub A_snapshot_from_a_dead_instance_is_offered_back_intact()
            Dim folder As String = NewFolder()
            Dim text As String = "<speak>Moses & ’n Hebreeus עִברִית" & vbCrLf & "ë" & ChrW(0) & ChrW(1) & "</speak>"
            Dim crashed As New RecoveryStore(folder, NobodyAlive)
            Assert.IsTrue(crashed.Snapshot(text, "C:\studies\heelal.ssml", "heelal.ssml"))

            Dim items = New RecoveryStore(folder, NobodyAlive).FindOrphans()
            Assert.HasCount(1, items)
            Assert.AreEqual(text, items(0).Text)
            Assert.AreEqual("C:\studies\heelal.ssml", items(0).OriginalPath)
            Assert.AreEqual("heelal.ssml", items(0).DisplayName)
        End Sub

        <TestMethod>
        Public Sub An_untitled_document_has_no_original_path()
            Dim folder As String = NewFolder()
            Assert.IsTrue(New RecoveryStore(folder, NobodyAlive).Snapshot("tekst", Nothing, "Untitled"))
            Dim items = New RecoveryStore(folder, NobodyAlive).FindOrphans()
            Assert.HasCount(1, items)
            Assert.IsNull(items(0).OriginalPath)
        End Sub

        <TestMethod>
        Public Sub Each_snapshot_replaces_the_previous_one_from_the_same_instance()
            Dim folder As String = NewFolder()
            Dim store As New RecoveryStore(folder, NobodyAlive)
            store.Snapshot("eerste", Nothing, "Untitled")
            store.Snapshot("tweede", Nothing, "Untitled")
            Assert.HasCount(1, Directory.GetFiles(folder))
            Assert.AreEqual("tweede", New RecoveryStore(folder, NobodyAlive).FindOrphans()(0).Text)
        End Sub

        <TestMethod>
        Public Sub An_instance_never_offers_its_own_snapshot()
            Dim folder As String = NewFolder()
            Dim store As New RecoveryStore(folder, NobodyAlive)
            store.Snapshot("tekst", Nothing, "Untitled")
            Assert.IsEmpty(store.FindOrphans())
        End Sub

        <TestMethod>
        Public Sub A_running_instances_snapshot_is_not_offered_to_another_instance()
            Dim folder As String = NewFolder()
            Assert.IsTrue(New RecoveryStore(folder, EveryoneAlive).Snapshot("nog besig", Nothing, "Untitled"))
            Assert.IsEmpty(New RecoveryStore(folder, EveryoneAlive).FindOrphans())
        End Sub

        <TestMethod>
        Public Sub Two_instances_keep_separate_snapshots()
            Dim folder As String = NewFolder()
            Dim a As New RecoveryStore(folder, NobodyAlive)
            Dim b As New RecoveryStore(folder, NobodyAlive)
            a.Snapshot("van A", Nothing, "A")
            b.Snapshot("van B", Nothing, "B")
            Assert.HasCount(2, Directory.GetFiles(folder))
            Dim seenByNew = New RecoveryStore(folder, NobodyAlive).FindOrphans()
            Assert.HasCount(2, seenByNew)
        End Sub

        <TestMethod>
        Public Sub Discard_removes_the_snapshot_and_is_safe_to_repeat()
            Dim folder As String = NewFolder()
            Dim store As New RecoveryStore(folder, NobodyAlive)
            store.Discard()
            store.Snapshot("tekst", Nothing, "Untitled")
            store.Discard()
            store.Discard()
            Assert.IsEmpty(Directory.GetFiles(folder))
        End Sub

        <TestMethod>
        Public Sub Remove_deletes_a_restored_orphan()
            Dim folder As String = NewFolder()
            Assert.IsTrue(New RecoveryStore(folder, NobodyAlive).Snapshot("tekst", Nothing, "Untitled"))
            Dim reader As New RecoveryStore(folder, NobodyAlive)
            Dim item = reader.FindOrphans()(0)
            reader.Remove(item)
            Assert.IsEmpty(reader.FindOrphans())
            Assert.IsEmpty(Directory.GetFiles(folder))
        End Sub

        <TestMethod>
        Public Sub Garbage_files_are_ignored_and_left_alone()
            Dim folder As String = NewFolder()
            Dim good As New RecoveryStore(folder, NobodyAlive)
            good.Snapshot("goeie", Nothing, "Untitled")
            File.WriteAllText(Path.Combine(folder, "recovery-empty.json"), String.Empty)
            File.WriteAllText(Path.Combine(folder, "recovery-text.json"), "not json at all")
            File.WriteAllText(Path.Combine(folder, "recovery-trunc.json"), "{""Version"":1,""Text"":""afgebr")
            File.WriteAllText(Path.Combine(folder, "recovery-version.json"), "{""Version"":99,""Text"":""x""}")
            File.WriteAllText(Path.Combine(folder, "recovery-notext.json"), "{""Version"":1}")
            File.WriteAllBytes(Path.Combine(folder, "recovery-binary.json"), New Byte() {0, 1, 2, &HFF, &HFE})
            File.WriteAllText(Path.Combine(folder, "recovery-x.json.tmp"), "{""Version"":1,""Text"":""tmp""}")
            File.WriteAllText(Path.Combine(folder, "other.json"), "{}")

            Dim items = New RecoveryStore(folder, NobodyAlive).FindOrphans()
            Assert.HasCount(1, items)
            Assert.AreEqual("goeie", items(0).Text)
            Assert.HasCount(9, Directory.GetFiles(folder))
        End Sub

        <TestMethod>
        Public Sub A_missing_folder_offers_nothing()
            Dim folder As String = Path.Combine(NewFolder(), "does-not-exist-yet")
            Assert.IsEmpty(New RecoveryStore(folder, NobodyAlive).FindOrphans())
        End Sub

        <TestMethod>
        Public Sub A_snapshot_that_cannot_be_written_returns_false_and_never_throws()
            Dim folder As String = NewFolder()
            Dim blocker As String = Path.Combine(folder, "iets")
            File.WriteAllText(blocker, "ek is 'n lêer")
            ' The recovery "folder" is a file, so nothing can be written there.
            Assert.IsFalse(New RecoveryStore(blocker, NobodyAlive).Snapshot("tekst", Nothing, "Untitled"))
        End Sub

        <TestMethod>
        Public Sub Text_with_an_unpaired_surrogate_is_still_snapshotted_with_a_replacement_character()
            ' The editor cannot hold an unpaired surrogate, but if one ever arrives the rest of the edits must
            ' still be kept: the snapshot succeeds and the bad character comes back as U+FFFD.
            Dim folder As String = NewFolder()
            Dim text As String = "a" & ChrW(&HD800) & "b"
            Assert.IsTrue(New RecoveryStore(folder, NobodyAlive).Snapshot(text, Nothing, "Untitled"))
            Assert.AreEqual("a" & ChrW(&HFFFD) & "b", New RecoveryStore(folder, NobodyAlive).FindOrphans()(0).Text)
        End Sub

        <TestMethod>
        Public Sub A_large_document_snapshots_and_restores_exactly()
            Dim folder As String = NewFolder()
            Dim text As String = String.Join(LF, Enumerable.Range(1, 20000).Select(Function(n) "Reël " & n & " & ë <b>"))
            Assert.IsTrue(New RecoveryStore(folder, NobodyAlive).Snapshot(text, Nothing, "Untitled"))
            Assert.AreEqual(text, New RecoveryStore(folder, NobodyAlive).FindOrphans()(0).Text)
        End Sub

        <TestMethod>
        Public Sub The_real_liveness_check_sees_this_running_process_as_alive()
            Dim folder As String = NewFolder()
            ' Same process: the default probe must see its own earlier snapshot as belonging to a live instance.
            Dim first As New RecoveryStore(folder)
            first.Snapshot("nog besig", Nothing, "Untitled")
            Assert.IsEmpty(New RecoveryStore(folder).FindOrphans())
        End Sub

        <TestMethod>
        Public Sub Blank_folder_and_null_item_are_caller_bugs()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() MakeStore(" "))
            Dim store As New RecoveryStore(NewFolder())
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() store.Remove(Nothing))
        End Sub

        Private Shared Sub MakeStore(folder As String)
            Dim unused As New RecoveryStore(folder)
        End Sub

        <TestMethod>
        Public Sub The_default_folder_is_under_local_application_data()
            Assert.Contains("Omskep", RecoveryStore.DefaultFolder())
            Assert.EndsWith("recovery", RecoveryStore.DefaultFolder())
        End Sub

    End Class

End Namespace
