<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class MainForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New System.ComponentModel.Container()
        mnuMain = New MenuStrip()
        mnuFile = New ToolStripMenuItem()
        mnuNew = New ToolStripMenuItem()
        mnuOpen = New ToolStripMenuItem()
        mnuSave = New ToolStripMenuItem()
        mnuSaveAs = New ToolStripMenuItem()
        sepFile = New ToolStripSeparator()
        mnuExport = New ToolStripMenuItem()
        sepFile2 = New ToolStripSeparator()
        mnuExit = New ToolStripMenuItem()
        mnuEdit = New ToolStripMenuItem()
        mnuUndo = New ToolStripMenuItem()
        mnuRedo = New ToolStripMenuItem()
        sepEdit1 = New ToolStripSeparator()
        mnuCut = New ToolStripMenuItem()
        mnuCopy = New ToolStripMenuItem()
        mnuPaste = New ToolStripMenuItem()
        mnuPastePlain = New ToolStripMenuItem()
        sepEdit2 = New ToolStripSeparator()
        mnuSelectAll = New ToolStripMenuItem()
        sepEdit3 = New ToolStripSeparator()
        mnuGoToError = New ToolStripMenuItem()
        mnuGoToCause = New ToolStripMenuItem()
        mnuView = New ToolStripMenuItem()
        mnuWordWrap = New ToolStripMenuItem()
        mnuTools = New ToolStripMenuItem()
        mnuSettings = New ToolStripMenuItem()
        pnlBanner = New Panel()
        lblBanner = New Label()
        flpBannerButtons = New FlowLayoutPanel()
        btnRetry = New Button()
        btnOpenSettings = New Button()
        ssmlEditor = New Editor.SsmlEditor()
        statusMain = New StatusStrip()
        lblStatus = New ToolStripStatusLabel()
        lblNote = New ToolStripStatusLabel()
        lblCheck = New ToolStripStatusLabel()
        lblCount = New ToolStripStatusLabel()
        tmrCheck = New System.Windows.Forms.Timer(components)
        tmrAutosave = New System.Windows.Forms.Timer(components)
        tmrNote = New System.Windows.Forms.Timer(components)
        mnuMain.SuspendLayout()
        pnlBanner.SuspendLayout()
        flpBannerButtons.SuspendLayout()
        statusMain.SuspendLayout()
        SuspendLayout()
        ' 
        ' mnuMain
        ' 
        mnuMain.Items.AddRange(New ToolStripItem() {mnuFile, mnuEdit, mnuView, mnuTools})
        mnuMain.Location = New Point(0, 0)
        mnuMain.Name = "mnuMain"
        mnuMain.Size = New Size(900, 24)
        mnuMain.TabIndex = 0
        ' 
        ' mnuFile
        ' 
        mnuFile.DropDownItems.AddRange(New ToolStripItem() {mnuNew, mnuOpen, mnuSave, mnuSaveAs, sepFile, mnuExport, sepFile2, mnuExit})
        mnuFile.Name = "mnuFile"
        mnuFile.Text = "&File"
        ' 
        ' mnuNew
        ' 
        mnuNew.Name = "mnuNew"
        mnuNew.ShortcutKeys = Keys.Control Or Keys.N
        mnuNew.Text = "&New"
        ' 
        ' mnuOpen
        ' 
        mnuOpen.Name = "mnuOpen"
        mnuOpen.ShortcutKeys = Keys.Control Or Keys.O
        mnuOpen.Text = "&Open..."
        ' 
        ' mnuSave
        ' 
        mnuSave.Name = "mnuSave"
        mnuSave.ShortcutKeys = Keys.Control Or Keys.S
        mnuSave.Text = "&Save"
        ' 
        ' mnuSaveAs
        ' 
        mnuSaveAs.Name = "mnuSaveAs"
        mnuSaveAs.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.S
        mnuSaveAs.Text = "Save &As..."
        ' 
        ' sepFile
        ' 
        sepFile.Name = "sepFile"
        ' 
        ' mnuExport
        ' 
        mnuExport.Name = "mnuExport"
        mnuExport.ShortcutKeys = Keys.Control Or Keys.E
        mnuExport.Text = "&Export..."
        ' 
        ' sepFile2
        ' 
        sepFile2.Name = "sepFile2"
        ' 
        ' mnuExit
        ' 
        mnuExit.Name = "mnuExit"
        mnuExit.Text = "E&xit"
        ' 
        ' mnuEdit
        ' 
        mnuEdit.DropDownItems.AddRange(New ToolStripItem() {mnuUndo, mnuRedo, sepEdit1, mnuCut, mnuCopy, mnuPaste, mnuPastePlain, sepEdit2, mnuSelectAll, sepEdit3, mnuGoToError, mnuGoToCause})
        mnuEdit.Name = "mnuEdit"
        mnuEdit.Text = "&Edit"
        ' 
        ' mnuUndo
        ' 
        mnuUndo.Name = "mnuUndo"
        mnuUndo.ShortcutKeys = Keys.Control Or Keys.Z
        mnuUndo.Text = "&Undo"
        ' 
        ' mnuRedo
        ' 
        mnuRedo.Name = "mnuRedo"
        mnuRedo.ShortcutKeys = Keys.Control Or Keys.Y
        mnuRedo.Text = "&Redo"
        ' 
        ' sepEdit1
        ' 
        sepEdit1.Name = "sepEdit1"
        ' 
        ' mnuCut
        ' 
        mnuCut.Name = "mnuCut"
        mnuCut.ShortcutKeys = Keys.Control Or Keys.X
        mnuCut.Text = "Cu&t"
        ' 
        ' mnuCopy
        ' 
        mnuCopy.Name = "mnuCopy"
        mnuCopy.ShortcutKeys = Keys.Control Or Keys.C
        mnuCopy.Text = "&Copy"
        ' 
        ' mnuPaste
        ' 
        mnuPaste.Name = "mnuPaste"
        mnuPaste.ShortcutKeys = Keys.Control Or Keys.V
        mnuPaste.Text = "&Paste"
        ' 
        ' mnuPastePlain
        ' 
        mnuPastePlain.Name = "mnuPastePlain"
        mnuPastePlain.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.V
        mnuPastePlain.Text = "Paste as plain &text"
        ' 
        ' sepEdit2
        ' 
        sepEdit2.Name = "sepEdit2"
        ' 
        ' mnuSelectAll
        ' 
        mnuSelectAll.Name = "mnuSelectAll"
        mnuSelectAll.ShortcutKeys = Keys.Control Or Keys.A
        mnuSelectAll.Text = "Select &all"
        ' 
        ' sepEdit3
        ' 
        sepEdit3.Name = "sepEdit3"
        ' 
        ' mnuGoToError
        ' 
        mnuGoToError.Name = "mnuGoToError"
        mnuGoToError.ShortcutKeys = Keys.F8
        mnuGoToError.Text = "Go to &error"
        ' 
        ' mnuGoToCause
        ' 
        mnuGoToCause.Name = "mnuGoToCause"
        mnuGoToCause.ShortcutKeys = Keys.Shift Or Keys.F8
        mnuGoToCause.Text = "Go to probable &cause"
        ' 
        ' mnuView
        ' 
        mnuView.DropDownItems.AddRange(New ToolStripItem() {mnuWordWrap})
        mnuView.Name = "mnuView"
        mnuView.Text = "&View"
        ' 
        ' mnuWordWrap
        ' 
        mnuWordWrap.Checked = True
        mnuWordWrap.CheckOnClick = True
        mnuWordWrap.CheckState = CheckState.Checked
        mnuWordWrap.Name = "mnuWordWrap"
        mnuWordWrap.Text = "&Word wrap"
        ' 
        ' mnuTools
        ' 
        mnuTools.DropDownItems.AddRange(New ToolStripItem() {mnuSettings})
        mnuTools.Name = "mnuTools"
        mnuTools.Text = "&Tools"
        ' 
        ' mnuSettings
        ' 
        mnuSettings.Name = "mnuSettings"
        mnuSettings.Text = "&Settings..."
        ' 
        ' pnlBanner
        ' 
        pnlBanner.BackColor = SystemColors.Info
        pnlBanner.Controls.Add(lblBanner)
        pnlBanner.Controls.Add(flpBannerButtons)
        pnlBanner.Dock = DockStyle.Top
        pnlBanner.Location = New Point(0, 24)
        pnlBanner.Name = "pnlBanner"
        pnlBanner.Padding = New Padding(10)
        pnlBanner.Size = New Size(900, 76)
        pnlBanner.TabIndex = 1
        pnlBanner.Visible = False
        ' 
        ' lblBanner
        ' 
        lblBanner.Dock = DockStyle.Fill
        lblBanner.ForeColor = SystemColors.InfoText
        lblBanner.Location = New Point(10, 10)
        lblBanner.Name = "lblBanner"
        lblBanner.Size = New Size(641, 56)
        lblBanner.TabIndex = 0
        lblBanner.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' flpBannerButtons
        ' 
        flpBannerButtons.AutoSize = True
        flpBannerButtons.Controls.Add(btnRetry)
        flpBannerButtons.Controls.Add(btnOpenSettings)
        flpBannerButtons.Dock = DockStyle.Right
        flpBannerButtons.Location = New Point(651, 10)
        flpBannerButtons.Name = "flpBannerButtons"
        flpBannerButtons.Size = New Size(239, 56)
        flpBannerButtons.TabIndex = 1
        flpBannerButtons.WrapContents = False
        ' 
        ' btnRetry
        ' 
        btnRetry.AutoSize = True
        btnRetry.Name = "btnRetry"
        btnRetry.TabIndex = 0
        btnRetry.Text = "Try again"
        btnRetry.UseVisualStyleBackColor = True
        ' 
        ' btnOpenSettings
        ' 
        btnOpenSettings.AutoSize = True
        btnOpenSettings.Name = "btnOpenSettings"
        btnOpenSettings.TabIndex = 1
        btnOpenSettings.Text = "Open Settings..."
        btnOpenSettings.UseVisualStyleBackColor = True
        ' 
        ' ssmlEditor
        ' 
        ssmlEditor.Dock = DockStyle.Fill
        ssmlEditor.Location = New Point(0, 100)
        ssmlEditor.Name = "ssmlEditor"
        ssmlEditor.Size = New Size(900, 438)
        ssmlEditor.TabIndex = 2
        ' 
        ' statusMain
        ' 
        statusMain.Items.AddRange(New ToolStripItem() {lblStatus, lblNote, lblCheck, lblCount})
        statusMain.Location = New Point(0, 538)
        statusMain.Name = "statusMain"
        statusMain.ShowItemToolTips = True
        statusMain.Size = New Size(900, 22)
        statusMain.TabIndex = 3
        ' 
        ' lblStatus
        ' 
        lblStatus.Name = "lblStatus"
        lblStatus.Text = ""
        ' 
        ' lblNote
        ' 
        lblNote.Name = "lblNote"
        lblNote.Spring = True
        lblNote.Text = ""
        lblNote.TextAlign = ContentAlignment.MiddleLeft
        ' 
        ' lblCheck
        ' 
        lblCheck.BorderSides = ToolStripStatusLabelBorderSides.Left
        lblCheck.Name = "lblCheck"
        lblCheck.Text = ""
        ' 
        ' lblCount
        ' 
        lblCount.BorderSides = ToolStripStatusLabelBorderSides.Left
        lblCount.Name = "lblCount"
        lblCount.Text = ""
        ' 
        ' tmrCheck
        ' 
        tmrCheck.Interval = 400
        ' 
        ' tmrAutosave
        ' 
        tmrAutosave.Interval = 5000
        ' 
        ' tmrNote
        ' 
        tmrNote.Interval = 8000
        ' 
        ' MainForm
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(900, 560)
        Controls.Add(ssmlEditor)
        Controls.Add(pnlBanner)
        Controls.Add(statusMain)
        Controls.Add(mnuMain)
        MainMenuStrip = mnuMain
        MinimumSize = New Size(640, 420)
        Name = "MainForm"
        StartPosition = FormStartPosition.CenterScreen
        Text = "Omskep"
        mnuMain.ResumeLayout(False)
        mnuMain.PerformLayout()
        pnlBanner.ResumeLayout(False)
        pnlBanner.PerformLayout()
        flpBannerButtons.ResumeLayout(False)
        flpBannerButtons.PerformLayout()
        statusMain.ResumeLayout(False)
        statusMain.PerformLayout()
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Friend WithEvents mnuMain As MenuStrip
    Friend WithEvents mnuFile As ToolStripMenuItem
    Friend WithEvents mnuNew As ToolStripMenuItem
    Friend WithEvents mnuOpen As ToolStripMenuItem
    Friend WithEvents mnuSave As ToolStripMenuItem
    Friend WithEvents mnuSaveAs As ToolStripMenuItem
    Friend WithEvents sepFile As ToolStripSeparator
    Friend WithEvents mnuExport As ToolStripMenuItem
    Friend WithEvents sepFile2 As ToolStripSeparator
    Friend WithEvents mnuExit As ToolStripMenuItem
    Friend WithEvents mnuEdit As ToolStripMenuItem
    Friend WithEvents mnuUndo As ToolStripMenuItem
    Friend WithEvents mnuRedo As ToolStripMenuItem
    Friend WithEvents sepEdit1 As ToolStripSeparator
    Friend WithEvents mnuCut As ToolStripMenuItem
    Friend WithEvents mnuCopy As ToolStripMenuItem
    Friend WithEvents mnuPaste As ToolStripMenuItem
    Friend WithEvents mnuPastePlain As ToolStripMenuItem
    Friend WithEvents sepEdit2 As ToolStripSeparator
    Friend WithEvents mnuSelectAll As ToolStripMenuItem
    Friend WithEvents sepEdit3 As ToolStripSeparator
    Friend WithEvents mnuGoToError As ToolStripMenuItem
    Friend WithEvents mnuGoToCause As ToolStripMenuItem
    Friend WithEvents mnuView As ToolStripMenuItem
    Friend WithEvents mnuWordWrap As ToolStripMenuItem
    Friend WithEvents mnuTools As ToolStripMenuItem
    Friend WithEvents mnuSettings As ToolStripMenuItem
    Friend WithEvents pnlBanner As Panel
    Friend WithEvents lblBanner As Label
    Friend WithEvents flpBannerButtons As FlowLayoutPanel
    Friend WithEvents btnRetry As Button
    Friend WithEvents btnOpenSettings As Button
    Friend WithEvents ssmlEditor As Editor.SsmlEditor
    Friend WithEvents statusMain As StatusStrip
    Friend WithEvents lblStatus As ToolStripStatusLabel
    Friend WithEvents lblNote As ToolStripStatusLabel
    Friend WithEvents lblCheck As ToolStripStatusLabel
    Friend WithEvents lblCount As ToolStripStatusLabel
    Friend WithEvents tmrCheck As System.Windows.Forms.Timer
    Friend WithEvents tmrAutosave As System.Windows.Forms.Timer
    Friend WithEvents tmrNote As System.Windows.Forms.Timer

End Class
