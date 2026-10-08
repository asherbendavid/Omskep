Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Import

Namespace Documents

    <TestClass>
    Public Class HebrewOrderTests

        Private Shared ReadOnly Aleph As String = ChrW(&H5D0)
        Private Shared ReadOnly Lamed As String = ChrW(&H5DC)
        Private Shared ReadOnly He As String = ChrW(&H5D4)
        Private Shared ReadOnly Yod As String = ChrW(&H5D9)
        Private Shared ReadOnly Vav As String = ChrW(&H5D5)
        Private Shared ReadOnly FinalMem As String = ChrW(&H5DD)
        Private Shared ReadOnly Tsere As String = ChrW(&H5B5)

        ' Logical (reading) order of Elohim: aleph lamed he yod final-mem.
        Private Shared ReadOnly Elohim As String = Aleph & Lamed & He & Yod & FinalMem

        Private Shared Function Reversed(value As String) As String
            Dim chars As Char() = value.ToCharArray()
            Array.Reverse(chars)
            Return New String(chars)
        End Function

        <TestMethod>
        Public Sub The_El_example_from_the_real_PDF_is_put_back_in_reading_order()
            Dim fromPdf As String = Lamed & Tsere & Aleph                 ' as shown in the editor
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(fromPdf, False)
            Assert.AreEqual(Aleph & Tsere & Lamed, result.Text)           ' aleph with tsere, then lamed
            Assert.AreEqual(1, result.RunsReversed)
        End Sub

        <TestMethod>
        Public Sub A_phrase_gets_its_letters_and_its_word_order_restored()
            Dim logical As String = Elohim & " " & He & Vav & Aleph
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(Reversed(logical), True)
            Assert.AreEqual(logical, result.Text)
            Assert.AreEqual(1, result.RunsReversed)
        End Sub

        <TestMethod>
        Public Sub In_a_whole_document_a_run_that_already_looks_correct_is_left_alone()
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns("x " & Elohim & " y", True)
            Assert.AreEqual("x " & Elohim & " y", result.Text)
            Assert.AreEqual(0, result.RunsReversed)
            Assert.AreEqual(1, result.RunsSkipped)
            Assert.IsEmpty(result.Edits)
        End Sub

        <TestMethod>
        Public Sub A_selection_is_reversed_even_if_it_looks_correct_because_the_user_decided()
            Assert.AreEqual(Reversed(Elohim), HebrewOrder.ReverseRuns(Elohim, False).Text)
        End Sub

        <TestMethod>
        Public Sub Text_around_the_Hebrew_is_untouched()
            Dim raw As String = "Die woord " & Reversed(Elohim) & " beteken God."
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(raw, True)
            Assert.AreEqual("Die woord " & Elohim & " beteken God.", result.Text)

            Dim markup As String = "<voice name=""he-IL-AvriNeural"">" & Reversed(Elohim) & "</voice>"
            Assert.AreEqual("<voice name=""he-IL-AvriNeural"">" & Elohim & "</voice>", HebrewOrder.ReverseRuns(markup, True).Text)
        End Sub

        <TestMethod>
        Public Sub Spaces_outside_a_run_are_not_part_of_it()
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns("  " & Reversed(Elohim) & "  ", True)
            Assert.AreEqual("  " & Elohim & "  ", result.Text)
        End Sub

        <TestMethod>
        Public Sub Several_runs_give_edits_that_apply_from_the_last_to_the_first()
            Dim raw As String = "a " & Reversed(Elohim) & " b " & Reversed(Aleph & Lamed & Vav) & " c"
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(raw, False)
            Assert.AreEqual(2, result.RunsReversed)
            Assert.HasCount(2, result.Edits)
            Assert.IsGreaterThan(result.Edits(1).Start, result.Edits(0).Start)

            Dim applied As New StringBuilder(raw)
            For Each edit In result.Edits
                applied.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText)
            Next
            Assert.AreEqual(result.Text, applied.ToString())
        End Sub

        <TestMethod>
        Public Sub Reversing_twice_gives_the_original_back()
            Dim raw As String = "a " & Lamed & Tsere & Aleph & " " & Reversed(Elohim)
            Dim once As String = HebrewOrder.ReverseRuns(raw, False).Text
            Assert.AreEqual(raw, HebrewOrder.ReverseRuns(once, False).Text)
        End Sub

        <TestMethod>
        Public Sub A_presentation_form_keeps_its_letter_in_place_and_is_normalised()
            ' vav with holam as one presentation character, then lamed: reversed it is lamed then vav + holam.
            Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(ChrW(&HFB4B) & Lamed, False)
            Assert.AreEqual(Lamed & Vav & ChrW(&H5B9), result.Text)
        End Sub

        <TestMethod>
        Public Sub No_Hebrew_null_and_empty_change_nothing()
            Assert.AreEqual("Gewone teks <b>&amp;</b>", HebrewOrder.ReverseRuns("Gewone teks <b>&amp;</b>", True).Text)
            Assert.AreEqual(0, HebrewOrder.ReverseRuns("abc", False).RunsReversed)
            Assert.AreEqual(String.Empty, HebrewOrder.ReverseRuns(Nothing, True).Text)
            Assert.AreEqual(String.Empty, HebrewOrder.ReverseRuns(String.Empty, False).Text)
        End Sub

        <TestMethod>
        Public Sub Random_garbage_never_throws_and_only_hebrew_runs_change()
            Dim pool As String() = {Aleph, Lamed, FinalMem, Tsere, ChrW(&HFB4B), " ", "  ", "a", "b", "<", ">", "&", ChrW(10), ChrW(&HD800), ChrW(&HDC00), ChrW(&H5BE), ChrW(&H5C3), "1"}
            Dim hebrewOrSpace As New Regex("[\u0591-\u05C7\u05D0-\u05EA\u05EF-\u05F4\uFB1D-\uFB4F ]")
            For seed As Integer = 1 To 400
                Dim rng As New Random(seed)
                Dim sb As New StringBuilder()
                For k As Integer = 1 To rng.Next(0, 40)
                    sb.Append(pool(rng.Next(pool.Length)))
                Next
                Dim raw As String = sb.ToString()
                For Each skip As Boolean In {True, False}
                    Dim result As HebrewReverseResult = HebrewOrder.ReverseRuns(raw, skip)
                    Assert.HasCount(result.RunsReversed, result.Edits, $"seed {seed}")
                    Assert.AreEqual(hebrewOrSpace.Replace(raw, String.Empty), hebrewOrSpace.Replace(result.Text, String.Empty), $"seed {seed}")
                Next
            Next
        End Sub

    End Class

    <TestClass>
    Public Class CommandLineArgumentsTests

        Private Shared ReadOnly Exists As Func(Of String, Boolean) = Function(path As String) path.EndsWith(".ssml", StringComparison.Ordinal)

        Private Shared Function RejectsPath(path As String) As Boolean
            Throw New ArgumentException("bad path")
        End Function

        <TestMethod>
        Public Sub The_first_existing_file_after_the_program_name_is_returned()
            Dim args As String() = {"C:\Apps\Omskep.exe", "C:\Studies\O'Brien Gelees\Heelal ë.ssml"}
            Assert.AreEqual("C:\Studies\O'Brien Gelees\Heelal ë.ssml", CommandLineArguments.FindDocumentPath(args, Exists))
        End Sub

        <TestMethod>
        Public Sub The_program_name_itself_is_never_taken_as_the_document()
            Assert.IsNull(CommandLineArguments.FindDocumentPath({"C:\Apps\weird.ssml"}, Exists))
        End Sub

        <TestMethod>
        Public Sub Switches_and_missing_files_are_ignored()
            Dim args As String() = {"Omskep.exe", "--debug", "/x", "missing.txt", " ", "C:\a\b.ssml", "C:\a\c.ssml"}
            Assert.AreEqual("C:\a\b.ssml", CommandLineArguments.FindDocumentPath(args, Exists))
        End Sub

        <TestMethod>
        Public Sub No_arguments_null_and_unusable_paths_give_nothing()
            Assert.IsNull(CommandLineArguments.FindDocumentPath(Nothing, Exists))
            Assert.IsNull(CommandLineArguments.FindDocumentPath({"Omskep.exe"}, Exists))
            Assert.IsNull(CommandLineArguments.FindDocumentPath({"Omskep.exe", "bad" & ChrW(0) & "path.ssml"}, AddressOf RejectsPath))
        End Sub

    End Class

    <TestClass>
    Public Class ImportTextDescriptionTests

        Private NotInheritable Class NoReader
            Implements IPdfPageReader

            Public Function ReadPages(pdfPath As String) As IReadOnlyList(Of String) Implements IPdfPageReader.ReadPages
                Throw New NotSupportedException()
            End Function
        End Class

        <TestMethod>
        Public Sub A_text_file_import_says_which_file_it_came_from()
            Dim result As ImportResult = New DocumentImporter(New NoReader()).ImportText("Hallo wereld", "af-ZA", "v", "the text file notes.txt")
            Assert.Contains("Imported the text file notes.txt.", result.Message)
        End Sub

        <TestMethod>
        Public Sub Pasted_text_still_says_pasted()
            Dim result As ImportResult = New DocumentImporter(New NoReader()).ImportText("Hallo wereld", "af-ZA", "v")
            Assert.Contains("Imported the pasted text.", result.Message)
        End Sub

    End Class

End Namespace
