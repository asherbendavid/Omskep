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
        ''' <param name="title">Seeds the omskep-meta block's Title field (docs/omskep-meta-spec.md). Null/blank leaves it unset.</param>
        ''' <param name="artist">Seeds Artist. Null/blank leaves it unset.</param>
        ''' <param name="album">Seeds Album. Null/blank leaves it unset.</param>
        ''' <param name="track">Seeds Track. Null/blank leaves it unset.</param>
        ''' <param name="year">Seeds Year. Null/blank leaves it unset.</param>
        ''' <param name="copyright">Seeds Copyright. Null/blank leaves it unset.</param>
        Public Shared Function Compose(body As String, locale As String, voiceShortName As String,
                                Optional title As String = Nothing,
                                Optional artist As String = Nothing,
                                Optional album As String = Nothing,
                                Optional track As String = Nothing,
                                Optional year As String = Nothing,
                                Optional copyright As String = Nothing) As String
            RequireScaffold(locale, voiceShortName)
            Dim lf As String = ChrW(10)
            Dim sb As New StringBuilder()
            sb.Append("<?omskep-meta").Append(lf)
            AppendMetaLine(sb, "Title", title, lf)
            AppendMetaLine(sb, "Artist", artist, lf)
            AppendMetaLine(sb, "Album", album, lf)
            AppendMetaLine(sb, "Track", track, lf)
            AppendMetaLine(sb, "Year", year, lf)
            AppendMetaLine(sb, "Copyright", copyright, lf)
            sb.Append("?>").Append(lf)
            sb.Append("<speak version=""1.0"" xmlns=""").Append(SsmlNamespace).Append(""" xml:lang=""")
            sb.Append(EscapeAttribute(locale)).Append(""">").Append(lf)
            sb.Append("<voice name=""").Append(EscapeAttribute(voiceShortName)).Append(""">").Append(lf)
            sb.Append(body).Append(lf)
            sb.Append("</voice>").Append(lf)
            sb.Append("</speak>").Append(lf)
            Return sb.ToString()
        End Function

        ''' <summary>Writes one "Key:" or "Key: Value" line of the omskep-meta block (docs/omskep-meta-spec.md).
        ''' A stray newline or "?>" in a supplied value would corrupt the block's grammar; both are neutralized
        ''' defensively, though neither is expected from a PDF-metadata string, a filename, or hand typing.</summary>
        Private Shared Sub AppendMetaLine(sb As StringBuilder, key As String, value As String, lf As String)
            sb.Append(key).Append(":"c)
            If Not String.IsNullOrWhiteSpace(value) Then
                Dim safe As String = value.Trim().Replace(ChrW(13), " "c).Replace(ChrW(10), " "c).Replace("?>", "? >")
                sb.Append(" "c).Append(safe)
            End If
            sb.Append(lf)
        End Sub

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
