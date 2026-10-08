Imports System.Text.RegularExpressions
Imports Omskep.Core.Documents

''' <summary>
''' Find and Replace in one small window that stays open while you work (Ctrl+F or Ctrl+H, F3 for the next match).
''' All searching is done by <see cref="TextSearch"/>; this form only reads the boxes, asks the editor to select or edit,
''' and reports the outcome. Replace All is a single undo step and is verified by the editor, which undoes it if the result
''' is not exactly what was expected.
''' </summary>
Public NotInheritable Class FindReplaceForm
    Inherits Form

    Private ReadOnly _owner As Form
    Private ReadOnly _editor As Editor.SsmlEditor
    Private ReadOnly _afterEdit As Action

    Private ReadOnly _findBox As New TextBox()
    Private ReadOnly _replaceBox As New TextBox()
    Private ReadOnly _matchCase As New CheckBox()
    Private ReadOnly _wholeWord As New CheckBox()
    Private ReadOnly _useRegex As New CheckBox()
    Private ReadOnly _next As New Button()
    Private ReadOnly _previous As New Button()
    Private ReadOnly _replace As New Button()
    Private ReadOnly _replaceAll As New Button()
    Private ReadOnly _close As New Button()
    Private ReadOnly _result As New Label()
    Private _placed As Boolean

    Public Sub New(owner As Form, editor As Editor.SsmlEditor, afterEdit As Action)
        _owner = owner
        _editor = editor
        _afterEdit = afterEdit

        Text = "Find and Replace"
        FormBorderStyle = FormBorderStyle.FixedToolWindow
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        KeyPreview = True
        AutoScaleMode = AutoScaleMode.Font
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink

        _findBox.Width = 340
        _replaceBox.Width = 340
        _matchCase.Text = "Match &case"
        _matchCase.AutoSize = True
        _wholeWord.Text = "&Whole word"
        _wholeWord.AutoSize = True
        _useRegex.Text = "Regular e&xpression"
        _useRegex.AutoSize = True
        _next.Text = "Find &Next"
        _previous.Text = "Find &Previous"
        _replace.Text = "&Replace"
        _replaceAll.Text = "Replace &All"
        _close.Text = "Close"
        For Each b As Button In {_next, _previous, _replace, _replaceAll, _close}
            b.AutoSize = True
        Next
        _result.AutoSize = True
        _result.MaximumSize = New Size(520, 0)

        Dim grid As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 2,
            .Padding = New Padding(10),
            .Dock = DockStyle.Fill
        }
        grid.Controls.Add(MakeLabel("Find &what:"), 0, 0)
        grid.Controls.Add(_findBox, 1, 0)
        grid.Controls.Add(MakeLabel("Re&place with:"), 0, 1)
        grid.Controls.Add(_replaceBox, 1, 1)

        Dim options As New FlowLayoutPanel() With {.AutoSize = True, .WrapContents = False}
        options.Controls.AddRange(New Control() {_matchCase, _wholeWord, _useRegex})
        grid.Controls.Add(options, 0, 2)
        grid.SetColumnSpan(options, 2)

        Dim buttons As New FlowLayoutPanel() With {.AutoSize = True}
        buttons.Controls.AddRange(New Control() {_next, _previous, _replace, _replaceAll, _close})
        grid.Controls.Add(buttons, 0, 3)
        grid.SetColumnSpan(buttons, 2)

        grid.Controls.Add(_result, 0, 4)
        grid.SetColumnSpan(_result, 2)
        Controls.Add(grid)

        Dim order As Control() = {_findBox, _replaceBox, _matchCase, _wholeWord, _useRegex, _next, _previous, _replace, _replaceAll, _close}
        For i As Integer = 0 To order.Length - 1
            order(i).TabIndex = i
        Next
        AcceptButton = _next
        CancelButton = _close

        AddHandler _next.Click, Sub(sender, e) FindNext(True)
        AddHandler _previous.Click, Sub(sender, e) FindNext(False)
        AddHandler _replace.Click, Sub(sender, e) ReplaceCurrent()
        AddHandler _replaceAll.Click, Sub(sender, e) ReplaceEverything()
        AddHandler _close.Click, Sub(sender, e) Hide()
        AddHandler KeyDown, AddressOf OnDialogKeyDown
        AddHandler Activated, Sub(sender, e) UpdateEnabled()
    End Sub

    Private Shared Function MakeLabel(caption As String) As Label
        Return New Label() With {.Text = caption, .AutoSize = True, .Anchor = AnchorStyles.Left, .Margin = New Padding(3, 6, 3, 6)}
    End Function

    ' ---- showing ----

    ''' <summary>Opens (or brings forward) the window; the selected text, if short and on one line, becomes the search text.</summary>
    Public Sub ShowFor(focusReplace As Boolean)
        PrefillFromSelection()
        If Not _placed Then
            Location = New Point(Math.Max(0, _owner.Right - Width - 40), _owner.Top + 100)
            _placed = True
        End If
        If Not Visible Then Show(_owner)
        UpdateEnabled()
        Activate()
        If focusReplace Then
            _replaceBox.Focus()
            _replaceBox.SelectAll()
        Else
            _findBox.Focus()
            _findBox.SelectAll()
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        ' Closing with the X only hides the window, so the search text and options are still there next time.
        If e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Hide()
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub OnDialogKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.F3 Then
            FindNext(Not e.Shift)
            e.Handled = True
        End If
    End Sub

    Private Sub UpdateEnabled()
        Dim canEdit As Boolean = _editor.IsEditable
        _replaceBox.Enabled = canEdit
        _replace.Enabled = canEdit
        _replaceAll.Enabled = canEdit
    End Sub

    Private Sub PrefillFromSelection()
        Dim picked As String = _editor.SelectedText
        If picked.Length = 0 OrElse picked.Length > 200 OrElse picked.Contains(ChrW(10)) Then Return
        _findBox.Text = If(_useRegex.Checked, Regex.Escape(picked), picked)
    End Sub

    ' ---- actions ----

    Private Function CurrentOptions() As SearchOptions
        Return New SearchOptions(_matchCase.Checked, _wholeWord.Checked, _useRegex.Checked)
    End Function

    Private Sub Say(message As String, isProblem As Boolean)
        _result.Text = message
        _result.ForeColor = If(isProblem, Color.Firebrick, SystemColors.ControlText)
    End Sub

    ''' <summary>Selects the next (or previous) match, wrapping round the document. Called by F3 and Shift+F3 as well.</summary>
    Public Sub FindNext(forward As Boolean)
        If _findBox.Text.Length = 0 Then
            ShowFor(False)
            Say("Enter the text to find.", True)
            Return
        End If
        Dim start As Integer = If(forward, _editor.SelectionEndIndex, _editor.SelectionStartIndex)
        Dim outcome As FindOutcome = TextSearch.Find(_editor.DocumentText, _findBox.Text, CurrentOptions(), start, forward, True)
        If outcome.ErrorMessage IsNot Nothing Then
            Say(outcome.ErrorMessage, True)
        ElseIf outcome.Match Is Nothing Then
            Say("Not found.", True)
        Else
            _editor.SelectCharacterRange(outcome.Match.Start, outcome.Match.Length)
            Say(If(outcome.Wrapped, "Search wrapped around the document.", String.Empty), False)
        End If
    End Sub

    Private Sub ReplaceCurrent()
        If Not _editor.IsEditable Then Return
        If _findBox.Text.Length = 0 Then
            Say("Enter the text to find.", True)
            Return
        End If
        Dim text As String = _editor.DocumentText
        Dim selectionStart As Integer = _editor.SelectionStartIndex
        Dim edit As TextEdit = TextSearch.ReplaceAt(text, _findBox.Text, _replaceBox.Text, CurrentOptions(),
                                                    selectionStart, _editor.SelectionEndIndex - selectionStart)
        If edit Is Nothing Then
            ' The selection is not a match yet: this click just finds the next one.
            FindNext(True)
            Return
        End If
        Dim expected As String = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText)
        If Not _editor.ApplyEdits({edit}, expected) Then
            Say("The replacement could not be made safely, so nothing was changed.", True)
            Return
        End If
        _afterEdit()
        _editor.SelectCharacterRange(edit.Start + edit.NewText.Length, 0)
        FindNext(True)
    End Sub

    Private Sub ReplaceEverything()
        If Not _editor.IsEditable Then Return
        Dim outcome As ReplaceAllOutcome = TextSearch.ReplaceAll(_editor.DocumentText, _findBox.Text, _replaceBox.Text, CurrentOptions())
        If outcome.ErrorMessage IsNot Nothing Then
            Say(outcome.ErrorMessage, True)
            Return
        End If
        If outcome.Count = 0 Then
            Say("Not found.", True)
            Return
        End If
        If Not _editor.ApplyEdits(outcome.Edits, outcome.NewText) Then
            Say("The replacements could not be made safely, so nothing was changed.", True)
            Return
        End If
        _afterEdit()
        Say("Replaced " & outcome.Count.ToString(Globalization.CultureInfo.InvariantCulture) & " occurrence" &
            If(outcome.Count = 1, "", "s") & ". Ctrl+Z in the editor undoes all of them.", False)
    End Sub

End Class
