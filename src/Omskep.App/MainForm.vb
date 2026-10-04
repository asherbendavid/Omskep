Imports System.Threading
Imports Omskep.Core.Session

''' <summary>
''' The main window. It holds no decisions: MainFormState says what is enabled and what to show, AppSession does the work.
''' </summary>
Public Class MainForm

    Private ReadOnly _session As AppSession
    Private ReadOnly _closing As New CancellationTokenSource()

    ''' <summary>For the Visual Studio designer only.</summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(session As AppSession)
        Me.New()
        _session = session
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

        mnuOpen.Enabled = st.EditEnabled
        mnuSave.Enabled = st.EditEnabled
        mnuExport.Enabled = st.ExportEnabled
        mnuSettings.Enabled = True

        pnlBanner.Visible = st.ShowBanner
        lblBanner.Text = st.BannerText
        btnRetry.Visible = st.ShowRetry
        btnRetry.Enabled = st.RetryEnabled
        lblStatus.Text = st.StatusText
    End Sub

    ' ---- startup / shutdown ----

    Private Async Sub MainForm_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        Try
            Await _session.StartupAsync(_closing.Token)
        Catch ex As OperationCanceledException
            ' The window was closed while the startup fetch was running.
        End Try
        ApplyState()
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        _closing.Cancel()
    End Sub

    Private Sub MainForm_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
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

    ' ---- menu ----

    Private Sub mnuSettings_Click(sender As Object, e As EventArgs) Handles mnuSettings.Click
        ShowSettings()
    End Sub

    Private Sub mnuExit_Click(sender As Object, e As EventArgs) Handles mnuExit.Click
        Close()
    End Sub

    ' PLACEHOLDER (phase 2): documents are not implemented yet. Delete these two handlers' bodies when the editor arrives.
    Private Sub mnuOpen_Click(sender As Object, e As EventArgs) Handles mnuOpen.Click
        MessageBox.Show(Me, "Opening documents arrives with the editor in the next phase.", "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub mnuSave_Click(sender As Object, e As EventArgs) Handles mnuSave.Click
        MessageBox.Show(Me, "Saving documents arrives with the editor in the next phase.", "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' PLACEHOLDER (phase 3): export is not implemented yet.
    Private Sub mnuExport_Click(sender As Object, e As EventArgs) Handles mnuExport.Click
        MessageBox.Show(Me, "Exporting audio arrives in phase 3.", "Omskep", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub ShowSettings()
        Using dialog As New SettingsForm(_session)
            dialog.ShowDialog(Me)
        End Using
        ApplyState()
    End Sub

End Class
