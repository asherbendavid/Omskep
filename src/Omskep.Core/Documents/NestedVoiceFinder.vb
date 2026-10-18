Option Strict On
Option Explicit On
Option Infer On

Namespace Documents

    ''' <summary>One hand-typed &lt;voice&gt; element found nested inside another, as a 1-based line and
    ''' column and a length in characters -- the same shape ImportFlag already uses, so SsmlEditor can mark
    ''' it the same way it marks review flags.</summary>
    Public NotInheritable Class NestedVoiceMark

        Public ReadOnly Property Line As Integer
        Public ReadOnly Property Column As Integer
        Public ReadOnly Property Length As Integer

        Public Sub New(line As Integer, column As Integer, length As Integer)
            Me.Line = line
            Me.Column = column
            Me.Length = length
        End Sub

    End Class

    ''' <summary>
    ''' Finds every &lt;voice&gt; element that opens while another &lt;voice&gt; is already open -- the one
    ''' thing SsmlComposer refuses unconditionally at export time, surfaced here while typing instead of
    ''' waiting for export. Pure, Important tier: wrong behaviour here is a missed or misplaced squiggle,
    ''' not data loss -- SsmlComposer's own export-time gate is the real backstop regardless of what this
    ''' finds, so this function is deliberately lenient rather than strict.
    '''
    ''' Unlike VoiceSplitter, this runs on every keystroke via the same debounced timer as the
    ''' well-formedness check, on text that is very often mid-edit and not well-formed. So it never assumes
    ''' well-formedness: on anything it cannot make sense of (an unterminated tag, for example) it simply
    ''' stops and returns whatever it already found, rather than guessing or throwing.
    ''' </summary>
    Public NotInheritable Class NestedVoiceFinder

        Private Sub New()
        End Sub

        Public Shared Function Find(documentText As String) As IReadOnlyList(Of NestedVoiceMark)
            Dim marks As New List(Of NestedVoiceMark)()
            If String.IsNullOrEmpty(documentText) Then Return marks

            Dim offsets As List(Of Integer) = FindNestedVoiceOffsets(documentText)
            If offsets.Count = 0 Then Return marks

            Dim lineStarts As New List(Of Integer) From {0}
            For p As Integer = 0 To documentText.Length - 1
                If documentText(p) = ChrW(10) Then lineStarts.Add(p + 1)
            Next

            For Each offset As Integer In offsets
                Dim lineIndex As Integer = lineStarts.BinarySearch(offset)
                ' VB's Not binds LOOSER than binary minus (unlike C#'s ~), so this must be parenthesized:
                ' un-parenthesized, "Not lineIndex - 1" parses as "Not (lineIndex - 1)", not the intended
                ' "(Not lineIndex) - 1" -- the bug that produced the out-of-range index seen in testing.
                If lineIndex < 0 Then lineIndex = (Not lineIndex) - 1
                Dim column As Integer = offset - lineStarts(lineIndex) + 1
                marks.Add(New NestedVoiceMark(lineIndex + 1, column, "<voice".Length))
            Next

            Return marks
        End Function

        ''' <summary>The character offsets of every &lt;voice&gt; open tag (including a self-closing one)
        ''' that starts while another &lt;voice&gt; is already open.</summary>
        Private Shared Function FindNestedVoiceOffsets(text As String) As List(Of Integer)
            Dim offsets As New List(Of Integer)()
            Dim n As Integer = text.Length
            Dim i As Integer = 0
            Dim depth As Integer = 0

            While i < n
                If text(i) <> "<"c Then
                    i += 1
                    Continue While
                End If

                If Matches(text, i, "<!--") Then
                    Dim closeAt As Integer = text.IndexOf("-->", i + 4, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3

                ElseIf Matches(text, i, "<![CDATA[") Then
                    Dim closeAt As Integer = text.IndexOf("]]>", i + 9, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 3

                ElseIf i + 1 < n AndAlso text(i + 1) = "?"c Then
                    Dim closeAt As Integer = text.IndexOf("?>", i + 2, StringComparison.Ordinal)
                    If closeAt < 0 Then Exit While
                    i = closeAt + 2

                ElseIf i + 1 < n AndAlso text(i + 1) = "/"c Then
                    Dim gt As Integer = FindUnquotedGreaterThan(text, i + 2)
                    If gt < 0 Then Exit While
                    Dim name As String = text.Substring(i + 2, gt - (i + 2)).Trim()
                    If String.Equals(name, "voice", StringComparison.Ordinal) Then
                        depth = Math.Max(0, depth - 1) ' floor at 0: an extra close is malformed, not our problem to flag here
                    End If
                    i = gt + 1

                Else
                    Dim tagStart As Integer = i
                    Dim gt As Integer = FindUnquotedGreaterThan(text, i + 1)
                    If gt < 0 Then Exit While
                    Dim selfClosing As Boolean = gt > 0 AndAlso text(gt - 1) = "/"c
                    Dim nameEnd As Integer = i + 1
                    While nameEnd < n AndAlso Not Char.IsWhiteSpace(text(nameEnd)) AndAlso text(nameEnd) <> ">"c AndAlso text(nameEnd) <> "/"c
                        nameEnd += 1
                    End While
                    Dim name As String = text.Substring(i + 1, nameEnd - (i + 1))
                    If String.Equals(name, "voice", StringComparison.Ordinal) Then
                        If depth >= 1 Then offsets.Add(tagStart)
                        If Not selfClosing Then depth += 1
                    End If
                    i = gt + 1
                End If
            End While

            Return offsets
        End Function

        Private Shared Function Matches(text As String, at As Integer, literal As String) As Boolean
            Return at + literal.Length <= text.Length AndAlso String.CompareOrdinal(text, at, literal, 0, literal.Length) = 0
        End Function

        ''' <summary>The index of the first '>' at or after fromIndex that is not inside a quoted attribute
        ''' value. -1 if none is found before the text ends (an unterminated tag -- the caller stops rather
        ''' than guess).</summary>
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

    End Class

End Namespace
