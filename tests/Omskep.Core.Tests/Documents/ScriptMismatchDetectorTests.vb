Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class ScriptMismatchDetectorTests

        Private Shared Function Block(shortName As String, locale As String, text As String) As VoiceBlockInfo
            Return New VoiceBlockInfo(shortName, locale, True, text)
        End Function

        <TestMethod>
        Public Sub MatchingLatinVoiceAndText_NoWarning()
            Dim blocks = {Block("en-US-JennyNeural", "en-US", "Hello there, how are you?")}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub AfrikaansAndEnglish_BothLatin_NeitherWarns()
            ' Not a special-cased exclusion -- both simply map to Latin in the table, same as any
            ' other Latin-script locale would.
            Dim blocks = {Block("af-ZA-WillemNeural", "af-ZA", "Goeie more almal"), Block("en-US-JennyNeural", "en-US", "Good morning everyone")}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub HebrewTextUnderLatinVoice_Warns()
            Dim blocks = {Block("en-US-JennyNeural", "en-US", "שלום עולם")}
            Dim warnings = ScriptMismatchDetector.Detect(blocks)

            Assert.HasCount(1, warnings)
            Assert.AreEqual(Script.Latin, warnings(0).ExpectedScript)
            Assert.AreEqual(Script.Hebrew, warnings(0).ActualScript)
        End Sub

        <TestMethod>
        Public Sub LatinTextUnderHebrewVoice_Warns()
            Dim blocks = {Block("he-IL-HilaNeural", "he-IL", "Hello there, this is English text")}
            Dim warnings = ScriptMismatchDetector.Detect(blocks)

            Assert.HasCount(1, warnings)
            Assert.AreEqual(Script.Hebrew, warnings(0).ExpectedScript)
            Assert.AreEqual(Script.Latin, warnings(0).ActualScript)
        End Sub

        <TestMethod>
        Public Sub HebrewWithVowelPoints_StillClassifiesAsHebrew_NoFalseWarning()
            ' Vowel points are combining marks, not letters -- they must not dilute or break the
            ' dominant-script count for the real Hebrew letters around them.
            Dim textWithNikud As String = "שָׁלוֹם עוֹלָם" ' Hebrew with niqqud
            Dim blocks = {Block("he-IL-HilaNeural", "he-IL", textWithNikud)}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub GreekPolytonicText_ClassifiesAsGreek()
            ' A locale with no table entry isn't checked at all, so wire a Greek expectation directly
            ' against DominantScript to confirm polytonic characters (Greek Extended block) are
            ' recognised, independent of whether a Greek locale is in the current table.
            Dim polytonic As String = "ἄνθρωπος" ' includes a polytonic diacritic (Greek Extended block)
            Assert.AreEqual(Script.Greek, ScriptMismatchDetector.DominantScript(polytonic))
        End Sub

        <TestMethod>
        Public Sub PunctuationAndDigitsOnlyBlock_NoClassifiableLetters_NoWarning()
            Dim blocks = {Block("he-IL-HilaNeural", "he-IL", "123 -- 456, 789!")}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub EmptyBlockText_NoWarning_DoesNotThrow()
            Dim blocks = {Block("he-IL-HilaNeural", "he-IL", String.Empty)}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub LocaleNotInTable_IsNeverChecked_RegardlessOfText()
            Dim blocks = {Block("xx-YY-SomeVoice", "xx-YY", "שלום עולם")}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub NullLocale_IsNeverChecked()
            Dim blocks = {Block("unknown-voice", Nothing, "שלום עולם")}
            Assert.HasCount(0, ScriptMismatchDetector.Detect(blocks))
        End Sub

        <TestMethod>
        Public Sub WarningExcerpt_IsTruncatedAndTrimmed_ForALongBlock()
            Dim longHebrew As String = New String("א"c, 200)
            Dim blocks = {Block("en-US-JennyNeural", "en-US", "  " & longHebrew & "  ")}
            Dim warnings = ScriptMismatchDetector.Detect(blocks)

            Assert.HasCount(1, warnings)
            Assert.IsLessThanOrEqualTo(61, warnings(0).Excerpt.Length) ' 60 chars + the ellipsis character
            Assert.DoesNotStartWith(" ", warnings(0).Excerpt)
        End Sub

        <TestMethod>
        Public Sub NullBlockList_ReturnsEmpty_DoesNotThrow()
            Assert.HasCount(0, ScriptMismatchDetector.Detect(Nothing))
        End Sub

        <TestMethod>
        Public Sub MixedScriptBlock_SingleForeignWordAmongMostlyLatin_StillWarns()
            ' The realistic mistake this feature exists to catch: a single pasted foreign-script word
            ' (a name, a quote) inside an otherwise-correct block. It must warn even though Latin is
            ' overwhelmingly the block's overall "dominant" script -- presence, not majority.
            Dim blocks = {Block("en-US-JennyNeural", "en-US", "This is mostly English with א single stray letter")}
            Dim warnings = ScriptMismatchDetector.Detect(blocks)

            Assert.HasCount(1, warnings)
            Assert.AreEqual(Script.Hebrew, warnings(0).ActualScript)
        End Sub

    End Class

End Namespace
