Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class SsmlComposerTests

        ' ---- structural rule: no nested <voice> ----

        <TestMethod>
        Public Sub A_voice_nested_inside_another_voice_is_rejected()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""en-US-JennyNeural"">" & LF &
                "outer " & LF &
                "<voice name=""he-IL-HilaNeural"">inner</voice>" & LF &
                "</voice>" & LF & "</speak>"
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsFalse(result.IsSuccess)
            Assert.IsNull(result.Ssml)
            Assert.Contains("nested", result.FailureReason)
        End Sub

        <TestMethod>
        Public Sub Sibling_voice_elements_are_not_nesting()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""en-US-JennyNeural"">Hello.</voice>" & LF &
                "<voice name=""he-IL-HilaNeural"">Shalom.</voice>" & LF & "</speak>"
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains("Hello.", result.Ssml)
            Assert.Contains("Shalom.", result.Ssml)
        End Sub

        ' ---- a malformed document is refused, not guessed at ----

        <TestMethod>
        Public Sub A_malformed_document_is_not_composed()
            Dim result As CompositionResult = SsmlComposer.Compose(Wrap("Moses & Aaron"), Nothing)
            Assert.IsFalse(result.IsSuccess)
            Assert.IsNull(result.Ssml)
            Assert.Contains("not well-formed", result.FailureReason)
        End Sub

        <TestMethod>
        Public Sub Null_and_empty_text_are_refused_not_exceptions()
            For Each text As String In {Nothing, String.Empty}
                Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
                Assert.IsFalse(result.IsSuccess)
            Next
        End Sub

        ' ---- whitespace collapse ----

        <TestMethod>
        Public Sub A_run_of_whitespace_in_text_collapses_to_one_space()
            Dim text As String = Doc("Hallo" & LF & "   " & LF & "wêreld.")
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains("Hallo wêreld.", result.Ssml)
        End Sub

        <TestMethod>
        Public Sub A_composed_document_never_contains_a_raw_newline()
            Dim text As String = Doc("Een." & LF & "Twee." & LF & LF & "Drie.")
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsTrue(result.IsSuccess)
            Assert.DoesNotContain(LF, result.Ssml)
        End Sub

        <TestMethod>
        Public Sub Whitespace_collapse_never_touches_an_attribute_value()
            Dim text As String = Doc("Hallo <break time=""500ms""/> wêreld.")
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains("time=""500ms""", result.Ssml)
        End Sub

        ' ---- text survives composition unchanged ----

        <TestMethod>
        Public Sub Hebrew_and_accented_text_survive_composition_unchanged()
            Dim hebrew As String = "שָׁלוֹם"
            Dim accented As String = "café naïve"
            Dim text As String = Doc(hebrew & " " & accented & ".")
            Dim result As CompositionResult = SsmlComposer.Compose(text, Nothing)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains(hebrew, result.Ssml)
            Assert.Contains(accented, result.Ssml)
        End Sub

        ' ---- per-locale prosody rate wrapper ----

        <TestMethod>
        Public Sub A_locale_with_a_rate_preset_is_wrapped_in_prosody()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""he-IL-HilaNeural"">Shalom.</voice>" & LF & "</speak>"
            Dim rates As New Dictionary(Of String, Integer) From {{"he-IL", -30}}
            Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains("<prosody rate=""-30%"">", result.Ssml)
            Assert.Contains("Shalom.", result.Ssml)
        End Sub

        <TestMethod>
        Public Sub A_zero_percent_preset_still_wraps_with_an_explicit_sign()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""af-ZA-WillemNeural"">Hallo.</voice>" & LF & "</speak>"
            Dim rates As New Dictionary(Of String, Integer) From {{"af-ZA", 0}}
            Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
            Assert.IsTrue(result.IsSuccess)
            Assert.Contains("rate=""+0%""", result.Ssml)
        End Sub

        <TestMethod>
        Public Sub A_locale_with_no_preset_is_left_unwrapped()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""en-US-JennyNeural"">Hello.</voice>" & LF & "</speak>"
            Dim rates As New Dictionary(Of String, Integer) From {{"he-IL", -30}}
            Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
            Assert.IsTrue(result.IsSuccess)
            Assert.DoesNotContain("<prosody", result.Ssml)
        End Sub

        <TestMethod>
        Public Sub Nothing_or_an_empty_rate_dictionary_leaves_every_voice_unwrapped()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""he-IL-HilaNeural"">Shalom.</voice>" & LF & "</speak>"
            For Each rates As IReadOnlyDictionary(Of String, Integer) In {Nothing, New Dictionary(Of String, Integer)()}
                Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
                Assert.IsTrue(result.IsSuccess)
                Assert.DoesNotContain("<prosody", result.Ssml)
            Next
        End Sub

        <TestMethod>
        Public Sub Each_voice_in_a_multi_voice_document_gets_its_own_locales_rate_independently()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""en-US-JennyNeural"">Hello.</voice>" & LF &
                "<voice name=""he-IL-HilaNeural"">Shalom.</voice>" & LF &
                "<voice name=""en-US-JennyNeural"">Goodbye.</voice>" & LF & "</speak>"
            Dim rates As New Dictionary(Of String, Integer) From {{"he-IL", -30}}
            Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
            Assert.IsTrue(result.IsSuccess)
            Dim prosodyCount As Integer = result.Ssml.Split(New String() {"<prosody "}, StringSplitOptions.None).Length - 1
            Assert.AreEqual(1, prosodyCount)
        End Sub

        <TestMethod>
        Public Sub An_unparseable_voice_name_is_simply_left_unwrapped()
            Dim text As String =
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                "<voice name=""default"">Hallo.</voice>" & LF & "</speak>"
            Dim rates As New Dictionary(Of String, Integer) From {{"af-ZA", -10}}
            Dim result As CompositionResult = SsmlComposer.Compose(text, rates)
            Assert.IsTrue(result.IsSuccess)
            Assert.DoesNotContain("<prosody", result.Ssml)
        End Sub

    End Class

End Namespace
