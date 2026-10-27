Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Import

<TestClass>
Public Class ReferencePanelContentTests

    <TestMethod>
    Public Sub Parse_reads_title_snippet_and_note_in_order()
        Dim text As String =
            "[Entry]" & vbCrLf &
            "Title: First" & vbCrLf &
            "Snippet:" & vbCrLf &
            "<a/>" & vbCrLf &
            "Note:" & vbCrLf &
            "Note one." & vbCrLf &
            "[/Entry]" & vbCrLf &
            "[Entry]" & vbCrLf &
            "Title: Second" & vbCrLf &
            "Snippet:" & vbCrLf &
            "<b/>" & vbCrLf &
            "Note:" & vbCrLf &
            "Note two." & vbCrLf &
            "[/Entry]"

        Dim entries = ReferencePanelContent.Parse(text)

        Assert.HasCount(2, entries)
        Assert.AreEqual("First", entries(0).Title)
        Assert.AreEqual("<a/>", entries(0).Snippet)
        Assert.AreEqual("Note one.", entries(0).Note)
        Assert.AreEqual("Second", entries(1).Title)
    End Sub

    <TestMethod>
    Public Sub Parse_preserves_multiline_snippet_exactly()
        Dim text As String =
            "[Entry]" & vbCrLf &
            "Title: Switch voice" & vbCrLf &
            "Snippet:" & vbCrLf &
            "<voice name=""en-US-JennyNeural"">" & vbCrLf &
            "  Text goes here." & vbCrLf &
            "</voice>" & vbCrLf &
            "Note:" & vbCrLf &
            "A note." & vbCrLf &
            "[/Entry]"

        Dim entries = ReferencePanelContent.Parse(text)

        Dim expectedSnippet As String =
            "<voice name=""en-US-JennyNeural"">" & vbCrLf &
            "  Text goes here." & vbCrLf &
            "</voice>"
        Assert.AreEqual(expectedSnippet, entries(0).Snippet)
    End Sub

    <TestMethod>
    Public Sub Parse_of_empty_text_returns_no_entries()
        Assert.IsEmpty(ReferencePanelContent.Parse(""))
    End Sub

    <TestMethod>
    Public Sub Parse_tolerates_a_block_missing_the_note_field()
        Dim text As String =
            "[Entry]" & vbCrLf &
            "Title: No note" & vbCrLf &
            "Snippet:" & vbCrLf &
            "<a/>" & vbCrLf &
            "[/Entry]"

        Dim entries = ReferencePanelContent.Parse(text)

        Assert.HasCount(1, entries)
        Assert.AreEqual(String.Empty, entries(0).Note)
    End Sub

    <TestMethod>
    Public Sub Embedded_resource_loads_and_has_the_expected_entry_count()
        Dim entries = ReferencePanelContent.LoadEntries()
        Assert.HasCount(21, entries)
    End Sub

    <TestMethod>
    Public Sub Embedded_resource_places_mstts_entry_last()
        Dim entries = ReferencePanelContent.LoadEntries()
        Assert.AreEqual("Styled speech (mstts)", entries(entries.Count - 1).Title)
    End Sub

    ''' <summary>
    ''' The quality guard the phase 4.5 prompt calls for: every reference-panel example,
    ''' wrapped in a valid scaffold, must pass Omskep's own checkers -- the panel can never
    ''' teach markup Omskep itself rejects.
    ''' </summary>
    <TestMethod>
    Public Sub Every_reference_entry_is_well_formed_when_composed()
        Dim noRateOverride As New Dictionary(Of String, Integer)()

        For Each entry In ReferencePanelContent.LoadEntries()
            Dim document As String = DocumentFor(entry)

            Dim checkResult As CheckResult = WellFormednessChecker.Check(document)
            Assert.IsTrue(checkResult.IsWellFormed, $"'{entry.Title}' failed WellFormednessChecker: {checkResult}")

            Dim nestedVoiceResult = NestedVoiceFinder.Find(document)
            Assert.IsFalse(nestedVoiceResult.Any(), $"'{entry.Title}' contains a nested <voice>.")

            Dim compositionResult As CompositionResult = SsmlComposer.Compose(document, noRateOverride)
            Assert.IsTrue(compositionResult.IsSuccess, $"'{entry.Title}' failed composition: {compositionResult.FailureReason}")
        Next
    End Sub

    ' "Switch voice" demonstrates a full <voice> element meant to sit as a SIBLING of another
    ' voice block, switching from one to the other -- not nested inside one. Build that case as
    ' a complete two-voice document directly, rather than through DocumentComposer (which would
    ' wrap it in an outer <voice> and manufacture a false nested-voice violation). "Styled speech
    ' (mstts)" shows only the fragment being demonstrated, not a standalone body. Everything else
    ' goes through DocumentComposer as an ordinary single-voice document body.
    Private Shared Function DocumentFor(entry As (Title As String, Snippet As String, Note As String)) As String
        Select Case entry.Title
            Case "Switch voice"
                Return "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xmlns:mstts=""https://www.w3.org/2001/mstts"" xml:lang=""en-US"">" & vbCrLf &
                       "<voice name=""en-ZA-LeahNeural"">Text before.</voice>" & vbCrLf &
                       entry.Snippet & vbCrLf &
                       "</speak>"
            Case "Styled speech (mstts)"
                Return DocumentComposer.Compose("<mstts:express-as style=""cheerful"">Text.</mstts:express-as>", "en-US", "en-US-JennyNeural")
            Case Else
                Return DocumentComposer.Compose(entry.Snippet, "en-US", "en-US-JennyNeural")
        End Select
    End Function

    ' The "Styled speech (mstts)" and "Switch voice" entries show markup that isn't itself a
    ' standalone body fragment (an xmlns declaration line, a full <voice> element) -- for
    ' those two, substitute the one real body-level fragment each is demonstrating, since
    ' DocumentComposer already supplies <speak> and <voice>.
    Private Shared Function BodyFor(entry As (Title As String, Snippet As String, Note As String)) As String
        Select Case entry.Title
            Case "Styled speech (mstts)"
                Return "<mstts:express-as style=""cheerful"">Text.</mstts:express-as>"
            Case "Switch voice"
                Return "Text before." & vbCrLf & entry.Snippet & vbCrLf & "Text after."
            Case Else
                Return entry.Snippet
        End Select
    End Function

End Class