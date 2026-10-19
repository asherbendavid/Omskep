Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    ''' <summary>The span of a &lt;voice name="..."&gt; attribute's value, as character offsets into the
    ''' document that contains it.</summary>
    Public NotInheritable Class VoiceAttributeSpan

        Public ReadOnly Property ValueStart As Integer
        Public ReadOnly Property ValueLength As Integer

        Public Sub New(valueStart As Integer, valueLength As Integer)
            Me.ValueStart = valueStart
            Me.ValueLength = valueLength
        End Sub

    End Class

    ''' <summary>
    ''' Finds whether a document position sits inside a &lt;voice name="..."&gt; attribute's value -- the
    ''' question the editor needs answered before it offers the voice autocomplete dropdown instead of
    ''' the normal context menu. Pure, lenient like NestedVoiceFinder (runs on live, possibly mid-edit
    ''' text): never throws, degrades to "not found" on anything it can't make sense of.
    ''' </summary>
    Public NotInheritable Class VoiceAttributeLocator

        Private Sub New()
        End Sub

        ''' <summary>The attribute value span containing position, inclusive of both edges (so a caret at
        ''' the very start or end of the value still counts as "inside"), or Nothing if position isn't
        ''' inside any &lt;voice&gt; element's name attribute.</summary>
        Public Shared Function Locate(documentText As String, position As Integer) As VoiceAttributeSpan
            If String.IsNullOrEmpty(documentText) OrElse position < 0 OrElse position > documentText.Length Then Return Nothing

            Dim n As Integer = documentText.Length
            Dim i As Integer = 0

            While i < n
                If documentText(i) <> "<"c Then
                    i += 1
                    Continue While
                End If

                If Matches(documentText, i, "<!--") Then
                    Dim closeAt As Integer = documentText.IndexOf("-->", i + 4, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3

                ElseIf Matches(documentText, i, "<![CDATA[") Then
                    Dim closeAt As Integer = documentText.IndexOf("]]>", i + 9, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3

                ElseIf i + 1 < n AndAlso documentText(i + 1) = "?"c Then
                    Dim closeAt As Integer = documentText.IndexOf("?>", i + 2, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 2

                ElseIf i + 1 < n AndAlso documentText(i + 1) = "/"c Then
                    Dim gt As Integer = FindUnquotedGreaterThan(documentText, i + 2)
                    If gt < 0 Then Exit While
                    i = gt + 1

                Else
                    Dim tagStart As Integer = i
                    Dim gt As Integer = FindUnquotedGreaterThan(documentText, i + 1)
                    If gt < 0 Then Exit While
                    Dim nameEnd As Integer = i + 1
                    While nameEnd < n AndAlso Not Char.IsWhiteSpace(documentText(nameEnd)) AndAlso documentText(nameEnd) <> ">"c AndAlso documentText(nameEnd) <> "/"c
                        nameEnd += 1
                    End While
                    If String.Equals(documentText.Substring(tagStart + 1, nameEnd - (tagStart + 1)), "voice", StringComparison.Ordinal) Then
                        Dim valueStart As Integer = -1
                        Dim valueLength As Integer = 0
                        FindAttributeValue(documentText, tagStart, gt, "name", valueStart, valueLength)
                        If valueStart >= 0 AndAlso valueStart <= position AndAlso position <= valueStart + valueLength Then
                            Return New VoiceAttributeSpan(valueStart, valueLength)
                        End If
                    End If
                    i = gt + 1
                End If
            End While

            Return Nothing
        End Function

        Private Shared Function Matches(text As String, at As Integer, literal As String) As Boolean
            Return at + literal.Length <= text.Length AndAlso String.CompareOrdinal(text, at, literal, 0, literal.Length) = 0
        End Function

        Private Shared Function FindUnquotedGreaterThan(text As String, fromIndex As Integer) As Integer
            Dim inQuote As Char = ChrW(0)
            For p As Integer = fromIndex To text.Length - 1
                Dim c As Char = text(p)
                If inQuote <> ChrW(0) Then
                    If c = inQuote Then inQuote = ChrW(0)
                ElseIf c = """"c OrElse c = "'"c Then
                    inQuote = c
                ElseIf c = ">"c Then
                    Return p
                End If
            Next
            Return -1
        End Function

        Private Shared Sub FindAttributeValue(text As String, tagStart As Integer, tagEnd As Integer, attrName As String, ByRef valueStart As Integer, ByRef valueLength As Integer)
            valueStart = -1
            valueLength = 0
            Dim idx As Integer = tagStart
            While idx < tagEnd
                Dim found As Integer = text.IndexOf(attrName, idx, tagEnd - idx, StringComparison.Ordinal)
                If found < 0 Then Exit While
                Dim precededOk As Boolean = found = tagStart OrElse Char.IsWhiteSpace(text(found - 1))
                Dim p As Integer = found + attrName.Length
                While p < tagEnd AndAlso Char.IsWhiteSpace(text(p))
                    p += 1
                End While
                If precededOk AndAlso p < tagEnd AndAlso text(p) = "="c Then
                    p += 1
                    While p < tagEnd AndAlso Char.IsWhiteSpace(text(p))
                        p += 1
                    End While
                    If p < tagEnd AndAlso (text(p) = """"c OrElse text(p) = "'"c) Then
                        Dim quote As Char = text(p)
                        Dim vStart As Integer = p + 1
                        Dim q As Integer = text.IndexOf(quote, vStart)
                        If q >= 0 AndAlso q <= tagEnd Then
                            valueStart = vStart
                            valueLength = q - vStart
                            Return
                        End If
                    End If
                End If
                idx = found + attrName.Length
            End While
        End Sub

    End Class

End Namespace
