Partial Class VoicePickerForm
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
        pnlSearch = New Panel()
        txtSearch = New TextBox()
        lblSearch = New Label()
        lvVoices = New ListView()
        colVoice = New ColumnHeader()
        pnlButtons = New FlowLayoutPanel()
        btnCancel = New Button()
        btnOK = New Button()
        pnlSearch.SuspendLayout()
        pnlButtons.SuspendLayout()
        SuspendLayout()
        ' 
        ' pnlSearch
        ' 
        pnlSearch.Controls.Add(txtSearch)
        pnlSearch.Controls.Add(lblSearch)
        pnlSearch.Dock = DockStyle.Top
        pnlSearch.Location = New Point(0, 0)
        pnlSearch.Name = "pnlSearch"
        pnlSearch.Padding = New Padding(8, 6, 8, 4)
        pnlSearch.Size = New Size(480, 34)
        pnlSearch.TabIndex = 1
        ' 
        ' txtSearch
        ' 
        txtSearch.Dock = DockStyle.Fill
        txtSearch.Location = New Point(59, 6)
        txtSearch.Name = "txtSearch"
        txtSearch.Size = New Size(413, 23)
        txtSearch.TabIndex = 0
        ' 
        ' lblSearch
        ' 
        lblSearch.AutoSize = True
        lblSearch.Dock = DockStyle.Left
        lblSearch.Location = New Point(8, 6)
        lblSearch.Name = "lblSearch"
        lblSearch.Padding = New Padding(0, 4, 6, 0)
        lblSearch.Size = New Size(51, 19)
        lblSearch.TabIndex = 1
        lblSearch.Text = "Search:"
        ' 
        ' lvVoices
        ' 
        lvVoices.Columns.AddRange(New ColumnHeader() {colVoice})
        lvVoices.Dock = DockStyle.Fill
        lvVoices.FullRowSelect = True
        lvVoices.Location = New Point(0, 34)
        lvVoices.MultiSelect = False
        lvVoices.Name = "lvVoices"
        lvVoices.Size = New Size(480, 342)
        lvVoices.TabIndex = 0
        lvVoices.UseCompatibleStateImageBehavior = False
        lvVoices.View = View.Details
        ' 
        ' colVoice
        ' 
        colVoice.Text = "Voice"
        colVoice.Width = 440
        ' 
        ' pnlButtons
        ' 
        pnlButtons.Controls.Add(btnCancel)
        pnlButtons.Controls.Add(btnOK)
        pnlButtons.Dock = DockStyle.Bottom
        pnlButtons.FlowDirection = FlowDirection.RightToLeft
        pnlButtons.Location = New Point(0, 376)
        pnlButtons.Name = "pnlButtons"
        pnlButtons.Padding = New Padding(8)
        pnlButtons.Size = New Size(480, 44)
        pnlButtons.TabIndex = 2
        ' 
        ' btnCancel
        ' 
        btnCancel.DialogResult = DialogResult.Cancel
        btnCancel.Location = New Point(371, 11)
        btnCancel.Name = "btnCancel"
        btnCancel.Size = New Size(90, 23)
        btnCancel.TabIndex = 0
        btnCancel.Text = "Cancel"
        ' 
        ' btnOK
        ' 
        btnOK.Location = New Point(275, 11)
        btnOK.Name = "btnOK"
        btnOK.Size = New Size(90, 23)
        btnOK.TabIndex = 1
        btnOK.Text = "OK"
        ' 
        ' VoicePickerForm
        ' 
        AcceptButton = btnOK
        CancelButton = btnCancel
        ClientSize = New Size(480, 420)
        Controls.Add(lvVoices)
        Controls.Add(pnlSearch)
        Controls.Add(pnlButtons)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "VoicePickerForm"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Use another voice"
        pnlSearch.ResumeLayout(False)
        pnlSearch.PerformLayout()
        pnlButtons.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents pnlSearch As Panel
    Friend WithEvents lblSearch As Label
    Friend WithEvents txtSearch As TextBox
    Friend WithEvents lvVoices As ListView
    Friend WithEvents colVoice As ColumnHeader
    Friend WithEvents pnlButtons As FlowLayoutPanel
    Friend WithEvents btnOK As Button
    Friend WithEvents btnCancel As Button

End Class