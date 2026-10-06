Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text
Imports System.Xml
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Import

Namespace Documents

    <TestClass>
    Public Class DocumentScaffoldTests

        Private Shared ReadOnly LF As String = ChrW(10)

        <TestMethod>
        Public Sub A_new_scaffold_is_blank()
            Assert.IsTrue(DocumentScaffold.IsBlank(DocumentComposer.Compose(String.Empty, "af-ZA", "af-ZA-WillemNeural")))
            Assert.IsTrue(DocumentScaffold.IsBlank(Nothing))
            Assert.IsTrue(DocumentScaffold.IsBlank(String.Empty))
            Assert.IsTrue(DocumentScaffold.IsBlank("  " & LF & " "))
        End Sub

        <TestMethod>
        Public Sub Any_body_text_or_other_markup_makes_it_content()
            Assert.IsFalse(DocumentScaffold.IsBlank(DocumentComposer.Compose("x", "af-ZA", "v")))
            Assert.IsFalse(DocumentScaffold.IsBlank("<speak><voice name=""v""><break time=""1s""/></voice></speak>"))
            Assert.IsFalse(DocumentScaffold.IsBlank("<!-- <voice> --><speak></speak>"))
            Assert.IsFalse(DocumentScaffold.IsBlank("just text"))
        End Sub

        <TestMethod>
        Public Sub A_half_typed_tag_counts_as_content_so_it_is_never_overwritten()
            Assert.IsFalse(DocumentScaffold.IsBlank("<speak"))
            Assert.IsFalse(DocumentScaffold.IsBlank("<speak><voice name=""v"""))
            Assert.IsFalse(DocumentScaffold.IsBlank("<"))
        End Sub

        <TestMethod>
        Public Sub A_greater_than_inside_an_attribute_does_not_confuse_the_check()
            Assert.IsTrue(DocumentScaffold.IsBlank("<speak><voice name=""a>b""></voice></speak>"))
        End Sub

    End Class

    <TestClass>
    Public Class ScaffoldDefaultsTests

        Private Shared Function Voices(ParamArray pairs As String()) As IReadOnlyDictionary(Of String, String)
            Dim d As New Dictionary(Of String, String)()
            For i As Integer = 0 To pairs.Length - 2 Step 2
                d(pairs(i)) = pairs(i + 1)
            Next
            Return d
        End Function

        <TestMethod>
        Public Sub Nothing_or_empty_gives_the_built_in_afrikaans_voice()
            Dim a As ScaffoldChoice = ScaffoldDefaults.Choose(Nothing)
            Assert.AreEqual("af-ZA", a.Locale)
            Assert.AreEqual("af-ZA-WillemNeural", a.VoiceShortName)
            Assert.AreEqual("af-ZA-WillemNeural", ScaffoldDefaults.Choose(Voices()).VoiceShortName)
        End Sub

        <TestMethod>
        Public Sub The_afrikaans_entry_wins_when_usable()
            Dim c As ScaffoldChoice = ScaffoldDefaults.Choose(Voices("en-US", "en-US-JennyNeural", "af-ZA", "af-ZA-AdriNeural"))
            Assert.AreEqual("af-ZA", c.Locale)
            Assert.AreEqual("af-ZA-AdriNeural", c.VoiceShortName)
        End Sub

        <TestMethod>
        Public Sub Without_afrikaans_the_first_locale_in_order_is_used()
            Dim c As ScaffoldChoice = ScaffoldDefaults.Choose(Voices("en-US", "v1", "de-DE", "v2"))
            Assert.AreEqual("de-DE", c.Locale)
            Assert.AreEqual("v2", c.VoiceShortName)
        End Sub

        <TestMethod>
        Public Sub Blank_entries_are_skipped()
            Dim c As ScaffoldChoice = ScaffoldDefaults.Choose(Voices("af-ZA", "  ", "de-DE", "", "en-US", " en-US-JennyNeural "))
            Assert.AreEqual("en-US", c.Locale)
            Assert.AreEqual("en-US-JennyNeural", c.VoiceShortName)
            Assert.AreEqual("af-ZA-WillemNeural", ScaffoldDefaults.Choose(Voices("af-ZA", "", "de-DE", " ")).VoiceShortName)
        End Sub

    End Class

    <TestClass>
    Public Class FileKindsTests

        <TestMethod>
        Public Sub Extensions_decide_the_kind_case_insensitively()
            Assert.AreEqual(FileKind.Pdf, FileKinds.Classify("C:\a\Study.PDF"))
            Assert.AreEqual(FileKind.Ssml, FileKinds.Classify("study.ssml"))
            Assert.AreEqual(FileKind.Ssml, FileKinds.Classify("study.XML"))
            Assert.AreEqual(FileKind.Text, FileKinds.Classify("notes.txt"))
            Assert.AreEqual(FileKind.Text, FileKinds.Classify("noextension"))
            Assert.AreEqual(FileKind.Text, FileKinds.Classify("archive.tar.gz"))
        End Sub

        <TestMethod>
        Public Sub Blank_paths_are_text_not_an_exception()
            Assert.AreEqual(FileKind.Text, FileKinds.Classify(Nothing))
            Assert.AreEqual(FileKind.Text, FileKinds.Classify("  "))
        End Sub

        <TestMethod>
        Public Sub SSML_is_recognised_by_its_first_element_even_with_a_BOM_or_leading_blank_lines()
            Assert.IsTrue(FileKinds.LooksLikeSsml("<speak version=""1.0"">"))
            Assert.IsTrue(FileKinds.LooksLikeSsml(ChrW(&HFEFF) & vbCrLf & "  <speak>"))
            Assert.IsTrue(FileKinds.LooksLikeSsml("<?xml version=""1.0""?><speak/>"))
        End Sub

        <TestMethod>
        Public Sub Ordinary_text_and_other_markup_are_not_SSML()
            Assert.IsFalse(FileKinds.LooksLikeSsml(Nothing))
            Assert.IsFalse(FileKinds.LooksLikeSsml(String.Empty))
            Assert.IsFalse(FileKinds.LooksLikeSsml("Hallo wereld"))
            Assert.IsFalse(FileKinds.LooksLikeSsml("<html><body>speak</body></html>"))
            Assert.IsFalse(FileKinds.LooksLikeSsml("<Speak>"))
            Assert.IsFalse(FileKinds.LooksLikeSsml("a <speak>"))
        End Sub

    End Class

    <TestClass>
    Public Class PasteHandlerTests

        Private Shared ReadOnly LF As String = ChrW(10)
        Private Const Locale As String = "af-ZA"
        Private Const Voice As String = "af-ZA-WillemNeural"

        Private Shared ReadOnly Blank As String = DocumentComposer.Compose(String.Empty, Locale, Voice)
        Private Shared ReadOnly WithText As String = DocumentComposer.Compose("Reeds iets.", Locale, Voice)

        Private Shared Function Plan(current As String, clip As String, plain As Boolean) As PastePlan
            Return PasteHandler.Plan(current, clip, Locale, Voice, plain)
        End Function

        Private Shared Sub AssertWellFormed(xml As String)
            Dim settings As New XmlReaderSettings() With {.DtdProcessing = DtdProcessing.Prohibit}
            Try
                Using reader As XmlReader = XmlReader.Create(New StringReader(xml), settings)
                    While reader.Read()
                    End While
                End Using
            Catch ex As XmlException
                Assert.Fail("Not well-formed: " & ex.Message)
            End Try
        End Sub

        ' ---- nothing to paste ----

        <TestMethod>
        Public Sub An_empty_or_blank_clipboard_pastes_nothing_in_both_modes()
            For Each clip As String In {Nothing, String.Empty, "  ", vbCrLf}
                Assert.AreEqual(PasteKind.NothingToPaste, Plan(Blank, clip, False).Kind)
                Assert.AreEqual(PasteKind.NothingToPaste, Plan(WithText, clip, True).Kind)
            Next
        End Sub

        <TestMethod>
        Public Sub Text_that_is_only_removable_characters_pastes_nothing()
            Dim ornament As String = ChrW(&HF098) & ChrW(&HF099) & "  "
            Assert.AreEqual(PasteKind.NothingToPaste, Plan(WithText, ornament, False).Kind)
            Assert.AreEqual(PasteKind.NothingToPaste, Plan(Blank, ornament, False).Kind)
        End Sub

        ' ---- normal paste into a blank document ----

        <TestMethod>
        Public Sub Paste_into_a_blank_document_builds_a_complete_document()
            Dim clip As String = ChrW(&H201C) & "Moses & Aaron" & ChrW(&H201D) & vbCrLf & ChrW(&H2018) & "n Man."
            Dim plan1 As PastePlan = Plan(Blank, clip, False)
            Assert.AreEqual(PasteKind.ReplaceDocument, plan1.Kind)
            Assert.StartsWith("<speak version=""1.0""", plan1.Text)
            Assert.Contains("Moses &amp; Aaron", plan1.Text)
            Assert.Contains(ChrW(&H201C) & "Moses", plan1.Text)
            Assert.Contains(TextSanitizer.ArticleQuote & "n Man.", plan1.Text)
            Assert.DoesNotContain(vbCr, plan1.Text)
            AssertWellFormed(plan1.Text)
            Assert.Contains("Imported the pasted text.", plan1.Message)
        End Sub

        <TestMethod>
        Public Sub Paste_into_a_completely_empty_editor_also_builds_a_document()
            Assert.AreEqual(PasteKind.ReplaceDocument, Plan(String.Empty, "Hallo", False).Kind)
            Assert.AreEqual(PasteKind.ReplaceDocument, Plan(Nothing, "Hallo", False).Kind)
        End Sub

        ' ---- normal paste into a document with content ----

        <TestMethod>
        Public Sub Paste_into_a_document_with_content_inserts_cleaned_text_only()
            Dim clip As String = "Moses & Aaron <vry>" & ChrW(&HF098) & vbCrLf
            Dim plan1 As PastePlan = Plan(WithText, clip, False)
            Assert.AreEqual(PasteKind.InsertText, plan1.Kind)
            Assert.AreEqual("Moses &amp; Aaron &lt;vry&gt;" & LF, plan1.Text)
            Assert.DoesNotContain("<speak", plan1.Text)
            Assert.Contains("Cleaned up: removed 1 decorative symbol.", plan1.Message)
        End Sub

        <TestMethod>
        Public Sub A_paste_with_nothing_to_clean_says_so_briefly()
            Dim plan1 As PastePlan = Plan(WithText, "Gewone teks.", False)
            Assert.AreEqual("Pasted.", plan1.Message)
        End Sub

        <TestMethod>
        Public Sub A_half_typed_document_is_never_replaced()
            Dim plan1 As PastePlan = Plan("<speak><voice name=""v""", "nuwe teks", False)
            Assert.AreEqual(PasteKind.InsertText, plan1.Kind)
        End Sub

        ' ---- paste as plain text ----

        <TestMethod>
        Public Sub Plain_paste_inserts_exactly_what_was_copied()
            Dim clip As String = "<b>bold</b> & " & ChrW(&H2018) & "n"
            Dim plan1 As PastePlan = Plan(WithText, clip, True)
            Assert.AreEqual(PasteKind.InsertText, plan1.Kind)
            Assert.AreEqual(clip, plan1.Text)
            Assert.AreEqual("Pasted as plain text.", plan1.Message)
        End Sub

        <TestMethod>
        Public Sub Plain_paste_into_a_blank_document_still_only_inserts()
            Assert.AreEqual(PasteKind.InsertText, Plan(Blank, "Hallo", True).Kind)
        End Sub

        <TestMethod>
        Public Sub Plain_paste_normalises_line_endings_and_drops_NUL()
            Assert.AreEqual("a" & LF & "b" & LF & "c" & "d", Plan(WithText, "a" & vbCrLf & "b" & vbCr & "c" & ChrW(0) & "d", True).Text)
        End Sub

        ' ---- bad input ----

        <TestMethod>
        Public Sub A_blank_voice_or_locale_is_a_caller_bug_when_a_document_must_be_built()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() PasteHandler.Plan(Blank, "x", " ", Voice, False))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() PasteHandler.Plan(Blank, "x", Locale, String.Empty, False))
        End Sub

        <TestMethod>
        Public Sub Random_clipboard_garbage_always_gives_content_that_fits_inside_a_well_formed_document()
            Dim pool As String() = {"<", ">", "&", "'", ChrW(34), ChrW(&H2018), "n", " ", LF, vbCrLf, ChrW(0), ChrW(1), ChrW(&HD800), ChrW(&HDC00), ChrW(&HF098), ChrW(&HFB4B), "a", "speak", "]]>", "<!--"}
            For seed As Integer = 1 To 300
                Dim rng As New Random(seed)
                Dim sb As New StringBuilder()
                For k As Integer = 1 To rng.Next(0, 50)
                    sb.Append(pool(rng.Next(pool.Length)))
                Next
                Dim clip As String = sb.ToString()
                For Each current As String In {Blank, WithText}
                    Dim plan1 As PastePlan = Plan(current, clip, False)
                    If plan1.Kind = PasteKind.ReplaceDocument Then
                        AssertWellFormed(plan1.Text)
                    ElseIf plan1.Kind = PasteKind.InsertText Then
                        AssertWellFormed("<speak>" & plan1.Text & "</speak>")
                    End If
                Next
            Next
        End Sub

    End Class

End Namespace
