Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Voices

Namespace Documents

    <TestClass>
    Public Class VoiceBlockAnalyzerTests

        Private Shared Function Voice(shortName As String, locale As String) As VoiceInfo
            Return New VoiceInfo With {.ShortName = shortName, .Locale = locale, .DisplayName = shortName, .Gender = "Female"}
        End Function

        Private Shared Function Catalog() As VoiceCatalog
            Return New VoiceCatalog({Voice("af-ZA-WillemNeural", "af-ZA"), Voice("he-IL-HilaNeural", "he-IL")})
        End Function

        <TestMethod>
        Public Sub SingleVoiceBlock_ResolvesLocaleFromCatalog_AndText()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis""><voice name=""af-ZA-WillemNeural"">Hello there</voice></speak>"
            Dim blocks As IReadOnlyList(Of VoiceBlockInfo) = VoiceBlockAnalyzer.Analyze(ssml, Catalog())

            Assert.HasCount(1, blocks)
            Assert.AreEqual("af-ZA-WillemNeural", blocks(0).ShortName)
            Assert.AreEqual("af-ZA", blocks(0).Locale)
            Assert.IsTrue(blocks(0).ExistsInCatalog)
            Assert.AreEqual("Hello there", blocks(0).Text)
        End Sub

        <TestMethod>
        Public Sub VoiceNotInCatalog_FallsBackToShortNameLocaleGuess_ExistsInCatalogIsFalse()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis""><voice name=""en-US-SomeNewVoice"">Hi</voice></speak>"
            Dim blocks As IReadOnlyList(Of VoiceBlockInfo) = VoiceBlockAnalyzer.Analyze(ssml, Catalog())

            Assert.HasCount(1, blocks)
            Assert.IsFalse(blocks(0).ExistsInCatalog)
            Assert.AreEqual("en-US", blocks(0).Locale)
        End Sub

        <TestMethod>
        Public Sub VoiceWithUnguessableShortName_HasNullLocale_DoesNotThrow()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis""><voice name=""NotAProperShortName"">Hi</voice></speak>"
            Dim blocks As IReadOnlyList(Of VoiceBlockInfo) = VoiceBlockAnalyzer.Analyze(ssml, Catalog())

            Assert.HasCount(1, blocks)
            Assert.IsNull(blocks(0).Locale)
        End Sub

        <TestMethod>
        Public Sub MultipleVoiceBlocks_AllReturned_InDocumentOrder()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis"">" &
                "<voice name=""af-ZA-WillemNeural"">First</voice><voice name=""he-IL-HilaNeural"">Second</voice>" &
                "</speak>"
            Dim blocks As IReadOnlyList(Of VoiceBlockInfo) = VoiceBlockAnalyzer.Analyze(ssml, Catalog())

            Assert.HasCount(2, blocks)
            Assert.AreEqual("First", blocks(0).Text)
            Assert.AreEqual("Second", blocks(1).Text)
        End Sub

        <TestMethod>
        Public Sub NoVoiceElements_ReturnsEmptyList()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis"">Hello</speak>"
            Assert.HasCount(0, VoiceBlockAnalyzer.Analyze(ssml, Catalog()))
        End Sub

        <TestMethod>
        Public Sub NullOrEmptySsml_ReturnsEmptyList_DoesNotThrow()
            Assert.HasCount(0, VoiceBlockAnalyzer.Analyze(Nothing, Catalog()))
            Assert.HasCount(0, VoiceBlockAnalyzer.Analyze(String.Empty, Catalog()))
        End Sub

        <TestMethod>
        Public Sub MalformedSsml_ReturnsEmptyList_DoesNotThrow()
            Assert.HasCount(0, VoiceBlockAnalyzer.Analyze("<speak><voice name=""x"">unterminated", Catalog()))
        End Sub

        <TestMethod>
        Public Sub NullCatalog_StillFallsBackToGuess_DoesNotThrow()
            Dim ssml As String = "<speak xmlns=""http://www.w3.org/2001/10/synthesis""><voice name=""af-ZA-WillemNeural"">Hi</voice></speak>"
            Dim blocks As IReadOnlyList(Of VoiceBlockInfo) = VoiceBlockAnalyzer.Analyze(ssml, Nothing)

            Assert.HasCount(1, blocks)
            Assert.IsFalse(blocks(0).ExistsInCatalog)
            Assert.AreEqual("af-ZA", blocks(0).Locale)
        End Sub

    End Class

End Namespace
