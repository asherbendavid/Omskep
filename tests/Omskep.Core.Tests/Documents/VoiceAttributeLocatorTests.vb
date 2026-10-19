Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class VoiceAttributeLocatorTests

        <TestMethod>
        Public Sub CaretInMiddleOfValue_IsLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Dim valueStart As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Dim span As VoiceAttributeSpan = VoiceAttributeLocator.Locate(text, valueStart + 3)

            Assert.IsNotNull(span)
            Assert.AreEqual(valueStart, span.ValueStart)
            Assert.AreEqual("en-US-JennyNeural".Length, span.ValueLength)
        End Sub

        <TestMethod>
        Public Sub CaretAtStartOfValue_IsLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Dim valueStart As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Assert.IsNotNull(VoiceAttributeLocator.Locate(text, valueStart))
        End Sub

        <TestMethod>
        Public Sub CaretAtEndOfValue_IsLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Dim valueStart As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Assert.IsNotNull(VoiceAttributeLocator.Locate(text, valueStart + "en-US-JennyNeural".Length))
        End Sub

        <TestMethod>
        Public Sub CaretInEmptyValue_IsLocated()
            Dim text As String = "<speak><voice name="""">hi</voice></speak>"
            Dim valueStart As Integer = text.IndexOf("name=""", StringComparison.Ordinal) + "name=""".Length
            Assert.IsNotNull(VoiceAttributeLocator.Locate(text, valueStart))
        End Sub

        <TestMethod>
        Public Sub CaretJustBeforeOpeningQuote_IsNotLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Dim equalsAt As Integer = text.IndexOf("name=", StringComparison.Ordinal)
            Dim quoteAt As Integer = equalsAt + "name=".Length
            Assert.IsNull(VoiceAttributeLocator.Locate(text, quoteAt)) ' on the quote itself, not inside the value
        End Sub

        <TestMethod>
        Public Sub CaretInVoiceBodyText_NotInAttribute_IsNotLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Dim bodyAt As Integer = text.IndexOf("hi", StringComparison.Ordinal)
            Assert.IsNull(VoiceAttributeLocator.Locate(text, bodyAt))
        End Sub

        <TestMethod>
        Public Sub CaretInSomeOtherAttribute_IsNotLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"" xml:lang=""en-US"">hi</voice></speak>"
            Dim langAt As Integer = text.IndexOf("en-US"">hi", StringComparison.Ordinal) ' inside the xml:lang value
            Assert.IsNull(VoiceAttributeLocator.Locate(text, langAt))
        End Sub

        <TestMethod>
        Public Sub CaretInSelfClosingVoiceNameValue_IsLocated()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural""/></speak>"
            Dim valueStart As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Assert.IsNotNull(VoiceAttributeLocator.Locate(text, valueStart + 2))
        End Sub

        <TestMethod>
        Public Sub CaretInSurnameLikeAttribute_DoesNotFalsePositiveOnNameSubstring()
            ' A hypothetical attribute whose name merely ends in "name" must not be mistaken for the
            ' voice's own name attribute.
            Dim text As String = "<speak><voice surname=""en-US-JennyNeural"" name=""real"">hi</voice></speak>"
            Dim fakeValueAt As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Dim span As VoiceAttributeSpan = VoiceAttributeLocator.Locate(text, fakeValueAt)
            ' Either not located at all, or located as the real "name" attribute elsewhere -- either way,
            ' it must not report the surname value's position as a match.
            If span IsNot Nothing Then Assert.AreNotEqual(fakeValueAt, span.ValueStart)
        End Sub

        <TestMethod>
        Public Sub NotInsideAnyVoiceElement_IsNotLocated()
            Dim text As String = "<speak>plain text, no voice here</speak>"
            Assert.IsNull(VoiceAttributeLocator.Locate(text, 10))
        End Sub

        <TestMethod>
        Public Sub NullOrEmptyDocument_ReturnsNull_DoesNotThrow()
            Assert.IsNull(VoiceAttributeLocator.Locate(Nothing, 0))
            Assert.IsNull(VoiceAttributeLocator.Locate(String.Empty, 0))
        End Sub

        <TestMethod>
        Public Sub OutOfRangePosition_ReturnsNull_DoesNotThrow()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">hi</voice></speak>"
            Assert.IsNull(VoiceAttributeLocator.Locate(text, -1))
            Assert.IsNull(VoiceAttributeLocator.Locate(text, text.Length + 50))
        End Sub

        <TestMethod>
        Public Sub UnterminatedVoiceTag_DoesNotThrow()
            Dim text As String = "<speak><voice name=""en-US-Jenny"
            VoiceAttributeLocator.Locate(text, text.Length - 1)
        End Sub

        <TestMethod>
        Public Sub TextLookingLikeVoiceInsideComment_IsNotLocated()
            Dim text As String = "<speak><!-- <voice name=""en-US-JennyNeural""> --></speak>"
            Dim fakeValueAt As Integer = text.IndexOf("en-US", StringComparison.Ordinal)
            Assert.IsNull(VoiceAttributeLocator.Locate(text, fakeValueAt))
        End Sub

    End Class

End Namespace
