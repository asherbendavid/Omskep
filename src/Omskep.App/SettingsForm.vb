Imports System.Threading
Imports Omskep.Core.Session
Imports Omskep.Core.Speech

''' <summary>
''' The Settings page. It holds no decisions: SettingsFormState says what is visible and enabled, AppSession decides
''' what gets saved. The key is typed into a masked box, cleared as soon as it has been saved, and never shown again.
''' </summary>
Public Class SettingsForm

    Private ReadOnly _session As AppSession
    Private _replacing As Boolean
    Private _cts As CancellationTokenSource

    ''' <summary>For the Visual Studio designer only.</summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(session As AppSession)
        Me.New()
        _session = session
        For Each regionCounter In AzureRegion.CommonRegions
            cboRegion.Items.Add(regionCounter)
        Next
        cboRegion.Text = _session.Region
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
        Dim own = _cts IsNot Nothing
        Dim st = SettingsFormState.From(
            keySaved:=_session.KeyIsSaved,
            replacing:=_replacing,
            ownRequestRunning:=own,
            otherRequestRunning:=_session.IsBusy AndAlso Not own,
            typedKeyLength:=txtKey.TextLength,
            regionValid:=AzureRegion.IsValid(cboRegion.Text))

        txtKey.Visible = st.ShowKeyBox
        flpSaved.Visible = st.ShowSavedPanel
        cboRegion.Enabled = st.InputsEnabled
        txtKey.Enabled = st.InputsEnabled
        btnTest.Enabled = st.TestEnabled
        btnTest.Text = If(st.TestIsCancel, "Cancel", "&Test connection")
        btnRefresh.Enabled = st.RefreshEnabled
        btnReplace.Enabled = st.ReplaceEnabled
        btnClear.Enabled = st.ClearEnabled
        progressBusy.Visible = st.ShowProgress
    End Sub

    Private Sub ShowResult(result As ActionResult)
        lblResult.ForeColor = If(result.Success, Color.DarkGreen, Color.Firebrick)
        lblResult.Text = result.Message
    End Sub

    ' ---- inputs ----

    Private Sub cboRegion_TextChanged(sender As Object, e As EventArgs) Handles cboRegion.TextChanged
        ApplyState()
    End Sub

    Private Sub txtKey_TextChanged(sender As Object, e As EventArgs) Handles txtKey.TextChanged
        ApplyState()
    End Sub

    ' ---- buttons ----

    Private Async Sub btnTest_Click(sender As Object, e As EventArgs) Handles btnTest.Click
        If _cts IsNot Nothing Then
            _cts.Cancel()   ' the button reads "Cancel" while our own request runs
            Return
        End If

        ' Read the inputs now, before the first await.
        Dim regionText = cboRegion.Text
        Dim typedKey = txtKey.Text
        Await RunRequestAsync(Function(ct) _session.TestConnectionAsync(regionText, typedKey, ct), clearKeyBoxOnSuccess:=True)
    End Sub

    Private Async Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        Await RunRequestAsync(Function(ct) _session.RefreshVoicesAsync(ct), clearKeyBoxOnSuccess:=False)
    End Sub

    Private Sub btnReplace_Click(sender As Object, e As EventArgs) Handles btnReplace.Click
        _replacing = True
        ApplyState()
        txtKey.Focus()
    End Sub

    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        Dim answer = MessageBox.Show(Me, "Remove the saved Azure key from this computer?", "Omskep",
                                     MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
        If answer <> DialogResult.Yes Then Return

        ShowResult(_session.ClearKey())
        _replacing = False
        txtKey.Clear()
        ApplyState()
    End Sub

    Private Async Function RunRequestAsync(work As Func(Of CancellationToken, Task(Of ActionResult)), clearKeyBoxOnSuccess As Boolean) As Task
        Dim cts As New CancellationTokenSource()
        _cts = cts
        lblResult.ForeColor = SystemColors.ControlText
        lblResult.Text = ConnectionMessages.Testing
        ApplyState()
        Try
            Dim result = Await work(cts.Token)
            If IsDisposed Then Return
            ShowResult(result)
            If result.Success AndAlso clearKeyBoxOnSuccess Then
                txtKey.Clear()   ' do not leave the key sitting in the box once it is saved
                _replacing = False
            End If
        Catch ex As OperationCanceledException
            If Not IsDisposed Then
                lblResult.ForeColor = SystemColors.ControlText
                lblResult.Text = "Cancelled. Nothing was changed."
            End If
        Finally
            _cts = Nothing
            cts.Dispose()
            ApplyState()
        End Try
    End Function

    ' ---- closing ----

    Private Sub SettingsForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If _cts IsNot Nothing Then _cts.Cancel()
    End Sub

    Private Sub SettingsForm_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If _session IsNot Nothing Then RemoveHandler _session.StateChanged, AddressOf OnSessionStateChanged
        txtKey.Clear()
    End Sub

End Class
