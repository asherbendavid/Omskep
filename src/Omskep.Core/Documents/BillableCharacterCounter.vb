Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    ''' <summary>
    ''' An upper bound on the characters Azure will bill: every character except the speak and voice
    ''' tags, with CRLF counted as one. It can only equal or exceed the real bill (phase 0 measurements:
    ''' whitespace at the edges, next to tags and inside prosody is sometimes billed less, never more).
    ''' Entities such as "&amp;amp;" are counted as written, which is the safe side. Characters are counted
    ''' as UTF-16 units, which is also the safe side for characters outside the basic plane.
    '''
    ''' Pure and linear in the length of the text. Run it on the final composed SSML, including
    ''' anything the app adds at export (prosody wrappers, a disclosure line).
    ''' </summary>
    Public NotInheritable Class BillableCharacterCounter

        Private Sub New()
        End Sub

        Public Shared Function CountUpperBound(ssml As String) As Integer
            If String.IsNullOrEmpty(ssml) Then Return 0

            Dim total As Integer = 0
            Dim i As Integer = 0
            While i < ssml.Length
                Dim c As Char = ssml(i)

                If c = "<"c Then
                    ' Comments, CDATA and processing instructions are billed like text and are skipped
                    ' over as a block, so a tag written inside one is never mistaken for a free tag.
                    Dim blockEnd As Integer = EndOfBlock(ssml, i)
                    If blockEnd > i Then
                        total += CountPlain(ssml, i, blockEnd)
                        i = blockEnd
                        Continue While
                    End If

                    Dim tagEnd As Integer = EndOfFreeTag(ssml, i)
                    If tagEnd > i Then
                        i = tagEnd
                        Continue While
                    End If
                End If

                If c = ChrW(13) AndAlso i + 1 < ssml.Length AndAlso ssml(i + 1) = ChrW(10) Then
                    ' CRLF counts as one: count the LF on the next pass.
                    i += 1
                    Continue While
                End If

                total += 1
                i += 1
            End While
            Return total
        End Function

        ''' <summary>Counts text in [start, stop) with CRLF as one.</summary>
        Private Shared Function CountPlain(ssml As String, start As Integer, [stop] As Integer) As Integer
            Dim n As Integer = 0
            For k As Integer = start To [stop] - 1
                If ssml(k) = ChrW(13) AndAlso k + 1 < [stop] AndAlso ssml(k + 1) = ChrW(10) Then Continue For
                n += 1
            Next
            Return n
        End Function

        ''' <summary>If a comment, CDATA section or processing instruction starts at index i, returns the index
        ''' just after it (or the end of the text if it is never closed); otherwise returns i.</summary>
        Private Shared Function EndOfBlock(ssml As String, i As Integer) As Integer
            If StartsWithAt(ssml, i, "<!--") Then Return AfterMarker(ssml, i + 4, "-->")
            If StartsWithAt(ssml, i, "<![CDATA[") Then Return AfterMarker(ssml, i + 9, "]]>")
            If StartsWithAt(ssml, i, "<?") Then Return AfterMarker(ssml, i + 2, "?>")
            Return i
        End Function

        Private Shared Function AfterMarker(ssml As String, from As Integer, marker As String) As Integer
            Dim found As Integer = ssml.IndexOf(marker, from, StringComparison.Ordinal)
            Return If(found < 0, ssml.Length, found + marker.Length)
        End Function

        ''' <summary>If a complete speak or voice tag (opening, closing or self-closing, with attributes) starts
        ''' at index i, returns the index just after its closing ">". Returns i for anything else, including a
        ''' half-typed tag with no closing ">" (which is then counted as ordinary text).</summary>
        Private Shared Function EndOfFreeTag(ssml As String, i As Integer) As Integer
            Dim p As Integer = i + 1
            If p < ssml.Length AndAlso ssml(p) = "/"c Then p += 1

            Dim nameLength As Integer = NameLengthAt(ssml, p)
            If nameLength = 0 Then Return i
            ' The character after the name must end the name: whitespace, ">" or "/".
            Dim after As Integer = p + nameLength
            If after >= ssml.Length Then Return i
            Dim nextChar As Char = ssml(after)
            If Not (Char.IsWhiteSpace(nextChar) OrElse nextChar = ">"c OrElse nextChar = "/"c) Then Return i

            ' Scan to the closing ">", skipping over quoted attribute values (which may contain ">").
            Dim quote As Char = ChrW(0)
            For k As Integer = after To ssml.Length - 1
                Dim ch As Char = ssml(k)
                If quote <> ChrW(0) Then
                    If ch = quote Then quote = ChrW(0)
                ElseIf ch = ChrW(34) OrElse ch = "'"c Then
                    quote = ch
                ElseIf ch = ">"c Then
                    Return k + 1
                ElseIf ch = "<"c Then
                    Return i ' a new tag starts first: this one is half-typed
                End If
            Next
            Return i
        End Function

        ''' <summary>Length of "speak" or "voice" at p (case-sensitive, as in XML), else 0.</summary>
        Private Shared Function NameLengthAt(ssml As String, p As Integer) As Integer
            If StartsWithAt(ssml, p, "speak") Then Return 5
            If StartsWithAt(ssml, p, "voice") Then Return 5
            Return 0
        End Function

        Private Shared Function StartsWithAt(ssml As String, index As Integer, value As String) As Boolean
            Return index >= 0 AndAlso index + value.Length <= ssml.Length AndAlso
                   String.CompareOrdinal(ssml, index, value, 0, value.Length) = 0
        End Function

    End Class

End Namespace
