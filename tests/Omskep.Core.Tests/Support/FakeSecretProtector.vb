Imports System.Security.Cryptography
Imports Omskep.Core.Secrets

Namespace Support

    ''' <summary>
    ''' Reversible, deliberately non-secure stand-in for DPAPI. Output is never the plaintext, and
    ''' Unprotect throws CryptographicException for anything it did not produce, like the real thing.
    ''' </summary>
    Public NotInheritable Class FakeSecretProtector
        Implements ISecretProtector

        Private Const XorMask As Byte = &H5A

        Public Property FailProtectWith As Exception
        Public Property FailUnprotectWith As Exception
        ''' <summary>When set, Unprotect returns this instead of decoding.</summary>
        Public Property UnprotectResultOverride As Byte()
        ''' <summary>The exact array Save passed to Protect, so tests can check it was wiped afterwards.</summary>
        Public Property LastPlaintext As Byte()

        Public Function Protect(plain As Byte()) As Byte() Implements ISecretProtector.Protect
            If FailProtectWith IsNot Nothing Then Throw FailProtectWith
            LastPlaintext = plain
            Dim result(plain.Length + 1) As Byte
            result(0) = &HD9
            result(1) = &HAA
            For i = 0 To plain.Length - 1
                result(i + 2) = CByte(plain(i) Xor XorMask)
            Next
            Return result
        End Function

        Public Function Unprotect(protectedBlob As Byte()) As Byte() Implements ISecretProtector.Unprotect
            If FailUnprotectWith IsNot Nothing Then Throw FailUnprotectWith
            If UnprotectResultOverride IsNot Nothing Then Return UnprotectResultOverride
            If protectedBlob.Length < 2 OrElse protectedBlob(0) <> &HD9 OrElse protectedBlob(1) <> &HAA Then
                Throw New CryptographicException("Fake protector: not a blob this protector made.")
            End If
            Dim result(protectedBlob.Length - 3) As Byte
            For i = 0 To result.Length - 1
                result(i) = CByte(protectedBlob(i + 2) Xor XorMask)
            Next
            Return result
        End Function

    End Class

End Namespace
