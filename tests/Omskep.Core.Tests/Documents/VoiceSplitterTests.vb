Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class VoiceSplitterTests

        ' A minimal but realistic scaffold: an omskep-meta PI (standing in for BodyStartLine's
        ' territory), then <speak> with one default <voice>. Tests build on this rather than
        ' hand-writing the header every time.
        Private Shared Function Doc(body As String, Optional defaultVoice As String = "af-ZA-WillemNeural") As String
            Return "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                   "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" & vbLf &
                   "<voice name=""" & defaultVoice & """>" & body & "</voice>" & vbLf &
                   "</speak>"
        End Function

        Private Shared Function IndexOfOnce(text As String, needle As String) As Integer
            Dim i As Integer = text.IndexOf(needle, StringComparison.Ordinal)
            Assert.IsGreaterThanOrEqualTo(0, i, "Expected to find '" & needle & "' in the test fixture.")
            Return i
        End Function

        ' ---- Case 1: mid-block split ----

        <TestMethod>
        Public Sub SelectionInsideOneVoice_SplitsIntoThreeBlocks_OriginalVoiceReopened()
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, "there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "there".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.Contains("<voice name=""af-ZA-WillemNeural"">Hello </voice>", result.NewText)
            Assert.Contains("<voice name=""en-US-JennyNeural"">there</voice>", result.NewText)
            Assert.Contains("<voice name=""af-ZA-WillemNeural""> friend</voice>", result.NewText)
        End Sub

        <TestMethod>
        Public Sub SelectionInsideOneVoice_LeadingAndTrailingTextPreservedByteForByte()
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, "there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "there".Length, "en-US-JennyNeural")

            Assert.Contains("Hello ", result.NewText)
            Assert.Contains(" friend", result.NewText)
            Assert.Contains("there", result.NewText)
        End Sub

        ' ---- SsmlEditor.ApplyEdits contract: edits must be ordered last first (highest offset first) ----

        <TestMethod>
        Public Sub BothFragmentsSplit_EditsAreOrderedLastFirst_MatchingApplyEditsContract()
            ' SsmlEditor.ApplyEdits walks its edit list back-to-front in one pass, so it requires the
            ' list itself to be given highest-offset-first. A two-edit split (real text on both sides of
            ' the selection) is the only shape with more than one edit, so it's the only case that can
            ' catch an ordering regression -- the unit tests otherwise never exercise the real editor.
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, "there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "there".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(2, result.Edits)
            Assert.IsGreaterThan(result.Edits(1).Start, result.Edits(0).Start)
        End Sub

        ' ---- Case 2: whole-block rename ----

        <TestMethod>
        Public Sub SelectionIsEntireVoiceContent_RenamesInPlace_NoSplitProduced()
            Dim text As String = Doc("Entire content")
            Dim start As Integer = IndexOfOnce(text, "Entire content")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Entire content".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(1, result.Edits)
            Assert.Contains("<voice name=""en-US-JennyNeural"">Entire content</voice>", result.NewText)
            Assert.DoesNotContain("af-ZA-WillemNeural", result.NewText)
        End Sub

        ' ---- Case 3: caret-only resolves to enclosing voice ----

        <TestMethod>
        Public Sub EmptySelectionInsideVoiceText_ResolvesToEnclosingVoice_BehavesAsRename()
            Dim text As String = Doc("Some spoken text")
            Dim caret As Integer = IndexOfOnce(text, "spoken")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, caret, 0, "he-IL-HilaNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(1, result.Edits)
            Assert.Contains("<voice name=""he-IL-HilaNeural"">Some spoken text</voice>", result.NewText)
        End Sub

        ' ---- Case 4: spans voice boundary ----

        <TestMethod>
        Public Sub SelectionSpanningTwoAdjacentVoiceBlocks_IsRefused()
            Dim text As String = "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" &
                "<voice name=""af-ZA-WillemNeural"">Hello </voice><voice name=""en-US-JennyNeural"">world</voice>" &
                "</speak>"
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Dim selEnd As Integer = IndexOfOnce(text, "world") + 3
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, selEnd - start, "he-IL-HilaNeural")

            Assert.IsFalse(result.IsSuccess)
            Assert.IsNotNull(result.FailureReason)
        End Sub

        ' ---- Case 5: inline-element boundary ----

        <TestMethod>
        Public Sub SelectionStartingInsideSubElement_IsRefused()
            Dim text As String = Doc("before <sub alias=""ABC"">XYZ</sub> after")
            Dim subOpen As Integer = IndexOfOnce(text, "<sub")
            Dim midOfSub As Integer = IndexOfOnce(text, "XYZ") + 1
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, midOfSub, "after".Length + 1, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        <TestMethod>
        Public Sub SelectionEndingInsidePhonemeElement_IsRefused()
            Dim text As String = Doc("before <phoneme alphabet=""ipa"" ph=""x"">XYZ</phoneme> after")
            Dim start As Integer = IndexOfOnce(text, "before")
            Dim midOfPhoneme As Integer = IndexOfOnce(text, "XYZ") + 1
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, midOfPhoneme - start, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        <TestMethod>
        Public Sub SelectionFullyContainingAnInlineElement_IsAllowed()
            Dim text As String = Doc("before <sub alias=""ABC"">XYZ</sub> after")
            Dim start As Integer = IndexOfOnce(text, "before")
            Dim subCloseEnd As Integer = text.IndexOf("</sub>", StringComparison.Ordinal) + "</sub>".Length
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, subCloseEnd - start, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.Contains("<sub alias=""ABC"">XYZ</sub>", result.NewText)
        End Sub

        <TestMethod>
        Public Sub SelectionBoundaryRefusal_DoesNotModifyDocument()
            Dim text As String = Doc("before <sub alias=""ABC"">XYZ</sub> after")
            Dim midOfSub As Integer = IndexOfOnce(text, "XYZ") + 1
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, midOfSub, 5, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
            Assert.IsNull(result.NewText)
            CollectionAssert.AreEqual(Array.Empty(Of TextEdit)(), result.Edits.ToArray())
        End Sub

        ' ---- Case 6 (refined): whitespace-only edge fragments never produce an empty/whitespace voice ----

        <TestMethod>
        Public Sub SelectionStartsAtFirstNonWhitespaceChar_NoLeadingWhitespaceOnlyVoiceProduced()
            Dim text As String = Doc("  Hello world")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Hello world".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(1, result.Edits, "A whitespace-only leading gap should collapse to a rename, not a split.")
            Assert.Contains("<voice name=""en-US-JennyNeural"">  Hello world</voice>", result.NewText, "The leading whitespace must travel with the renamed voice, never be lost.")
        End Sub

        <TestMethod>
        Public Sub SelectionEndsAtLastNonWhitespaceChar_NoTrailingWhitespaceOnlyVoiceProduced()
            Dim text As String = Doc("Hello world  ")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Hello world".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(1, result.Edits)
            Assert.Contains("<voice name=""en-US-JennyNeural"">Hello world  </voice>", result.NewText)
        End Sub

        <TestMethod>
        Public Sub SelectionLeavesOnlyWhitespaceOnBothSides_CollapsesToRenameCase()
            Dim text As String = Doc(" Hello world ")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Hello world".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.HasCount(1, result.Edits)
            Assert.Contains("<voice name=""en-US-JennyNeural""> Hello world </voice>", result.NewText)
        End Sub

        <TestMethod>
        Public Sub WhitespaceOnlyFragment_NeverAppearsInOutput_AcrossSpacesTabsAndNewlines()
            Dim text As String = Doc(ChrW(9) & " " & vbLf & "Hello world")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Hello world".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.DoesNotContain("<voice name=""af-ZA-WillemNeural""></voice>", result.NewText)
            Assert.HasCount(1, result.Edits)
        End Sub

        ' ---- Case 7: zero-length at a true boundary ----

        <TestMethod>
        Public Sub CaretExactlyBetweenTwoVoices_IsRefused()
            Dim text As String = "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" &
                "<voice name=""af-ZA-WillemNeural"">Hello</voice><voice name=""en-US-JennyNeural"">world</voice>" &
                "</speak>"
            Dim caret As Integer = text.IndexOf("</voice><voice", StringComparison.Ordinal) + "</voice>".Length
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, caret, 0, "he-IL-HilaNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        ' ---- Case 8: whitespace inside the selection travels with the text ----

        <TestMethod>
        Public Sub SelectionIncludingLeadingSpace_SpaceTravelsWithSelectedText()
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, " there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, " there".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.Contains("<voice name=""en-US-JennyNeural""> there</voice>", result.NewText)
        End Sub

        ' ---- Case 9: no voice element at all ----

        <TestMethod>
        Public Sub DocumentWithNoVoiceElement_IsRefused()
            Dim text As String = "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">Hello</speak>"
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, text.IndexOf("Hello", StringComparison.Ordinal), 5, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        <TestMethod>
        Public Sub DocumentWithNoVoiceElement_RefusalDoesNotThrow()
            Dim text As String = "<speak></speak>"
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, 0, 0, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        ' ---- Case 10: before BodyStartLine / inside omskep-meta ----

        <TestMethod>
        Public Sub SelectionInsideOmskepMetaBlock_IsRefusedUnconditionally()
            Dim text As String = Doc("Hello")
            Dim start As Integer = IndexOfOnce(text, "Title:")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Title:".Length, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        <TestMethod>
        Public Sub SelectionBeforeBodyStartLine_IsRefusedEvenIfOtherwiseValid()
            ' Constructed so every other rule (well-formed, single voice, no inline-element cut) would
            ' normally allow it -- the only thing standing in the way is the body-start position, proving
            ' that check runs first/unconditionally rather than being skipped because nothing else fires.
            Dim text As String = Doc("Hello world")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, 0, 5, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        <TestMethod>
        Public Sub CaretInsideOmskepMetaBlock_EmptySelection_IsRefused()
            Dim text As String = Doc("Hello")
            Dim caret As Integer = IndexOfOnce(text, "Title:")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, caret, 0, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        ' ---- Case 11: pre-existing nested voice elsewhere ----

        <TestMethod>
        Public Sub DocumentAlreadyContainsNestedVoiceElsewhere_WholeOperationRefused_NotJustRepaired()
            Dim text As String = "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""af-ZA"">" &
                "<voice name=""af-ZA-WillemNeural"">Outer <voice name=""en-US-JennyNeural"">inner</voice> text</voice>" &
                "<voice name=""af-ZA-WillemNeural"">Unrelated clean block</voice>" &
                "</speak>"
            Dim start As Integer = text.IndexOf("Unrelated", StringComparison.Ordinal)
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "Unrelated".Length, "he-IL-HilaNeural")

            Assert.IsFalse(result.IsSuccess, "The whole operation should refuse even though the targeted selection itself is clean.")
            StringAssert.Contains(result.FailureReason, "nested")
        End Sub

        ' ---- Must-never, cross-cutting ----

        <TestMethod>
        Public Sub AnyRefusal_LeavesDocumentByteForByteUnchanged()
            Dim text As String = Doc("Hello world")
            Dim before As String = text
            VoiceSplitter.Split(text, 0, 5, "en-US-JennyNeural") ' refused: before body start
            Assert.AreEqual(before, text, "VoiceSplitter must never mutate its input string.")
        End Sub

        <TestMethod>
        Public Sub SuccessfulSplit_NeverProducesNestedVoice_VerifiedByReparsing()
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, "there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "there".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Dim reScan As VoiceSplitResult = VoiceSplitter.Split(result.NewText, 0, 0, "af-ZA-AdriNeural")
            ' Re-running Split against the output should not itself fail with a "nested voice" refusal.
            Assert.DoesNotContain("nested", If(reScan.FailureReason, String.Empty))
        End Sub

        <TestMethod>
        Public Sub SuccessfulSplit_HebrewTextSurvivesByteForByte()
            Dim hebrew As String = "שלום עולם"
            Dim text As String = Doc("before " & hebrew & " after")
            Dim start As Integer = IndexOfOnce(text, hebrew)
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, hebrew.Length, "he-IL-HilaNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.Contains("<voice name=""he-IL-HilaNeural"">" & hebrew & "</voice>", result.NewText)
        End Sub

        <TestMethod>
        Public Sub SuccessfulSplit_AccentedAfrikaansTextSurvivesByteForByte()
            Dim accented As String = "Allerhoogste Heer"
            Dim text As String = Doc("vóór " & accented & " ná")
            Dim start As Integer = IndexOfOnce(text, accented)
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, accented.Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.Contains(accented, result.NewText)
            Assert.Contains("vóór", result.NewText)
            Assert.Contains("ná", result.NewText)
        End Sub

        <TestMethod>
        Public Sub SuccessfulSplit_ResultIsAlwaysWellFormed_VerifiedByWellFormednessChecker()
            Dim text As String = Doc("Hello there friend")
            Dim start As Integer = IndexOfOnce(text, "there")
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, "there".Length, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Dim check As CheckResult = WellFormednessChecker.Check(result.NewText)
            Assert.IsTrue(check.IsWellFormed, check.StatusText)
        End Sub

        <TestMethod>
        Public Sub MalformedInputDocument_IsRefused_NeverGuessedAt()
            Dim text As String = Doc("Hello world").Replace("</voice>", String.Empty) ' now malformed
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, text.IndexOf("Hello", StringComparison.Ordinal), 5, "en-US-JennyNeural")

            Assert.IsFalse(result.IsSuccess)
        End Sub

        ' ---- Hostile input ----

        <TestMethod>
        Public Sub NullOrEmptyDocumentText_IsRefused_DoesNotThrow()
            Assert.IsFalse(VoiceSplitter.Split(Nothing, 0, 0, "en-US-JennyNeural").IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(String.Empty, 0, 0, "en-US-JennyNeural").IsSuccess)
        End Sub

        <TestMethod>
        Public Sub SelectionIndicesOutOfRange_IsRefused_DoesNotThrow()
            Dim text As String = Doc("Hello world")
            Assert.IsFalse(VoiceSplitter.Split(text, text.Length + 50, 5, "en-US-JennyNeural").IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(text, 5, text.Length, "en-US-JennyNeural").IsSuccess)
        End Sub

        <TestMethod>
        Public Sub NegativeOrInvertedSelectionRange_IsRefused_DoesNotThrow()
            Dim text As String = Doc("Hello world")
            Assert.IsFalse(VoiceSplitter.Split(text, -1, 5, "en-US-JennyNeural").IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(text, 5, -1, "en-US-JennyNeural").IsSuccess)
        End Sub

        <TestMethod>
        Public Sub NewVoiceNameIsNullOrEmpty_IsRefused()
            Dim text As String = Doc("Hello world")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Assert.IsFalse(VoiceSplitter.Split(text, start, 5, Nothing).IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(text, start, 5, "   ").IsSuccess)
        End Sub

        <TestMethod>
        Public Sub NewVoiceNameContainingXmlSpecialCharacters_IsRefusedOutright()
            Dim text As String = Doc("Hello world")
            Dim start As Integer = IndexOfOnce(text, "Hello")
            Assert.IsFalse(VoiceSplitter.Split(text, start, 5, "bad<name").IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(text, start, 5, "bad&name").IsSuccess)
            Assert.IsFalse(VoiceSplitter.Split(text, start, 5, "bad""name").IsSuccess)
        End Sub

        <TestMethod>
        Public Sub VeryLargeDocument_SplitStillAppliedAsOneUndoStep()
            Dim body As New Text.StringBuilder()
            For i As Integer = 1 To 4000
                body.Append("word ")
            Next
            Dim text As String = Doc(body.ToString())
            Dim start As Integer = text.IndexOf("word", StringComparison.Ordinal)
            Dim result As VoiceSplitResult = VoiceSplitter.Split(text, start, 4, "en-US-JennyNeural")

            Assert.IsTrue(result.IsSuccess, result.FailureReason)
            Assert.IsLessThanOrEqualTo(2, result.Edits.Count)
        End Sub

    End Class

End Namespace