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
        mnuMain = New MenuStrip()
        mnuFile = New ToolStripMenuItem()
        mnuOpen = New ToolStripMenuItem()
        mnuSave = New ToolStripMenuItem()
        mnuExport = New ToolStripMenuItem()
        sepFile = New ToolStripSeparator()
        mnuExit = New ToolStripMenuItem()
        mnuTools = New ToolStripMenuItem()
        mnuSettings = New ToolStripMenuItem()
        pnlBanner = New Panel()
        lblBanner = New Label()
        flpBannerButtons = New FlowLayoutPanel()
        btnRetry = New Button()
        btnOpenSettings = New Button()
        lblPlaceholder = New Label()
        statusMain = New StatusStrip()
        lblStatus = New ToolStripStatusLabel()
        mnuMain.SuspendLayout()
        pnlBanner.SuspendLayout()
        flpBannerButtons.SuspendLayout()
        statusMain.SuspendLayout()
        SuspendLayout()
        ' 
        ' mnuMain
        ' 
        mnuMain.Items.AddRange(New ToolStripItem() {mnuFile, mnuTools})
        mnuMain.Location = New Point(0, 0)
        mnuMain.Name = "mnuMain"
        mnuMain.Size = New Size(900, 24)
        mnuMain.TabIndex = 0
        ' 
        ' mnuFile
        ' 
        mnuFile.DropDownItems.AddRange(New ToolStripItem() {mnuOpen, mnuSave, mnuExport, sepFile, mnuExit})
        mnuFile.Name = "mnuFile"
        mnuFile.Text = "&File"
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
        mnuSave.Text = "&Save..."
        ' 
        ' mnuExport
        ' 
        mnuExport.Name = "mnuExport"
        mnuExport.ShortcutKeys = Keys.Control Or Keys.E
        mnuExport.Text = "&Export..."
        ' 
        ' sepFile
        ' 
        sepFile.Name = "sepFile"
        ' 
        ' mnuExit
        ' 
        mnuExit.Name = "mnuExit"
        mnuExit.Text = "E&xit"
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
        ' lblPlaceholder
        ' 
        lblPlaceholder.Dock = DockStyle.Fill
        lblPlaceholder.ForeColor = SystemColors.GrayText
        lblPlaceholder.Location = New Point(0, 100)
        lblPlaceholder.Name = "lblPlaceholder"
        lblPlaceholder.Size = New Size(900, 438)
        lblPlaceholder.TabIndex = 2
        lblPlaceholder.Text = "The SSML editor arrives in the next phase."
        lblPlaceholder.TextAlign = ContentAlignment.MiddleCenter
        ' 
        ' statusMain
        ' 
        statusMain.Items.AddRange(New ToolStripItem() {lblStatus})
        statusMain.Location = New Point(0, 538)
        statusMain.Name = "statusMain"
        statusMain.Size = New Size(900, 22)
        statusMain.TabIndex = 3
        ' 
        ' lblStatus
        ' 
        lblStatus.Name = "lblStatus"
        lblStatus.Text = ""
        ' 
        ' MainForm
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(900, 560)
        Controls.Add(lblPlaceholder)
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
    Friend WithEvents mnuOpen As ToolStripMenuItem
    Friend WithEvents mnuSave As ToolStripMenuItem
    Friend WithEvents mnuExport As ToolStripMenuItem
    Friend WithEvents sepFile As ToolStripSeparator
    Friend WithEvents mnuExit As ToolStripMenuItem
    Friend WithEvents mnuTools As ToolStripMenuItem
    Friend WithEvents mnuSettings As ToolStripMenuItem
    Friend WithEvents pnlBanner As Panel
    Friend WithEvents lblBanner As Label
    Friend WithEvents flpBannerButtons As FlowLayoutPanel
    Friend WithEvents btnRetry As Button
    Friend WithEvents btnOpenSettings As Button
    Friend WithEvents lblPlaceholder As Label
    Friend WithEvents statusMain As StatusStrip
    Friend WithEvents lblStatus As ToolStripStatusLabel

End Class
