Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports System.Text.RegularExpressions

Namespace Documents

    Public NotInheritable Class SearchOptions

        Public ReadOnly Property MatchCase As Boolean
        Public ReadOnly Property WholeWord As Boolean

        ''' <summary>The pattern is a .NET regular expression (and the replacement may use $1, \n and \t).</summary>
        Public ReadOnly Property UseRegex As Boolean

        Public Sub New(Optional matchCase As Boolean = False, Optional wholeWord As Boolean = False, Optional useRegex As Boolean = False)
            Me.MatchCase = matchCase
            Me.WholeWord = wholeWord
            Me.UseRegex = useRegex
        End Sub

    End Class

    Public NotInheritable Class SearchMatch

        Public ReadOnly Property Start As Integer
        Public ReadOnly Property Length As Integer

        Public Sub New(start As Integer, length As Integer)
            Me.Start = start
            Me.Length = length
        End Sub

    End Class

    Public NotInheritable Class FindOutcome

        ''' <summary>The match, or Nothing (see ErrorMessage for a reason other than "no match").</summary>
        Public ReadOnly Property Match As SearchMatch

        ''' <summary>True when the search passed the end (or start) of the document and went round to the other side.</summary>
        Public ReadOnly Property Wrapped As Boolean

        ''' <summary>Why the search could not run (bad pattern, too slow, nothing to find); Nothing otherwise.</summary>
        Public ReadOnly Property ErrorMessage As String

        Public Sub New(match As SearchMatch, wrapped As Boolean, errorMessage As String)
            Me.Match = match
            Me.Wrapped = wrapped
            Me.ErrorMessage = errorMessage
        End Sub

    End Class

    Public NotInheritable Class ReplaceAllOutcome

        ''' <summary>The document with every replacement made.</summary>
        Public ReadOnly Property NewText As String

        ''' <summary>The replacements, last one first, ready for the editor to apply as one undo step.</summary>
        Public ReadOnly Property Edits As IReadOnlyList(Of TextEdit)

        Public ReadOnly Property Count As Integer
        Public ReadOnly Property ErrorMessage As String

        Public Sub New(newText As String, edits As IReadOnlyList(Of TextEdit), count As Integer, errorMessage As String)
            Me.NewText = newText
            Me.Edits = edits
            Me.Count = count
            Me.ErrorMessage = errorMessage
        End Sub

    End Class

    ''' <summary>
    ''' Find and replace over a string. Pure. Never throws: a bad pattern or a runaway one becomes an error message.
    ''' Matches of zero length (for example the pattern "^") are ignored, so a search can never loop in place or
    ''' insert text everywhere. Replace All returns edits instead of changing anything, so the editor can apply them as one
    ''' undo step and verify the result.
    ''' </summary>
    Public NotInheritable Class TextSearch

        Public Const DefaultTimeoutMilliseconds As Integer = 2000

        Private Sub New()
        End Sub

        Public Shared Function Find(text As String, pattern As String, options As SearchOptions, startIndex As Integer,
                                    forward As Boolean, wrap As Boolean,
                                    Optional timeoutMilliseconds As Integer = DefaultTimeoutMilliseconds) As FindOutcome
            Dim problem As String = Nothing
            Dim rx As Regex = Build(pattern, options, timeoutMilliseconds, problem)
            If rx Is Nothing Then Return New FindOutcome(Nothing, False, problem)

            Dim haystack As String = If(text, String.Empty)
            Dim start As Integer = Math.Max(0, Math.Min(startIndex, haystack.Length))
            Try
                If forward Then
                    Dim found As Match = FirstNonEmpty(rx, haystack, start)
                    If found IsNot Nothing Then Return New FindOutcome(New SearchMatch(found.Index, found.Length), False, Nothing)
                    If wrap Then
                        found = FirstNonEmpty(rx, haystack, 0)
                        If found IsNot Nothing Then Return New FindOutcome(New SearchMatch(found.Index, found.Length), True, Nothing)
                    End If
                Else
                    Dim before As Match = Nothing
                    Dim lastOverall As Match = Nothing
                    For Each candidate As Match In rx.Matches(haystack)
                        If candidate.Length = 0 Then Continue For
                        lastOverall = candidate
                        If candidate.Index + candidate.Length <= start Then before = candidate
                    Next
                    If before IsNot Nothing Then Return New FindOutcome(New SearchMatch(before.Index, before.Length), False, Nothing)
                    If wrap AndAlso lastOverall IsNot Nothing Then
                        Return New FindOutcome(New SearchMatch(lastOverall.Index, lastOverall.Length), True, Nothing)
                    End If
                End If
            Catch ex As RegexMatchTimeoutException
                Return New FindOutcome(Nothing, False, TooSlow)
            End Try
            Return New FindOutcome(Nothing, False, Nothing)
        End Function

        ''' <summary>The replacement for the selection, if the selection is exactly one match; otherwise Nothing.</summary>
        Public Shared Function ReplaceAt(text As String, pattern As String, replacement As String, options As SearchOptions,
                                         selectionStart As Integer, selectionLength As Integer,
                                         Optional timeoutMilliseconds As Integer = DefaultTimeoutMilliseconds) As TextEdit
            Dim problem As String = Nothing
            Dim rx As Regex = Build(pattern, options, timeoutMilliseconds, problem)
            Dim haystack As String = If(text, String.Empty)
            If rx Is Nothing OrElse selectionLength <= 0 OrElse selectionStart < 0 OrElse selectionStart + selectionLength > haystack.Length Then Return Nothing
            Try
                Dim found As Match = rx.Match(haystack, selectionStart)
                If Not found.Success OrElse found.Index <> selectionStart OrElse found.Length <> selectionLength Then Return Nothing
                Return New TextEdit(found.Index, found.Length, Expand(found, replacement, options))
            Catch ex As RegexMatchTimeoutException
                Return Nothing
            End Try
        End Function

        Public Shared Function ReplaceAll(text As String, pattern As String, replacement As String, options As SearchOptions,
                                          Optional timeoutMilliseconds As Integer = DefaultTimeoutMilliseconds) As ReplaceAllOutcome
            Dim haystack As String = If(text, String.Empty)
            Dim problem As String = Nothing
            Dim rx As Regex = Build(pattern, options, timeoutMilliseconds, problem)
            If rx Is Nothing Then Return New ReplaceAllOutcome(haystack, New List(Of TextEdit)(), 0, problem)

            Dim ascending As New List(Of TextEdit)()
            Dim result As New StringBuilder(haystack.Length)
            Dim copiedUpTo As Integer = 0
            Try
                For Each found As Match In rx.Matches(haystack)
                    If found.Length = 0 Then Continue For
                    Dim replacementText As String = Expand(found, replacement, options)
                    result.Append(haystack, copiedUpTo, found.Index - copiedUpTo).Append(replacementText)
                    copiedUpTo = found.Index + found.Length
                    ascending.Add(New TextEdit(found.Index, found.Length, replacementText))
                Next
            Catch ex As RegexMatchTimeoutException
                Return New ReplaceAllOutcome(haystack, New List(Of TextEdit)(), 0, TooSlow)
            End Try
            result.Append(haystack, copiedUpTo, haystack.Length - copiedUpTo)

            Dim descending As New List(Of TextEdit)(ascending)
            descending.Reverse()
            Return New ReplaceAllOutcome(result.ToString(), descending, ascending.Count, Nothing)
        End Function

        ' ---- helpers ----

        Private Const TooSlow As String = "The search took too long. Try a simpler pattern."

        Private Shared Function Build(pattern As String, options As SearchOptions, timeoutMilliseconds As Integer,
                                      ByRef problem As String) As Regex
            problem = Nothing
            If String.IsNullOrEmpty(pattern) Then
                problem = "Enter the text to find."
                Return Nothing
            End If
            Dim opts As SearchOptions = If(options, New SearchOptions())
            Dim body As String = If(opts.UseRegex, pattern, Regex.Escape(pattern))
            If opts.WholeWord Then body = "(?<![\p{L}\p{N}_])(?:" & body & ")(?![\p{L}\p{N}_])"
            Dim flags As RegexOptions = RegexOptions.CultureInvariant Or RegexOptions.Multiline
            If Not opts.MatchCase Then flags = flags Or RegexOptions.IgnoreCase
            Try
                Return New Regex(body, flags, TimeSpan.FromMilliseconds(Math.Max(1, timeoutMilliseconds)))
            Catch ex As ArgumentException
                problem = "The pattern is not valid: " & ex.Message
                Return Nothing
            End Try
        End Function

        Private Shared Function FirstNonEmpty(rx As Regex, haystack As String, start As Integer) As Match
            Dim found As Match = rx.Match(haystack, start)
            While found.Success AndAlso found.Length = 0
                found = found.NextMatch()
            End While
            Return If(found.Success, found, Nothing)
        End Function

        Private Shared Function Expand(found As Match, replacement As String, options As SearchOptions) As String
            Dim wanted As String = If(replacement, String.Empty)
            If options Is Nothing OrElse Not options.UseRegex Then Return wanted
            Return found.Result(UnescapeReplacement(wanted))
        End Function

        ''' <summary>In a regular-expression replacement, \n becomes a line break, \t a tab and \\ a backslash.
        ''' Any other backslash stays as typed.</summary>
        Private Shared Function UnescapeReplacement(value As String) As String
            If value.IndexOf("\"c) < 0 Then Return value
            Dim sb As New StringBuilder(value.Length)
            Dim i As Integer = 0
            While i < value.Length
                Dim c As Char = value(i)
                If c = "\"c AndAlso i + 1 < value.Length Then
                    Select Case value(i + 1)
                        Case "n"c
                            sb.Append(ChrW(10))
                            i += 2
                            Continue While
                        Case "t"c
                            sb.Append(ChrW(9))
                            i += 2
                            Continue While
                        Case "\"c
                            sb.Append("\"c)
                            i += 2
                            Continue While
                    End Select
                End If
                sb.Append(c)
                i += 1
            End While
            Return sb.ToString()
        End Function

    End Class

End Namespace
