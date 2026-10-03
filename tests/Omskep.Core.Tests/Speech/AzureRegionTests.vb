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

    End Class

End Namespace
