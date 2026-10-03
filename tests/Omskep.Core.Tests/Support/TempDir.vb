Imports System.IO

Namespace Support

    ''' <summary>
    ''' A throwaway folder per test. The name contains an apostrophe on purpose: the developer's
    ''' Windows profile path does, and persistence code must work under it.
    ''' </summary>
    Public NotInheritable Class TempDir
        Implements IDisposable

        Public ReadOnly Property FolderPath As String

        Public Sub New()
            FolderPath = Path.Combine(Path.GetTempPath(), "omskep-it's-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(FolderPath)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            Try
                Directory.Delete(FolderPath, True)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' Temp folder cleanup is best effort.
            End Try
        End Sub
    End Class

End Namespace
