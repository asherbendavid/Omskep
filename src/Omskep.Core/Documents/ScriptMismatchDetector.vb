Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    Public Enum Script
        Latin
        Hebrew
        Greek
    End Enum

    ''' <summary>One &lt;voice&gt; block whose dominant script doesn't match what its locale expects.
    ''' Azure bills the characters either way, even when the mismatch means little useful audio results
    ''' -- say so wherever this is shown.</summary>
    Public NotInheritable Class ScriptMismatchWarning

        Public ReadOnly Property ShortName As String
        Public ReadOnly Property Locale As String
        Public ReadOnly Property ExpectedScript As Script
        Public ReadOnly Property ActualScript As Script
        Public ReadOnly Property Excerpt As String

        Public Sub New(shortName As String, locale As String, expectedScript As Script, actualScript As Script, excerpt As String)
            Me.ShortName = shortName
            Me.Locale = locale
            Me.ExpectedScript = expectedScript
            Me.ActualScript = actualScript
            Me.Excerpt = excerpt
        End Sub

    End Class

    ''' <summary>
    ''' Compares each voice block's dominant Unicode script against the script its locale expects, per
    ''' the requirements' "must never proceed [silently]" rule -- Important tier, warns rather than
    ''' blocks, since a human confirms the export either way. The locale table is the only thing that
    ''' changes when a new locale is added; nothing else in this class needs to know about it.
    ''' </summary>
    Public NotInheritable Class ScriptMismatchDetector

        Private Sub New()
        End Sub

        ''' <summary>Locale -&gt; the script its voice is expected to speak. A locale absent from this
        ''' table is simply never checked -- there is no "Afrikaans and English are excluded" special
        ''' case; both just happen to map to Latin here, same as any other Latin-script locale would.</summary>
        Private Shared ReadOnly LocaleScripts As New Dictionary(Of String, Script)(StringComparer.OrdinalIgnoreCase) From {
            {"af-ZA", Script.Latin},
            {"en-US", Script.Latin},
            {"en-GB", Script.Latin},
            {"he-IL", Script.Hebrew}
        }

        Public Shared Function Detect(blocks As IReadOnlyList(Of VoiceBlockInfo)) As IReadOnlyList(Of ScriptMismatchWarning)
            Dim warnings As New List(Of ScriptMismatchWarning)()
            If blocks Is Nothing Then Return warnings

            For Each block As VoiceBlockInfo In blocks
                If block.Locale Is Nothing Then Continue For
                Dim expected As Script
                If Not LocaleScripts.TryGetValue(block.Locale, expected) Then Continue For

                Dim foreign As Script? = FirstUnexpectedScript(block.Text, expected)
                If foreign Is Nothing Then Continue For

                warnings.Add(New ScriptMismatchWarning(block.ShortName, block.Locale, expected, foreign.Value, Excerpt(block.Text)))
            Next

            Return warnings
        End Function

        ''' <summary>The first classifiable letter's script that ISN'T the expected one -- deliberately
        ''' presence-based, not majority-based: a single foreign-script word mixed into an otherwise
        ''' correct block (a pasted name or quote, say) is exactly the realistic mistake this exists to
        ''' catch, and it would never show up as the block's overall "dominant" script. Nothing if every
        ''' classifiable letter matches the expected script (or there are no classifiable letters at
        ''' all).</summary>
        Private Shared Function FirstUnexpectedScript(text As String, expected As Script) As Script?
            If String.IsNullOrEmpty(text) Then Return Nothing
            For Each c As Char In text
                Dim script As Script? = ClassifyChar(c)
                If script IsNot Nothing AndAlso script.Value <> expected Then Return script.Value
            Next
            Return Nothing
        End Function

        ''' <summary>The most common script among this text's letter characters (combining marks, such
        ''' as Hebrew vowel points, are not letters and are correctly skipped rather than miscounted).
        ''' Nothing if the text has no classifiable letters at all. Not used by Detect (see
        ''' FirstUnexpectedScript) -- kept as a general-purpose utility in its own right.</summary>
        Public Shared Function DominantScript(text As String) As Script?
            If String.IsNullOrEmpty(text) Then Return Nothing

            Dim counts As New Dictionary(Of Script, Integer)()
            For Each c As Char In text
                Dim script As Script? = ClassifyChar(c)
                If script Is Nothing Then Continue For
                counts(script.Value) = If(counts.ContainsKey(script.Value), counts(script.Value) + 1, 1)
            Next

            If counts.Count = 0 Then Return Nothing
            Return counts.OrderByDescending(Function(kv) kv.Value).First().Key
        End Function

        Private Shared Function ClassifyChar(c As Char) As Script?
            If Not Char.IsLetter(c) Then Return Nothing
            Dim code As Integer = AscW(c)

            If (code >= &H41 AndAlso code <= &H5A) OrElse (code >= &H61 AndAlso code <= &H7A) OrElse
               (code >= &HC0 AndAlso code <= &HFF) OrElse (code >= &H100 AndAlso code <= &H24F) Then
                Return Script.Latin
            ElseIf (code >= &H590 AndAlso code <= &H5FF) OrElse (code >= &HFB1D AndAlso code <= &HFB4F) Then
                Return Script.Hebrew
            ElseIf (code >= &H370 AndAlso code <= &H3FF) OrElse (code >= &H1F00 AndAlso code <= &H1FFF) Then
                Return Script.Greek
            End If

            Return Nothing
        End Function

        Private Shared Function Excerpt(text As String) As String
            Const maxLength As Integer = 60
            If text Is Nothing Then Return String.Empty
            Dim trimmed As String = text.Trim()
            If trimmed.Length <= maxLength Then Return trimmed
            Return trimmed.Substring(0, maxLength) & "…"
        End Function

    End Class

End Namespace
