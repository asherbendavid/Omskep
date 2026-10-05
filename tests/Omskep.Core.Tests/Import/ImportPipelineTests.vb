Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text
Imports System.Xml
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Import

Namespace Import

    ''' <summary>Test double: returns fixed pages or throws a fixed exception.</summary>
    Friend NotInheritable Class FakePdfReader
        Implements IPdfPageReader

        Private ReadOnly _pages As IReadOnlyList(Of String)
        Private ReadOnly _failure As Exception

        Public Sub New(pages As IReadOnlyList(Of String))
            _pages = pages
        End Sub

        Public Sub New(failure As Exception)
            _failure = failure
        End Sub

        Public Function ReadPages(pdfPath As String) As IReadOnlyList(Of String) Implements IPdfPageReader.ReadPages
            If _failure IsNot Nothing Then Throw _failure
            Return _pages
        End Function

    End Class

    Friend Module ImportTestHelpers
        Public ReadOnly LF As String = ChrW(10)
        Public Const Locale As String = "af-ZA"
        Public Const Voice As String = "af-ZA-WillemNeural"

        Public Sub AssertWellFormedDocument(document As String)
            Dim settings As New XmlReaderSettings() With {.DtdProcessing = DtdProcessing.Prohibit}
            Try
                Using reader As XmlReader = XmlReader.Create(New StringReader(document), settings)
                    While reader.Read()
                    End While
                End Using
            Catch ex As XmlException
                Assert.Fail("Document is not well-formed: " & ex.Message)
            End Try
        End Sub

        Public Function CountOf(value As String, find As String) As Integer
            Dim n As Integer = 0
            Dim pos As Integer = value.IndexOf(find, StringComparison.Ordinal)
            While pos >= 0
                n += 1
                pos = value.IndexOf(find, pos + find.Length, StringComparison.Ordinal)
            End While
            Return n
        End Function

        Public Function Importer(reader As IPdfPageReader) As DocumentImporter
            Return New DocumentImporter(reader)
        End Function
    End Module

    <TestClass>
    Public Class DocumentComposerTests

        <TestMethod>
        Public Sub Compose_wraps_body_in_speak_and_voice()
            Dim expected As String = "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" & LF &
                                     "<voice name=""af-ZA-WillemNeural"">" & LF &
                                     "Hallo" & LF &
                                     "</voice>" & LF &
                                     "</speak>" & LF
            Assert.AreEqual(expected, DocumentComposer.Compose("Hallo", Locale, Voice))
        End Sub

        <TestMethod>
        Public Sub Compose_output_is_well_formed_even_with_hostile_attribute_values()
            Dim document As String = DocumentComposer.Compose("x", "a""b<c&d", "v'w>""&")
            AssertWellFormedDocument(document)
        End Sub

        <TestMethod>
        Public Sub Compose_with_empty_body_is_still_well_formed()
            AssertWellFormedDocument(DocumentComposer.Compose(String.Empty, Locale, Voice))
        End Sub

        <TestMethod>
        Public Sub Compose_rejects_a_blank_locale_or_voice()
            Dim noLocale As ArgumentException = Assert.ThrowsExactly(Of ArgumentException)(Sub() DocumentComposer.Compose("x", " ", Voice))
            Assert.AreEqual("locale", noLocale.ParamName)
            Dim noVoice As ArgumentException = Assert.ThrowsExactly(Of ArgumentException)(Sub() DocumentComposer.Compose("x", Locale, String.Empty))
            Assert.AreEqual("voiceShortName", noVoice.ParamName)
        End Sub

    End Class

    <TestClass>
    Public Class ImportFlagFinderTests

        <TestMethod>
        Public Sub Wide_space_run_is_flagged_with_line_column_and_length()
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find("one" & LF & "ab" & New String(" "c, 5) & "cd")
            Assert.HasCount(1, flags)
            Assert.AreEqual(ImportFlagKind.WideSpaceRun, flags(0).Kind)
            Assert.AreEqual(2, flags(0).Line)
            Assert.AreEqual(3, flags(0).Column)
            Assert.AreEqual(5, flags(0).Length)
        End Sub

        <TestMethod>
        Public Sub One_or_two_spaces_are_not_flagged()
            Assert.IsEmpty(ImportFlagFinder.Find("a b  c"))
        End Sub

        <TestMethod>
        Public Sub Line_end_hyphen_after_a_letter_is_flagged()
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find("ver-" & LF & "skillende")
            Assert.HasCount(1, flags)
            Assert.AreEqual(ImportFlagKind.LineEndHyphen, flags(0).Kind)
            Assert.AreEqual(1, flags(0).Line)
            Assert.AreEqual(4, flags(0).Column)
        End Sub

        <TestMethod>
        Public Sub Hyphens_that_are_not_split_words_are_not_flagged()
            ' mid-line, after a digit, after a space, and a lone hyphen
            Assert.IsEmpty(ImportFlagFinder.Find("see-" & "en" & LF & "1950-" & LF & "a -" & LF & "-"))
        End Sub

        <TestMethod>
        Public Sub Flag_lines_match_document_lines_including_the_scaffold()
            Dim document As String = DocumentComposer.Compose("a" & LF & "b   c", Locale, Voice)
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find(document)
            Assert.HasCount(1, flags)
            ' line 1 speak, 2 voice, 3 "a", 4 "b   c"
            Assert.AreEqual(4, flags(0).Line)
            Assert.AreEqual(2, flags(0).Column)
        End Sub

        <TestMethod>
        Public Sub Null_empty_and_huge_runs_do_not_throw()
            Assert.IsEmpty(ImportFlagFinder.Find(Nothing))
            Assert.IsEmpty(ImportFlagFinder.Find(String.Empty))
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find(New String(" "c, 200000))
            Assert.HasCount(1, flags)
            Assert.AreEqual(200000, flags(0).Length)
        End Sub

        <TestMethod>
        Public Sub Windows_line_endings_do_not_hide_a_line_end_hyphen()
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find("ver-" & ChrW(13) & LF & "x")
            Assert.HasCount(1, flags)
        End Sub

    End Class

    <TestClass>
    Public Class DocumentImporterTests

        Private Const Header As String = "Torah Navorsing Akademie     Die Heelal: Toeval?"

        Private Shared Function Pages(ParamArray texts As String()) As IReadOnlyList(Of String)
            Return texts
        End Function

        Private Shared Function StudyPage(n As Integer) As String
            Return Header & vbCrLf & n & " " & vbCrLf & "Hierdie is bladsy " & n & " met genoeg woorde om as teks te tel." & vbCrLf &
                   "Die tweede reël van die bladsy eindig met ver-" & vbCrLf & "skillende woorde. " & vbCrLf
        End Function

        ' ---- happy path ----

        <TestMethod>
        Public Sub A_normal_pdf_becomes_a_well_formed_document()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), StudyPage(2), StudyPage(3)))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.Imported, result.Outcome)
            Assert.IsTrue(result.Succeeded)
            Assert.AreEqual(3, result.PageCount)
            Assert.IsEmpty(result.EmptyPages)
            AssertWellFormedDocument(result.Document)
            Assert.StartsWith("<speak version=""1.0""", result.Document)
        End Sub

        <TestMethod>
        Public Sub Headers_and_page_numbers_are_never_silently_removed()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), StudyPage(2), StudyPage(3)))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(3, CountOf(result.Document, Header))
            Assert.AreEqual(3, CountOf(result.Document, "Hierdie is bladsy"))
        End Sub

        <TestMethod>
        Public Sub Pages_are_separated_by_one_blank_line_and_use_LF_only()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages("Eerste bladsy se teks hier." & vbCrLf, "Tweede bladsy se teks hier." & vbCrLf))).ImportPdf("x.pdf", Locale, Voice)
            Assert.Contains("Eerste bladsy se teks hier." & LF & LF & "Tweede bladsy se teks hier." & LF & "</voice>", result.Document)
            Assert.DoesNotContain(vbCr, result.Document)
        End Sub

        <TestMethod>
        Public Sub Wide_gaps_and_line_end_hyphens_are_flagged_per_page()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), StudyPage(2)))).ImportPdf("x.pdf", Locale, Voice)
            Dim wide As Integer = 0
            Dim hyphens As Integer = 0
            For Each flag In result.Flags
                If flag.Kind = ImportFlagKind.WideSpaceRun Then wide += 1 Else hyphens += 1
            Next
            Assert.AreEqual(2, wide)
            Assert.AreEqual(2, hyphens)
            Assert.Contains("For review: 2 wide gaps and 2 line-end hyphens.", result.Message)
        End Sub

        <TestMethod>
        Public Sub Sanitizer_counts_are_summed_over_all_pages_and_reported()
            Dim ornament As String = "2026.06" & vbCrLf & ChrW(&HF098) & ChrW(&HF099) & vbCrLf & "Die titel van die studie"
            Dim quotes As String = "Dit is " & ChrW(&H2018) & "n goeie bladsy met " & ChrW(&H2018) & "n klomp woorde."
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(ornament, quotes))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(2, result.Sanitation.RemovedPrivateUseTotal)
            Assert.AreEqual(2, result.Sanitation.FixedArticleQuotes)
            Assert.Contains("removed 2 decorative symbols", result.Message)
            Assert.Contains("corrected 2 quote marks", result.Message)
        End Sub

        ' ---- empty pages ----

        <TestMethod>
        Public Sub Pages_without_text_are_skipped_and_listed_by_number()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), String.Empty, StudyPage(3), "  " & vbCrLf & " ", StudyPage(5)))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.ImportedWithEmptyPages, result.Outcome)
            Assert.IsTrue(result.Succeeded)
            Assert.AreEqual(5, result.PageCount)
            Assert.HasCount(2, result.EmptyPages)
            Assert.Contains(2, result.EmptyPages)
            Assert.Contains(4, result.EmptyPages)
            Assert.Contains("2 pages had no readable text and were skipped: 2, 4.", result.Message)
            Assert.AreEqual(3, CountOf(result.Document, "Hierdie is bladsy"))
        End Sub

        <TestMethod>
        Public Sub A_page_holding_only_decorative_or_control_characters_counts_as_empty()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), ChrW(&HF098) & ChrW(0) & " ", StudyPage(3)))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.ImportedWithEmptyPages, result.Outcome)
            Assert.AreEqual(2, result.EmptyPages(0))
        End Sub

        <TestMethod>
        Public Sub A_null_page_from_the_reader_is_treated_as_empty()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(StudyPage(1), Nothing, StudyPage(3)))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.ImportedWithEmptyPages, result.Outcome)
            Assert.Contains(2, result.EmptyPages)
        End Sub

        <TestMethod>
        Public Sub Many_empty_pages_are_summarised_not_listed_in_full()
            Dim all As New List(Of String)()
            all.Add(StudyPage(1))
            For k As Integer = 2 To 15
                all.Add(String.Empty)
            Next
            Dim result As ImportResult = Importer(New FakePdfReader(all)).ImportPdf("x.pdf", Locale, Voice)
            Assert.HasCount(14, result.EmptyPages)
            Assert.Contains("2, 3, 4, 5, 6, 7, 8, 9, 10, 11 and 4 more.", result.Message)
        End Sub

        ' ---- failure outcomes ----

        <TestMethod>
        Public Sub A_scanned_pdf_with_no_text_gives_a_clear_message_not_an_empty_document()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages(String.Empty, String.Empty, String.Empty))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.NoTextFound, result.Outcome)
            Assert.IsFalse(result.Succeeded)
            Assert.AreEqual(String.Empty, result.Document)
            Assert.Contains("scanned", result.Message)
        End Sub

        <TestMethod>
        Public Sub A_scan_whose_only_text_is_page_numbers_counts_as_no_text()
            Dim result As ImportResult = Importer(New FakePdfReader(Pages("1", "2", "3", "4"))).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.NoTextFound, result.Outcome)
            Assert.AreEqual(String.Empty, result.Document)
        End Sub

        <TestMethod>
        Public Sub A_pdf_with_no_pages_gives_no_text_found()
            Assert.AreEqual(ImportOutcome.NoTextFound, Importer(New FakePdfReader(Pages())).ImportPdf("x.pdf", Locale, Voice).Outcome)
            Assert.AreEqual(ImportOutcome.NoTextFound, Importer(New FakePdfReader(DirectCast(Nothing, IReadOnlyList(Of String)))).ImportPdf("x.pdf", Locale, Voice).Outcome)
        End Sub

        <TestMethod>
        Public Sub A_password_protected_pdf_gives_a_clear_message()
            Dim result As ImportResult = Importer(New FakePdfReader(New PdfPasswordRequiredException())).ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(ImportOutcome.PasswordProtected, result.Outcome)
            Assert.AreEqual(String.Empty, result.Document)
            Assert.Contains("password", result.Message)
        End Sub

        <TestMethod>
        Public Sub Any_other_reader_failure_becomes_an_unreadable_result_not_an_exception()
            Dim failures As Exception() = {
                New IOException("The process cannot access the file."),
                New InvalidOperationException("Bad xref table."),
                New UnauthorizedAccessException("Denied."),
                New OutOfMemoryException("Too big.")
            }
            For Each failure In failures
                Dim result As ImportResult = Importer(New FakePdfReader(failure)).ImportPdf("x.pdf", Locale, Voice)
                Assert.AreEqual(ImportOutcome.Unreadable, result.Outcome)
                Assert.AreEqual(String.Empty, result.Document)
                Assert.Contains(failure.Message, result.Message)
            Next
        End Sub

        <TestMethod>
        Public Sub A_blank_voice_or_locale_is_a_caller_bug_and_throws_before_reading()
            Dim importer1 As DocumentImporter = Importer(New FakePdfReader(New IOException("must not be reached")))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() importer1.ImportPdf("x.pdf", Locale, " "))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() importer1.ImportText("x", String.Empty, Voice))
        End Sub

        ' ---- pasted text ----

        <TestMethod>
        Public Sub Pasted_word_text_keeps_curly_quotes_and_escapes_a_literal_ampersand()
            Dim pasted As String = ChrW(&H201C) & "Moses & Aaron" & ChrW(&H201D) & " het gesê:" & vbCrLf & ChrW(&H2018) & "n Mens, <vry> en ` ok." & vbCrLf
            Dim result As ImportResult = Importer(New FakePdfReader(Pages())).ImportText(pasted, Locale, Voice)
            Assert.AreEqual(ImportOutcome.Imported, result.Outcome)
            Assert.Contains(ChrW(&H201C) & "Moses &amp; Aaron" & ChrW(&H201D), result.Document)
            Assert.Contains("&lt;vry&gt;", result.Document)
            Assert.DoesNotContain(vbCr, result.Document)
            AssertWellFormedDocument(result.Document)
        End Sub

        <TestMethod>
        Public Sub Empty_or_whitespace_paste_gives_no_text_found()
            Dim importer1 As DocumentImporter = Importer(New FakePdfReader(Pages()))
            For Each pasted As String In {Nothing, String.Empty, "   ", vbCrLf & vbCrLf, ChrW(&HF098).ToString()}
                Dim result As ImportResult = importer1.ImportText(pasted, Locale, Voice)
                Assert.AreEqual(ImportOutcome.NoTextFound, result.Outcome)
                Assert.AreEqual(String.Empty, result.Document)
            Next
        End Sub

        ' ---- re-entry and scale ----

        <TestMethod>
        Public Sub Importing_twice_with_the_same_importer_gives_identical_results()
            Dim importer1 As DocumentImporter = Importer(New FakePdfReader(Pages(StudyPage(1), StudyPage(2))))
            Dim first As ImportResult = importer1.ImportPdf("x.pdf", Locale, Voice)
            Dim second As ImportResult = importer1.ImportPdf("x.pdf", Locale, Voice)
            Assert.AreEqual(first.Document, second.Document)
            Assert.AreEqual(first.Message, second.Message)
        End Sub

        <TestMethod>
        Public Sub A_forty_five_page_study_imports_quickly_and_stays_well_formed()
            Dim all As New List(Of String)()
            For k As Integer = 1 To 45
                Dim page As New StringBuilder()
                For line As Integer = 1 To 30
                    page.Append("Dit is reël ").Append(line).Append(" met ").Append(ChrW(&H2018)).Append("n woord & nog ").Append(ChrW(&H2019)).Append("s teks.").Append(vbCrLf)
                Next
                all.Add(page.ToString())
            Next
            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim result As ImportResult = Importer(New FakePdfReader(all)).ImportPdf("x.pdf", Locale, Voice)
            watch.Stop()
            Assert.AreEqual(ImportOutcome.Imported, result.Outcome)
            Assert.AreEqual(45 * 30, result.Sanitation.FixedArticleQuotes)
            AssertWellFormedDocument(result.Document)
            Assert.IsLessThan(3000L, watch.ElapsedMilliseconds)
        End Sub

    End Class

End Namespace
