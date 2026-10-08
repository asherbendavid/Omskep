Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class TextSearchTests

        Private Shared ReadOnly LF As String = ChrW(10)
        Private Shared ReadOnly Plain As New SearchOptions()
        Private Shared ReadOnly Rx As New SearchOptions(useRegex:=True)

        Private Shared Function Apply(text As String, edits As IEnumerable(Of TextEdit)) As String
            Dim sb As New StringBuilder(text)
            For Each edit In edits
                sb.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText)
            Next
            Return sb.ToString()
        End Function

        ' ---- find ----

        <TestMethod>
        Public Sub Literal_search_ignores_case_by_default_and_matches_accented_letters()
            Dim found As FindOutcome = TextSearch.Find("Dit is DIE HEELAL en die heelal.", "die heelal", Plain, 0, True, False)
            Assert.AreEqual(7, found.Match.Start)
            Dim accent As FindOutcome = TextSearch.Find("Reël en REËL", "reël", Plain, 3, True, False)
            Assert.AreEqual(8, accent.Match.Start)
        End Sub

        <TestMethod>
        Public Sub Match_case_is_respected()
            Dim cased As New SearchOptions(matchCase:=True)
            Assert.IsNull(TextSearch.Find("Heelal", "heelal", cased, 0, True, False).Match)
            Assert.AreEqual(0, TextSearch.Find("Heelal", "Heelal", cased, 0, True, False).Match.Start)
        End Sub

        <TestMethod>
        Public Sub Special_characters_are_literal_unless_regex_is_on()
            Assert.AreEqual(4, TextSearch.Find("axb a.b", "a.b", Plain, 0, True, False).Match.Start)
            Assert.AreEqual(0, TextSearch.Find("axb a.b", "a.b", Rx, 0, True, False).Match.Start)
            Assert.AreEqual(2, TextSearch.Find("x &amp; y", "&amp;", Plain, 0, True, False).Match.Start)
        End Sub

        <TestMethod>
        Public Sub Whole_word_does_not_match_inside_a_longer_word()
            Dim whole As New SearchOptions(wholeWord:=True)
            Assert.AreEqual(9, TextSearch.Find("heelalle heelal", "heelal", whole, 0, True, False).Match.Start)
            Assert.IsNull(TextSearch.Find("heelalle", "heelal", whole, 0, True, False).Match)
            Assert.AreEqual(2, TextSearch.Find("a (b) c", "(b)", whole, 0, True, False).Match.Start)
        End Sub

        <TestMethod>
        Public Sub Searching_forward_starts_at_the_given_index_and_can_wrap()
            Dim text As String = "een twee een"
            Assert.AreEqual(9, TextSearch.Find(text, "een", Plain, 1, True, False).Match.Start)
            Assert.IsNull(TextSearch.Find(text, "een", Plain, 10, True, False).Match)
            Dim wrapped As FindOutcome = TextSearch.Find(text, "een", Plain, 10, True, True)
            Assert.AreEqual(0, wrapped.Match.Start)
            Assert.IsTrue(wrapped.Wrapped)
        End Sub

        <TestMethod>
        Public Sub Searching_backward_finds_the_match_before_the_index_and_can_wrap()
            Dim text As String = "een twee een"
            Assert.AreEqual(0, TextSearch.Find(text, "een", Plain, 9, False, False).Match.Start)
            Assert.IsNull(TextSearch.Find(text, "een", Plain, 2, False, False).Match)
            Dim wrapped As FindOutcome = TextSearch.Find(text, "een", Plain, 2, False, True)
            Assert.AreEqual(9, wrapped.Match.Start)
            Assert.IsTrue(wrapped.Wrapped)
        End Sub

        <TestMethod>
        Public Sub Multi_line_regex_can_find_a_header_and_page_number_block()
            Dim text As String = "Bladsy een" & LF & "Torah Navorsing Akademie     Die Heelal: Toeval?" & LF & "38 " & LF & "Meer teks"
            Dim found As FindOutcome = TextSearch.Find(text, "^Torah Navorsing Akademie\s+Die Heelal: Toeval\?\n\d+ ?\n", Rx, 0, True, False)
            Assert.AreEqual(11, found.Match.Start)
        End Sub

        <TestMethod>
        Public Sub Nothing_found_is_not_an_error()
            Dim found As FindOutcome = TextSearch.Find("abc", "xyz", Plain, 0, True, True)
            Assert.IsNull(found.Match)
            Assert.IsNull(found.ErrorMessage)
        End Sub

        <TestMethod>
        Public Sub An_empty_pattern_and_a_bad_pattern_give_messages_not_exceptions()
            Assert.AreEqual("Enter the text to find.", TextSearch.Find("abc", String.Empty, Plain, 0, True, True).ErrorMessage)
            Assert.AreEqual("Enter the text to find.", TextSearch.Find("abc", Nothing, Plain, 0, True, True).ErrorMessage)
            Dim bad As FindOutcome = TextSearch.Find("abc", "(unclosed", Rx, 0, True, True)
            Assert.StartsWith("The pattern is not valid", bad.ErrorMessage)
            Assert.IsNotNull(TextSearch.ReplaceAll("abc", "[", "x", Rx).ErrorMessage)
        End Sub

        <TestMethod>
        Public Sub A_runaway_pattern_is_stopped_with_a_message()
            Dim text As String = New String("a"c, 40) & "b"
            Dim found As FindOutcome = TextSearch.Find(text, "(a+)+$", Rx, 0, True, False, 100)
            Assert.AreEqual("The search took too long. Try a simpler pattern.", found.ErrorMessage)
            Assert.AreEqual("The search took too long. Try a simpler pattern.", TextSearch.ReplaceAll(text, "(a+)+$", "x", Rx, 100).ErrorMessage)
        End Sub

        <TestMethod>
        Public Sub Matches_of_zero_length_are_ignored()
            Assert.IsNull(TextSearch.Find("abc", "^", Rx, 0, True, True).Match)
            Dim all As ReplaceAllOutcome = TextSearch.ReplaceAll("abc", "x*", "-", Rx)
            Assert.AreEqual(0, all.Count)
            Assert.AreEqual("abc", all.NewText)
        End Sub

        ' ---- replace all ----

        <TestMethod>
        Public Sub Replace_all_gives_edits_that_reproduce_the_new_text_last_first()
            Dim text As String = "Een twee een drie EEN"
            Dim all As ReplaceAllOutcome = TextSearch.ReplaceAll(text, "een", "vyf", Plain)
            Assert.AreEqual(3, all.Count)
            Assert.AreEqual("vyf twee vyf drie vyf", all.NewText)
            Assert.IsGreaterThan(all.Edits(1).Start, all.Edits(0).Start)
            Assert.AreEqual(all.NewText, Apply(text, all.Edits))
        End Sub

        <TestMethod>
        Public Sub Replacing_with_nothing_deletes_the_matches()
            Assert.AreEqual("ac", TextSearch.ReplaceAll("abc", "b", String.Empty, Plain).NewText)
            Assert.AreEqual("ac", TextSearch.ReplaceAll("abc", "b", Nothing, Plain).NewText)
        End Sub

        <TestMethod>
        Public Sub One_regex_removes_every_header_and_page_number_block()
            Dim page As Func(Of Integer, String) = Function(n As Integer) "Torah Navorsing Akademie     Die Heelal: Toeval?" & LF & n & " " & LF & "Teks van bladsy " & n & "." & LF
            Dim text As String = page(1) & page(2) & page(3)
            Dim all As ReplaceAllOutcome = TextSearch.ReplaceAll(text, "^Torah Navorsing Akademie\s+Die Heelal: Toeval\?\n\d+ ?\n", String.Empty, Rx)
            Assert.AreEqual(3, all.Count)
            Assert.AreEqual("Teks van bladsy 1." & LF & "Teks van bladsy 2." & LF & "Teks van bladsy 3." & LF, all.NewText)
            Assert.AreEqual(all.NewText, Apply(text, all.Edits))
        End Sub

        <TestMethod>
        Public Sub Regex_replacement_can_use_groups_and_line_break_escapes()
            Assert.AreEqual("b-a", TextSearch.ReplaceAll("a-b", "(\w)-(\w)", "$2-$1", Rx).NewText)
            Assert.AreEqual("a" & LF & "b", TextSearch.ReplaceAll("a b", " ", "\n", Rx).NewText)
            Assert.AreEqual("a" & ChrW(9) & "b", TextSearch.ReplaceAll("a b", " ", "\t", Rx).NewText)
            Assert.AreEqual("a\b", TextSearch.ReplaceAll("a b", " ", "\\", Rx).NewText)
        End Sub

        <TestMethod>
        Public Sub Literal_replacement_is_never_interpreted()
            Assert.AreEqual("$1 \n", TextSearch.ReplaceAll("x", "x", "$1 \n", Plain).NewText)
        End Sub

        <TestMethod>
        Public Sub Whole_word_replace_all_leaves_longer_words_alone()
            Dim whole As New SearchOptions(wholeWord:=True)
            Assert.AreEqual("kat katte kat", TextSearch.ReplaceAll("kat katte kat", "katte", "katte", whole).NewText)
            Assert.AreEqual("hond katte hond", TextSearch.ReplaceAll("kat katte kat", "kat", "hond", whole).NewText)
        End Sub

        <TestMethod>
        Public Sub Replace_at_replaces_only_a_selection_that_is_exactly_a_match()
            Dim edit As TextEdit = TextSearch.ReplaceAt("een twee", "twee", "drie", Plain, 4, 4)
            Assert.AreEqual(4, edit.Start)
            Assert.AreEqual("drie", edit.NewText)
            Assert.IsNull(TextSearch.ReplaceAt("een twee", "twee", "drie", Plain, 4, 3))
            Assert.IsNull(TextSearch.ReplaceAt("een twee", "twee", "drie", Plain, 0, 4))
            Assert.IsNull(TextSearch.ReplaceAt("een twee", "twee", "drie", Plain, 4, 0))
            Assert.IsNull(TextSearch.ReplaceAt("een twee", "twee", "drie", Plain, 6, 10))
        End Sub

        <TestMethod>
        Public Sub Hebrew_and_other_Unicode_text_is_searched_and_replaced_correctly()
            Dim text As String = "x " & ChrW(&H5D0) & ChrW(&H5DC) & " y " & Char.ConvertFromUtf32(&H1F600) & " z"
            Dim all As ReplaceAllOutcome = TextSearch.ReplaceAll(text, ChrW(&H5D0) & ChrW(&H5DC), "El", Plain)
            Assert.AreEqual("x El y " & Char.ConvertFromUtf32(&H1F600) & " z", all.NewText)
            Assert.AreEqual(all.NewText, Apply(text, all.Edits))
            Assert.AreEqual(1, TextSearch.ReplaceAll(text, Char.ConvertFromUtf32(&H1F600), "!", Plain).Count)
        End Sub

        <TestMethod>
        Public Sub Null_text_is_treated_as_empty()
            Assert.IsNull(TextSearch.Find(Nothing, "a", Plain, 0, True, True).Match)
            Assert.AreEqual(0, TextSearch.ReplaceAll(Nothing, "a", "b", Plain).Count)
        End Sub

        <TestMethod>
        Public Sub Random_patterns_and_texts_never_throw_and_edits_always_match_the_new_text()
            Dim texts As String() = {"", "a", "aaa bbb", "een" & LF & "twee" & LF, "<speak>x &amp; y</speak>", ChrW(&H5D0) & " " & ChrW(&H5DC)}
            Dim patterns As String() = {"a", "a+", "(a|b)*", "^", "$", "\b", "[", "(", "a{2,}", ".*", "\n", "(?<x>a)\k<x>", "&amp;", "<[^>]+>", "\p{L}+", "(a+)+$", ChrW(&H5D0)}
            For Each t In texts
                For Each pat In patterns
                    For Each useRegex In {True, False}
                        Dim opts As New SearchOptions(useRegex:=useRegex)
                        Dim found As FindOutcome = TextSearch.Find(t, pat, opts, 0, True, True, 200)
                        Dim backwards As FindOutcome = TextSearch.Find(t, pat, opts, t.Length, False, True, 200)
                        Dim all As ReplaceAllOutcome = TextSearch.ReplaceAll(t, pat, "<>", opts, 200)
                        If all.ErrorMessage Is Nothing Then
                            Assert.AreEqual(all.NewText, Apply(t, all.Edits), $"{pat} on {t}")
                            Assert.HasCount(all.Count, all.Edits)
                        End If
                        If found.Match IsNot Nothing Then
                            Assert.IsGreaterThan(0, found.Match.Length)
                            Assert.IsLessThan(t.Length + 1, found.Match.Start + found.Match.Length)
                        End If
                        Assert.IsNotNull(backwards)
                    Next
                Next
            Next
        End Sub

    End Class

End Namespace
