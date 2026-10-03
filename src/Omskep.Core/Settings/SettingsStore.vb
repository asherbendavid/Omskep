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
        ''' <summary>File could not be read even after retries (locked, permissions). Left exactly as it was.</summary>
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

    ''' <summary>
    ''' Reads and atomically writes settings.json in one folder. Every writer must use Update (or Save for a
    ''' whole-object replace) so read-modify-write cycles cannot interleave and lose each other's changes.
    ''' </summary>
    Public NotInheritable Class SettingsStore
        Public Const FileName As String = "settings.json"

        ' Antivirus and indexers briefly lock freshly written files, so a read can fail transiently too.
        Private Shared ReadOnly DefaultReadRetryDelaysMs As Integer() = {25, 50, 100, 200, 400}

        Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
            .WriteIndented = True,
            .AllowTrailingCommas = True,
            .ReadCommentHandling = JsonCommentHandling.Skip
        }

        Private ReadOnly _lock As New Object()
        Private ReadOnly _utcNow As Func(Of DateTime)
        Private ReadOnly _readAllBytes As Func(Of String, Byte())
        Private ReadOnly _readRetryDelaysMs As IReadOnlyList(Of Integer)

        Public ReadOnly Property SettingsPath As String

        Public Sub New(folder As String, Optional utcNow As Func(Of DateTime) = Nothing)
            Me.New(folder, utcNow, Nothing, Nothing)
        End Sub

        ''' <summary>
        ''' Test seam: <paramref name="readAllBytes"/> replaces the file read (Nothing = File.ReadAllBytes) and
        ''' <paramref name="readRetryDelaysMs"/> gives the wait before each read retry (Nothing = about 0.8 s total).
        ''' </summary>
        Public Sub New(folder As String, utcNow As Func(Of DateTime), readAllBytes As Func(Of String, Byte()), readRetryDelaysMs As IReadOnlyList(Of Integer))
            If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("A folder is required.", NameOf(folder))
            SettingsPath = Path.Combine(folder, FileName)

            If utcNow Is Nothing Then
                _utcNow = Function() DateTime.UtcNow
            Else
                _utcNow = utcNow
            End If

            If readAllBytes Is Nothing Then
                _readAllBytes = Function(p As String) File.ReadAllBytes(p)
            Else
                _readAllBytes = readAllBytes
            End If

            If readRetryDelaysMs Is Nothing Then
                _readRetryDelaysMs = DefaultReadRetryDelaysMs
            Else
                _readRetryDelaysMs = readRetryDelaysMs
            End If
        End Sub

        Public Function Load() As SettingsLoadResult
            SyncLock _lock
                Return LoadCore()
            End SyncLock
        End Function

        ''' <summary>Replaces the whole file with these settings. Prefer Update for changing one thing.</summary>
        ''' <remarks>Throws IOException / UnauthorizedAccessException on failure; the previous file is left intact.</remarks>
        Public Sub Save(settings As AppSettings)
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            SyncLock _lock
                SaveCore(settings)
            End SyncLock
        End Sub

        ''' <summary>
        ''' Atomic read-modify-write: loads the current settings, applies <paramref name="change"/>, saves.
        ''' A damaged file is set aside and the change applies to fresh defaults. If the file cannot be read
        ''' (locked), this throws IOException instead of overwriting content it could not see.
        ''' </summary>
        Public Sub Update(change As Action(Of AppSettings))
            If change Is Nothing Then Throw New ArgumentNullException(NameOf(change))
            SyncLock _lock
                Dim current = LoadCore()
                If current.Status = SettingsLoadStatus.Unreadable Then
                    Throw New IOException("The settings file is in use by another program and could not be read.")
                End If
                change(current.Value)
                SaveCore(current.Value)
            End SyncLock
        End Sub

        Private Sub SaveCore(settings As AppSettings)
            settings.Normalize()
            AtomicFile.WriteAllBytes(SettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions))
        End Sub

        Private Function LoadCore() As SettingsLoadResult
            Dim bytes As Byte() = Nothing
            Dim attempt As Integer = 0
            Do
                Try
                    bytes = _readAllBytes(SettingsPath)
                    Exit Do
                Catch ex As FileNotFoundException
                    Return New SettingsLoadResult(SettingsLoadStatus.NotFound, New AppSettings(), String.Empty)
                Catch ex As DirectoryNotFoundException
                    Return New SettingsLoadResult(SettingsLoadStatus.NotFound, New AppSettings(), String.Empty)
                Catch ex As Exception When (TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException) AndAlso attempt < _readRetryDelaysMs.Count
                    Threading.Thread.Sleep(_readRetryDelaysMs(attempt))
                    attempt += 1
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    Return New SettingsLoadResult(SettingsLoadStatus.Unreadable, New AppSettings(), ex.GetType().Name)
                End Try
            Loop

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
