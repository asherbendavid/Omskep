Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    ''' <summary>How Open treats a file.</summary>
    Public Enum FileKind
        ''' <summary>An SSML document: loaded exactly as it is.</summary>
        Ssml
        ''' <summary>A PDF: text is extracted and imported.</summary>
        Pdf
        ''' <summary>Any other file: plain text to be imported, unless it turns out to be SSML.</summary>
        Text
    End Enum

    ''' <summary>Decides how a file is opened. Pure.</summary>
    Public NotInheritable Class FileKinds

        Private Sub New()
        End Sub

        Public Shared Function Classify(path As String) As FileKind
            If String.IsNullOrWhiteSpace(path) Then Return FileKind.Text
            Select Case System.IO.Path.GetExtension(path).ToLowerInvariant()
                Case ".pdf"
                    Return FileKind.Pdf
                Case ".ssml", ".xml"
                    Return FileKind.Ssml
                Case Else
                    Return FileKind.Text
            End Select
        End Function

        ''' <summary>True when the text already is an SSML document (starts with a speak element or an XML declaration).
        ''' A plain-text file that looks like this is opened as it is, not imported.</summary>
        Public Shared Function LooksLikeSsml(fileText As String) As Boolean
            If String.IsNullOrEmpty(fileText) Then Return False
            Dim trimmed As String = fileText.TrimStart(ChrW(&HFEFF), " "c, ChrW(9), ChrW(10), ChrW(13))
            Return trimmed.StartsWith("<speak", StringComparison.Ordinal) OrElse
                   trimmed.StartsWith("<?xml", StringComparison.Ordinal)
        End Function

    End Class

End Namespace
