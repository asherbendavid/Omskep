Imports Omskep.Core.Access
Imports Omskep.Core.Secrets

Namespace Support

    ''' <summary>In-memory ISecretStore for tests of code that depends on a store. Never touches disk.</summary>
    Public NotInheritable Class InMemorySecretStore
        Implements ISecretStore

        Private _key As String

        ''' <summary>When set, Load returns this regardless of what was saved (simulates damaged/locked states).</summary>
        Public Property ForcedLoadResult As SecretLoadResult

        Public Function Load() As SecretLoadResult Implements ISecretStore.Load
            If ForcedLoadResult IsNot Nothing Then Return ForcedLoadResult
            If _key Is Nothing Then Return New SecretLoadResult(KeyState.Absent, Nothing)
            Return New SecretLoadResult(KeyState.Present, _key)
        End Function

        Public Sub Save(key As String) Implements ISecretStore.Save
            If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentException("A key is required.", NameOf(key))
            _key = key.Trim()
        End Sub

        Public Sub Clear() Implements ISecretStore.Clear
            _key = Nothing
        End Sub
    End Class

End Namespace
