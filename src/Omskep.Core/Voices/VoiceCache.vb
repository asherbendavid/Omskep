Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports Omskep.Core.Access
Imports Omskep.Core.Storage

Namespace Voices

    ''' <summary>The few voice fields the app needs. Parsed from Azure's list in a later step.</summary>
    Public NotInheritable Class VoiceInfo
        <JsonPropertyName("shortName")>
        Public Property ShortName As String
        <JsonPropertyName("locale")>
        Public Property Locale As String
        <JsonPropertyName("displayName")>
        Public Property DisplayName As String
        <JsonPropertyName("gender")>
        Public Property Gender As String
        <JsonPropertyName("voiceType")>
        Public Property VoiceType As String
    End Class

    ''' <summary>Contents of voices-cache.json. Kept apart from settings.json so a bad cache can never endanger the key.</summary>
    Public NotInheritable Class VoiceCache
        Public Const CurrentSchemaVersion As Integer = 1

        <JsonPropertyName("schemaVersion")>
        Public Property SchemaVersion As Integer = CurrentSchemaVersion

        ''' <summary>The region the list was fetched for. A cache is only valid for that region.</summary>
        <JsonPropertyName("region")>
        Public Property Region As String

        <JsonPropertyName("fetchedUtc")>
        Public Property FetchedUtc As DateTime

        <JsonPropertyName("voices")>
        Public Property Voices As List(Of VoiceInfo) = New List(Of VoiceInfo)()

        ''' <summary>Feeds the lockout state machine: usable only if non-empty and for the current region.</summary>
        Public Function StatusFor(currentRegion As String) As CacheStatus
            Return AccessEvaluator.CacheStatusFor(Region, If(Voices Is Nothing, 0, Voices.Count), currentRegion)
        End Function

        Friend Sub Normalize()
            If Voices Is Nothing Then Voices = New List(Of VoiceInfo)()
            Voices.RemoveAll(Function(v) v Is Nothing)
        End Sub
    End Class

    Public Enum VoiceCacheLoadStatus
        Loaded
        NotFound
        ''' <summary>Unparseable or unreadable. Treated as no cache; it is simply re-fetched.</summary>
        Unusable
    End Enum

    Public NotInheritable Class VoiceCacheLoadResult
        Public ReadOnly Property Status As VoiceCacheLoadStatus
        ''' <summary>Never Nothing: an empty cache unless Status is Loaded.</summary>
        Public ReadOnly Property Cache As VoiceCache

        Public Sub New(status As VoiceCacheLoadStatus, cache As VoiceCache)
            Me.Status = status
            Me.Cache = cache
        End Sub
    End Class

    Public NotInheritable Class VoiceCacheStore
        Public Const FileName As String = "voices-cache.json"

        ' Compact on disk: the list is large and nobody hand-edits it.
        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {.WriteIndented = False}

        Private ReadOnly _lock As New Object()

        Public ReadOnly Property CachePath As String

        Public Sub New(folder As String)
            If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("A folder is required.", NameOf(folder))
            CachePath = Path.Combine(folder, FileName)
        End Sub

        Public Function Load() As VoiceCacheLoadResult
            Dim bytes As Byte()
            Try
                bytes = File.ReadAllBytes(CachePath)
            Catch ex As FileNotFoundException
                Return New VoiceCacheLoadResult(VoiceCacheLoadStatus.NotFound, New VoiceCache())
            Catch ex As DirectoryNotFoundException
                Return New VoiceCacheLoadResult(VoiceCacheLoadStatus.NotFound, New VoiceCache())
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return New VoiceCacheLoadResult(VoiceCacheLoadStatus.Unusable, New VoiceCache())
            End Try

            Dim parsed As VoiceCache = Nothing
            Try
                Using stream As New MemoryStream(bytes, False)
                    parsed = JsonSerializer.Deserialize(Of VoiceCache)(stream, JsonOptions)
                End Using
            Catch ex As JsonException
                parsed = Nothing
            End Try

            If parsed Is Nothing Then Return New VoiceCacheLoadResult(VoiceCacheLoadStatus.Unusable, New VoiceCache())
            parsed.Normalize()
            Return New VoiceCacheLoadResult(VoiceCacheLoadStatus.Loaded, parsed)
        End Function

        Public Sub Save(cache As VoiceCache)
            If cache Is Nothing Then Throw New ArgumentNullException(NameOf(cache))
            SyncLock _lock
                cache.Normalize()
                AtomicFile.WriteAllBytes(CachePath, JsonSerializer.SerializeToUtf8Bytes(cache, JsonOptions))
            End SyncLock
        End Sub

        ''' <summary>Removes the cache (key or region changed). Safe to call when there is none.</summary>
        Public Sub Clear()
            SyncLock _lock
                Try
                    File.Delete(CachePath)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    ' A cache we cannot delete is overwritten by the next save, or ignored by region mismatch.
                End Try
            End SyncLock
        End Sub
    End Class

End Namespace
