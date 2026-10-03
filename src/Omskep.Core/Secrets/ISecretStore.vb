Imports Omskep.Core.Access

Namespace Secrets

    ''' <summary>
    ''' Turns key bytes into an opaque blob and back. The real implementation (Windows DPAPI) lives in
    ''' Omskep.App because it is Windows-only; Core stays platform-neutral and tests use a fake.
    ''' Unprotect must throw CryptographicException for a blob it cannot decrypt.
    ''' </summary>
    Public Interface ISecretProtector
        Function Protect(plain As Byte()) As Byte()
        Function Unprotect(protectedBlob As Byte()) As Byte()
    End Interface

    ''' <summary>The saved Azure key. Region and other settings are not secret and live in AppSettings.</summary>
    Public Interface ISecretStore
        Function Load() As SecretLoadResult
        ''' <summary>Saves or replaces the key. Throws ArgumentException for a blank key or one with whitespace inside.</summary>
        Sub Save(key As String)
        ''' <summary>Removes the key. Safe to call when none is saved.</summary>
        Sub Clear()
    End Interface

    ''' <summary>
    ''' Status is the startup KeyState the lockout state machine consumes. Key is Nothing unless
    ''' Status is Present. There is deliberately no free-text detail: nothing here can leak the key.
    ''' </summary>
    Public NotInheritable Class SecretLoadResult
        Public ReadOnly Property Status As KeyState
        Public ReadOnly Property Key As String

        Public Sub New(status As KeyState, key As String)
            Me.Status = status
            Me.Key = key
        End Sub
    End Class

End Namespace
