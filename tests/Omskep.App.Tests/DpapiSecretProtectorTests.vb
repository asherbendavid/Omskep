Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.App.Secrets

Namespace Secrets

    ''' <summary>
    ''' Runs against the real Windows DPAPI, so it needs a Windows test project that references Omskep.App.
    ''' Everything else about key storage is covered by the platform-neutral FileSecretStore tests.
    ''' </summary>
    <TestClass>
    Public Class DpapiSecretProtectorTests

        Private Shared ReadOnly Plain As Byte() = Encoding.UTF8.GetBytes("0123456789abcdef0123456789ABCDEF")

        Private Shared Sub RequireWindows()
            If Not OperatingSystem.IsWindows() Then Assert.Inconclusive("DPAPI is Windows-only.")
        End Sub

        <TestMethod>
        Public Sub RoundTrip_ReturnsTheOriginalBytes()
            RequireWindows()
            Dim p As New DpapiSecretProtector()
            Dim blob = p.Protect(Plain)
            Assert.IsTrue(p.Unprotect(blob).SequenceEqual(Plain))
        End Sub

        <TestMethod>
        Public Sub ProtectedBlob_DoesNotContainThePlaintext()
            RequireWindows()
            Dim blob = New DpapiSecretProtector().Protect(Plain)
            Dim blobText = Encoding.Latin1.GetString(blob)
            Assert.IsFalse(blobText.Contains(Encoding.Latin1.GetString(Plain), StringComparison.Ordinal))
        End Sub

        <TestMethod>
        Public Sub CorruptedBlob_ThrowsCryptographicException()
            RequireWindows()
            Dim p As New DpapiSecretProtector()
            Dim blob = p.Protect(Plain)
            For i = blob.Length - 12 To blob.Length - 1
                blob(i) = CByte(blob(i) Xor &HFF)
            Next
            Assert.Throws(Of CryptographicException)(Sub() p.Unprotect(blob))
        End Sub

        <TestMethod>
        Public Sub TruncatedBlob_ThrowsCryptographicException()
            RequireWindows()
            Dim p As New DpapiSecretProtector()
            Dim blob = p.Protect(Plain)
            Dim truncated = blob.Take(blob.Length \ 2).ToArray()
            Assert.Throws(Of CryptographicException)(Sub() p.Unprotect(truncated))
        End Sub

        <TestMethod>
        Public Sub GarbageBlob_ThrowsCryptographicException()
            RequireWindows()
            Dim p As New DpapiSecretProtector()
            Assert.Throws(Of CryptographicException)(Sub() p.Unprotect(New Byte() {1, 2, 3, 4, 5, 6, 7, 8}))
        End Sub

        <TestMethod>
        Public Sub BlobMadeWithOtherEntropy_CannotBeReadByOurProtector()
            RequireWindows()
            Dim foreign = ProtectedData.Protect(Plain, Encoding.UTF8.GetBytes("someone else"), DataProtectionScope.CurrentUser)
            Dim p As New DpapiSecretProtector()
            Assert.Throws(Of CryptographicException)(Sub() p.Unprotect(foreign))
        End Sub

        <TestMethod>
        Public Sub NullInput_Throws()
            RequireWindows()
            Dim p As New DpapiSecretProtector()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() p.Protect(Nothing))
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() p.Unprotect(Nothing))
        End Sub

    End Class

End Namespace
