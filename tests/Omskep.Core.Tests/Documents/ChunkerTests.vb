Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class ChunkerTests

        Private Shared Function VoiceDoc(ParamArray voices As String()) As String
            Return "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" & LF &
                   String.Join(LF, voices) & LF & "</speak>"
        End Function

        ' ---- degenerate / defensive ----

        <TestMethod>
        Public Sub A_document_with_no_voice_elements_fails_cleanly()
            Dim result As ChunkingResult = Chunker.Chunk(VoiceDoc())
            Assert.IsFalse(result.IsSuccess)
            Assert.Contains("no <voice>", result.FailureReason)
        End Sub

        <TestMethod>
        Public Sub Malformed_input_does_not_throw()
            Dim result As ChunkingResult = Chunker.Chunk("<speak><voice name=""v"">unterminated")
            Assert.IsFalse(result.IsSuccess)
            Assert.IsNotNull(result.FailureReason)
        End Sub

        ' ---- free split: whole <voice> elements ----

        <TestMethod>
        Public Sub Two_short_voices_combine_into_one_chunk_when_they_fit_together()
            Dim text As String = VoiceDoc(
                "<voice name=""en-US-JennyNeural"">" & New String("a"c, 50) & "</voice>",
                "<voice name=""af-ZA-WillemNeural"">" & New String("b"c, 50) & "</voice>")
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=20)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(1, result.Chunks)
            Assert.AreEqual(10.0, result.Chunks(0).EstimatedSeconds, 0.001)
            Assert.Contains("aaaa", result.Chunks(0).Ssml)
            Assert.Contains("bbbb", result.Chunks(0).Ssml)
        End Sub

        <TestMethod>
        Public Sub Voices_split_into_separate_chunks_when_they_do_not_fit_together()
            Dim text As String = VoiceDoc(
                "<voice name=""en-US-JennyNeural"">" & New String("a"c, 50) & "</voice>",
                "<voice name=""af-ZA-WillemNeural"">" & New String("b"c, 50) & "</voice>")
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=7)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(2, result.Chunks)
            Assert.Contains("aaaa", result.Chunks(0).Ssml)
            Assert.DoesNotContain("bbbb", result.Chunks(0).Ssml)
            Assert.Contains("bbbb", result.Chunks(1).Ssml)
            Assert.DoesNotContain("aaaa", result.Chunks(1).Ssml)
        End Sub

        <TestMethod>
        Public Sub Three_voices_pack_two_per_chunk_when_the_third_would_not_fit()
            Dim text As String = VoiceDoc(
                "<voice name=""v1"">" & New String("a"c, 30) & "</voice>",
                "<voice name=""v2"">" & New String("b"c, 30) & "</voice>",
                "<voice name=""v3"">" & New String("c"c, 30) & "</voice>")
            ' each voice ~3s; cap 7 fits two (6s) but not a third (9s)
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=7)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(2, result.Chunks)
            Assert.Contains("aaaa", result.Chunks(0).Ssml)
            Assert.Contains("bbbb", result.Chunks(0).Ssml)
            Assert.Contains("cccc", result.Chunks(1).Ssml)
        End Sub

        ' ---- billable count of composed chunks ----

        <TestMethod>
        Public Sub TotalBillableCharacters_sums_every_chunks_billable_count()
            Dim text As String = VoiceDoc(
                "<voice name=""v1"">" & New String("a"c, 30) & "</voice>",
                "<voice name=""v2"">" & New String("b"c, 30) & "</voice>",
                "<voice name=""v3"">" & New String("c"c, 30) & "</voice>")
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=7) ' two chunks, per the test above
            Assert.IsTrue(result.IsSuccess)

            Dim expected As Integer = 0
            For Each chunk As DocumentChunk In result.Chunks
                expected += BillableCharacterCounter.CountUpperBound(chunk.Ssml)
            Next
            Assert.AreEqual(expected, Chunker.TotalBillableCharacters(result.Chunks))
            Assert.AreEqual(90, Chunker.TotalBillableCharacters(result.Chunks)) ' 3 x 30, no other markup
        End Sub

        <TestMethod>
        Public Sub TotalBillableCharacters_is_zero_for_no_chunks_or_nothing()
            Assert.AreEqual(0, Chunker.TotalBillableCharacters(New List(Of DocumentChunk)()))
            Assert.AreEqual(0, Chunker.TotalBillableCharacters(Nothing))
        End Sub

        ' ---- duration estimate ----

        <TestMethod>
        Public Sub Baseline_duration_estimate_is_characters_over_ten()
            Dim text As String = VoiceDoc("<voice name=""v"">" & New String("x"c, 100) & "</voice>")
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=1000)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(1, result.Chunks)
            Assert.AreEqual(10.0, result.Chunks(0).EstimatedSeconds, 0.001)
        End Sub

        <TestMethod>
        Public Sub A_numeric_prosody_rate_scales_the_estimate()
            Dim voiceInner As String = "<prosody rate=""-50%"">" & New String("x"c, 100) & "</prosody>"
            Dim voiceXml As String = "<voice name=""v"">" & voiceInner & "</voice>"
            Dim text As String = VoiceDoc(voiceXml)
            Dim expectedBillable As Integer = BillableCharacterCounter.CountUpperBound(voiceXml)
            Dim expectedSeconds As Double = expectedBillable / 10.0 * 2.0 ' -50% => 1/(1-0.5) = 2.0

            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=1000)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(1, result.Chunks)
            Assert.AreEqual(expectedSeconds, result.Chunks(0).EstimatedSeconds, 0.001)
        End Sub

        <TestMethod>
        Public Sub A_keyword_rate_is_preserved_verbatim_and_estimated_conservatively()
            ' No numeric percent to parse: the estimator falls back to a fixed conservative multiplier
            ' (matches the phase 0 measurement of "slow", ~x1.55) rather than assuming no slowdown.
            Const UnknownRateMultiplier As Double = 1.55
            Dim voiceInner As String = "<prosody rate=""slow"">" & New String("x"c, 100) & "</prosody>"
            Dim voiceXml As String = "<voice name=""v"">" & voiceInner & "</voice>"
            Dim text As String = VoiceDoc(voiceXml)
            Dim expectedBillable As Integer = BillableCharacterCounter.CountUpperBound(voiceXml)
            Dim expectedSeconds As Double = expectedBillable / 10.0 * UnknownRateMultiplier

            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=1000)
            Assert.IsTrue(result.IsSuccess)
            Assert.AreEqual(expectedSeconds, result.Chunks(0).EstimatedSeconds, 0.001)
            Assert.Contains("rate=""slow""", result.Chunks(0).Ssml)
        End Sub

        ' ---- reconstructed split: inside one oversized voice ----

        <TestMethod>
        Public Sub An_oversized_single_voice_splits_at_sentence_ends()
            Dim s1 As String = New String("A"c, 20) & ". "
            Dim s2 As String = New String("B"c, 20) & ". "
            Dim s3 As String = New String("C"c, 20) & "."
            Dim text As String = VoiceDoc("<voice name=""v"">" & s1 & s2 & s3 & "</voice>")

            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=5)
            Assert.IsTrue(result.IsSuccess)
            Assert.HasCount(2, result.Chunks)
            Assert.Contains("AAAA", result.Chunks(0).Ssml)
            Assert.Contains("BBBB", result.Chunks(0).Ssml)
            Assert.DoesNotContain("CCCC", result.Chunks(0).Ssml)
            Assert.Contains("CCCC", result.Chunks(1).Ssml)
            Assert.IsLessThan(5.0, result.Chunks(0).EstimatedSeconds)
            Assert.IsLessThan(5.0, result.Chunks(1).EstimatedSeconds)
        End Sub

        <TestMethod>
        Public Sub A_keyword_rate_survives_a_reconstructed_split_verbatim()
            Dim s1 As String = New String("A"c, 20) & ". "
            Dim s2 As String = New String("B"c, 20) & ". "
            Dim s3 As String = New String("C"c, 20) & "."
            Dim text As String = VoiceDoc("<voice name=""v""><prosody rate=""slow"">" & s1 & s2 & s3 & "</prosody></voice>")

            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=5)
            Assert.IsTrue(result.IsSuccess)
            Assert.IsGreaterThan(1, result.Chunks.Count)
            For Each chunk As DocumentChunk In result.Chunks
                If chunk.Ssml.Contains("<prosody") Then
                    Assert.Contains("rate=""slow""", chunk.Ssml)
                End If
            Next
        End Sub

        <TestMethod>
        Public Sub An_oversized_voice_with_no_sentence_end_is_reported_not_forced()
            Dim text As String = VoiceDoc("<voice name=""v"">" & New String("x"c, 100) & "</voice>")
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=5)
            Assert.IsFalse(result.IsSuccess)
            Assert.IsNull(result.Chunks)
            Assert.Contains("sentence end", result.FailureReason)
        End Sub

        <TestMethod>
        Public Sub An_inline_sub_element_is_never_split_across_chunks()
            Dim prefix As String = New String("p"c, 30) & ". "
            Dim suffix As String = " " & New String("q"c, 30) & "."
            Dim inner As String = prefix & "<sub alias=""Jop"">Job</sub>" & suffix
            Dim text As String = VoiceDoc("<voice name=""v"">" & inner & "</voice>")

            ' Cap set above the prefix's own estimate but below prefix+sub, so a split is forced
            ' without any single unit (prefix, the <sub>, or suffix) alone exceeding the cap.
            Dim result As ChunkingResult = Chunker.Chunk(text, capSeconds:=3.5)
            Assert.IsTrue(result.IsSuccess)
            Assert.IsGreaterThan(1, result.Chunks.Count)

            Dim totalJobCount As Integer = 0
            For Each chunk As DocumentChunk In result.Chunks
                Dim opens As Integer = chunk.Ssml.Split(New String() {"<sub "}, StringSplitOptions.None).Length - 1
                Dim closes As Integer = chunk.Ssml.Split(New String() {"</sub>"}, StringSplitOptions.None).Length - 1
                Assert.AreEqual(opens, closes, "a <sub> was split across chunks: " & chunk.Ssml)
                totalJobCount += chunk.Ssml.Split(New String() {"Job"}, StringSplitOptions.None).Length - 1
            Next
            Assert.AreEqual(1, totalJobCount)
        End Sub

    End Class

End Namespace