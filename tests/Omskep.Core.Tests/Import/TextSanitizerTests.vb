Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports System.Xml
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Import

Namespace Import

    <TestClass>
    Public Class TextSanitizerTests

        Private Shared ReadOnly LF As String = ChrW(10)

        Private Shared Function Clean(raw As String) As String
            Return TextSanitizer.Sanitize(raw).Body
        End Function

        ' ---- Real-world characters observed in the study PDFs ----

        <TestMethod>
        Public Sub Unicode_punctuation_passes_through_unchanged()
            Dim raw As String = ChrW(&H2022) & " " & ChrW(&H2013) & " " & ChrW(&H2014) & " " &
                                ChrW(&H201C) & "x" & ChrW(&H201D) & " " & ChrW(&H2212) & " " &
                                ChrW(&H2026) & " " & ChrW(&H2018) & "y" & ChrW(&H2019)
            Assert.AreEqual(raw, Clean(raw))
        End Sub

        <TestMethod>
        Public Sub Decorative_private_use_glyphs_are_removed_and_counted()
            Dim raw As String = "A" & ChrW(&HF098) & ChrW(&HF099) & "B" & ChrW(&HF098)
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual("AB", result.Body)
            Assert.HasCount(2, result.RemovedPrivateUse)
            Assert.AreEqual(2, result.RemovedPrivateUse(&HF098))
            Assert.AreEqual(1, result.RemovedPrivateUse(&HF099))
            Assert.AreEqual(3, result.RemovedPrivateUseTotal)
        End Sub

        <TestMethod>
        Public Sub Title_page_ornament_line_becomes_an_empty_line_not_a_deleted_line()
            Dim raw As String = "2026.06 " & ChrW(13) & ChrW(10) & ChrW(&HF098) & ChrW(&HF099) & " " & ChrW(13) & ChrW(10) & "Die Torah"
            Assert.AreEqual("2026.06" & LF & LF & "Die Torah", Clean(raw))
        End Sub

        <TestMethod>
        Public Sub Supplementary_plane_private_use_is_removed()
            Dim raw As String = "a" & Char.ConvertFromUtf32(&HF0000) & "b" & Char.ConvertFromUtf32(&H10FFFD) & "c"
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual("abc", result.Body)
            Assert.AreEqual(2, result.RemovedPrivateUseTotal)
        End Sub

        <TestMethod>
        Public Sub Wrong_direction_quote_before_standalone_n_is_fixed()
            Dim result As SanitizeResult = TextSanitizer.Sanitize("Dit is " & ChrW(&H2018) & "n Man en " & ChrW(&H2018) & "n Vrou.")
            Assert.AreEqual("Dit is " & TextSanitizer.ArticleQuote & "n Man en " & TextSanitizer.ArticleQuote & "n Vrou.", result.Body)
            Assert.AreEqual(2, result.FixedArticleQuotes)
        End Sub

        <TestMethod>
        Public Sub Article_quote_is_fixed_at_start_of_text_and_after_a_line_break()
            Dim raw As String = ChrW(&H2018) & "n Man" & LF & ChrW(&H2018) & "n Vrou"
            Dim q As String = TextSanitizer.ArticleQuote
            Assert.AreEqual(q & "n Man" & LF & q & "n Vrou", Clean(raw))
        End Sub

        <TestMethod>
        Public Sub Right_quote_form_before_n_is_normalised_too()
            Dim q As String = TextSanitizer.ArticleQuote
            Assert.AreEqual(q & "n Man", Clean(ChrW(&H2019) & "n Man"))
        End Sub

        <TestMethod>
        Public Sub Quote_before_a_longer_word_or_after_a_letter_is_left_alone()
            ' A quoted word starting with n, and a quote glued to a preceding letter.
            Dim raw As String = ChrW(&H2018) & "nuwe" & ChrW(&H2019) & " en ma" & ChrW(&H2018) & "n"
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual(raw, result.Body)
            Assert.AreEqual(0, result.FixedArticleQuotes)
        End Sub

        <TestMethod>
        Public Sub Article_quote_already_in_the_target_form_is_not_counted_as_a_fix()
            Dim q As String = TextSanitizer.ArticleQuote
            Dim result As SanitizeResult = TextSanitizer.Sanitize(q & "n Man")
            Assert.AreEqual(q & "n Man", result.Body)
            Assert.AreEqual(0, result.FixedArticleQuotes)
        End Sub

        <TestMethod>
        Public Sub Hebrew_presentation_form_is_normalised_to_base_letter_plus_point()
            Dim presentation As String = ChrW(&HFB4B)               ' vav with holam
            Dim decomposed As String = ChrW(&H5D5) & ChrW(&H5B9)    ' vav + holam
            Dim fromPresentation As String = Clean("x" & presentation & "y")
            Assert.DoesNotContain(presentation, fromPresentation)
            Assert.AreEqual(Clean("x" & decomposed & "y"), fromPresentation)
            Assert.AreEqual("x" & decomposed & "y", fromPresentation)
        End Sub

        <TestMethod>
        Public Sub Letters_with_separate_diaeresis_are_composed()
            ' e + combining diaeresis becomes the single letter that Afrikaans text uses.
            Assert.AreEqual("Mo" & ChrW(&HEB) & "l", Clean("Moe" & ChrW(&H308) & "l"))
        End Sub

        <TestMethod>
        Public Sub Only_ampersand_less_than_and_greater_than_are_escaped()
            Assert.AreEqual("A &amp; B &lt; C &gt; D", Clean("A & B < C > D"))
            Dim others As String = "''" & ChrW(34) & " " & ChrW(&HE9) & ChrW(&HFC) & ChrW(&HF8) & ChrW(&H416) & ChrW(&H5D0)
            Assert.AreEqual(others, Clean(others))
        End Sub

        <TestMethod>
        Public Sub Text_that_already_looks_like_an_entity_is_treated_as_plain_text()
            Assert.AreEqual("&amp;amp; &amp;lt;", Clean("&amp; &lt;"))
        End Sub

        ' ---- Line endings, whitespace ----

        <TestMethod>
        Public Sub All_line_break_forms_become_LF()
            Dim raw As String = "a" & ChrW(13) & ChrW(10) & "b" & ChrW(13) & "c" & ChrW(11) & "d" & ChrW(12) & "e" & ChrW(10) & "f"
            Assert.AreEqual("a" & LF & "b" & LF & "c" & LF & "d" & LF & "e" & LF & "f", Clean(raw))
        End Sub

        <TestMethod>
        Public Sub Trailing_spaces_and_tabs_are_trimmed_but_interior_runs_are_kept()
            Dim gap As String = New String(" "c, 61)
            Dim raw As String = "Torah" & gap & "Heelal   " & LF & "next " & ChrW(9) & LF & "  indented"
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual("Torah" & gap & "Heelal" & LF & "next" & LF & "  indented", result.Body)
            Assert.AreEqual(2, result.TrimmedLines)
        End Sub

        <TestMethod>
        Public Sub Words_are_never_removed()
            Dim raw As String = "Een twee drie " & ChrW(&HF098) & " vier" & ChrW(0) & " vyf"
            Assert.AreEqual("Een twee drie  vier vyf", Clean(raw))
        End Sub

        ' ---- Bad input ----

        <TestMethod>
        Public Sub Null_empty_and_whitespace_only_input_do_not_throw()
            Assert.AreEqual(String.Empty, Clean(Nothing))
            Assert.AreEqual(String.Empty, Clean(String.Empty))
            Assert.AreEqual(LF, Clean("   " & LF & "  "))
        End Sub

        <TestMethod>
        Public Sub Control_characters_are_removed_and_counted_but_tab_survives_inside_a_line()
            Dim raw As String = "a" & ChrW(0) & "b" & ChrW(1) & "c" & ChrW(&H1F) & "d" & ChrW(9) & "e"
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual("abcd" & ChrW(9) & "e", result.Body)
            Assert.AreEqual(3, result.RemovedInvalidCharacters)
        End Sub

        <TestMethod>
        Public Sub Lone_surrogates_are_removed_and_a_valid_pair_survives()
            Dim emoji As String = Char.ConvertFromUtf32(&H1F600)
            Dim raw As String = "a" & ChrW(&HD800) & "b" & ChrW(&HDC00) & "c" & emoji & "d" & ChrW(&HD800) & "e"
            Dim result As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual("abc" & emoji & "de", result.Body)
            Assert.AreEqual(3, result.RemovedInvalidCharacters)
        End Sub

        <TestMethod>
        Public Sub Noncharacters_FFFE_and_FFFF_are_removed()
            Dim result As SanitizeResult = TextSanitizer.Sanitize("a" & ChrW(&HFFFE) & "b" & ChrW(&HFFFF) & "c")
            Assert.AreEqual("abc", result.Body)
            Assert.AreEqual(2, result.RemovedInvalidCharacters)
        End Sub

        <TestMethod>
        Public Sub Markup_like_garbage_is_neutralised()
            Dim result As String = Clean("<speak><voice name=""x"">]]> <!-- & &#0; </speak>")
            Assert.DoesNotContain("<", result)
            Assert.DoesNotContain(">", result)
            AssertWellFormed(result, "markup-like garbage")
        End Sub

        <TestMethod>
        Public Sub Random_garbage_always_produces_well_formed_xml_content()
            ' Robustness test: seeded, so a failure is reproducible.
            Dim pool As String() = {
                "a", "b", " ", " ", " ", LF, ChrW(13), ChrW(9), "<", ">", "&", "'", ChrW(34), ChrW(&H2018), "n",
                ChrW(0), ChrW(1), ChrW(8), ChrW(11), ChrW(12), ChrW(&H1F), ChrW(&H7F), ChrW(&H85),
                ChrW(&HD800), ChrW(&HDC00), ChrW(&HFFFE), ChrW(&HFFFF), ChrW(&HFFFD),
                ChrW(&HE000), ChrW(&HF098), ChrW(&HF8FF), ChrW(&HFB4B), ChrW(&H5D0), ChrW(&H308),
                Char.ConvertFromUtf32(&H1F600), Char.ConvertFromUtf32(&HF0000), Char.ConvertFromUtf32(&H10FFFF),
                Char.ConvertFromUtf32(&H1FFFE)
            }
            For seed As Integer = 1 To 400
                Dim rng As New Random(seed)
                Dim sb As New StringBuilder()
                Dim length As Integer = rng.Next(0, 80)
                For k As Integer = 1 To length
                    sb.Append(pool(rng.Next(pool.Length)))
                Next
                Dim result As SanitizeResult = TextSanitizer.Sanitize(sb.ToString())
                AssertWellFormed(result.Body, $"seed {seed}")
                Assert.DoesNotContain(ChrW(13).ToString(), result.Body)
                Assert.DoesNotContain(ChrW(&HF098).ToString(), result.Body)
            Next
        End Sub

        <TestMethod>
        Public Sub Sanitizing_twice_gives_identical_results_and_keeps_no_state()
            Dim raw As String = ChrW(&H2018) & "n Man & " & ChrW(&HFB4B) & ChrW(&HF098) & "  " & LF
            Dim first As SanitizeResult = TextSanitizer.Sanitize(raw)
            Dim second As SanitizeResult = TextSanitizer.Sanitize(raw)
            Assert.AreEqual(first.Body, second.Body)
            Assert.AreEqual(first.FixedArticleQuotes, second.FixedArticleQuotes)
            Assert.AreEqual(first.RemovedPrivateUseTotal, second.RemovedPrivateUseTotal)
        End Sub

        <TestMethod>
        Public Sub A_million_characters_sanitize_in_reasonable_time()
            Dim unit As String = "Die heelal is " & ChrW(&H2018) & "n wonder & meer." & ChrW(&HF098) & "   " & ChrW(13) & ChrW(10)
            Dim big As New StringBuilder()
            While big.Length < 1000000
                big.Append(unit)
            End While
            Dim watch As Stopwatch = Stopwatch.StartNew()
            Dim result As SanitizeResult = TextSanitizer.Sanitize(big.ToString())
            watch.Stop()
            Assert.IsLessThan(3000L, watch.ElapsedMilliseconds)
            Assert.IsGreaterThan(0, result.FixedArticleQuotes)
        End Sub

        ' ---- helpers ----

        ''' <summary>Wraps the body in a root element and reads it with DTDs prohibited.</summary>
        Private Shared Sub AssertWellFormed(body As String, context As String)
            Dim settings As New XmlReaderSettings() With {.DtdProcessing = DtdProcessing.Prohibit}
            Try
                Using reader As XmlReader = XmlReader.Create(New IO.StringReader("<speak>" & body & "</speak>"), settings)
                    While reader.Read()
                    End While
                End Using
            Catch ex As XmlException
                Assert.Fail($"Not well-formed ({context}): {ex.Message}")
            End Try
        End Sub

    End Class

End Namespace
