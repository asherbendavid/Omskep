Imports System.Text
Imports System.Text.Json
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Speech

Namespace Speech

    <TestClass>
    Public Class VoiceListParserTests

        Private Shared Function Bytes(json As String) As Byte()
            Return Encoding.UTF8.GetBytes(json)
        End Function

        <TestMethod>
        Public Sub Parse_MapsTheFieldsWeKeep_AndIgnoresTheRest()
            Dim json = "[{""Name"":""Microsoft Server Speech Text to Speech Voice (af-ZA, WillemNeural)"",""DisplayName"":""Willem"",""LocalName"":""Willem"",""ShortName"":""af-ZA-WillemNeural"",""Gender"":""Male"",""Locale"":""af-ZA"",""LocaleName"":""Afrikaans (South Africa)"",""SampleRateHertz"":""48000"",""VoiceType"":""Neural"",""Status"":""GA"",""StyleList"":[""cheerful""],""WordsPerMinute"":""157""}]"
            Dim v = VoiceListParser.Parse(Bytes(json))
            Assert.HasCount(1, v)
            Assert.AreEqual("af-ZA-WillemNeural", v(0).ShortName)
            Assert.AreEqual("af-ZA", v(0).Locale)
            Assert.AreEqual("Willem", v(0).DisplayName)
            Assert.AreEqual("Male", v(0).Gender)
            Assert.AreEqual("Neural", v(0).VoiceType)
        End Sub

        <TestMethod>
        Public Sub Parse_PropertyNamesAreCaseInsensitive()
            Dim v = VoiceListParser.Parse(Bytes("[{""shortname"":""xx-XX-ANeural"",""LOCALE"":""xx-XX""}]"))
            Assert.HasCount(1, v)
            Assert.AreEqual("xx-XX-ANeural", v(0).ShortName)
        End Sub

        <TestMethod>
        Public Sub Parse_SortsByLocaleThenName()
            Dim json = "[{""ShortName"":""he-IL-HilaNeural"",""Locale"":""he-IL""},{""ShortName"":""af-ZA-WillemNeural"",""Locale"":""af-ZA""},{""ShortName"":""af-ZA-AdriNeural"",""Locale"":""af-ZA""}]"
            Dim v = VoiceListParser.Parse(Bytes(json))
            Assert.AreEqual("af-ZA-AdriNeural", v(0).ShortName)
            Assert.AreEqual("af-ZA-WillemNeural", v(1).ShortName)
            Assert.AreEqual("he-IL-HilaNeural", v(2).ShortName)
        End Sub

        <TestMethod>
        Public Sub Parse_SkipsJunkEntries_AndCollapsesDuplicates()
            Dim json = "[null,{""ShortName"":"""",""Locale"":""xx-XX""},{""ShortName"":""a"",""Locale"":""""},{""Locale"":""xx-XX""},{""ShortName"":""xx-XX-ANeural"",""Locale"":""xx-XX""},{""ShortName"":""XX-XX-aneural"",""Locale"":""xx-XX""}]"
            Dim v = VoiceListParser.Parse(Bytes(json))
            Assert.HasCount(1, v)
            Assert.AreEqual("xx-XX-ANeural", v(0).ShortName)
        End Sub

        <TestMethod>
        Public Sub Parse_MissingDisplayName_FallsBackToShortName()
            Dim v = VoiceListParser.Parse(Bytes("[{""ShortName"":""xx-XX-ANeural"",""Locale"":""xx-XX""}]"))
            Assert.AreEqual("xx-XX-ANeural", v(0).DisplayName)
        End Sub

        <TestMethod>
        Public Sub Parse_EmptyArray_IsEmptyNotAnError()
            Assert.IsEmpty(VoiceListParser.Parse(Bytes("[]")))
        End Sub

        <TestMethod>
        Public Sub Parse_NotAnArray_Throws()
            For Each bad In {"", "null", "{}", "{""voices"":[]}", "not json", "[1,2,3]", "[{""ShortName"":5}]", "[{"}
                Dim threw As Boolean = False
                Try
                    VoiceListParser.Parse(Bytes(bad))
                Catch ex As JsonException
                    threw = True
                End Try
                Assert.IsTrue(threw, "should throw for: " & bad)
            Next
        End Sub

        <TestMethod>
        Public Sub Parse_NullBytes_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() VoiceListParser.Parse(Nothing))
        End Sub

    End Class

End Namespace
