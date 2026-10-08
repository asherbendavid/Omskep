Option Strict On
Option Explicit On
Option Infer On

Imports System.Text
Imports System.Text.RegularExpressions

Namespace Documents

    ''' <summary>Replace the characters [Start, Start + Length) with NewText. Offsets are .NET character offsets
    ''' in the text the edit was computed for.</summary>
    Public NotInheritable Class TextEdit

        Public ReadOnly Property Start As Integer
        Public ReadOnly Property Length As Integer
        Public ReadOnly Property NewText As String

        Public Sub New(start As Integer, length As Integer, newText As String)
            Me.Start = start
            Me.Length = length
            Me.NewText = newText
        End Sub

    End Class

    Public NotInheritable Class HebrewReverseResult

        ''' <summary>The input with the edits applied.</summary>
        Public ReadOnly Property Text As String

        ''' <summary>The edits, last one first, so they can be applied one after another without shifting offsets.</summary>
        Public ReadOnly Property Edits As IReadOnlyList(Of TextEdit)

        Public ReadOnly Property RunsReversed As Integer

        ''' <summary>Runs left alone because they already look like correct reading order.</summary>
        Public ReadOnly Property RunsSkipped As Integer

        Public Sub New(text As String, edits As IReadOnlyList(Of TextEdit), runsReversed As Integer, runsSkipped As Integer)
            Me.Text = text
            Me.Edits = edits
            Me.RunsReversed = runsReversed
            Me.RunsSkipped = runsSkipped
        End Sub

    End Class

    ''' <summary>
    ''' Puts Hebrew text that came out of a PDF in visual (left-to-right) order back into reading order.
    ''' PDFs store Hebrew in the order it is drawn, and the extractor does not reverse it, so each word arrives
    ''' backwards (and a phrase with its words in reverse order). Reversing each run of Hebrew letters, points and
    ''' the spaces between them restores it.
    '''
    ''' User-initiated only, never part of the automatic import: a run that was already in reading order would be
    ''' ruined by it. Points (vowel marks) can end up beside the wrong letter in pointed text; they are not read aloud,
    ''' so only the order of the letters matters for narration.
    ''' </summary>
    Public NotInheritable Class HebrewOrder

        ' Points and cantillation, letters, yiddish digraphs and presentation forms.
        Private Const HebrewChar As String = "\u0591-\u05C7\u05D0-\u05EA\u05EF-\u05F4\uFB1D-\uFB4F"

        ' One Hebrew character, then any number of (spaces and one more Hebrew character): a run, no trailing space.
        Private Shared ReadOnly RunPattern As New Regex(
            "[" & HebrewChar & "](?:[ ]*[" & HebrewChar & "])*", RegexOptions.CultureInvariant)

        Private Sub New()
        End Sub

        ''' <param name="skipRunsThatLookCorrect">True for a whole document: a run is left alone when it has a final-form
        ''' letter at the end of a word and none at the start of one, which only happens in reading order. False for a
        ''' selection, where the user has decided.</param>
        Public Shared Function ReverseRuns(text As String, skipRunsThatLookCorrect As Boolean) As HebrewReverseResult
            If String.IsNullOrEmpty(text) Then
                Return New HebrewReverseResult(If(text, String.Empty), New List(Of TextEdit)(), 0, 0)
            End If

            Dim ascending As New List(Of TextEdit)()
            Dim skipped As Integer = 0
            For Each found As Match In RunPattern.Matches(text)
                If skipRunsThatLookCorrect AndAlso LooksCorrect(found.Value) Then
                    skipped += 1
                    Continue For
                End If
                Dim chars As Char() = found.Value.ToCharArray()
                Array.Reverse(chars)
                ascending.Add(New TextEdit(found.Index, found.Length, New String(chars).Normalize(NormalizationForm.FormC)))
            Next

            Dim result As New StringBuilder(text)
            Dim descending As New List(Of TextEdit)()
            For k As Integer = ascending.Count - 1 To 0 Step -1
                Dim edit As TextEdit = ascending(k)
                result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText)
                descending.Add(edit)
            Next
            Return New HebrewReverseResult(result.ToString(), descending, ascending.Count, skipped)
        End Function

        ''' <summary>True when the run has final-form letters ending words and none starting words.</summary>
        Friend Shared Function LooksCorrect(run As String) As Boolean
            Dim finalAtStart As Integer = 0
            Dim finalAtEnd As Integer = 0
            For Each word As String In run.Split(" "c)
                Dim first As Char = ChrW(0)
                Dim last As Char = ChrW(0)
                For Each c As Char In word
                    If c >= ChrW(&H5D0) AndAlso c <= ChrW(&H5EA) Then
                        If first = ChrW(0) Then first = c
                        last = c
                    End If
                Next
                If first = ChrW(0) Then Continue For
                If IsFinalForm(first) Then finalAtStart += 1
                If IsFinalForm(last) Then finalAtEnd += 1
            Next
            Return finalAtEnd > 0 AndAlso finalAtStart = 0
        End Function

        Private Shared Function IsFinalForm(letter As Char) As Boolean
            Select Case AscW(letter)
                Case &H5DA, &H5DD, &H5DF, &H5E3, &H5E5   ' kaf, mem, nun, pe, tsadi
                    Return True
                Case Else
                    Return False
            End Select
        End Function

    End Class

End Namespace
