Option Strict On
Option Explicit On
Option Infer On

Imports System.Linq
Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Export
Imports Omskep.Core.Session

Public Class ExportForm

    Private ReadOnly _session As AppSession
    Private ReadOnly _preparation As ExportPreparationResult
    Private ReadOnly _initialFolder As String
    Private _cts As CancellationTokenSource
    Private _lastStatus As ExportStatus?
    Private _lastResult As ExportResult
    Private _lastOutputPath As String
    Private _dialogResultOnClose As DialogResult = DialogResult.Cancel

    Public Sub New(session As AppSession, preparation As ExportPreparationResult, initialFolder As String)
        InitializeComponent()
        _session = session
        _preparation = preparation
        _initialFolder = initialFolder
    End Sub

    Private Sub ExportForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lblCount.Text = $"up to {_preparation.BillableUpperBound} characters"
        txtTitle.Text = _preparation.Fields.Title
        txtArtist.Text = _preparation.Fields.Artist
        txtAlbum.Text = _preparation.Fields.Album
        txtTrack.Text = If(_preparation.Fields.Track.HasValue, _preparation.Fields.Track.Value.ToString(), "")
        txtYear.Text = _preparation.Fields.Year
        txtCopyright.Text = _preparation.Fields.Copyright
        txtGenre.Text = _preparation.Fields.Genre
        txtComment.Text = _preparation.Fields.Comment
        ShowConfirmPanel()
    End Sub

    Private Sub ShowConfirmPanel()
        pnlProgress.Visible = False
        pnlConfirm.Visible = True
        AcceptButton = btnExport
        CancelButton = btnCancelConfirm
    End Sub

    Private Sub btnCopyComment_Click(sender As Object, e As EventArgs) Handles btnCopyComment.Click
        If Not String.IsNullOrEmpty(txtComment.Text) Then Clipboard.SetText(txtComment.Text)
    End Sub

    Private Sub btnCancelConfirm_Click(sender As Object, e As EventArgs) Handles btnCancelConfirm.Click
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Private Async Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        Dim outputPath As String = PromptForOutputPath()
        If outputPath Is Nothing Then Return ' Save As was cancelled; stay on the confirm screen

        ShowProgressPanel()
        Await RunExportAsync(outputPath)
    End Sub

    Private Function PromptForOutputPath() As String
        Using dialog As New SaveFileDialog() With {
            .Title = "Export audio",
            .Filter = "MP3 audio (*.mp3)|*.mp3",
            .DefaultExt = "mp3",
            .AddExtension = True,
            .OverwritePrompt = True,
            .FileName = _preparation.DefaultFileName,
            .InitialDirectory = _initialFolder
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return Nothing
            Return dialog.FileName
        End Using
    End Function

    Private Sub ShowProgressPanel()
        pnlConfirm.Visible = False
        pnlProgress.Visible = True
        lblStatus.Text = "Starting export…"
        ProgressBar1.Minimum = 0
        ProgressBar1.Maximum = Math.Max(1, _preparation.Chunks.Count)
        ProgressBar1.Value = 0
        txtResult.Visible = False
        btnClose.Visible = False
        btnCancelExport.Visible = True
        btnCancelExport.Enabled = True
        AcceptButton = Nothing
        CancelButton = Nothing
        btnCancelExport.DialogResult = DialogResult.None
    End Sub

    Private Sub btnCancelExport_Click(sender As Object, e As EventArgs) Handles btnCancelExport.Click
        _cts?.Cancel()
        btnCancelExport.Enabled = False
        lblStatus.Text = "Cancelling…"
    End Sub

    Private Async Function RunExportAsync(outputPath As String, Optional priorAttempt As ExportResult = Nothing) As Task
        _lastOutputPath = outputPath
        _cts = New CancellationTokenSource()
        Dim engine As ExportEngine = _session.CreateExportEngine()
        Dim progress As New Progress(Of ExportProgress)(AddressOf OnExportProgress)

        Dim result As ExportResult
        Try
            result = Await engine.ExportAsync(_preparation.Chunks, outputPath, _cts.Token, progress, priorAttempt)
        Catch ex As OperationCanceledException
            ' No ExportResult was ever produced here, so there is nothing to resume from -- a fresh
            ' export (closing this dialog and choosing Export again) is the only option, same as
            ' the documented ExportEngineTests gap for a cancel that lands mid-chunk.
            _lastResult = Nothing
            ShowResult(canTryAgain:=False, succeeded:=False,
                   message:="Export was cancelled while a chunk was in progress. No progress was saved for this attempt.")
            Return
        End Try

        _lastResult = result

        Select Case result.Status
            Case ExportStatus.Complete
                Dim stampResult As Id3StampResult = Id3Stamper.Stamp(outputPath, _preparation.Fields)
                If stampResult.Succeeded Then
                    ShowResult(canTryAgain:=False, succeeded:=True,
                           message:="Exported successfully to:" & vbCrLf & outputPath)
                Else
                    ' Audio and file are fine; only tagging failed. Try Again re-enters ExportAsync with
                    ' this same (fully Done) result, which skips straight to rewriting the file and lets
                    ' Id3Stamper retry -- no re-synthesis.
                    ShowResult(canTryAgain:=True, succeeded:=False,
                           message:="The audio exported successfully, but the ID3 tags could not be written: " & stampResult.Message)
                End If
            Case ExportStatus.FailedWriteOnly
                ShowResult(canTryAgain:=True, succeeded:=False,
                       message:="Every part of the audio was synthesized successfully, but the file could not be saved: " &
                       result.Message & vbCrLf & vbCrLf & "Try Again will only retry the write -- none of the audio will be re-synthesized.")
            Case ExportStatus.Cancelled
                ShowResult(canTryAgain:=True, succeeded:=False,
                       message:="Export was cancelled." & DescribeResumeHint(result))
            Case Else ' FailedResumable
                ShowResult(canTryAgain:=True, succeeded:=False, message:=DescribeFailure(result))
        End Select
    End Function

    Private Sub ShowResult(canTryAgain As Boolean, succeeded As Boolean, message As String)
        lblStatus.Text = If(succeeded, "Done.", "Export did not complete.")
        txtResult.Text = message
        txtResult.Visible = True
        If succeeded Then ProgressBar1.Value = ProgressBar1.Maximum
        btnCancelExport.Visible = False
        btnTryAgain.Visible = canTryAgain
        btnClose.Visible = True
        AcceptButton = btnClose
        CancelButton = btnClose
        _dialogResultOnClose = If(succeeded, DialogResult.OK, DialogResult.Cancel)
    End Sub

    Private Sub OnExportProgress(p As ExportProgress)
        ProgressBar1.Maximum = Math.Max(1, p.TotalChunks)
        ProgressBar1.Value = Math.Min(ProgressBar1.Maximum, p.ChunkIndex)
        lblStatus.Text = If(p.State = ChunkLiveState.Retrying,
            $"Retrying chunk {p.ChunkIndex + 1} of {p.TotalChunks} (attempt {p.RetryAttempt})…",
            $"Sending chunk {p.ChunkIndex + 1} of {p.TotalChunks}…")
    End Sub

    ''' <summary>Plain, specific wording for a resumable failure -- which chunks finished, which
    ''' didn't, and that trying again won't need to resend the finished ones (and may double-bill
    ''' a chunk that was mid-retry when it failed, per PossiblyBilledTwice).</summary>
    Private Shared Function DescribeFailure(result As ExportResult) As String
        Dim done As Integer = Enumerable.Count(result.Chunks, Function(c) c.Status = ChunkExportStatus.Done)
        Dim total As Integer = result.Chunks.Count
        Dim billedTwiceNote As String = If(result.Chunks.Any(Function(c) c.PossiblyBilledTwice),
            " A chunk that was retrying when this happened may be billed twice if it is resent.", "")
        Return $"{result.Message}{vbCrLf}{vbCrLf}{done} of {total} chunks finished and will not be resent if you try again.{billedTwiceNote}"
    End Function

    Private Shared Function DescribeResumeHint(result As ExportResult) As String
        Dim done As Integer = Enumerable.Count(result.Chunks, Function(c) c.Status = ChunkExportStatus.Done)
        If done = 0 Then Return ""
        Return $" {done} of {result.Chunks.Count} chunks had already finished and will not be resent if you try again."
    End Function

    Private Sub ShowResult(status As ExportStatus, message As String)
        lblStatus.Text = If(status = ExportStatus.Complete, "Done.", "Export did not complete.")
        txtResult.Text = message
        txtResult.Visible = True
        btnCancelExport.Visible = False
        btnClose.Visible = True
        AcceptButton = btnClose
        CancelButton = btnClose
        If status = ExportStatus.Complete Then ProgressBar1.Value = ProgressBar1.Maximum
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        DialogResult = _dialogResultOnClose
        Close()
    End Sub

    Private Async Sub btnTryAgain_Click(sender As Object, e As EventArgs) Handles btnTryAgain.Click
        ShowProgressPanel()
        Await RunExportAsync(_lastOutputPath, _lastResult)
    End Sub

End Class