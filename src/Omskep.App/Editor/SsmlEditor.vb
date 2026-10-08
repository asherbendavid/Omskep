Option Strict On
Option Explicit On
Option Infer On

Imports System.ComponentModel
Imports System.Text
Imports System.Windows.Forms
Imports Omskep.Core.Documents
Imports Omskep.Core.Import
Imports ScintillaNET

Namespace Editor

    ''' <summary>
    ''' The text editor, wrapped so the rest of the app never touches Scintilla directly and the control
    ''' can be replaced later. Holds no document logic: it shows text, reports edits, and draws marks that
    ''' other code asks for. All positions in its API are 1-based lines and columns counted in .NET
    ''' characters; the control's own position units stay inside this class.
    '''
    ''' Pasting is deliberately not done here: Ctrl+V, Ctrl+Shift+V and Shift+Insert are switched off in the
    ''' control so the form's menu commands (which sanitize) are the only way text is pasted.
    ''' </summary>
    Public NotInheritable Class SsmlEditor
        Inherits UserControl

        ' Scintilla's XML (HTML-family) lexer style numbers.
        Private Const StyleTag As Integer = 1
        Private Const StyleTagUnknown As Integer = 2
        Private Const StyleAttribute As Integer = 3
        Private Const StyleAttributeUnknown As Integer = 4
        Private Const StyleNumber As Integer = 5
        Private Const StyleDoubleString As Integer = 6
        Private Const StyleSingleString As Integer = 7
        Private Const StyleOther As Integer = 8
        Private Const StyleComment As Integer = 9
        Private Const StyleEntity As Integer = 10
        Private Const StyleTagEnd As Integer = 11
        Private Const StyleXmlStart As Integer = 12
        Private Const StyleXmlEnd As Integer = 13
        Private Const StyleCData As Integer = 17

        ' Indicators 0-7 belong to lexers; ours start at 8.
        Private Const ErrorIndicator As Integer = 8
        Private Const CauseIndicator As Integer = 9
        Private Const FlagIndicator As Integer = 10

        Private ReadOnly _sci As New Scintilla()
        Private _loading As Boolean
        Private _usesBytePositions As Boolean

        ''' <summary>The text was changed by the user or by an editing command (not by LoadText).</summary>
        Public Event ContentChanged()

        ''' <summary>True when the text no longer matches the last save point; False when it matches again
        ''' (for example after undoing back to it).</summary>
        Public Event DirtyChanged(isDirty As Boolean)

        Public Sub New()
            SuspendLayout()
            _sci.Dock = DockStyle.Fill
            Controls.Add(_sci)
            ResumeLayout()

            ConfigureEditor()
            CalibratePositions()
            ConfigureLexer()
            ConfigureMargins()
            ConfigureIndicators()

            AddHandler _sci.TextChanged, AddressOf OnSciTextChanged
            AddHandler _sci.SavePointLeft, Sub(sender As Object, e As EventArgs) RaiseEvent DirtyChanged(True)
            AddHandler _sci.SavePointReached, Sub(sender As Object, e As EventArgs) RaiseEvent DirtyChanged(False)
        End Sub

        ' ---- setup ----

        ''' <summary>
        ''' Scintilla itself counts UTF-8 bytes, but ScintillaNET converts to characters for most of its methods, and which
        ''' one a given version uses is easy to get wrong. So it is measured, once, with a character that takes two bytes:
        ''' the control's own length tells which unit its positions use.
        ''' </summary>
        Private Sub CalibratePositions()
            _sci.Text = ChrW(&HEB)
            _usesBytePositions = _sci.TextLength > 1
            _sci.Text = String.Empty
            _sci.EmptyUndoBuffer()
            _sci.SetSavePoint()
        End Sub

        ''' <summary>How many position units the text spans in this control: bytes or characters, as measured.</summary>
        Private Function Units(value As String) As Integer
            Return If(_usesBytePositions, Encoding.UTF8.GetByteCount(value), value.Length)
        End Function


        Private Sub ConfigureEditor()
            _sci.Technology = Technology.DirectWrite
            _sci.EolMode = Eol.Lf
            _sci.WrapMode = WrapMode.Word
            _sci.TabWidth = 4
            _sci.UseTabs = False
            ' Opaque, because the alpha channel is not applied by this control. A pale cyan keeps text readable.
            _sci.CaretLineBackColor = Color.FromArgb(255, 226, 243, 250)

            _sci.StyleResetDefault()
            _sci.Styles(Style.[Default]).Font = "Consolas"
            _sci.Styles(Style.[Default]).Size = 11
            _sci.StyleClearAll()

            ' Paste is done by the form so that clipboard text is sanitized first.
            _sci.ClearCmdKey(Keys.Control Or Keys.V)
            _sci.ClearCmdKey(Keys.Control Or Keys.Shift Or Keys.V)
            _sci.ClearCmdKey(Keys.Shift Or Keys.Insert)
        End Sub

        Private Sub ConfigureLexer()
            _sci.LexerName = "xml"
            _sci.Styles(StyleTag).ForeColor = Color.FromArgb(0, 0, 160)
            _sci.Styles(StyleTagUnknown).ForeColor = Color.FromArgb(0, 0, 160)
            _sci.Styles(StyleTagEnd).ForeColor = Color.FromArgb(0, 0, 160)
            _sci.Styles(StyleAttribute).ForeColor = Color.FromArgb(150, 60, 0)
            _sci.Styles(StyleAttributeUnknown).ForeColor = Color.FromArgb(150, 60, 0)
            _sci.Styles(StyleDoubleString).ForeColor = Color.FromArgb(20, 120, 20)
            _sci.Styles(StyleSingleString).ForeColor = Color.FromArgb(20, 120, 20)
            _sci.Styles(StyleNumber).ForeColor = Color.FromArgb(20, 120, 20)
            _sci.Styles(StyleEntity).ForeColor = Color.FromArgb(160, 0, 160)
            _sci.Styles(StyleComment).ForeColor = Color.Gray
            _sci.Styles(StyleCData).ForeColor = Color.Gray
            _sci.Styles(StyleXmlStart).ForeColor = Color.Gray
            _sci.Styles(StyleXmlEnd).ForeColor = Color.Gray
            _sci.Styles(StyleOther).ForeColor = Color.Gray
        End Sub

        Private Sub ConfigureMargins()
            _sci.Margins(0).Type = MarginType.Number
            _sci.Margins(1).Width = 0
            _sci.Margins(2).Width = 0
            _sci.Styles(Style.LineNumber).ForeColor = Color.Gray
            _sci.Styles(Style.LineNumber).BackColor = Color.FromArgb(245, 245, 245)
            UpdateLineNumberWidth()
        End Sub

        Private Sub ConfigureIndicators()
            _sci.Indicators(ErrorIndicator).Style = IndicatorStyle.Squiggle
            _sci.Indicators(ErrorIndicator).ForeColor = Color.Red
            _sci.Indicators(CauseIndicator).Style = IndicatorStyle.Squiggle
            _sci.Indicators(CauseIndicator).ForeColor = Color.DarkOrange
            _sci.Indicators(FlagIndicator).Style = IndicatorStyle.StraightBox
            _sci.Indicators(FlagIndicator).ForeColor = Color.Gold
            _sci.Indicators(FlagIndicator).Alpha = 90
            _sci.Indicators(FlagIndicator).Under = True
        End Sub

        ' ---- text ----

        ''' <summary>The whole document as .NET text with LF line endings.</summary>
        Public ReadOnly Property DocumentText As String
            Get
                Return _sci.Text
            End Get
        End Property

        ''' <summary>Replaces the document without counting as an edit: clears undo, marks the text as saved
        ''' and clears all marks. Use for New, Open, Import and Restore.</summary>
        Public Sub LoadText(newText As String)
            _loading = True
            Try
                Dim wasReadOnly As Boolean = _sci.ReadOnly
                _sci.ReadOnly = False
                _sci.Text = If(newText, String.Empty)
                _sci.EmptyUndoBuffer()
                _sci.SetSavePoint()
                ClearMarks()
                _sci.ReadOnly = wasReadOnly
            Finally
                _loading = False
            End Try
            UpdateLineNumberWidth()
        End Sub

        ''' <summary>The current text was written to disk: it becomes the save point.</summary>
        Public Sub MarkSaved()
            _sci.SetSavePoint()
        End Sub

        ''' <summary>Typing and the edit commands work; False while locked.</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property IsEditable As Boolean
            Get
                Return Not _sci.ReadOnly
            End Get
            Set(value As Boolean)
                _sci.ReadOnly = Not value
            End Set
        End Property

        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property WordWrap As Boolean
            Get
                Return _sci.WrapMode <> WrapMode.None
            End Get
            Set(value As Boolean)
                _sci.WrapMode = If(value, WrapMode.Word, WrapMode.None)
            End Set
        End Property

        ''' <summary>DirectWrite rendering (on by default); turn off if the text looks wrong on a machine.</summary>
        <Browsable(False), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)>
        Public Property UseDirectWrite As Boolean
            Get
                Return _sci.Technology = Technology.DirectWrite
            End Get
            Set(value As Boolean)
                _sci.Technology = If(value, Technology.DirectWrite, Technology.[Default])
            End Set
        End Property

        ''' <summary>Replaces Scintilla's built-in right-click menu (whose Paste would bypass the sanitizing paste)
        ''' with the app's own menu.</summary>
        Public Sub UseContextMenu(menu As ContextMenuStrip)
            _sci.UsePopup(PopupMode.Never)
            _sci.ContextMenuStrip = menu
        End Sub

        Public ReadOnly Property LineCount As Integer
            Get
                Return _sci.Lines.Count
            End Get
        End Property

        ' ---- editing commands (the form's menu calls these) ----

        Public Sub UndoEdit()
            If _sci.CanUndo Then _sci.Undo()
        End Sub

        Public Sub RedoEdit()
            If _sci.CanRedo Then _sci.Redo()
        End Sub

        Public Sub CutSelection()
            If IsEditable Then _sci.Cut()
        End Sub

        Public Sub CopySelection()
            _sci.Copy()
        End Sub

        Public Sub SelectEverything()
            _sci.SelectAll()
        End Sub

        ''' <summary>Inserts text at the caret, replacing any selection, as one undo step.</summary>
        Public Sub InsertAtCaret(insertText As String)
            If Not IsEditable OrElse String.IsNullOrEmpty(insertText) Then Return
            _sci.BeginUndoAction()
            Try
                _sci.ReplaceSelection(insertText)
            Finally
                _sci.EndUndoAction()
            End Try
        End Sub

        ''' <summary>The selected text, or an empty string.</summary>
        Public ReadOnly Property SelectedText As String
            Get
                Return _sci.SelectedText
            End Get
        End Property

        ''' <summary>Replaces the selection with the given text, as one undo step.</summary>
        Public Sub ReplaceSelectionText(replacement As String)
            InsertAtCaret(replacement)
        End Sub

        ''' <summary>Applies edits as one undo step. Give them last first (as HebrewOrder does) so offsets stay valid.
        ''' Offsets are character offsets in <see cref="DocumentText"/>; the marks and caret elsewhere are undisturbed.
        ''' The result is checked against expectedText; if it differs, everything is undone and False is returned, so a
        ''' wrongly placed edit can never stay in the document.</summary>
        Public Function ApplyEdits(edits As IEnumerable(Of TextEdit), expectedText As String) As Boolean
            If Not IsEditable OrElse edits Is Nothing Then Return False
            Dim snapshot As String = _sci.Text
            _sci.BeginUndoAction()
            Try
                For Each edit As TextEdit In edits
                    Dim startPos As Integer = Units(snapshot.Substring(0, edit.Start))
                    Dim endPos As Integer = startPos + Units(snapshot.Substring(edit.Start, edit.Length))
                    _sci.SetTargetRange(startPos, endPos)
                    _sci.ReplaceTarget(edit.NewText)
                Next
            Finally
                _sci.EndUndoAction()
            End Try
            If String.Equals(_sci.Text, expectedText, StringComparison.Ordinal) Then Return True
            _sci.Undo()
            Return False
        End Function

        ''' <summary>Replaces the whole document as one undo step, so one Undo brings the old text back.</summary>
        Public Sub ReplaceAllText(newText As String)
            If Not IsEditable Then Return
            _sci.BeginUndoAction()
            Try
                _sci.SelectAll()
                _sci.ReplaceSelection(If(newText, String.Empty))
            Finally
                _sci.EndUndoAction()
            End Try
        End Sub

        ' ---- marks ----

        ''' <summary>Removes the error, cause and flag marks.</summary>
        Public Sub ClearMarks()
            For Each indicator As Integer In {ErrorIndicator, CauseIndicator, FlagIndicator}
                _sci.IndicatorCurrent = indicator
                _sci.IndicatorClearRange(0, _sci.TextLength)
            Next
        End Sub

        ''' <summary>Draws (or clears) the squiggles for a check result. Only marks; never edits.</summary>
        Public Sub ShowCheck(result As CheckResult)
            _sci.IndicatorCurrent = ErrorIndicator
            _sci.IndicatorClearRange(0, _sci.TextLength)
            _sci.IndicatorCurrent = CauseIndicator
            _sci.IndicatorClearRange(0, _sci.TextLength)
            If result Is Nothing OrElse result.IsWellFormed Then Return

            Dim errorLine As Integer = Math.Min(Math.Max(1, result.Line), _sci.Lines.Count)
            Dim lineText As String = LineTextWithoutEol(errorLine)
            ' An error reported on an empty line (typically at the very end) is marked on the last text before it.
            While lineText.Length = 0 AndAlso errorLine > 1
                errorLine -= 1
                lineText = LineTextWithoutEol(errorLine)
            End While
            If lineText.Length > 0 Then
                Dim startIndex As Integer = Math.Min(Math.Max(0, result.Column - 1), lineText.Length - 1)
                Dim endIndex As Integer = startIndex + 1
                While endIndex < lineText.Length AndAlso Not IsTokenBoundary(lineText(endIndex))
                    endIndex += 1
                End While
                MarkRange(ErrorIndicator, errorLine, startIndex + 1, endIndex - startIndex)
            End If

            If result.HasCause Then
                ' The cause position is the element's name; include the "<" before it.
                MarkRange(CauseIndicator, result.CauseLine, Math.Max(1, result.CauseColumn - 1), result.CauseElement.Length + 1)
            End If
        End Sub

        ''' <summary>Highlights places to review (wide gaps, line-end hyphens). They follow the text as it is edited.</summary>
        Public Sub ShowFlags(flags As IEnumerable(Of ImportFlag))
            _sci.IndicatorCurrent = FlagIndicator
            _sci.IndicatorClearRange(0, _sci.TextLength)
            If flags Is Nothing Then Return
            For Each flag As ImportFlag In flags
                MarkRange(FlagIndicator, flag.Line, flag.Column, flag.Length)
            Next
        End Sub

        ''' <summary>Moves the caret to a line and column (1-based) and scrolls it into view.</summary>
        Public Sub GoToLineColumn(line As Integer, column As Integer)
            Dim startPos As Integer
            Dim byteLength As Integer
            If Not TryRange(line, column, 0, startPos, byteLength) Then Return
            _sci.GotoPosition(startPos)
            _sci.ScrollCaret()
            _sci.Focus()
        End Sub

        ' ---- internals ----

        Private Sub OnSciTextChanged(sender As Object, e As EventArgs)
            UpdateLineNumberWidth()
            If Not _loading Then RaiseEvent ContentChanged()
        End Sub

        Private Sub UpdateLineNumberWidth()
            Dim digits As Integer = Math.Max(3, _sci.Lines.Count.ToString(Globalization.CultureInfo.InvariantCulture).Length)
            _sci.Margins(0).Width = _sci.TextWidth(Style.LineNumber, New String("9"c, digits)) + 8
        End Sub

        Private Function LineTextWithoutEol(line As Integer) As String
            Return _sci.Lines(line - 1).Text.TrimEnd(ChrW(10), ChrW(13))
        End Function

        Private Shared Function IsTokenBoundary(c As Char) As Boolean
            Return Char.IsWhiteSpace(c) OrElse c = "<"c OrElse c = ">"c
        End Function

        Private Sub MarkRange(indicator As Integer, line As Integer, column As Integer, length As Integer)
            Dim startPos As Integer
            Dim byteLength As Integer
            If Not TryRange(line, column, length, startPos, byteLength) OrElse byteLength <= 0 Then Return
            _sci.IndicatorCurrent = indicator
            _sci.IndicatorFillRange(startPos, byteLength)
        End Sub

        ''' <summary>Converts a 1-based line and column and a length in characters to the control's own position and
        ''' length (see <see cref="Units"/>). Out-of-range input is clamped.</summary>
        Private Function TryRange(line As Integer, column As Integer, length As Integer,
                                  ByRef startPos As Integer, ByRef byteLength As Integer) As Boolean
            If line < 1 OrElse _sci.Lines.Count = 0 Then Return False
            Dim target As Integer = Math.Min(line, _sci.Lines.Count)
            Dim scintillaLine As Line = _sci.Lines(target - 1)
            Dim lineText As String = scintillaLine.Text
            Dim startIndex As Integer = Math.Max(0, Math.Min(column - 1, lineText.Length))
            Dim count As Integer = Math.Max(0, Math.Min(length, lineText.Length - startIndex))
            startPos = scintillaLine.Position + Units(lineText.Substring(0, startIndex))
            byteLength = Units(lineText.Substring(startIndex, count))
            Return True
        End Function

    End Class

End Namespace