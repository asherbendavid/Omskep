Option Strict On
Option Explicit On
Option Infer On

Imports System.Diagnostics
Imports System.IO
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports Omskep.Core.Storage

Namespace Documents

    ''' <summary>A snapshot left behind by an instance of the app that is no longer running.</summary>
    Public NotInheritable Class RecoveryItem

        Public ReadOnly Property FilePath As String
        Public ReadOnly Property Text As String

        ''' <summary>The file the document was opened from or saved to; Nothing for an untitled document.</summary>
        Public ReadOnly Property OriginalPath As String

        Public ReadOnly Property DisplayName As String
        Public ReadOnly Property SavedUtc As Date

        Friend Sub New(filePath As String, text As String, originalPath As String, displayName As String, savedUtc As Date)
            Me.FilePath = filePath
            Me.Text = text
            Me.OriginalPath = originalPath
            Me.DisplayName = displayName
            Me.SavedUtc = savedUtc
        End Sub

    End Class

    ''' <summary>
    ''' Keeps a copy of the unsaved document on disk so a crash, power cut or kill does not lose the edits.
    '''
    ''' Each running instance owns one file of its own (so two instances never overwrite each other) and
    ''' writes it atomically. When the document is saved, replaced or the app exits normally the file is
    ''' discarded. At startup, files whose owner is no longer running are orphans: they are offered for
    ''' restoring and are never deleted automatically. Unreadable files are ignored, not deleted.
    '''
    ''' Snapshot is best-effort and never throws: autosave must not be able to break editing.
    ''' </summary>
    Public NotInheritable Class RecoveryStore

        Private Const FilePattern As String = "recovery-*.json"
        Private Const CurrentVersion As Integer = 1

        ''' <summary>Stored form. Public settable members only because the JSON serializer needs them.</summary>
        Public NotInheritable Class RecoveryRecord
            Public Property Version As Integer
            Public Property OwnerPid As Integer
            Public Property OwnerStartUtc As Date
            Public Property SavedUtc As Date
            Public Property OriginalPath As String
            Public Property DisplayName As String
            Public Property Text As String
        End Class

        Private ReadOnly _folder As String
        Private ReadOnly _ownFile As String
        Private ReadOnly _ownerIsAlive As Func(Of Integer, Date, Boolean)
        Private ReadOnly _pid As Integer
        Private ReadOnly _startUtc As Date

        Private Shared ReadOnly Options As New JsonSerializerOptions() With {
            .Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }

        ''' <param name="folder">Where snapshots live. Created when first needed.</param>
        ''' <param name="ownerIsAlive">Test seam: given a process id and start time, is that process still running?
        ''' Nothing means the real check.</param>
        Public Sub New(folder As String, Optional ownerIsAlive As Func(Of Integer, Date, Boolean) = Nothing)
            If String.IsNullOrWhiteSpace(folder) Then Throw New ArgumentException("A folder is required.", NameOf(folder))
            _folder = folder
            _ownFile = Path.Combine(folder, "recovery-" & Guid.NewGuid().ToString("N") & ".json")
            _ownerIsAlive = If(ownerIsAlive, AddressOf ProcessIsAlive)
            _pid = Environment.ProcessId
            Using current As Process = Process.GetCurrentProcess()
                _startUtc = current.StartTime.ToUniversalTime()
            End Using
        End Sub

        ''' <summary>The usual place: Omskep\recovery under the local application data folder.</summary>
        Public Shared Function DefaultFolder() As String
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Omskep", "recovery")
        End Function

        ''' <summary>Writes (or replaces) this instance's snapshot. Returns False if it could not be written.</summary>
        Public Function Snapshot(text As String, originalPath As String, displayName As String) As Boolean
            Try
                Dim record As New RecoveryRecord() With {
                    .Version = CurrentVersion,
                    .OwnerPid = _pid,
                    .OwnerStartUtc = _startUtc,
                    .SavedUtc = Date.UtcNow,
                    .OriginalPath = originalPath,
                    .DisplayName = displayName,
                    .Text = If(text, String.Empty)
                }
                AtomicFile.WriteAllBytes(_ownFile, JsonSerializer.SerializeToUtf8Bytes(record, Options))
                Return True
            Catch ex As Exception
                ' Best effort by design: any failure (disk full, locked folder, text that cannot be encoded)
                ' is reported through the return value and must never reach the editing code.
                Return False
            End Try
        End Function

        ''' <summary>Removes this instance's snapshot (the document was saved, replaced, or the app is closing).</summary>
        Public Sub Discard()
            TryDelete(_ownFile)
        End Sub

        ''' <summary>Snapshots from instances that are no longer running, oldest first.</summary>
        Public Function FindOrphans() As IReadOnlyList(Of RecoveryItem)
            Dim items As New List(Of RecoveryItem)()
            Try
                If Not Directory.Exists(_folder) Then Return items
                For Each candidate As String In Directory.GetFiles(_folder, FilePattern)
                    If String.Equals(candidate, _ownFile, StringComparison.OrdinalIgnoreCase) Then Continue For
                    Dim record As RecoveryRecord = TryRead(candidate)
                    If record Is Nothing Then Continue For
                    If _ownerIsAlive(record.OwnerPid, record.OwnerStartUtc) Then Continue For
                    items.Add(New RecoveryItem(candidate, record.Text, record.OriginalPath, record.DisplayName, record.SavedUtc))
                Next
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' An unreadable folder offers nothing to restore; it must not stop the app from starting.
            End Try
            items.Sort(Function(a, b) a.SavedUtc.CompareTo(b.SavedUtc))
            Return items
        End Function

        ''' <summary>Deletes an orphan after it was restored or the user chose to discard it.</summary>
        Public Sub Remove(item As RecoveryItem)
            If item Is Nothing Then Throw New ArgumentNullException(NameOf(item))
            ' Only ever delete inside the recovery folder.
            Dim itemFolder As String = Path.GetDirectoryName(Path.GetFullPath(item.FilePath))
            Dim ownFolder As String = Path.GetFullPath(_folder).TrimEnd(Path.DirectorySeparatorChar)
            If String.Equals(itemFolder, ownFolder, StringComparison.OrdinalIgnoreCase) Then TryDelete(item.FilePath)
        End Sub

        Private Shared Function TryRead(filePath As String) As RecoveryRecord
            Try
                Dim record As RecoveryRecord = JsonSerializer.Deserialize(Of RecoveryRecord)(File.ReadAllBytes(filePath), Options)
                If record Is Nothing OrElse record.Version <> CurrentVersion OrElse record.Text Is Nothing Then Return Nothing
                Return record
            Catch ex As Exception When TypeOf ex Is JsonException OrElse TypeOf ex Is IOException OrElse
                                       TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is NotSupportedException
                Return Nothing
            End Try
        End Function

        Private Shared Function ProcessIsAlive(pid As Integer, startUtc As Date) As Boolean
            Try
                Using other As Process = Process.GetProcessById(pid)
                    Return Math.Abs((other.StartTime.ToUniversalTime() - startUtc).TotalSeconds) < 2
                End Using
            Catch ex As Exception
                ' No such process, or not allowed to look at it: it is not one of ours that is running.
                Return False
            End Try
        End Function

        Private Shared Sub TryDelete(path As String)
            Try
                File.Delete(path)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' Best effort; a leftover file is offered for recovery next time rather than lost.
            End Try
        End Sub

    End Class

End Namespace
