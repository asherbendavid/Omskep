Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Voices

Namespace Voices

    <TestClass>
    Public Class VoiceCatalogTests

        Private Shared Function V(shortName As String, locale As String) As VoiceInfo
            Return New VoiceInfo() With {.ShortName = shortName, .Locale = locale, .DisplayName = shortName}
        End Function

        <TestMethod>
        Public Sub Empty_HasNothing_AndLookupsAreSafe()
            Dim c = VoiceCatalog.Empty
            Assert.AreEqual(0, c.Count)
            Assert.IsEmpty(c.Locales)
            Assert.IsEmpty(c.VoicesFor("af-ZA"))
            Assert.IsEmpty(c.VoicesFor(Nothing))
            Assert.IsNull(c.TryFind("anything"))
            Assert.IsFalse(c.Contains("anything"))
        End Sub

        <TestMethod>
        Public Sub GroupsByLocale_SortedLocalesAndSortedVoices()
            Dim c As New VoiceCatalog({
                V("he-IL-HilaNeural", "he-IL"),
                V("af-ZA-WillemNeural", "af-ZA"),
                V("en-ZA-LukeNeural", "en-ZA"),
                V("af-ZA-AdriNeural", "af-ZA")})

            Assert.AreEqual(4, c.Count)
            Assert.HasCount(3, c.Locales)
            Assert.AreEqual("af-ZA", c.Locales(0))
            Assert.AreEqual("en-ZA", c.Locales(1))
            Assert.AreEqual("he-IL", c.Locales(2))

            Dim af = c.VoicesFor("af-ZA")
            Assert.HasCount(2, af)
            Assert.AreEqual("af-ZA-AdriNeural", af(0).ShortName)
            Assert.AreEqual("af-ZA-WillemNeural", af(1).ShortName)
        End Sub

        <TestMethod>
        Public Sub Lookups_IgnoreCase_AndTrim()
            Dim c As New VoiceCatalog({V("af-ZA-WillemNeural", "af-ZA")})
            Assert.HasCount(1, c.VoicesFor("AF-za"))
            Assert.HasCount(1, c.VoicesFor("  af-ZA "))
            Assert.IsNotNull(c.TryFind("AF-ZA-WILLEMNEURAL"))
            Assert.IsTrue(c.Contains(" af-ZA-WillemNeural "))
        End Sub

        <TestMethod>
        Public Sub UnknownLocaleOrVoice_GivesEmptyOrNothing()
            Dim c As New VoiceCatalog({V("af-ZA-WillemNeural", "af-ZA")})
            Assert.IsEmpty(c.VoicesFor("xx-XX"))
            Assert.IsNull(c.TryFind("nope"))
            Assert.IsFalse(c.Contains(""))
        End Sub

        <TestMethod>
        Public Sub JunkEntries_AreSkipped_AndDuplicatesKeepTheFirst()
            Dim first = V("af-ZA-WillemNeural", "af-ZA")
            Dim c As New VoiceCatalog({Nothing, V("", "af-ZA"), V("x", ""), V(Nothing, "af-ZA"), first, V("AF-ZA-willemneural", "af-ZA")})
            Assert.AreEqual(1, c.Count)
            Assert.AreSame(first, c.TryFind("af-ZA-WillemNeural"))
        End Sub

        <TestMethod>
        Public Sub ChangingTheSourceList_AfterConstruction_DoesNotChangeTheCatalog()
            Dim source As New List(Of VoiceInfo) From {V("a-A-ANeural", "a-A")}
            Dim c As New VoiceCatalog(source)
            source.Add(V("b-B-BNeural", "b-B"))
            source.Clear()
            Assert.AreEqual(1, c.Count)
            Assert.HasCount(1, c.Locales)
        End Sub

        <TestMethod>
        Public Sub NullList_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing))
        End Sub

        Private Shared Sub Build(voices As IEnumerable(Of VoiceInfo))
            Dim unused = New VoiceCatalog(voices)
        End Sub

        <TestMethod>
        Public Sub RealisticSize_556Voices_153Locales_GroupsQuickly()
            Dim list As New List(Of VoiceInfo)()
            For i = 0 To 555
                Dim loc = "l" & (i Mod 153).ToString("D3") & "-X"
                list.Add(V(loc & "-Voice" & i.ToString("D3") & "Neural", loc))
            Next
            Dim c As New VoiceCatalog(list)
            Assert.AreEqual(556, c.Count)
            Assert.HasCount(153, c.Locales)
        End Sub

    End Class

End Namespace
