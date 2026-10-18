Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class NestedVoiceFinderTests

        <TestMethod>
        Public Sub EmptyOrNullDocument_ReturnsNoMarks_DoesNotThrow()
            Assert.HasCount(0, NestedVoiceFinder.Find(Nothing))
            Assert.HasCount(0, NestedVoiceFinder.Find(String.Empty))
        End Sub

        <TestMethod>
        Public Sub CleanDocumentWithSeveralVoices_ReturnsNoMarks()
            Dim text As String = "<speak><voice name=""A"">one</voice><voice name=""B"">two</voice></speak>"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub DocumentWithNoVoiceAtAll_ReturnsNoMarks()
            Assert.HasCount(0, NestedVoiceFinder.Find("<speak>Hello</speak>"))
        End Sub

        <TestMethod>
        Public Sub SingleNestedVoice_ReturnsOneMark_AtTheInnerTagsPosition()
            Dim text As String = "<speak><voice name=""A"">outer <voice name=""B"">inner</voice> text</voice></speak>"
            Dim innerTagStart As Integer = text.IndexOf("<voice name=""B""", StringComparison.Ordinal)

            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks)
            Assert.AreEqual(1, marks(0).Line)
            Assert.AreEqual(innerTagStart + 1, marks(0).Column) ' 1-based column
            Assert.AreEqual("<voice".Length, marks(0).Length)
        End Sub

        <TestMethod>
        Public Sub TwoSeparateNestedVoices_ReturnsTwoMarks()
            Dim text As String =
                "<speak>" &
                "<voice name=""A"">outer1 <voice name=""B"">inner1</voice></voice>" &
                "<voice name=""C"">outer2 <voice name=""D"">inner2</voice></voice>" &
                "</speak>"

            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(2, marks)
        End Sub

        <TestMethod>
        Public Sub TripleNestedVoice_FlagsEachLevelBeyondTheFirst()
            Dim text As String = "<speak><voice name=""A""><voice name=""B""><voice name=""C"">x</voice></voice></voice></speak>"
            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(2, marks) ' B and C both open while another voice is already open; A does not
        End Sub

        <TestMethod>
        Public Sub SelfClosingNestedVoice_IsStillFlagged()
            Dim text As String = "<speak><voice name=""A"">before <voice name=""B""/> after</voice></speak>"
            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks)
        End Sub

        <TestMethod>
        Public Sub SelfClosingVoice_DoesNotPersistDepth_SubsequentSiblingVoiceIsClean()
            ' The self-closing <voice/> opens and closes in the same breath -- it must not leave depth
            ' raised for whatever voice comes after it.
            Dim text As String = "<speak><voice name=""A""><voice name=""B""/></voice><voice name=""C"">clean</voice></speak>"
            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks) ' only B, nested inside A; C is a normal sibling, not nested
        End Sub

        <TestMethod>
        Public Sub NestedVoiceOnASecondLine_ReportsCorrectLineAndColumn()
            ' Deliberately NOT at column 1 -- an offset that exactly matches a line-start offset takes a
            ' different path through BinarySearch (an exact match) than one that falls between two line
            ' starts (the insertion-point path), and only the latter is realistic for a nested tag.
            Dim text As String = "<speak>" & vbLf & "<voice name=""A"">outer " & vbLf & "  <voice name=""B"">inner</voice></voice>" & vbLf & "</speak>"
            Dim innerLineStart As Integer = text.LastIndexOf(vbLf, text.IndexOf("<voice name=""B""", StringComparison.Ordinal), StringComparison.Ordinal) + 1
            Dim innerTagStart As Integer = text.IndexOf("<voice name=""B""", StringComparison.Ordinal)

            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks)
            Assert.AreEqual(3, marks(0).Line) ' line 1: <speak>, line 2: outer voice + "outer", line 3: inner voice
            Assert.AreEqual(innerTagStart - innerLineStart + 1, marks(0).Column)
        End Sub

        <TestMethod>
        Public Sub TextLookingLikeVoiceInsideAComment_IsNotFlagged()
            Dim text As String = "<speak><voice name=""A"">real <!-- <voice name=""B"">fake</voice> --> text</voice></speak>"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub TextLookingLikeVoiceInsideCData_IsNotFlagged()
            Dim text As String = "<speak><voice name=""A""><![CDATA[<voice name=""B"">fake</voice>]]></voice></speak>"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub AttributeValueContainingAngleBracketLikeText_DoesNotConfuseTheScanner()
            Dim text As String = "<speak><voice name=""A &lt;voice&gt; B"">real</voice></speak>"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub ElementNamedVoicemailIsNotMistakenForVoice()
            Dim text As String = "<speak><voice name=""A""><voicemail>not a voice element</voicemail></voice></speak>"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        ' ---- hostile / malformed input: this runs on every keystroke, so mid-edit garbage is routine ----

        <TestMethod>
        Public Sub UnterminatedVoiceTag_DoesNotThrow_ReturnsWhateverWasFoundBeforeIt()
            Dim text As String = "<speak><voice name=""A""><voice name=""B"">inner, but then the user is still typing <voice name="
            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks) ' the first nesting (B inside A) was complete and found before the dangling tag
        End Sub

        <TestMethod>
        Public Sub UnterminatedComment_DoesNotThrow()
            Dim text As String = "<speak><voice name=""A"">text <!-- never closed"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub DanglingLessThanAtEndOfDocument_DoesNotThrow()
            Dim text As String = "<speak><voice name=""A"">text</voice><"
            Assert.HasCount(0, NestedVoiceFinder.Find(text))
        End Sub

        <TestMethod>
        Public Sub ExtraClosingVoiceTag_DoesNotGoNegativeOrThrow()
            Dim text As String = "<speak></voice></voice><voice name=""A""><voice name=""B"">x</voice></voice></speak>"
            Dim marks As IReadOnlyList(Of NestedVoiceMark) = NestedVoiceFinder.Find(text)

            Assert.HasCount(1, marks) ' the leading stray closes must not push depth negative and mask the real nesting
        End Sub

        <TestMethod>
        Public Sub CompletelyUnrelatedGarbageText_ReturnsNoMarks_DoesNotThrow()
            Assert.HasCount(0, NestedVoiceFinder.Find("not xml at all, just <<< some > symbols > here"))
        End Sub

        <TestMethod>
        Public Sub VeryLargeCleanDocument_CompletesPromptlyWithNoMarks()
            Dim body As New Text.StringBuilder()
            body.Append("<speak>")
            For i As Integer = 1 To 2000
                body.Append("<voice name=""A"">word </voice>")
            Next
            body.Append("</speak>")
            Assert.HasCount(0, NestedVoiceFinder.Find(body.ToString()))
        End Sub

    End Class

End Namespace
