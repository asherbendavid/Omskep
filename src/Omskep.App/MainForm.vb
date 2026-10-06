Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports Omskep.Core.Documents
Imports Omskep.Core.Import
Imports Omskep.Core.Session

''' <summary>
''' The main window. It holds no decisions: MainFormState and DocumentFormState say what is enabled and what to show,
''' AppSession does the access work, DocumentModel tracks the document, and the Core classes do the cleaning and checking.
''' This class only moves values between them and the controls.
''' </summary>
Public Class MainForm

    Private Const OpenFilter As String =
        "Documents (*.ssml;*.xml;*.txt;*.pdf)|*.ssml;*.xml;*.txt;*.pdf|" &
        "SSML documents (*.ssml;*.xml)|*.ssml;*.xml|Study PDFs (*.pdf)|*.pdf|Text files (*.txt)|*.txt|All files (*.*)|*.*"
    Private Const SaveFilter As String = "SSML documents (*.ssml)|*.ssml|All files (*.*)|*.*"

    Private ReadOnly _session As AppSession
    Private ReadOnly _closing As New CancellationTokenSource()
    Private ReadOnly _model As New DocumentModel()
    Private ReadOnly _recovery As New RecoveryStore(RecoveryStore.DefaultFolder())
    Private ReadOnly _importer As New DocumentImporter(New PdfPigPageReader())
    Private ReadOnly _editOnlyItems As New List(Of ToolStripItem)()

    Private _billableCount As Integer
    Private _lastSnapshotRevision As Integer = -1
    Private _busy As Boolean
    Private _keepRecovery As Boolean
    Private _startupDone As Boolean
    Private _checkFull As String = String.Empty
    Private _noteFull As String = String.Empty

    ''' <summary>For the Visual Studio designer only.</summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(session As AppSession)
        Me.New()
        _session = session
        BuildEditorContextMenu()
        mnuWordWrap.Checked = _session.WordWrapPreference()
        ssmlEditor.WordWrap = mnuWordWrap.Checked
        AddHandler _session.StateChanged, AddressOf OnSessionStateChanged
        ApplyState()
    End Sub

    ' ---- state -> controls ----

    Private Sub OnSessionStateChanged(sender As Object, e As EventArgs)
        If IsDisposed Then Return
        If InvokeRequired Then
            If IsHandleCreated Then BeginInvoke(New Action(AddressOf ApplyState))
        Else
            ApplyState()
        End If
    End Sub

    Private Sub ApplyState()
        If _session Is Nothing OrElse IsDisposed Then Return
        Dim st = MainFormState.From(_session.Access, _session.IsBusy, _session.CanRetry, _session.VoiceCount)
        Dim ds = DocumentFormState.From(_session.Access, _model, _billableCount)
        Dim canStart As Boolean = ds.NewOpenPasteEnabled AndAlso Not _busy
        Dim hasError As Boolean = _model.LastCheck IsNot Nothing AndAlso Not _model.LastCheck.IsWellFormed

        mnuNew.Enabled = canStart
        mnuOpen.Enabled = canStart
        mnuSave.Enabled = ds.SaveEnabled AndAlso Not _busy
        mnuSaveAs.Enabled = ds.SaveAsEnabled AndAlso Not _busy
        mnuExport.Enabled = ds.ExportEnabled AndAlso Not _busy
        mnuExport.ToolTipText = ds.ExportBlockedReason
        mnuSettings.Enabled = True

        For Each item In _editOnlyItems
            item.Enabled = canStart
        Next
        mnuUndo.Enabled = canStart
        mnuRedo.Enabled = canStart
        mnuCut.Enabled = canStart
        mnuPaste.Enabled = canStart
        mnuPastePlain.Enabled = canStart
        mnuGoToError.Enabled = hasError
        mnuGoToCause.Enabled = hasError AndAlso _model.LastCheck.HasCause

        ssmlEditor.IsEditable = ds.EditorEnabled
        Text = ds.WindowTitle
        _checkFull = ds.CheckText
        lblCount.Text = ds.CountText

        ' Until startup has read the settings the access state is only a default, so show no banner yet.
        pnlBanner.Visible = _startupDone AndAlso st.ShowBanner
        lblBanner.Text = st.BannerText
        btnRetry.Visible = st.ShowRetry
        btnRetry.Enabled = st.RetryEnabled
        lblStatus.Text = If(_startupDone, st.StatusText, "Starting...")
        FitStatusBar()
    End Sub

    Private Sub ShowNote(message As String)
        _noteFull = message
        FitStatusBar()
        tmrNote.Stop()
        tmrNote.Start()
    End Sub

    Private Sub tmrNote_Tick(sender As Object, e As EventArgs) Handles tmrNote.Tick
        tmrNote.Stop()
        _noteFull = String.Empty
        FitStatusBar()
    End Sub

    Private Sub statusMain_SizeChanged(sender As Object, e As EventArgs) Handles statusMain.SizeChanged
        FitStatusBar()
    End Sub

    ''' <summary>
    ''' Shortens the note and the check text so the status bar never overflows when the window is narrow.
    ''' The full text stays available as the tooltip.
    ''' </summary>
    Private Sub FitStatusBar()
        Dim fixedWidth As Integer = StatusTextWidth(lblStatus.Text) + StatusTextWidth(lblCount.Text) + 70
        Dim budget As Integer = Math.Max(0, statusMain.ClientSize.Width - fixedWidth)

        lblNote.Text = Shorten(_noteFull, budget \ 3)
        lblNote.ToolTipText = _noteFull
        Dim noteUsed As Integer = If(lblNote.Text.Length = 0, 0, StatusTextWidth(lblNote.Text) + 12)

        lblCheck.Text = Shorten(_checkFull, budget - noteUsed)
        lblCheck.ToolTipText = _checkFull
    End Sub

    Private Function StatusTextWidth(value As String) As Integer
        If String.IsNullOrEmpty(value) Then Return 0
        Return TextRenderer.MeasureText(value, statusMain.Font).Width
    End Function

    Private Function Shorten(value As String, maxWidth As Integer) As String
        If String.IsNullOrEmpty(value) OrElse maxWidth <= 0 Then Return String.Empty
        If StatusTextWidth(value) <= maxWidth Then Return value
        Dim low As Integer = 0
        Dim high As Integer = value.Length
        While low < high
            Dim middle As Integer = (low + high + 1) \ 2
            If StatusTextWidth(value.Substring(0, middle) & ChrW(&H2026)) <= maxWidth Then
                low = middle
            Else
                high = middle - 1
            End If
        End While
        Return If(low = 0, String.Empty, value.Substring(0, low) & ChrW(&H2026))
    End Function

    Private Sub ShowError(message As String)
        MessageBox.Show(Me, message, "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    ''' <summary>The voice written into new documents: the saved Afrikaans default, or the next best, or the built-in voice.</summary>
    Private Function Scaffold() As ScaffoldChoice
        Return ScaffoldDefaults.Choose(_session.DefaultVoices())
    End Function

    ' ---- startup / shutdown ----

    Private Async Sub MainForm_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        StartNewDocument()
        OfferRecovery()
        tmrAutosave.Start()
        Try
            Await _session.StartupAsync(_closing.Token)
        Catch ex As OperationCanceledException
            ' The window was closed while the startup fetch was running.
        End Try
        If IsDisposed Then Return
        _startupDone = True
        ApplyState()
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If _busy Then
            e.Cancel = True
            ShowNote("Please wait for the import to finish.")
            Return
        End If

        If _model.HasUnsavedChanges Then
            If e.CloseReason = CloseReason.WindowsShutDown OrElse e.CloseReason = CloseReason.TaskManagerClosing Then
                ' No time to ask: keep a recovery copy, which is offered at the next start.
                _recovery.Snapshot(ssmlEditor.DocumentText, _model.FilePath, _model.DisplayName)
                _keepRecovery = True
            ElseIf Not ConfirmDiscardChanges() Then
                e.Cancel = True
                Return
            End If
        End If
        _closing.Cancel()
    End Sub

    Private Sub MainForm_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If Not _keepRecovery Then _recovery.Discard()
        If _session IsNot Nothing Then RemoveHandler _session.StateChanged, AddressOf OnSessionStateChanged
        _closing.Dispose()
    End Sub

    ' ---- banner buttons ----

    Private Async Sub btnRetry_Click(sender As Object, e As EventArgs) Handles btnRetry.Click
        Try
            Await _session.RetryAsync(_closing.Token)
        Catch ex As OperationCanceledException
            ' Closing.
        End Try
        ApplyState()
    End Sub

    Private Sub btnOpenSettings_Click(sender As Object, e As EventArgs) Handles btnOpenSettings.Click
        ShowSettings()
    End Sub

    ' ---- editor events ----

    Private Sub ssmlEditor_ContentChanged() Handles ssmlEditor.ContentChanged
        _model.Edited()
        tmrCheck.Stop()
        tmrCheck.Start()
        ApplyState()
    End Sub

    Private Sub ssmlEditor_DirtyChanged(isDirty As Boolean) Handles ssmlEditor.DirtyChanged
        If Not isDirty Then
            _model.ReachedSavePoint()
            ApplyState()
        End If
    End Sub

    Private Sub tmrCheck_Tick(sender As Object, e As EventArgs) Handles tmrCheck.Tick
        RunCheckNow()
    End Sub

    ''' <summary>Checks the text as it is now. A result is applied only if the text has not changed meanwhile.</summary>
    Private Sub RunCheckNow()
        tmrCheck.Stop()
        Dim documentText As String = ssmlEditor.DocumentText
        Dim revision As Integer = _model.Revision
        Dim result As CheckResult = WellFormednessChecker.Check(documentText)
        If _model.CheckCompleted(revision, result) Then ssmlEditor.ShowCheck(result)
        _billableCount = BillableCharacterCounter.CountUpperBound(documentText)
        ApplyState()
    End Sub

    ' ---- autosave ----

    Private Sub tmrAutosave_Tick(sender As Object, e As EventArgs) Handles tmrAutosave.Tick
        If Not _model.HasUnsavedChanges OrElse _model.Revision = _lastSnapshotRevision Then Return
        If _recovery.Snapshot(ssmlEditor.DocumentText, _model.FilePath, _model.DisplayName) Then
            _lastSnapshotRevision = _model.Revision
        Else
            ShowNote("Autosave failed. Save your work to a file to be safe.")
        End If
    End Sub

    Private Sub OfferRecovery()
        Dim orphans As IReadOnlyList(Of RecoveryItem) = _recovery.FindOrphans()
        For i As Integer = orphans.Count - 1 To 0 Step -1
            If _model.HasUnsavedChanges Then
                ShowNote("Other recovered documents are kept and will be offered again later.")
                Exit For
            End If
            Dim item As RecoveryItem = orphans(i)
            Dim answer As DialogResult = MessageBox.Show(Me,
                "Omskep found unsaved work from an earlier session:" & vbCrLf & item.DisplayName &
                " (" & item.SavedUtc.ToLocalTime().ToString("g") & ")" & vbCrLf & vbCrLf &
                "Yes: restore it" & vbCrLf & "No: discard it" & vbCrLf & "Cancel: decide next time",
                "Omskep", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            Select Case answer
                Case DialogResult.Yes
                    RestoreFrom(item)
                Case DialogResult.No
                    _recovery.Remove(item)
            End Select
        Next
    End Sub

    Private Sub RestoreFrom(item As RecoveryItem)
        ssmlEditor.LoadText(item.Text)
        If item.OriginalPath IsNot Nothing Then
            _model.OpenedFile(item.OriginalPath)
            _model.Edited()
        Else
            _model.Imported()
        End If
        ' Write our own copy first; only then is the old one removed, so there is never a moment with no copy.
        If _recovery.Snapshot(item.Text, _model.FilePath, _model.DisplayName) Then
            _lastSnapshotRevision = _model.Revision
            _recovery.Remove(item)
        Else
            ShowNote("Restored, but a new recovery copy could not be written yet; the old copy is kept.")
        End If
        RunCheckNow()
    End Sub

    ' ---- File menu ----

    Private Sub mnuNew_Click(sender As Object, e As EventArgs) Handles mnuNew.Click
        If _busy OrElse Not ConfirmDiscardChanges() Then Return
        StartNewDocument()
    End Sub

    Private Sub StartNewDocument()
        Dim choice As ScaffoldChoice = Scaffold()
        ssmlEditor.LoadText(DocumentComposer.Compose(String.Empty, choice.Locale, choice.VoiceShortName))
        _model.NewDocument()
        ssmlEditor.GoToLineColumn(DocumentScaffold.BodyStartLine, 1)
        AfterContentReplaced()
    End Sub

    Private Async Sub mnuOpen_Click(sender As Object, e As EventArgs) Handles mnuOpen.Click
        If _busy OrElse Not ConfirmDiscardChanges() Then Return
        Using dialog As New OpenFileDialog() With {.Title = "Open", .Filter = OpenFilter, .CheckFileExists = True}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Await OpenPathAsync(dialog.FileName)
        End Using
    End Sub

    Private Async Function OpenPathAsync(path As String) As Task
        Select Case FileKinds.Classify(path)
            Case FileKind.Pdf
                Await ImportPdfAsync(path)
            Case FileKind.Ssml
                Dim ssmlText As String = TryLoad(path)
                If ssmlText IsNot Nothing Then OpenLoadedSsml(path, ssmlText)
            Case Else
                Dim fileText As String = TryLoad(path)
                If fileText Is Nothing Then Return
                If FileKinds.LooksLikeSsml(fileText) Then
                    OpenLoadedSsml(path, fileText)
                Else
                    Dim choice As ScaffoldChoice = Scaffold()
                    ApplyImport(_importer.ImportText(fileText, choice.Locale, choice.VoiceShortName))
                End If
        End Select
    End Function

    Private Function TryLoad(path As String) As String
        Try
            Return DocumentFile.Load(path)
        Catch ex As DocumentFileException
            ShowError(ex.Message)
            Return Nothing
        End Try
    End Function

    Private Sub OpenLoadedSsml(path As String, fileText As String)
        ssmlEditor.LoadText(fileText)
        _model.OpenedFile(path)
        AfterContentReplaced()
    End Sub

    Private Async Function ImportPdfAsync(path As String) As Task
        Dim choice As ScaffoldChoice = Scaffold()
        Dim result As ImportResult = Nothing
        SetBusy(True)
        Try
            result = Await Task.Run(Function() _importer.ImportPdf(path, choice.Locale, choice.VoiceShortName))
        Finally
            SetBusy(False)
        End Try
        If IsDisposed Then Return
        ApplyImport(result)
    End Function

    Private Sub SetBusy(value As Boolean)
        _busy = value
        UseWaitCursor = value
        ApplyState()
    End Sub

    ''' <summary>Puts a successful import in the editor as a new unsaved document; tells the user about a failed one.</summary>
    Private Sub ApplyImport(result As ImportResult)
        If Not result.Succeeded Then
            ShowError(result.Message)
            Return
        End If
        ssmlEditor.LoadText(result.Document)
        ssmlEditor.ShowFlags(result.Flags)
        _model.Imported()
        AfterContentReplaced()
        MessageBox.Show(Me, result.Message, "Omskep", MessageBoxButtons.OK,
                        If(result.Outcome = ImportOutcome.ImportedWithEmptyPages, MessageBoxIcon.Warning, MessageBoxIcon.Information))
    End Sub

    ''' <summary>Common tail of New, Open, Import and Restore.</summary>
    Private Sub AfterContentReplaced()
        _recovery.Discard()
        _lastSnapshotRevision = -1
        RunCheckNow()
        ssmlEditor.Focus()
    End Sub

    Private Sub mnuSave_Click(sender As Object, e As EventArgs) Handles mnuSave.Click
        SaveDocument()
    End Sub

    Private Sub mnuSaveAs_Click(sender As Object, e As EventArgs) Handles mnuSaveAs.Click
        SaveDocumentAs()
    End Sub

    Private Function SaveDocument() As Boolean
        If _model.FilePath Is Nothing Then Return SaveDocumentAs()
        Return WriteTo(_model.FilePath)
    End Function

    Private Function SaveDocumentAs() As Boolean
        Using dialog As New SaveFileDialog() With {
            .Title = "Save As",
            .Filter = SaveFilter,
            .DefaultExt = "ssml",
            .AddExtension = True,
            .OverwritePrompt = True,
            .FileName = If(_model.FilePath Is Nothing, "Untitled.ssml", _model.DisplayName)
        }
            If _model.FilePath IsNot Nothing Then
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(_model.FilePath)
            End If
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return False
            Return WriteTo(dialog.FileName)
        End Using
    End Function

    Private Function WriteTo(path As String) As Boolean
        Try
            DocumentFile.Save(path, ssmlEditor.DocumentText)
        Catch ex As DocumentFileException
            ShowError(ex.Message)
            Return False
        End Try
        _model.Saved(path)
        ssmlEditor.MarkSaved()
        _recovery.Discard()
        _lastSnapshotRevision = -1
        ShowNote("Saved " & _model.DisplayName & ".")
        ApplyState()
        Return True
    End Function

    ''' <summary>False when the user cancelled or a needed save failed; True when it is safe to replace the document.</summary>
    Private Function ConfirmDiscardChanges() As Boolean
        If Not _model.HasUnsavedChanges Then Return True
        Dim answer As DialogResult = MessageBox.Show(Me, "Save changes to " & _model.DisplayName & "?", "Omskep",
                                                     MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
        Select Case answer
            Case DialogResult.Yes
                Return SaveDocument()
            Case DialogResult.No
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub mnuExit_Click(sender As Object, e As EventArgs) Handles mnuExit.Click
        Close()
    End Sub

    ' PLACEHOLDER (phase 3): export is not implemented yet. The button is already gated on a well-formed document.
    Private Sub mnuExport_Click(sender As Object, e As EventArgs) Handles mnuExport.Click
        MessageBox.Show(Me, "Exporting audio arrives in phase 3.", "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' ---- Edit menu ----

    Private Sub mnuUndo_Click(sender As Object, e As EventArgs) Handles mnuUndo.Click
        ssmlEditor.UndoEdit()
    End Sub

    Private Sub mnuRedo_Click(sender As Object, e As EventArgs) Handles mnuRedo.Click
        ssmlEditor.RedoEdit()
    End Sub

    Private Sub mnuCut_Click(sender As Object, e As EventArgs) Handles mnuCut.Click
        ssmlEditor.CutSelection()
    End Sub

    Private Sub mnuCopy_Click(sender As Object, e As EventArgs) Handles mnuCopy.Click
        ssmlEditor.CopySelection()
    End Sub

    Private Sub mnuPaste_Click(sender As Object, e As EventArgs) Handles mnuPaste.Click
        DoPaste(False)
    End Sub

    Private Sub mnuPastePlain_Click(sender As Object, e As EventArgs) Handles mnuPastePlain.Click
        DoPaste(True)
    End Sub

    Private Sub mnuSelectAll_Click(sender As Object, e As EventArgs) Handles mnuSelectAll.Click
        ssmlEditor.SelectEverything()
    End Sub

    Private Sub mnuGoToError_Click(sender As Object, e As EventArgs) Handles mnuGoToError.Click
        Dim check As CheckResult = _model.LastCheck
        If check IsNot Nothing AndAlso Not check.IsWellFormed Then ssmlEditor.GoToLineColumn(check.Line, check.Column)
    End Sub

    Private Sub mnuGoToCause_Click(sender As Object, e As EventArgs) Handles mnuGoToCause.Click
        Dim check As CheckResult = _model.LastCheck
        If check IsNot Nothing AndAlso check.HasCause Then ssmlEditor.GoToLineColumn(check.CauseLine, check.CauseColumn)
    End Sub

    Private Sub mnuWordWrap_Click(sender As Object, e As EventArgs) Handles mnuWordWrap.Click
        ssmlEditor.WordWrap = mnuWordWrap.Checked
        _session.SetWordWrapPreference(mnuWordWrap.Checked)
    End Sub

    ''' <summary>Paste (sanitizing) or Paste as plain text (raw). PasteHandler decides; this only reads the clipboard
    ''' and applies the plan.</summary>
    Private Sub DoPaste(plainText As Boolean)
        If _busy OrElse Not ssmlEditor.IsEditable Then Return
        Dim choice As ScaffoldChoice = Scaffold()
        Dim plan As PastePlan = PasteHandler.Plan(ssmlEditor.DocumentText, ReadClipboardText(), choice.Locale, choice.VoiceShortName, plainText)
        Select Case plan.Kind
            Case PasteKind.ReplaceDocument
                ssmlEditor.ReplaceAllText(plan.Text)
                ssmlEditor.ShowFlags(ImportFlagFinder.Find(ssmlEditor.DocumentText))
            Case PasteKind.InsertText
                ssmlEditor.InsertAtCaret(plan.Text)
                If Not plainText Then ssmlEditor.ShowFlags(ImportFlagFinder.Find(ssmlEditor.DocumentText))
        End Select
        ShowNote(plan.Message)
    End Sub

    Private Shared Function ReadClipboardText() As String
        Try
            Return If(Clipboard.ContainsText(), Clipboard.GetText(), Nothing)
        Catch ex As ExternalException
            ' Another program is holding the clipboard.
            Return Nothing
        End Try
    End Function

    ''' <summary>The editor's right-click menu: the same commands as the Edit menu, so Paste always sanitizes.</summary>
    Private Sub BuildEditorContextMenu()
        Dim menu As New ContextMenuStrip()
        _editOnlyItems.Add(menu.Items.Add("&Undo", Nothing, Sub(sender, e) ssmlEditor.UndoEdit()))
        _editOnlyItems.Add(menu.Items.Add("&Redo", Nothing, Sub(sender, e) ssmlEditor.RedoEdit()))
        menu.Items.Add(New ToolStripSeparator())
        _editOnlyItems.Add(menu.Items.Add("Cu&t", Nothing, Sub(sender, e) ssmlEditor.CutSelection()))
        menu.Items.Add("&Copy", Nothing, Sub(sender, e) ssmlEditor.CopySelection())
        _editOnlyItems.Add(menu.Items.Add("&Paste", Nothing, Sub(sender, e) DoPaste(False)))
        _editOnlyItems.Add(menu.Items.Add("Paste as plain &text", Nothing, Sub(sender, e) DoPaste(True)))
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Select &all", Nothing, Sub(sender, e) ssmlEditor.SelectEverything())
        ssmlEditor.UseContextMenu(menu)
    End Sub

    ' ---- Tools menu ----

    Private Sub mnuSettings_Click(sender As Object, e As EventArgs) Handles mnuSettings.Click
        ShowSettings()
    End Sub

    Private Sub ShowSettings()
        Using dialog As New SettingsForm(_session)
            dialog.ShowDialog(Me)
        End Using
        ApplyState()
    End Sub

End Class
