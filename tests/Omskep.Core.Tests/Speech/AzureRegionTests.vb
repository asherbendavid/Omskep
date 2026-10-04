Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Speech

Namespace Speech

    <TestClass>
    Public Class AzureRegionTests

        <TestMethod>
        Public Sub Normalize_AcceptsPlainRegionIds_TrimmedAndLowercased()
            Assert.AreEqual("southafricanorth", AzureRegion.Normalize("southafricanorth"))
            Assert.AreEqual("southafricanorth", AzureRegion.Normalize("  SouthAfricaNorth "))
            Assert.AreEqual("eastus2", AzureRegion.Normalize("eastus2"))
        End Sub

        <TestMethod>
        Public Sub Normalize_RejectsAnythingThatCouldRedirectTheKey()
            For Each bad In {Nothing, "", "   ", "evil.com/", "a.b", "x#", "x?y=1", "a/b", "a b", "a@b", "a:443", "a-b", "a_b", "ünï", "a" & vbLf & "b", New String("a"c, 41)}
                Assert.IsNull(AzureRegion.Normalize(bad), "should reject: " & If(bad, "(Nothing)"))
                Assert.IsFalse(AzureRegion.IsValid(bad))
            Next
        End Sub

        <TestMethod>
        Public Sub Normalize_AcceptsMaximumLength()
            Assert.AreEqual(40, AzureRegion.Normalize(New String("a"c, 40)).Length)
        End Sub

        <TestMethod>
        Public Sub CommonRegions_AreAllValid_Unique_AndIncludeTheDefault()
            Assert.IsGreaterThan(10, AzureRegion.CommonRegions.Count)
            Dim seen As New HashSet(Of String)()
            For Each r In AzureRegion.CommonRegions
                Assert.AreEqual(r, AzureRegion.Normalize(r), r)
                Assert.IsTrue(seen.Add(r), "duplicate: " & r)
            Next
            Assert.Contains(Omskep.Core.Settings.AzureSettings.DefaultRegion, seen)
            Assert.Contains("southafricanorth", seen)

        End Sub

    End Class

End Namespace
