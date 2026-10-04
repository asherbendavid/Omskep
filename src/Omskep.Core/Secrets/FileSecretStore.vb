Imports System.Security.Cryptography
Imports System.Text
Imports Omskep.Core.Access
Imports Omskep.Core.Settings

Namespace Secrets

    ''' <summary>
    ''' Keeps the Azure key as a protected blob inside settings.json. Every write goes through
    ''' SettingsStore.Update so it can never clobber (or be clobbered by) another settings change.
    ''' </summary>
    Public NotInheritable Class FileSecretStore
        Implements ISecretStore

        ' Strict: bytes that are not valid UTF-8 mean the blob is not our key, so they must not decode to garbage.
        Private Shared ReadOnly StrictUtf8 As New UTF8Encoding(False, True)

        Private ReadOnly _settings As SettingsStore
        Private ReadOnly _protector As ISecretProtector

        Public Sub New(settings As SettingsStore, protector As ISecretProtector)
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            If protector Is Nothing Then Throw New ArgumentNullException(NameOf(protector))
            _settings = settings
            _protector = protector
        End Sub

        Public Function Load() As SecretLoadResult Implements ISecretStore.Load
            Dim r = _settings.Load()
            Select Case r.Status
                Case SettingsLoadStatus.NotFound
                    Return New SecretLoadResult(KeyState.Absent, Nothing)
                Case SettingsLoadStatus.Damaged
                    Return New SecretLoadResult(KeyState.SettingsFileDamaged, Nothing)
                Case SettingsLoadStatus.Unreadable
                    Return New SecretLoadResult(KeyState.SettingsUnreadable, Nothing)
                Case SettingsLoadStatus.Loaded
                    Return DecodeKey(r.Value.Azure.KeyBlob)
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(r))
            End Select
        End Function

        Private Function DecodeKey(blobText As String) As SecretLoadResult
            If String.IsNullOrEmpty(blobText) Then Return New SecretLoadResult(KeyState.Absent, Nothing)

            Dim blob As Byte()
            Try
                blob = Convert.FromBase64String(blobText)
            Catch ex As FormatException
                Return New SecretLoadResult(KeyState.BlobUnreadable, Nothing)
            End Try

            Dim plain As Byte() = Nothing
            Try
                plain = _protector.Unprotect(blob)
                If plain Is Nothing OrElse plain.Length = 0 Then
                    Return New SecretLoadResult(KeyState.BlobUnreadable, Nothing)
                End If
                Return New SecretLoadResult(KeyState.Present, StrictUtf8.GetString(plain))
            Catch ex As Exception When TypeOf ex Is CryptographicException OrElse TypeOf ex Is ArgumentException
                ' Wrong Windows user, other machine, or a damaged blob. DecoderFallbackException is an ArgumentException.
                Return New SecretLoadResult(KeyState.BlobUnreadable, Nothing)
            Finally
                If plain IsNot Nothing Then Array.Clear(plain, 0, plain.Length)
            End Try
        End Function

        Public Sub Save(key As String) Implements ISecretStore.Save
            Dim cleaned = ValidateKey(key)
            Dim plain = Encoding.UTF8.GetBytes(cleaned)
            Try
                Dim blobText = Convert.ToBase64String(_protector.Protect(plain))
                _settings.Update(Sub(s) s.Azure.KeyBlob = blobText)
            Finally
                Array.Clear(plain, 0, plain.Length)
            End Try
        End Sub

        Public Sub Clear() Implements ISecretStore.Clear
            _settings.Update(Sub(s) s.Azure.KeyBlob = Nothing)
        End Sub

        ' Pasted keys often carry a trailing space or newline. Messages here must never include the key.
        Private Shared Function ValidateKey(key As String) As String
            Dim problem = KeyRules.Problem(key)
            If problem IsNot Nothing Then Throw New ArgumentException(problem, NameOf(key))
            Return key.Trim()
        End Function

    End Class

End Namespace
