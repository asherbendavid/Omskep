Option Strict On
Option Explicit On
Option Infer On

Imports System.Text.RegularExpressions

Namespace Import

    ''' <summary>
    ''' Finds places in a finished document that deserve a look. Reports only: it never
    ''' changes text. Run it on the document exactly as the editor shows it, so that line
    ''' numbers match.
    ''' </summary>
    Public NotInheritable Class ImportFlagFinder

        Private Shared ReadOnly WideRun As New Regex("[ ]{3,}", RegexOptions.CultureInvariant)

        Private Sub New()
        End Sub

        ''' <summary>Flags in document order (by line, then column).</summary>
        Public Shared Function Find(documentText As String) As IReadOnlyList(Of ImportFlag)
            Dim flags As New List(Of ImportFlag)()
            If String.IsNullOrEmpty(documentText) Then Return flags

            Dim lines As String() = documentText.Split(ChrW(10))
            For idx As Integer = 0 To lines.Length - 1
                Dim lineText As String = lines(idx).TrimEnd(ChrW(13))
                If lineText.Length < 2 Then Continue For

                For Each m As Match In WideRun.Matches(lineText)
                    flags.Add(New ImportFlag(ImportFlagKind.WideSpaceRun, idx + 1, m.Index + 1, m.Length))
                Next

                If lineText(lineText.Length - 1) = "-"c AndAlso Char.IsLetter(lineText(lineText.Length - 2)) Then
                    flags.Add(New ImportFlag(ImportFlagKind.LineEndHyphen, idx + 1, lineText.Length, 1))
                End If
            Next
            Return flags
        End Function

    End Class

End Namespace
