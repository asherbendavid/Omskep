Option Strict On
Option Explicit On
Option Infer On

Imports System.Text

Namespace Import

    ''' <summary>
    ''' Wraps already-sanitized text in the minimal SSML scaffolding: a speak element and one
    ''' default voice. Voice assignment authoring comes in a later phase.
    ''' </summary>
    Public NotInheritable Class DocumentComposer

        Public Const SsmlNamespace As String = "http://www.w3.org/2001/10/synthesis"

        Private Sub New()
        End Sub

        ''' <param name="body">Sanitized text (already escaped). Placed between the voice tags.</param>
        ''' <param name="locale">For example "af-ZA". Becomes xml:lang on the speak element.</param>
        ''' <param name="voiceShortName">For example "af-ZA-WillemNeural".</param>
        Public Shared Function Compose(body As String, locale As String, voiceShortName As String) As String
            RequireScaffold(locale, voiceShortName)
            Dim lf As String = ChrW(10)
            Dim sb As New StringBuilder()
            sb.Append("<speak version=""1.0"" xmlns=""").Append(SsmlNamespace).Append(""" xml:lang=""")
            sb.Append(EscapeAttribute(locale)).Append(""">").Append(lf)
            sb.Append("<voice name=""").Append(EscapeAttribute(voiceShortName)).Append(""">").Append(lf)
            sb.Append(body).Append(lf)
            sb.Append("</voice>").Append(lf)
            sb.Append("</speak>").Append(lf)
            Return sb.ToString()
        End Function

        Friend Shared Sub RequireScaffold(locale As String, voiceShortName As String)
            If String.IsNullOrWhiteSpace(locale) Then
                Throw New ArgumentException("A locale is required.", NameOf(locale))
            End If
            If String.IsNullOrWhiteSpace(voiceShortName) Then
                Throw New ArgumentException("A voice is required.", NameOf(voiceShortName))
            End If
        End Sub

        Private Shared Function EscapeAttribute(value As String) As String
            Return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").
                         Replace(ChrW(34), "&quot;").Replace("'", "&apos;")
        End Function

    End Class

End Namespace
