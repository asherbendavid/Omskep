Imports System.IO

Namespace Storage

    ''' <summary>
    ''' Crash-safe file replacement: write a complete temp file beside the target, flush it to disk,
    ''' then swap it in with a single replace/rename. A failure or power cut at any point leaves
    ''' either the complete old file or the complete new file, never a half-written one.
    ''' </summary>
    Public Module AtomicFile

        Public Const TempSuffix As String = ".tmp"

        ' Windows antivirus, the search indexer and backup tools briefly open freshly written files,
        ' which makes File.Replace fail with "Unable to remove the file to be replaced". Retrying the
        ' swap a few times over about 0.8 s rides that out without hiding a genuine failure.
        Private ReadOnly DefaultRetryDelaysMs As Integer() = {25, 50, 100, 200, 400}

        ''' <summary>Throws on failure (IOException / UnauthorizedAccessException); the old file is untouched.</summary>
        Public Sub WriteAllBytes(path As String, content As Byte())
            WriteAllBytes(path, content, Nothing, DefaultRetryDelaysMs)
        End Sub

        ''' <summary>
        ''' Test seam: <paramref name="swap"/> replaces the real swap step (Nothing = the real one) and
        ''' <paramref name="retryDelaysMs"/> gives the wait before each retry (its length is the retry count).
        ''' </summary>
        Public Sub WriteAllBytes(path As String, content As Byte(), swap As Action(Of String, String), retryDelaysMs As IReadOnlyList(Of Integer))
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A file path is required.", NameOf(path))
            If content Is Nothing Then Throw New ArgumentNullException(NameOf(content))
            If retryDelaysMs Is Nothing Then Throw New ArgumentNullException(NameOf(retryDelaysMs))
            If swap Is Nothing Then swap = AddressOf SwapIntoPlace

            Dim folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))
            If Not String.IsNullOrEmpty(folder) Then Directory.CreateDirectory(folder)

            Dim tempPath = path & TempSuffix
            Try
                ' FileMode.Create truncates a stale temp file left by an earlier crash.
                Using stream As New FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None)
                    stream.Write(content, 0, content.Length)
                    stream.Flush(True)   ' flush to disk, not just the OS cache
                End Using

                Dim attempt As Integer = 0
                Do
                    Try
                        swap(tempPath, path)
                        Exit Do
                    Catch ex As Exception When IsTransient(ex) AndAlso attempt < retryDelaysMs.Count
                        Threading.Thread.Sleep(retryDelaysMs(attempt))
                        attempt += 1
                    End Try
                Loop
            Catch
                TryDelete(tempPath)
                Throw
            End Try
        End Sub

        Private Sub SwapIntoPlace(tempPath As String, path As String)
            If File.Exists(path) Then
                File.Replace(tempPath, path, Nothing)
            Else
                File.Move(tempPath, path)
            End If
        End Sub

        ' A vanished temp file or folder will not come back by waiting; sharing/access errors may.
        Private Function IsTransient(ex As Exception) As Boolean
            If TypeOf ex Is FileNotFoundException OrElse TypeOf ex Is DirectoryNotFoundException Then Return False
            Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
        End Function

        Private Sub TryDelete(path As String)
            Try
                File.Delete(path)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' Best effort only; the original failure is what the caller needs to see.
            End Try
        End Sub

    End Module

End Namespace