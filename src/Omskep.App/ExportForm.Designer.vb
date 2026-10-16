<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ExportForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
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
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        pnlConfirm = New Panel()
        btnExport = New Button()
        btnCancelConfirm = New Button()
        btnCopyComment = New Button()
        txtYear = New TextBox()
        Label9 = New Label()
        lblCount = New Label()
        txtComment = New TextBox()
        Label7 = New Label()
        txtGenre = New TextBox()
        Label6 = New Label()
        txtCopyright = New TextBox()
        Label5 = New Label()
        txtTrack = New TextBox()
        Label4 = New Label()
        txtAlbum = New TextBox()
        Label3 = New Label()
        txtArtist = New TextBox()
        Label2 = New Label()
        txtTitle = New TextBox()
        Label1 = New Label()
        pnlProgress = New Panel()
        txtResult = New TextBox()
        ProgressBar1 = New ProgressBar()
        lblStatus = New Label()
        btnClose = New Button()
        btnCancelExport = New Button()
        btnTryAgain = New Button()
        pnlConfirm.SuspendLayout()
        pnlProgress.SuspendLayout()
        SuspendLayout()
        ' 
        ' pnlConfirm
        ' 
        pnlConfirm.Controls.Add(btnExport)
        pnlConfirm.Controls.Add(btnCancelConfirm)
        pnlConfirm.Controls.Add(btnCopyComment)
        pnlConfirm.Controls.Add(txtYear)
        pnlConfirm.Controls.Add(Label9)
        pnlConfirm.Controls.Add(lblCount)
        pnlConfirm.Controls.Add(txtComment)
        pnlConfirm.Controls.Add(Label7)
        pnlConfirm.Controls.Add(txtGenre)
        pnlConfirm.Controls.Add(Label6)
        pnlConfirm.Controls.Add(txtCopyright)
        pnlConfirm.Controls.Add(Label5)
        pnlConfirm.Controls.Add(txtTrack)
        pnlConfirm.Controls.Add(Label4)
        pnlConfirm.Controls.Add(txtAlbum)
        pnlConfirm.Controls.Add(Label3)
        pnlConfirm.Controls.Add(txtArtist)
        pnlConfirm.Controls.Add(Label2)
        pnlConfirm.Controls.Add(txtTitle)
        pnlConfirm.Controls.Add(Label1)
        pnlConfirm.Dock = DockStyle.Fill
        pnlConfirm.Location = New Point(0, 0)
        pnlConfirm.Name = "pnlConfirm"
        pnlConfirm.Size = New Size(800, 320)
        pnlConfirm.TabIndex = 0
        ' 
        ' btnExport
        ' 
        btnExport.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        btnExport.Location = New Point(632, 285)
        btnExport.Name = "btnExport"
        btnExport.Size = New Size(75, 23)
        btnExport.TabIndex = 21
        btnExport.Text = "Export..."
        btnExport.UseVisualStyleBackColor = True
        ' 
        ' btnCancelConfirm
        ' 
        btnCancelConfirm.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        btnCancelConfirm.Location = New Point(713, 285)
        btnCancelConfirm.Name = "btnCancelConfirm"
        btnCancelConfirm.Size = New Size(75, 23)
        btnCancelConfirm.TabIndex = 20
        btnCancelConfirm.Text = "Cancel"
        btnCancelConfirm.UseVisualStyleBackColor = True
        ' 
        ' btnCopyComment
        ' 
        btnCopyComment.Location = New Point(12, 207)
        btnCopyComment.Name = "btnCopyComment"
        btnCopyComment.Size = New Size(75, 23)
        btnCopyComment.TabIndex = 19
        btnCopyComment.Text = "Copy"
        btnCopyComment.UseVisualStyleBackColor = True
        ' 
        ' txtYear
        ' 
        txtYear.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtYear.Location = New Point(532, 99)
        txtYear.Name = "txtYear"
        txtYear.ReadOnly = True
        txtYear.Size = New Size(256, 23)
        txtYear.TabIndex = 18
        ' 
        ' Label9
        ' 
        Label9.AutoSize = True
        Label9.Location = New Point(497, 102)
        Label9.Name = "Label9"
        Label9.Size = New Size(29, 15)
        Label9.TabIndex = 17
        Label9.Text = "Year"
        ' 
        ' lblCount
        ' 
        lblCount.AutoSize = True
        lblCount.Location = New Point(12, 259)
        lblCount.Name = "lblCount"
        lblCount.Size = New Size(76, 15)
        lblCount.TabIndex = 16
        lblCount.Text = "Calculating..."
        ' 
        ' txtComment
        ' 
        txtComment.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtComment.Location = New Point(129, 186)
        txtComment.Multiline = True
        txtComment.Name = "txtComment"
        txtComment.ReadOnly = True
        txtComment.Size = New Size(659, 60)
        txtComment.TabIndex = 13
        ' 
        ' Label7
        ' 
        Label7.AutoSize = True
        Label7.Location = New Point(12, 189)
        Label7.Name = "Label7"
        Label7.Size = New Size(61, 15)
        Label7.TabIndex = 12
        Label7.Text = "Comment"
        ' 
        ' txtGenre
        ' 
        txtGenre.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtGenre.Location = New Point(129, 157)
        txtGenre.Name = "txtGenre"
        txtGenre.ReadOnly = True
        txtGenre.Size = New Size(659, 23)
        txtGenre.TabIndex = 11
        ' 
        ' Label6
        ' 
        Label6.AutoSize = True
        Label6.Location = New Point(12, 160)
        Label6.Name = "Label6"
        Label6.Size = New Size(38, 15)
        Label6.TabIndex = 10
        Label6.Text = "Genre"
        ' 
        ' txtCopyright
        ' 
        txtCopyright.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtCopyright.Location = New Point(129, 128)
        txtCopyright.Name = "txtCopyright"
        txtCopyright.ReadOnly = True
        txtCopyright.Size = New Size(659, 23)
        txtCopyright.TabIndex = 9
        ' 
        ' Label5
        ' 
        Label5.AutoSize = True
        Label5.Location = New Point(12, 131)
        Label5.Name = "Label5"
        Label5.Size = New Size(60, 15)
        Label5.TabIndex = 8
        Label5.Text = "Copyright"
        ' 
        ' txtTrack
        ' 
        txtTrack.Location = New Point(129, 99)
        txtTrack.Name = "txtTrack"
        txtTrack.ReadOnly = True
        txtTrack.Size = New Size(256, 23)
        txtTrack.TabIndex = 7
        ' 
        ' Label4
        ' 
        Label4.AutoSize = True
        Label4.Location = New Point(12, 102)
        Label4.Name = "Label4"
        Label4.Size = New Size(35, 15)
        Label4.TabIndex = 6
        Label4.Text = "Track"
        ' 
        ' txtAlbum
        ' 
        txtAlbum.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtAlbum.Location = New Point(129, 70)
        txtAlbum.Name = "txtAlbum"
        txtAlbum.ReadOnly = True
        txtAlbum.Size = New Size(659, 23)
        txtAlbum.TabIndex = 5
        ' 
        ' Label3
        ' 
        Label3.AutoSize = True
        Label3.Location = New Point(12, 73)
        Label3.Name = "Label3"
        Label3.Size = New Size(43, 15)
        Label3.TabIndex = 4
        Label3.Text = "Album"
        ' 
        ' txtArtist
        ' 
        txtArtist.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtArtist.Location = New Point(129, 41)
        txtArtist.Name = "txtArtist"
        txtArtist.ReadOnly = True
        txtArtist.Size = New Size(659, 23)
        txtArtist.TabIndex = 3
        ' 
        ' Label2
        ' 
        Label2.AutoSize = True
        Label2.Location = New Point(12, 44)
        Label2.Name = "Label2"
        Label2.Size = New Size(35, 15)
        Label2.TabIndex = 2
        Label2.Text = "Artist"
        ' 
        ' txtTitle
        ' 
        txtTitle.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        txtTitle.Location = New Point(129, 12)
        txtTitle.Name = "txtTitle"
        txtTitle.ReadOnly = True
        txtTitle.Size = New Size(659, 23)
        txtTitle.TabIndex = 1
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Location = New Point(12, 15)
        Label1.Name = "Label1"
        Label1.Size = New Size(30, 15)
        Label1.TabIndex = 0
        Label1.Text = "Title"
        ' 
        ' pnlProgress
        ' 
        pnlProgress.Controls.Add(btnTryAgain)
        pnlProgress.Controls.Add(txtResult)
        pnlProgress.Controls.Add(ProgressBar1)
        pnlProgress.Controls.Add(lblStatus)
        pnlProgress.Controls.Add(btnClose)
        pnlProgress.Controls.Add(btnCancelExport)
        pnlProgress.Dock = DockStyle.Fill
        pnlProgress.Location = New Point(0, 0)
        pnlProgress.Name = "pnlProgress"
        pnlProgress.Size = New Size(800, 320)
        pnlProgress.TabIndex = 1
        ' 
        ' txtResult
        ' 
        txtResult.Location = New Point(12, 70)
        txtResult.Multiline = True
        txtResult.Name = "txtResult"
        txtResult.ReadOnly = True
        txtResult.ScrollBars = ScrollBars.Vertical
        txtResult.Size = New Size(776, 204)
        txtResult.TabIndex = 4
        txtResult.Visible = False
        ' 
        ' ProgressBar1
        ' 
        ProgressBar1.Location = New Point(12, 41)
        ProgressBar1.Name = "ProgressBar1"
        ProgressBar1.Size = New Size(776, 23)
        ProgressBar1.Style = ProgressBarStyle.Continuous
        ProgressBar1.TabIndex = 3
        ' 
        ' lblStatus
        ' 
        lblStatus.AutoEllipsis = True
        lblStatus.Location = New Point(12, 9)
        lblStatus.Name = "lblStatus"
        lblStatus.Size = New Size(776, 26)
        lblStatus.TabIndex = 2
        lblStatus.Text = "Preparing..."
        ' 
        ' btnClose
        ' 
        btnClose.Location = New Point(632, 285)
        btnClose.Name = "btnClose"
        btnClose.Size = New Size(75, 23)
        btnClose.TabIndex = 1
        btnClose.Text = "Close"
        btnClose.UseVisualStyleBackColor = True
        btnClose.Visible = False
        ' 
        ' btnCancelExport
        ' 
        btnCancelExport.Location = New Point(713, 285)
        btnCancelExport.Name = "btnCancelExport"
        btnCancelExport.Size = New Size(75, 23)
        btnCancelExport.TabIndex = 0
        btnCancelExport.Text = "Cancel"
        btnCancelExport.UseVisualStyleBackColor = True
        ' 
        ' btnTryAgain
        ' 
        btnTryAgain.Location = New Point(551, 285)
        btnTryAgain.Name = "btnTryAgain"
        btnTryAgain.Size = New Size(75, 23)
        btnTryAgain.TabIndex = 5
        btnTryAgain.Text = "Try Again"
        btnTryAgain.UseVisualStyleBackColor = True
        btnTryAgain.Visible = False
        ' 
        ' ExportForm
        ' 
        AutoScaleDimensions = New SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(800, 320)
        Controls.Add(pnlProgress)
        Controls.Add(pnlConfirm)
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Name = "ExportForm"
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        Text = "Export MP3 File..."
        pnlConfirm.ResumeLayout(False)
        pnlConfirm.PerformLayout()
        pnlProgress.ResumeLayout(False)
        pnlProgress.PerformLayout()
        ResumeLayout(False)
    End Sub

    Friend WithEvents pnlConfirm As Panel
    Friend WithEvents txtYear As TextBox
    Friend WithEvents Label9 As Label
    Friend WithEvents lblCount As Label
    Friend WithEvents TextBox8 As TextBox
    Friend WithEvents Label8 As Label
    Friend WithEvents txtComment As TextBox
    Friend WithEvents Label7 As Label
    Friend WithEvents txtGenre As TextBox
    Friend WithEvents Label6 As Label
    Friend WithEvents txtCopyright As TextBox
    Friend WithEvents Label5 As Label
    Friend WithEvents txtTrack As TextBox
    Friend WithEvents Label4 As Label
    Friend WithEvents txtAlbum As TextBox
    Friend WithEvents Label3 As Label
    Friend WithEvents txtArtist As TextBox
    Friend WithEvents Label2 As Label
    Friend WithEvents txtTitle As TextBox
    Friend WithEvents Label1 As Label
    Friend WithEvents btnExport As Button
    Friend WithEvents btnCancelConfirm As Button
    Friend WithEvents btnCopyComment As Button
    Friend WithEvents pnlProgress As Panel
    Friend WithEvents ProgressBar1 As ProgressBar
    Friend WithEvents lblStatus As Label
    Friend WithEvents btnClose As Button
    Friend WithEvents btnCancelExport As Button
    Friend WithEvents txtResult As TextBox
    Friend WithEvents btnTryAgain As Button
End Class
