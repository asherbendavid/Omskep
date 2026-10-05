Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    Friend Module DocumentTestHelpers
        Public ReadOnly LF As String = ChrW(10)
        Public ReadOnly CR As String = ChrW(13)

        ''' <summary>A small well-formed SSML document, one element per line.</summary>
        Public Function Doc(ParamArray bodyLines As String()) As String
            Return "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" & LF &
                   "<voice name=""af-ZA-WillemNeural"">" & LF &
                   String.Join(LF, bodyLines) & LF &
                   "</voice>" & LF & "</speak>" & LF
        End Function

        ''' <summary>A path under a temp folder with an apostrophe in its name, valid on any OS.</summary>
        Public Function TestPath(fileName As String) As String
            Return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Omskep O'Brien", fileName)
        End Function

        Public Function Wrap(inner As String) As String
            Return "<speak version=""1.0""><voice name=""v"">" & inner & "</voice></speak>"
        End Function
    End Module

    <TestClass>
    Public Class WellFormednessCheckerTests

        <TestMethod>
        Public Sub A_well_formed_document_passes()
            Dim result As CheckResult = WellFormednessChecker.Check(Doc("Hallo <break time=""500ms""/> wereld."))
            Assert.IsTrue(result.IsWellFormed)
            Assert.AreEqual("Well-formed", result.StatusText)
            Assert.AreEqual(0, result.Line)
        End Sub

        <TestMethod>
        Public Sub Null_empty_and_whitespace_are_malformed_not_exceptions()
            For Each text As String In {Nothing, String.Empty, "   " & LF}
                Dim result As CheckResult = WellFormednessChecker.Check(text)
                Assert.IsFalse(result.IsWellFormed)
                Assert.AreEqual(1, result.Line)
            Next
        End Sub

        <TestMethod>
        Public Sub A_mismatched_end_tag_points_at_the_element_that_is_still_open()
            ' prosody is opened on line 3 and never closed; the parser only notices at </voice> on line 4.
            Dim result As CheckResult = WellFormednessChecker.Check(Doc("<prosody rate=""slow"">Stadig", "</voice-oops>"))
            Assert.IsFalse(result.IsWellFormed)
            Assert.IsTrue(result.HasCause)
            Assert.AreEqual("prosody", result.CauseElement)
            Assert.AreEqual(3, result.CauseLine)
            Assert.AreEqual(2, result.CauseColumn)
            Assert.AreEqual(4, result.Line)
        End Sub

        <TestMethod>
        Public Sub An_unclosed_prosody_is_blamed_even_when_the_error_is_many_lines_later()
            Dim body As New List(Of String)()
            body.Add("<prosody rate=""slow"">Begin")
            For k As Integer = 1 To 40
                body.Add("Reël " & k & ".")
            Next
            Dim result As CheckResult = WellFormednessChecker.Check(Doc(body.ToArray()))
            Assert.IsFalse(result.IsWellFormed)
            Assert.AreEqual("prosody", result.CauseElement)
            Assert.AreEqual(3, result.CauseLine)
            Assert.IsGreaterThan(40, result.Line)
            Assert.Contains("prosody", result.StatusText)
            Assert.Contains("line 3", result.StatusText)
        End Sub

        <TestMethod>
        Public Sub A_missing_voice_end_tag_blames_voice()
            Dim result As CheckResult = WellFormednessChecker.Check("<speak>" & LF & "<voice name=""v"">" & LF & "text" & LF & "</speak>" & LF)
            Assert.IsFalse(result.IsWellFormed)
            Assert.AreEqual("voice", result.CauseElement)
            Assert.AreEqual(2, result.CauseLine)
        End Sub

        <TestMethod>
        Public Sub A_document_cut_off_while_elements_are_open_blames_the_innermost()
            Dim result As CheckResult = WellFormednessChecker.Check("<speak>" & LF & "<voice name=""v"">" & LF & "text")
            Assert.IsFalse(result.IsWellFormed)
            Assert.AreEqual("voice", result.CauseElement)
        End Sub

        <TestMethod>
        Public Sub A_stray_ampersand_is_reported_without_blaming_an_element()
            Dim result As CheckResult = WellFormednessChecker.Check(Doc("Moses & Aaron"))
            Assert.IsFalse(result.IsWellFormed)
            Assert.AreEqual(3, result.Line)
            Assert.IsFalse(result.HasCause)
        End Sub

        <TestMethod>
        Public Sub A_stray_less_than_is_reported_without_blaming_an_element()
            Dim result As CheckResult = WellFormednessChecker.Check(Wrap("a < b"))
            Assert.IsFalse(result.IsWellFormed)
            Assert.IsFalse(result.HasCause)
        End Sub

        <TestMethod>
        Public Sub Half_typed_markup_is_malformed_never_an_exception()
            Dim halfTyped As String() = {
                Wrap("hi <break time="),
                Wrap("hi <break time=""50"),
                Wrap("hi <break"),
                Wrap("hi <"),
                Wrap("hi </"),
                Wrap("hi </voice"),
                Wrap("hi <prosody rate=slow>x</prosody>"),
                "<speak><voice name=""v"">hi <bre",
                "<",
                "</",
                "<speak",
                "<speak>&"
            }
            For Each candidate As String In halfTyped
                Dim result As CheckResult = WellFormednessChecker.Check(candidate)
                Assert.IsFalse(result.IsWellFormed, candidate)
                Assert.IsGreaterThan(0, result.Line, candidate)
                Assert.IsGreaterThan(0, result.Column, candidate)
            Next
        End Sub

        <TestMethod>
        Public Sub Structure_errors_are_reported()
            Assert.IsFalse(WellFormednessChecker.Check("just text").IsWellFormed)
            Assert.IsFalse(WellFormednessChecker.Check("<speak/><speak/>").IsWellFormed)
            Assert.IsFalse(WellFormednessChecker.Check("<speak></speak></speak>").IsWellFormed)
            Assert.IsFalse(WellFormednessChecker.Check("<speak a=""1"" a=""2""/>").IsWellFormed)
        End Sub

        <TestMethod>
        Public Sub A_doctype_is_refused_and_nothing_is_expanded()
            Dim bomb As String = "<!DOCTYPE lolz [<!ENTITY a ""aaaaaaaaaa""><!ENTITY b ""&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"">]>" & LF & "<speak>&b;</speak>"
            Dim result As CheckResult = WellFormednessChecker.Check(bomb)
            Assert.IsFalse(result.IsWellFormed)
        End Sub

        <TestMethod>
        Public Sub Control_characters_make_the_document_malformed()
            Assert.IsFalse(WellFormednessChecker.Check(Wrap("a" & ChrW(1) & "b")).IsWellFormed)
        End Sub

        <TestMethod>
        Public Sub An_error_line_follows_the_text_when_earlier_lines_are_added()
            Dim before As CheckResult = WellFormednessChecker.Check(Doc("een", "twee &", "drie"))
            Dim after As CheckResult = WellFormednessChecker.Check(Doc("nuut", "een", "twee &", "drie"))
            Assert.AreEqual(before.Line + 1, after.Line)
            Assert.AreEqual(before.Column, after.Column)
        End Sub

        <TestMethod>
        Public Sub Very_deep_nesting_does_not_throw()
            Dim sb As New StringBuilder()
            For k As Integer = 1 To 20000
                sb.Append("<a>")
            Next
            Dim result As CheckResult = WellFormednessChecker.Check(sb.ToString())
            Assert.IsFalse(result.IsWellFormed)
        End Sub

        <TestMethod>
        Public Sub Random_garbage_never_throws_and_always_gives_a_usable_position()
            Dim pool As String() = {"<", ">", "/", "&", "'", ChrW(34), "=", " ", LF, "a", "speak", "voice", "<speak>", "</speak>", "<voice>", "</voice>", "<!--", "-->", "<![CDATA[", "]]>", "&amp;", ChrW(0), ChrW(&HD800), ChrW(&HFFFF), "x"}
            For seed As Integer = 1 To 500
                Dim rng As New Random(seed)
                Dim sb As New StringBuilder()
                For k As Integer = 1 To rng.Next(0, 60)
                    sb.Append(pool(rng.Next(pool.Length)))
                Next
                Dim text As String = sb.ToString()
                Dim result As CheckResult = WellFormednessChecker.Check(text)
                If Not result.IsWellFormed Then
                    Assert.IsGreaterThan(0, result.Line, $"seed {seed}")
                    Assert.IsGreaterThan(0, result.Column, $"seed {seed}")
                End If
                Assert.AreEqual(result.StatusText, WellFormednessChecker.Check(text).StatusText, $"seed {seed}")
            Next
        End Sub

        <TestMethod>
        Public Sub The_largest_study_size_checks_quickly()
            Dim lines As New List(Of String)()
            For k As Integer = 1 To 1330
                lines.Add("Dit is reël " & k & " van die studie, met genoeg woorde om te tel &amp; meer.")
            Next
            Dim text As String = Doc(lines.ToArray())
            Assert.IsGreaterThan(80000, text.Length)
            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim result As CheckResult = WellFormednessChecker.Check(text)
            watch.Stop()
            Assert.IsTrue(result.IsWellFormed)
            Assert.IsLessThan(1000L, watch.ElapsedMilliseconds)
        End Sub

    End Class

    <TestClass>
    Public Class BillableCharacterCounterTests

        Private Const Sentence1 As String = "Die heelal is groot en wonderlik."
        Private Const Sentence2 As String = "Hy het alles met sorg gemaak."

        Private Shared Function Count(text As String) As Integer
            Return BillableCharacterCounter.CountUpperBound(text)
        End Function

        ' ---- phase 0 measured rows (structure reproduced; strings differ) ----

        <TestMethod>
        Public Sub Hello_text_only_counts_every_character_and_no_scaffold_tags()
            Assert.AreEqual(Sentence1.Length, Count(Wrap(Sentence1)))
        End Sub

        <TestMethod>
        Public Sub W1_a_single_space_counts_as_one()
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 1, Count(Wrap(Sentence1 & " " & Sentence2)))
        End Sub

        <TestMethod>
        Public Sub W2_six_spaces_count_as_six()
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 6, Count(Wrap(Sentence1 & New String(" "c, 6) & Sentence2)))
        End Sub

        <TestMethod>
        Public Sub W3_a_CRLF_counts_as_one_and_a_lone_LF_or_CR_also_one()
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 1, Count(Wrap(Sentence1 & CR & LF & Sentence2)))
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 1, Count(Wrap(Sentence1 & LF & Sentence2)))
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 1, Count(Wrap(Sentence1 & CR & Sentence2)))
        End Sub

        <TestMethod>
        Public Sub W4_CRLF_and_four_spaces_count_as_five()
            Assert.AreEqual(Sentence1.Length + Sentence2.Length + 5, Count(Wrap(Sentence1 & CR & LF & "    " & Sentence2)))
        End Sub

        <TestMethod>
        Public Sub W5_edge_whitespace_is_counted_which_is_the_upper_bound_Azure_may_bill_less()
            Assert.AreEqual(Sentence1.Length + 6, Count(Wrap("   " & Sentence1 & "   ")))
        End Sub

        <TestMethod>
        Public Sub Other_markup_counts_character_for_character()
            Dim brk As String = "<break time=""500ms""/>"
            Dim pros As String = "<prosody rate=""slow"">"
            Dim text As String = pros & Sentence1 & brk & Sentence2 & "</prosody>"
            Assert.AreEqual(text.Length, Count(Wrap(text)))
        End Sub

        <TestMethod>
        Public Sub P3_two_CRLF_and_indent_separators_without_a_wrapper()
            Dim sep As String = CR & LF & "      "
            Dim text As String = Sentence1 & sep & Sentence2 & sep & Sentence1
            Assert.AreEqual(Sentence1.Length * 2 + Sentence2.Length + 2 * 7, Count(Wrap(text)))
        End Sub

        ' ---- tag recognition ----

        <TestMethod>
        Public Sub The_full_scaffold_with_attributes_and_line_breaks_is_free()
            ' 5 = the line breaks after the speak tag, the voice tag, the body, the voice end tag and the speak end tag.
            Assert.AreEqual(Sentence1.Length + 5, Count(Doc(Sentence1)))
        End Sub

        <TestMethod>
        Public Sub Self_closing_and_spaced_closing_scaffold_tags_are_free()
            Assert.AreEqual(0, Count("<voice name=""v""/>"))
            Assert.AreEqual(3, Count("<speak>abc</speak >"))
            Assert.AreEqual(0, Count("<speak></speak>"))
        End Sub

        <TestMethod>
        Public Sub A_greater_than_inside_an_attribute_value_does_not_end_the_free_tag()
            Assert.AreEqual(2, Count("<voice name=""a>b"">hi</voice>"))
            Assert.AreEqual(2, Count("<voice name='a>b'>hi</voice>"))
        End Sub

        <TestMethod>
        Public Sub Look_alike_names_are_counted_as_text()
            Assert.AreEqual("<speaker>x</speaker>".Length, Count("<speaker>x</speaker>"))
            Assert.AreEqual("<voices>".Length, Count("<voices>"))
            Assert.AreEqual("<Speak>x</Speak>".Length, Count("<Speak>x</Speak>"))
            Assert.AreEqual("<voice-x>".Length, Count("<voice-x>"))
        End Sub

        <TestMethod>
        Public Sub Half_typed_scaffold_tags_are_counted_in_full()
            Assert.AreEqual("<voice name=""x""".Length, Count("<voice name=""x"""))
            Assert.AreEqual("<voice".Length, Count("<voice"))
            Assert.AreEqual("<voice <break/>".Length, Count("<voice <break/>"))
            Assert.AreEqual("</speak".Length, Count("</speak"))
            Assert.AreEqual("<".Length, Count("<"))
        End Sub

        <TestMethod>
        Public Sub Tags_inside_comments_cdata_and_instructions_are_not_free()
            Assert.AreEqual("<!-- <voice> -->".Length, Count("<!-- <voice> -->"))
            Assert.AreEqual("<![CDATA[<speak>]]>".Length, Count("<![CDATA[<speak>]]>"))
            Assert.AreEqual("<?x <voice> ?>".Length, Count("<?x <voice> ?>"))
            Assert.AreEqual("<!-- never closed <voice>".Length, Count("<!-- never closed <voice>"))
        End Sub

        <TestMethod>
        Public Sub CRLF_inside_a_comment_still_counts_as_one()
            Assert.AreEqual("<!--a-->".Length + 1, Count("<!--a" & CR & LF & "-->"))
        End Sub

        <TestMethod>
        Public Sub Entities_are_counted_as_written()
            Assert.AreEqual("A &amp; B".Length, Count(Wrap("A &amp; B")))
        End Sub

        <TestMethod>
        Public Sub Characters_outside_the_basic_plane_count_as_two_which_is_the_safe_side()
            Assert.AreEqual(2, Count(Wrap(Char.ConvertFromUtf32(&H1F600))))
        End Sub

        <TestMethod>
        Public Sub Null_and_empty_count_zero()
            Assert.AreEqual(0, Count(Nothing))
            Assert.AreEqual(0, Count(String.Empty))
        End Sub

        <TestMethod>
        Public Sub Random_garbage_never_throws_and_never_exceeds_the_length()
            Dim pool As String() = {"<", ">", "/", """", "'", "=", " ", LF, CR, "a", "speak", "voice", "<speak", "<voice ", "</voice>", "</speak>", "<!--", "-->", "<![CDATA[", "]]>", "<?", "?>", "x"}
            For seed As Integer = 1 To 500
                Dim rng As New Random(seed)
                Dim sb As New StringBuilder()
                For k As Integer = 1 To rng.Next(0, 60)
                    sb.Append(pool(rng.Next(pool.Length)))
                Next
                Dim text As String = sb.ToString()
                Dim n As Integer = Count(text)
                Assert.IsGreaterThan(-1, n, $"seed {seed}")
                Assert.IsLessThan(text.Length + 1, n, $"seed {seed}")
            Next
        End Sub

        <TestMethod>
        Public Sub The_largest_study_size_counts_instantly()
            Dim lines As New List(Of String)()
            For k As Integer = 1 To 1330
                lines.Add("Dit is reël " & k & " van die studie, met <break time=""200ms""/> woorde.")
            Next
            Dim text As String = Doc(lines.ToArray())
            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim n As Integer = Count(text)
            watch.Stop()
            Assert.IsGreaterThan(80000, n)
            Assert.IsLessThan(200L, watch.ElapsedMilliseconds)
        End Sub

    End Class

    <TestClass>
    Public Class DocumentModelTests

        <TestMethod>
        Public Sub A_new_model_is_a_pristine_untitled_document()
            Dim m As New DocumentModel()
            Assert.AreEqual(SaveState.Pristine, m.SaveState)
            Assert.AreEqual(CheckState.Unchecked, m.CheckState)
            Assert.IsNull(m.FilePath)
            Assert.AreEqual("Untitled", m.DisplayName)
            Assert.IsFalse(m.HasUnsavedChanges)
            Assert.IsFalse(m.IsWellFormed)
        End Sub

        <TestMethod>
        Public Sub Editing_makes_it_dirty_and_unchecked_and_bumps_the_revision()
            Dim m As New DocumentModel()
            Dim r0 As Integer = m.Revision
            m.Edited()
            Assert.AreEqual(SaveState.Dirty, m.SaveState)
            Assert.AreEqual(CheckState.Unchecked, m.CheckState)
            Assert.AreEqual(r0 + 1, m.Revision)
            Assert.IsTrue(m.HasUnsavedChanges)
        End Sub

        <TestMethod>
        Public Sub Opening_a_file_is_clean_with_the_file_name_as_display_name()
            Dim m As New DocumentModel()
            m.OpenedFile(TestPath("Heelal.ssml"))
            Assert.AreEqual(SaveState.Clean, m.SaveState)
            Assert.AreEqual("Heelal.ssml", m.DisplayName)
            Assert.AreEqual(CheckState.Unchecked, m.CheckState)
        End Sub

        <TestMethod>
        Public Sub An_import_is_dirty_and_untitled()
            Dim m As New DocumentModel()
            m.OpenedFile(TestPath("a.ssml"))
            m.Imported()
            Assert.AreEqual(SaveState.Dirty, m.SaveState)
            Assert.IsNull(m.FilePath)
            Assert.IsTrue(m.HasUnsavedChanges)
        End Sub

        <TestMethod>
        Public Sub Saving_makes_it_clean_and_keeps_the_check_result_and_revision()
            Dim m As New DocumentModel()
            m.Edited()
            Assert.IsTrue(m.CheckCompleted(m.Revision, CheckResult.Ok))
            Dim r As Integer = m.Revision
            m.Saved(TestPath("b.ssml"))
            Assert.AreEqual(SaveState.Clean, m.SaveState)
            Assert.AreEqual("b.ssml", m.DisplayName)
            Assert.AreEqual(CheckState.WellFormed, m.CheckState)
            Assert.AreEqual(r, m.Revision)
        End Sub

        <TestMethod>
        Public Sub A_check_result_for_old_text_is_ignored()
            Dim m As New DocumentModel()
            m.Edited()
            Dim oldRevision As Integer = m.Revision
            m.Edited()
            Assert.IsFalse(m.CheckCompleted(oldRevision, CheckResult.Ok))
            Assert.AreEqual(CheckState.Unchecked, m.CheckState)
            Assert.IsNull(m.LastCheck)
        End Sub

        <TestMethod>
        Public Sub A_check_result_after_new_or_open_for_the_previous_document_is_ignored()
            Dim m As New DocumentModel()
            m.Edited()
            Dim old As Integer = m.Revision
            m.NewDocument()
            Assert.IsFalse(m.CheckCompleted(old, CheckResult.Ok))
            m.Edited()
            Dim old2 As Integer = m.Revision
            m.OpenedFile(TestPath("x.ssml"))
            Assert.IsFalse(m.CheckCompleted(old2, CheckResult.Ok))
        End Sub

        <TestMethod>
        Public Sub A_malformed_result_is_stored_and_does_not_make_the_document_unsaveable()
            Dim m As New DocumentModel()
            m.Edited()
            Dim bad As CheckResult = WellFormednessChecker.Check(Wrap("a & b"))
            Assert.IsTrue(m.CheckCompleted(m.Revision, bad))
            Assert.AreEqual(CheckState.Malformed, m.CheckState)
            Assert.IsFalse(m.IsWellFormed)
            Assert.IsTrue(m.HasUnsavedChanges)
            m.Saved(TestPath("wip.ssml"))
            Assert.AreEqual(SaveState.Clean, m.SaveState)
            Assert.AreEqual(CheckState.Malformed, m.CheckState)
        End Sub

        <TestMethod>
        Public Sub The_last_result_is_kept_while_the_next_check_is_pending()
            Dim m As New DocumentModel()
            m.Edited()
            m.CheckCompleted(m.Revision, CheckResult.Ok)
            m.Edited()
            Assert.AreEqual(CheckState.Unchecked, m.CheckState)
            Assert.IsNotNull(m.LastCheck)
        End Sub

        <TestMethod>
        Public Sub Undoing_back_to_the_saved_content_makes_a_file_document_clean_again()
            Dim m As New DocumentModel()
            m.OpenedFile(TestPath("a.ssml"))
            m.Edited()
            m.ReachedSavePoint()
            Assert.AreEqual(SaveState.Clean, m.SaveState)
        End Sub

        <TestMethod>
        Public Sub Reaching_a_save_point_without_a_file_changes_nothing()
            Dim m As New DocumentModel()
            m.Imported()
            m.ReachedSavePoint()
            Assert.AreEqual(SaveState.Dirty, m.SaveState)
        End Sub

        <TestMethod>
        Public Sub Blank_paths_and_null_results_are_rejected()
            Dim m As New DocumentModel()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() m.OpenedFile(" "))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() m.Saved(Nothing))
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() m.CheckCompleted(m.Revision, Nothing))
        End Sub

    End Class

End Namespace
