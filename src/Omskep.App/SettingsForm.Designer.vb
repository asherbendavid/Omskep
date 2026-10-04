<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SettingsForm
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
        tlpMain = New TableLayoutPanel()
        lblRegion = New Label()
        cboRegion = New ComboBox()
        lblKey = New Label()
        pnlKeyHost = New Panel()
        flpSaved = New FlowLayoutPanel()
        lblSaved = New Label()
        btnReplace = New Button()
        btnClear = New Button()
        txtKey = New TextBox()
        lblHint = New Label()
        flpActions = New FlowLayoutPanel()
        btnTest = New Button()
        btnRefresh = New Button()
        progressBusy = New ProgressBar()
        lblResult = New Label()
        btnClose = New Button()
        tlpMain.SuspendLayout()
        pnlKeyHost.SuspendLayout()
        flpSaved.SuspendLayout()
        flpActions.SuspendLayout()
        SuspendLayout()
        ' 
        ' tlpMain
        ' 
        tlpMain.AutoSize = True
        tlpMain.AutoSizeMode = AutoSizeMode.GrowAndShrink
        tlpMain.ColumnCount = 2
        tlpMain.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tlpMain.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        tlpMain.Controls.Add(lblRegion, 0, 0)
        tlpMain.Controls.Add(cboRegion, 1, 0)
        tlpMain.Controls.Add(lblKey, 0, 1)
        tlpMain.Controls.Add(pnlKeyHost, 1, 1)
        tlpMain.Controls.Add(lblHint, 1, 2)
        tlpMain.Controls.Add(flpActions, 0, 3)
        tlpMain.Controls.Add(progressBusy, 0, 4)
        tlpMain.Controls.Add(lblResult, 0, 5)
        tlpMain.Controls.Add(btnClose, 0, 6)
        tlpMain.Location = New Point(0, 0)
        tlpMain.MinimumSize = New Size(560, 0)
        tlpMain.Name = "tlpMain"
        tlpMain.Padding = New Padding(12)
        tlpMain.RowCount = 7
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.RowStyles.Add(New RowStyle())
        tlpMain.TabIndex = 0
        ' 
        ' lblRegion
        ' 
        lblRegion.Anchor = AnchorStyles.Left
        lblRegion.AutoSize = True
        lblRegion.Name = "lblRegion"
        lblRegion.TabIndex = 0
        lblRegion.Text = "&Region:"
        ' 
        ' cboRegion
        ' 
        cboRegion.Anchor = AnchorStyles.Left Or AnchorStyles.Right
        cboRegion.DropDownStyle = ComboBoxStyle.DropDown
        cboRegion.MaxLength = 40
        cboRegion.Name = "cboRegion"
        cboRegion.TabIndex = 1
        ' 
        ' lblKey
        ' 
        lblKey.Anchor = AnchorStyles.Left
        lblKey.AutoSize = True
        lblKey.Name = "lblKey"
        lblKey.TabIndex = 2
        lblKey.Text = "Azure &key:"
        ' 
        ' pnlKeyHost
        ' 
        pnlKeyHost.AutoSize = True
        pnlKeyHost.AutoSizeMode = AutoSizeMode.GrowAndShrink
        pnlKeyHost.Controls.Add(flpSaved)
        pnlKeyHost.Controls.Add(txtKey)
        pnlKeyHost.Dock = DockStyle.Fill
        pnlKeyHost.Name = "pnlKeyHost"
        pnlKeyHost.TabIndex = 3
        ' 
        ' flpSaved
        ' 
        flpSaved.AutoSize = True
        flpSaved.Controls.Add(lblSaved)
        flpSaved.Controls.Add(btnReplace)
        flpSaved.Controls.Add(btnClear)
        flpSaved.Dock = DockStyle.Top
        flpSaved.Name = "flpSaved"
        flpSaved.TabIndex = 1
        flpSaved.Visible = False
        ' 
        ' lblSaved
        ' 
        lblSaved.Anchor = AnchorStyles.Left
        lblSaved.AutoSize = True
        lblSaved.Name = "lblSaved"
        lblSaved.TabIndex = 0
        lblSaved.Text = "Key saved on this computer."
        ' 
        ' btnReplace
        ' 
        btnReplace.AutoSize = True
        btnReplace.Name = "btnReplace"
        btnReplace.TabIndex = 1
        btnReplace.Text = "Replace..."
        btnReplace.UseVisualStyleBackColor = True
        ' 
        ' btnClear
        ' 
        btnClear.AutoSize = True
        btnClear.Name = "btnClear"
        btnClear.TabIndex = 2
        btnClear.Text = "Remove"
        btnClear.UseVisualStyleBackColor = True
        ' 
        ' txtKey
        ' 
        txtKey.Dock = DockStyle.Top
        txtKey.MaxLength = 256
        txtKey.Name = "txtKey"
        txtKey.TabIndex = 0
        txtKey.UseSystemPasswordChar = True
        ' 
        ' lblHint
        ' 
        lblHint.AutoSize = True
        lblHint.ForeColor = SystemColors.GrayText
        lblHint.MaximumSize = New Size(420, 0)
        lblHint.Name = "lblHint"
        lblHint.TabIndex = 4
        lblHint.Text = "A new key or region is saved only after the connection test succeeds. The key is stored on this computer, protected by Windows."
        ' 
        ' flpActions
        ' 
        flpActions.AutoSize = True
        flpActions.Controls.Add(btnTest)
        flpActions.Controls.Add(btnRefresh)
        flpActions.Name = "flpActions"
        flpActions.TabIndex = 5
        tlpMain.SetColumnSpan(flpActions, 2)
        ' 
        ' btnTest
        ' 
        btnTest.AutoSize = True
        btnTest.Name = "btnTest"
        btnTest.TabIndex = 0
        btnTest.Text = "&Test connection"
        btnTest.UseVisualStyleBackColor = True
        ' 
        ' btnRefresh
        ' 
        btnRefresh.AutoSize = True
        btnRefresh.Name = "btnRefresh"
        btnRefresh.TabIndex = 1
        btnRefresh.Text = "Refresh &voices"
        btnRefresh.UseVisualStyleBackColor = True
        ' 
        ' progressBusy
        ' 
        progressBusy.Dock = DockStyle.Fill
        progressBusy.MarqueeAnimationSpeed = 30
        progressBusy.Name = "progressBusy"
        progressBusy.Size = New Size(100, 8)
        progressBusy.Style = ProgressBarStyle.Marquee
        progressBusy.TabIndex = 6
        progressBusy.Visible = False
        tlpMain.SetColumnSpan(progressBusy, 2)
        ' 
        ' lblResult
        ' 
        lblResult.AutoSize = True
        lblResult.MaximumSize = New Size(536, 0)
        lblResult.Name = "lblResult"
        lblResult.TabIndex = 7
        tlpMain.SetColumnSpan(lblResult, 2)
        ' 
        ' btnClose
        ' 
        btnClose.Anchor = AnchorStyles.Right
        btnClose.AutoSize = True
        btnClose.DialogResult = DialogResult.Cancel
        btnClose.Name = "btnClose"
        btnClose.TabIndex = 8
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = True
        tlpMain.SetColumnSpan(btnClose, 2)
        ' 
        ' SettingsForm
        ' 
        AcceptButton = btnTest
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        CancelButton = btnClose
        ClientSize = New Size(560, 280)
        Controls.Add(tlpMain)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "SettingsForm"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Settings"
        tlpMain.ResumeLayout(False)
        tlpMain.PerformLayout()
        pnlKeyHost.ResumeLayout(False)
        pnlKeyHost.PerformLayout()
        flpSaved.ResumeLayout(False)
        flpSaved.PerformLayout()
        flpActions.ResumeLayout(False)
        flpActions.PerformLayout()
        ResumeLayout(False)
        PerformLayout()

    End Sub

    Friend WithEvents tlpMain As TableLayoutPanel
    Friend WithEvents lblRegion As Label
    Friend WithEvents cboRegion As ComboBox
    Friend WithEvents lblKey As Label
    Friend WithEvents pnlKeyHost As Panel
    Friend WithEvents flpSaved As FlowLayoutPanel
    Friend WithEvents lblSaved As Label
    Friend WithEvents btnReplace As Button
    Friend WithEvents btnClear As Button
    Friend WithEvents txtKey As TextBox
    Friend WithEvents lblHint As Label
    Friend WithEvents flpActions As FlowLayoutPanel
    Friend WithEvents btnTest As Button
    Friend WithEvents btnRefresh As Button
    Friend WithEvents progressBusy As ProgressBar
    Friend WithEvents lblResult As Label
    Friend WithEvents btnClose As Button

End Class
