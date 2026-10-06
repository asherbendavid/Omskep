Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    ''' <summary>Questions about a document's scaffolding (the speak and voice tags). Pure.</summary>
    Public NotInheritable Class DocumentScaffold

        ''' <summary>In a new document the body starts on this line (line 1 speak, line 2 voice).</summary>
        Public Const BodyStartLine As Integer = 3

        Private Sub New()
        End Sub

        ''' <summary>
        ''' True when nothing but whitespace and complete speak/voice tags is in the text: a new, empty document.
        ''' Anything else, including a half-typed tag or a tag inside a comment, counts as content.
        ''' </summary>
        Public Shared Function IsBlank(documentText As String) As Boolean
            If String.IsNullOrWhiteSpace(documentText) Then Return True
            Dim i As Integer = 0
            While i < documentText.Length
                Dim c As Char = documentText(i)
                If c = "<"c Then
                    Dim tagEnd As Integer = BillableCharacterCounter.EndOfFreeTag(documentText, i)
                    If tagEnd > i Then
                        i = tagEnd
                        Continue While
                    End If
                End If
                If Not Char.IsWhiteSpace(c) Then Return False
                i += 1
            End While
            Return True
        End Function

    End Class

    ''' <summary>The locale and voice a new document starts with.</summary>
    Public NotInheritable Class ScaffoldChoice

        Public ReadOnly Property Locale As String
        Public ReadOnly Property VoiceShortName As String

        Public Sub New(locale As String, voiceShortName As String)
            Me.Locale = locale
            Me.VoiceShortName = voiceShortName
        End Sub

    End Class

    ''' <summary>Picks the default voice written into new documents. Pure.</summary>
    Public NotInheritable Class ScaffoldDefaults

        Public Const FallbackLocale As String = "af-ZA"
        Public Const FallbackVoice As String = "af-ZA-WillemNeural"

        Private Sub New()
        End Sub

        ''' <summary>
        ''' From the settings' default voices (locale to voice short name): the Afrikaans entry when it is usable,
        ''' otherwise the first usable entry in locale order, otherwise the built-in Afrikaans voice. A usable
        ''' entry has a non-blank locale and voice. Whether the voice still exists is a phase 4 concern.
        ''' </summary>
        Public Shared Function Choose(defaultVoices As IReadOnlyDictionary(Of String, String)) As ScaffoldChoice
            If defaultVoices IsNot Nothing Then
                Dim preferred As String = Nothing
                If defaultVoices.TryGetValue(FallbackLocale, preferred) AndAlso Not String.IsNullOrWhiteSpace(preferred) Then
                    Return New ScaffoldChoice(FallbackLocale, preferred.Trim())
                End If
                For Each locale As String In defaultVoices.Keys.OrderBy(Function(k) k, StringComparer.Ordinal)
                    Dim voice As String = defaultVoices(locale)
                    If Not String.IsNullOrWhiteSpace(locale) AndAlso Not String.IsNullOrWhiteSpace(voice) Then
                        Return New ScaffoldChoice(locale.Trim(), voice.Trim())
                    End If
                Next
            End If
            Return New ScaffoldChoice(FallbackLocale, FallbackVoice)
        End Function

    End Class

End Namespace
