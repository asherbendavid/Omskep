Partial Class VoiceSettingsForm
    Inherits System.Windows.Forms.Form

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

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        pnlRows = New FlowLayoutPanel()
        pnlButtons = New FlowLayoutPanel()
        btnCancel = New Button()
        btnOK = New Button()
        pnlButtons.SuspendLayout()
        SuspendLayout()
        '
        'pnlRows
        '
        pnlRows.Dock = DockStyle.Fill
        pnlRows.AutoScroll = True
        pnlRows.FlowDirection = FlowDirection.TopDown
        pnlRows.WrapContents = False
        pnlRows.Padding = New Padding(12, 10, 12, 10)
        '
        'pnlButtons
        '
        pnlButtons.Dock = DockStyle.Bottom
        pnlButtons.FlowDirection = FlowDirection.RightToLeft
        pnlButtons.Height = 44
        pnlButtons.Padding = New Padding(8)
        pnlButtons.Controls.Add(btnCancel)
        pnlButtons.Controls.Add(btnOK)
        '
        'btnOK
        '
        btnOK.Text = "OK"
        btnOK.DialogResult = DialogResult.None ' btnOK_Click saves and sets this explicitly
        btnOK.Width = 90
        '
        'btnCancel
        '
        btnCancel.Text = "Cancel"
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Width = 90
        '
        'VoiceSettingsForm
        '
        AcceptButton = btnOK
        CancelButton = btnCancel
        ClientSize = New Size(520, 420)
        Controls.Add(pnlRows)
        Controls.Add(pnlButtons)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Voice settings"
        pnlButtons.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents pnlRows As FlowLayoutPanel
    Friend WithEvents pnlButtons As FlowLayoutPanel
    Friend WithEvents btnOK As Button
    Friend WithEvents btnCancel As Button

End Class