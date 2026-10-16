Option Strict On
Option Explicit On
Option Infer On

Imports System.IO

Namespace Export

    ''' <summary>
    ''' Builds the default export filename offered in the Save As dialog: "&lt;document name&gt; Alpha.mp3",
    ''' bumping to "&lt;document name&gt; Alpha (2).mp3", "(3)", etc. if that name already exists in the
    ''' target folder. This is only ever a *default* -- the Save As dialog lets the user type anything
    ''' else, including an RC name; this function knows nothing about RCs at all.
    ''' </summary>
    Public NotInheritable Class ExportFileNamer

        ''' <param name="documentStem">The .ssml file's own name, without its extension, or "Untitled"
        ''' for an unsaved document.</param>
        ''' <param name="outputFolder">The folder the file would be saved into -- used only to check
        ''' for an already-existing name to bump past.</param>
        Public Shared Function DefaultFileName(documentStem As String, outputFolder As String) As String
            Dim baseName As String = SanitizeForFileName(documentStem) & " Alpha"

            Dim candidate As String = baseName & ".mp3"
            Dim n As Integer = 2
            While File.Exists(Path.Combine(outputFolder, candidate))
                candidate = $"{baseName} ({n}).mp3"
                n += 1
            End While

            Return candidate
        End Function

        Private Shared Function SanitizeForFileName(stem As String) As String
            Dim result As String = If(String.IsNullOrWhiteSpace(stem), "Untitled", stem.Trim())
            For Each c As Char In Path.GetInvalidFileNameChars()
                result = result.Replace(c, "_"c)
            Next
            Return result
        End Function

    End Class

End Namespace
