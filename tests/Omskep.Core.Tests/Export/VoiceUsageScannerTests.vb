Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Export

Namespace Export

    <TestClass>
    Public Class VoiceUsageScannerTests

        Private Shared Function Chunk(ssml As String) As DocumentChunk
            Return New DocumentChunk(ssml, 1.0)
        End Function

        <TestMethod>
        Public Sub Distinct_voices_are_returned_in_first_appearance_order_not_sorted()
            Dim chunks = New List(Of DocumentChunk) From {
                Chunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='z-Voice'>a</voice><voice name='a-Voice'>b</voice></speak>")
            }
            Dim result = VoiceUsageScanner.FindDistinctVoiceNames(chunks)
            Assert.HasCount(2, result)
            Assert.AreEqual("z-Voice", result(0))
            Assert.AreEqual("a-Voice", result(1))
        End Sub

        <TestMethod>
        Public Sub A_voice_repeated_across_chunks_is_only_listed_once()
            Dim chunks = New List(Of DocumentChunk) From {
                Chunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='af-ZA-WillemNeural'>a</voice></speak>"),
                Chunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='af-ZA-WillemNeural'>b</voice></speak>")
            }
            Assert.HasCount(1, VoiceUsageScanner.FindDistinctVoiceNames(chunks))
        End Sub

        <TestMethod>
        Public Sub No_chunks_yields_no_voices()
            Assert.IsEmpty(VoiceUsageScanner.FindDistinctVoiceNames(New List(Of DocumentChunk)))
        End Sub

    End Class

End Namespace
