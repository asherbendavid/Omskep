Imports System.Globalization
Imports System.IO
Imports System.Text.Json
Imports Omskep.Core.Storage

Namespace Settings

    Public Enum SettingsLoadStatus
        ''' <summary>File read and parsed.</summary>
        Loaded
        ''' <summary>No settings file yet (first run, or already set aside as damaged).</summary>
        NotFound
        ''' <summary>File exists but is not valid settings JSON. It has been renamed aside, not deleted.</summary>
        Damaged
        ''' <summary>File could not be read right now (locked, permissions). Left exactly as it was.</summary>
        Unreadable
    End Enum

    Public NotInheritable Class SettingsLoadResult
        Public ReadOnly Property Status As SettingsLoadStatus
        ''' <summary>Never Nothing: defaults unless Status is Loaded.</summary>
        Public ReadOnly Property Value As AppSettings
        ''' <summary>Technical detail for logs. Never shown as-is to the user, never contains the key.</summary>
        Public ReadOnly Property Detail As String

        Public Sub New(status As SettingsLoadStatus, value As AppSettings, detail As String)
            Me.Status = status
            Me.Value = value
            Me.Detail = detail
        End Sub
    End Class

    ''' <summary>Reads and atomically writes settings.json in one folder.</summary>
    Public NotInheritable Class SettingsStore
        Public Const FileName As String = "settings.json"

        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
            .WriteIndented = True,
            .AllowTrailingCommas = True,
            .ReadCommentHandling = JsonCommentHandling.Skip
        }

        Private ReadOnly _saveLock As New Object()
        Private ReadOnly _utcNow As Func(Of DateTime)

        Public ReadOnly Property SettingsPath As String

        Public Sub New(folder As String, Optional utcNow As Func(Of DateTime) = Nothing)
            If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("A folder is required.", NameOf(folder))
            SettingsPath = Path.Combine(folder, FileName)
            If utcNow Is Nothing Then
                _utcNow = Function() DateTime.UtcNow
            Else
                _utcNow = utcNow
            End If
        End Sub

        Public Function Load() As SettingsLoadResult
            Dim bytes As Byte()
            Try
                bytes = File.ReadAllBytes(SettingsPath)
            Catch ex As FileNotFoundException
                Return New SettingsLoadResult(SettingsLoadStatus.NotFound, New AppSettings(), String.Empty)
            Catch ex As DirectoryNotFoundException
                Return New SettingsLoadResult(SettingsLoadStatus.NotFound, New AppSettings(), String.Empty)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                Return New SettingsLoadResult(SettingsLoadStatus.Unreadable, New AppSettings(), ex.GetType().Name)
            End Try

            Dim parsed As AppSettings = Nothing
            Dim detail As String = "Settings file is not valid JSON."
            ' Parse from a stream: VB cannot use Span types, and the stream overload also skips the
            ' UTF-8 BOM that Notepad adds (hand-editing is a supported repair path; a test pins this).
            Try
                Using stream As New MemoryStream(bytes, False)
                    parsed = JsonSerializer.Deserialize(Of AppSettings)(stream, JsonOptions)
                End Using
            Catch ex As JsonException
                detail = "Settings file is not valid JSON (" & ex.GetType().Name & ")."
            End Try

            If parsed Is Nothing Then
                SetAsideDamagedFile()
                Return New SettingsLoadResult(SettingsLoadStatus.Damaged, New AppSettings(), detail)
            End If

            parsed.Normalize()
            Return New SettingsLoadResult(SettingsLoadStatus.Loaded, parsed, String.Empty)
        End Function

        ''' <summary>Throws IOException / UnauthorizedAccessException on failure; the previous file is left intact.</summary>
        Public Sub Save(settings As AppSettings)
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            SyncLock _saveLock
                settings.Normalize()
                Dim bytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions)
                AtomicFile.WriteAllBytes(SettingsPath, bytes)
            End SyncLock
        End Sub

        ' Keeps the evidence (and any recoverable key blob) instead of overwriting it on the next save.
        Private Sub SetAsideDamagedFile()
            Try
                Dim stamp = _utcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
                File.Move(SettingsPath, SettingsPath & ".damaged-" & stamp, True)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' Best effort. If this fails the next save overwrites the damaged file, which is acceptable.
            End Try
        End Sub

    End Class

End Namespace
