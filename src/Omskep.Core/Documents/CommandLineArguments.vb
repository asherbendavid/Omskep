Option Strict On
Option Explicit On
Option Infer On

Imports System.IO

Namespace Documents

    ''' <summary>Reads what Windows passes when a file is double-clicked or chosen with "Open with". Pure apart
    ''' from the file check, which can be replaced for tests.</summary>
    Public NotInheritable Class CommandLineArguments

        Private Sub New()
        End Sub

        ''' <param name="arguments">As from Environment.GetCommandLineArgs: the first item is the program itself.</param>
        ''' <param name="fileExists">Nothing means File.Exists.</param>
        ''' <returns>The first argument that is an existing file, or Nothing. Switches (starting with - or /) are ignored.</returns>
        Public Shared Function FindDocumentPath(arguments As IEnumerable(Of String),
                                                Optional fileExists As Func(Of String, Boolean) = Nothing) As String
            If arguments Is Nothing Then Return Nothing
            Dim exists As Func(Of String, Boolean) = If(fileExists, AddressOf File.Exists)

            Dim index As Integer = 0
            For Each argument As String In arguments
                index += 1
                If index = 1 Then Continue For
                If String.IsNullOrWhiteSpace(argument) Then Continue For
                Dim trimmed As String = argument.Trim()
                If trimmed.StartsWith("-", StringComparison.Ordinal) OrElse trimmed.StartsWith("/", StringComparison.Ordinal) Then Continue For
                Try
                    If exists(trimmed) Then Return trimmed
                Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is NotSupportedException OrElse TypeOf ex Is IOException
                    ' Not a usable path: ignore it.
                End Try
            Next
            Return Nothing
        End Function

    End Class

End Namespace
