Imports System.Security.Cryptography
Imports System.Text
Imports Omskep.Core.Secrets

Namespace Secrets

    ''' <summary>
    ''' Windows DPAPI, current-user scope, with fixed application entropy. Protects the key against other
    ''' Windows users and against the settings file being copied to another machine or account. It does not
    ''' protect against other software already running as the same user; no local scheme can.
    ''' </summary>
    Public NotInheritable Class DpapiSecretProtector
        Implements ISecretProtector

        ' Not a secret: it only scopes blobs to this application and format version.
        Private Shared ReadOnly Entropy As Byte() = Encoding.UTF8.GetBytes("Omskep/azure-key/v1")

        Public Function Protect(plain As Byte()) As Byte() Implements ISecretProtector.Protect
            If plain Is Nothing Then Throw New ArgumentNullException(NameOf(plain))
            Return ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser)
        End Function

        ''' <summary>Throws CryptographicException when the blob is damaged or was made by another user, machine or entropy.</summary>
        Public Function Unprotect(protectedBlob As Byte()) As Byte() Implements ISecretProtector.Unprotect
            If protectedBlob Is Nothing Then Throw New ArgumentNullException(NameOf(protectedBlob))
            Return ProtectedData.Unprotect(protectedBlob, Entropy, DataProtectionScope.CurrentUser)
        End Function

    End Class

End Namespace
