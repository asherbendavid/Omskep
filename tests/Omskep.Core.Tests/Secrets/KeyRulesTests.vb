Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Secrets

Namespace Secrets

    <TestClass>
    Public Class KeyRulesTests

        <TestMethod>
        Public Sub Problem_IsNothing_ForNormalKeys_AndPastedWhitespaceAtTheEnds()
            Assert.IsNull(KeyRules.Problem("0123456789abcdef0123456789ABCDEF"))
            Assert.IsNull(KeyRules.Problem("  abc123" & vbCrLf))
        End Sub

        <TestMethod>
        Public Sub Problem_ExplainsBlankAndSpacedKeys_WithoutEchoingThem()
            For Each bad In {Nothing, "", "   ", vbCrLf}
                Assert.AreEqual("A key is required.", KeyRules.Problem(bad))
            Next
            For Each bad In {"abc def", "abc" & vbLf & "def", "abc" & vbTab & "def", "abc" & ChrW(7) & "def"}
                Dim message = KeyRules.Problem(bad)
                Assert.AreEqual("A key cannot contain spaces or line breaks.", message)
            Next
        End Sub

    End Class

End Namespace
