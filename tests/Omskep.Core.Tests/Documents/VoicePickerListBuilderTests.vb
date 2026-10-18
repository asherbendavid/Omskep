Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents
Imports Omskep.Core.Voices

Namespace Documents

    <TestClass>
    Public Class VoicePickerListBuilderTests

        Private Shared Function Voice(shortName As String, locale As String, Optional gender As String = "Female") As VoiceInfo
            Return New VoiceInfo With {.ShortName = shortName, .Locale = locale, .DisplayName = shortName, .Gender = gender}
        End Function

        Private Shared Function CatalogWithAfrikaansEnglishHebrew() As VoiceCatalog
            Return New VoiceCatalog({
                Voice("af-ZA-WillemNeural", "af-ZA", "Male"),
                Voice("af-ZA-AdriNeural", "af-ZA", "Female"),
                Voice("en-US-JennyNeural", "en-US", "Female"),
                Voice("en-US-GuyNeural", "en-US", "Male"),
                Voice("en-GB-RyanNeural", "en-GB", "Male"),
                Voice("he-IL-HilaNeural", "he-IL", "Female")
            })
        End Function

        Private Shared Function Doc(defaultLocale As String, body As String) As String
            Return "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                   "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"" xml:lang=""" & defaultLocale & """>" &
                   body & "</speak>"
        End Function

        <TestMethod>
        Public Sub NullOrEmptyCatalogOrDocument_ReturnsEmptyList_DoesNotThrow()
            Assert.HasCount(0, VoicePickerListBuilder.Build("<speak></speak>", Nothing))
            Assert.HasCount(0, VoicePickerListBuilder.Build("<speak></speak>", VoiceCatalog.Empty))
            Assert.HasCount(0, VoicePickerListBuilder.Build(Nothing, CatalogWithAfrikaansEnglishHebrew()))
            Assert.HasCount(0, VoicePickerListBuilder.Build(String.Empty, CatalogWithAfrikaansEnglishHebrew()))
        End Sub

        <TestMethod>
        Public Sub DefaultLocaleFirst_ThenRestAlphabetical_WhenNoVoicesUsedYet()
            ' A brand-new document: default locale af-ZA, no <voice> elements at all yet.
            Dim text As String = Doc("af-ZA", "")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.HasCount(4, groups) ' af-ZA, en-GB, en-US, he-IL
            Assert.AreEqual("af-ZA", groups(0).Locale)
            Assert.AreEqual("en-GB", groups(1).Locale)
            Assert.AreEqual("en-US", groups(2).Locale)
            Assert.AreEqual("he-IL", groups(3).Locale)
        End Sub

        <TestMethod>
        Public Sub UsedLocale_ComesSecond_BeforeTheRestOfTheCatalog()
            Dim text As String = Doc("af-ZA", "<voice name=""he-IL-HilaNeural"">Hebrew bit</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.HasCount(4, groups)
            Assert.AreEqual("af-ZA", groups(0).Locale) ' document default, always first
            Assert.AreEqual("he-IL", groups(1).Locale) ' used elsewhere, comes before the untouched rest
            Assert.AreEqual("en-GB", groups(2).Locale)
            Assert.AreEqual("en-US", groups(3).Locale)
        End Sub

        <TestMethod>
        Public Sub SeveralUsedLocales_AreAlphabeticalAmongThemselves()
            Dim text As String = Doc("af-ZA",
                "<voice name=""he-IL-HilaNeural"">x</voice><voice name=""en-US-JennyNeural"">y</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.AreEqual("af-ZA", groups(0).Locale)
            Assert.AreEqual("en-US", groups(1).Locale) ' en-US before he-IL, even though he-IL appeared first in the document
            Assert.AreEqual("he-IL", groups(2).Locale)
            Assert.AreEqual("en-GB", groups(3).Locale)
        End Sub

        <TestMethod>
        Public Sub DefaultLocaleAlsoUsedElsewhere_AppearsOnlyOnce()
            Dim text As String = Doc("af-ZA", "<voice name=""af-ZA-WillemNeural"">x</voice><voice name=""en-US-JennyNeural"">y</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.HasCount(4, groups)
            Assert.AreEqual("af-ZA", groups(0).Locale)
            Assert.AreEqual("en-US", groups(1).Locale)
        End Sub

        <TestMethod>
        Public Sub VoicesWithinALocale_MatchTheCatalogsOwnOrder()
            ' af-ZA has AdriNeural and WillemNeural -- VoiceCatalog already sorts by short name, and this
            ' function must not re-sort (or otherwise disturb) that order.
            Dim catalog As VoiceCatalog = CatalogWithAfrikaansEnglishHebrew()
            Dim text As String = Doc("af-ZA", "")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, catalog)

            CollectionAssert.AreEqual(catalog.VoicesFor("af-ZA").ToList(), groups(0).Voices.ToList())
        End Sub

        <TestMethod>
        Public Sub NoXmlLangOnSpeak_SkipsTheDefaultLocaleGroup_RestStillAlphabetical()
            Dim text As String = "<?omskep-meta" & vbLf & "Title:" & vbLf & "?>" & vbLf &
                "<speak version=""1.0"" xmlns=""http://www.w3.org/2001/10/synthesis"">" &
                "<voice name=""en-US-JennyNeural"">x</voice></speak>"
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.HasCount(4, groups)
            Assert.AreEqual("en-US", groups(0).Locale) ' used, so it's still promoted, just not via the "default" slot
        End Sub

        <TestMethod>
        Public Sub VoiceNameNotInCatalog_FallsBackToShortNameLocaleGuess()
            ' A name not in the (possibly stale/offline) catalog, but whose shape still implies a locale
            ' the catalog *does* have -- "ll-CC-..." -- should still promote that locale.
            Dim text As String = Doc("af-ZA", "<voice name=""en-US-SomeNewVoiceNotYetInCache"">x</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.AreEqual("af-ZA", groups(0).Locale)
            Assert.AreEqual("en-US", groups(1).Locale)
        End Sub

        <TestMethod>
        Public Sub VoiceNameNotInCatalogAndLocaleGuessAlsoNotInCatalog_IsSkippedEntirely_NoThrow()
            Dim text As String = Doc("af-ZA", "<voice name=""xx-YY-SomeUnknownVoice"">x</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.HasCount(4, groups) ' no fifth, invented "xx-YY" group
            Assert.AreEqual("af-ZA", groups(0).Locale)
        End Sub

        <TestMethod>
        Public Sub DefaultLocaleNotInCatalog_IsSkipped_NoCrash()
            Dim text As String = Doc("xx-YY", "<voice name=""en-US-JennyNeural"">x</voice>")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.AreEqual("en-US", groups(0).Locale) ' falls through to the "used" group, no empty xx-YY group
        End Sub

        <TestMethod>
        Public Sub XmlLangCasingDiffersFromCatalog_StillMatchesCaseInsensitively()
            Dim text As String = Doc("AF-za", "")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.AreEqual("af-ZA", groups(0).Locale) ' the catalog's own casing, not the document's
        End Sub

        <TestMethod>
        Public Sub VoiceInsideAComment_IsNotCountedAsUsed()
            Dim text As String = Doc("af-ZA", "<!-- <voice name=""he-IL-HilaNeural"">fake</voice> -->")
            Dim groups As IReadOnlyList(Of VoiceLocaleGroup) = VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())

            Assert.AreEqual("af-ZA", groups(0).Locale)
            Assert.AreEqual("en-GB", groups(1).Locale) ' he-IL was NOT promoted -- straight to the alphabetical rest
        End Sub

        <TestMethod>
        Public Sub MalformedDocument_DoesNotThrow_DegradesGracefully()
            Dim text As String = "<speak><voice name=""en-US-JennyNeural"">unterminated"
            ' Should not throw; exact grouping isn't the point here, just robustness.
            VoicePickerListBuilder.Build(text, CatalogWithAfrikaansEnglishHebrew())
        End Sub

    End Class

End Namespace
