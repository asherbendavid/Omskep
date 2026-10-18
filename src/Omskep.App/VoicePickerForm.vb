Imports Omskep.Core.Documents
Imports Omskep.Core.Voices

''' <summary>
''' Lets the person choose a voice from the ordered, locale-grouped list VoicePickerListBuilder
''' produces. Searchable by short name or display name. Holds no decisions of its own -- the ordering
''' and grouping are already decided by the time this form sees them.
''' </summary>
Public Class VoicePickerForm

    Private ReadOnly _groups As IReadOnlyList(Of VoiceLocaleGroup)

    Private _selectedVoice As VoiceInfo

    ''' <summary>The voice chosen, or Nothing if the dialog was cancelled. Only meaningful after
    ''' ShowDialog returns DialogResult.OK. VB's auto-implemented ReadOnly only allows assignment from
    ''' the constructor (same as C#'s get-only auto-property), so this needs the full block with a
    ''' private setter to be assignable from AcceptSelection.</summary>
    Public Property SelectedVoice As VoiceInfo
        Get
            Return _selectedVoice
        End Get
        Private Set(value As VoiceInfo)
            _selectedVoice = value
        End Set
    End Property

    ''' <summary>Shows the picker modally and returns the chosen voice, or Nothing if cancelled.</summary>
    Public Shared Function PickVoice(owner As IWin32Window, groups As IReadOnlyList(Of VoiceLocaleGroup), Optional currentVoiceShortName As String = Nothing) As VoiceInfo
        Using form As New VoicePickerForm(groups, currentVoiceShortName)
            Return If(form.ShowDialog(owner) = DialogResult.OK, form.SelectedVoice, Nothing)
        End Using
    End Function

    Private Sub New(groups As IReadOnlyList(Of VoiceLocaleGroup), currentVoiceShortName As String)
        InitializeComponent()
        _groups = If(groups, Array.Empty(Of VoiceLocaleGroup)())
        PopulateList(String.Empty)
        If Not String.IsNullOrWhiteSpace(currentVoiceShortName) Then SelectCurrent(currentVoiceShortName)
        UpdateOkEnabled()
    End Sub

    Private Sub PopulateList(filter As String)
        lvVoices.BeginUpdate()
        Try
            lvVoices.Items.Clear()
            lvVoices.Groups.Clear()

            Dim trimmedFilter As String = If(filter, String.Empty).Trim()

            For Each group As VoiceLocaleGroup In _groups
                Dim matching As List(Of VoiceInfo) = group.Voices.Where(Function(v) Matches(v, trimmedFilter)).ToList()
                If matching.Count = 0 Then Continue For

                Dim lvGroup As New ListViewGroup(group.Locale)
                lvVoices.Groups.Add(lvGroup)

                For Each voice As VoiceInfo In matching
                    Dim label As String = voice.ShortName
                    If Not String.IsNullOrWhiteSpace(voice.DisplayName) AndAlso Not String.Equals(voice.DisplayName, voice.ShortName, StringComparison.Ordinal) Then
                        label &= " (" & voice.DisplayName & ")"
                    End If
                    If Not String.IsNullOrWhiteSpace(voice.Gender) Then label &= " — " & voice.Gender

                    Dim item As New ListViewItem(label) With {.Group = lvGroup, .Tag = voice}
                    lvVoices.Items.Add(item)
                Next
            Next
        Finally
            lvVoices.EndUpdate()
        End Try
    End Sub

    Private Shared Function Matches(voice As VoiceInfo, filter As String) As Boolean
        If filter.Length = 0 Then Return True
        Return (voice.ShortName IsNot Nothing AndAlso voice.ShortName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
               (voice.DisplayName IsNot Nothing AndAlso voice.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
    End Function

    Private Sub SelectCurrent(shortName As String)
        For Each item As ListViewItem In lvVoices.Items
            Dim voice As VoiceInfo = TryCast(item.Tag, VoiceInfo)
            If voice IsNot Nothing AndAlso String.Equals(voice.ShortName, shortName, StringComparison.OrdinalIgnoreCase) Then
                item.Selected = True
                item.EnsureVisible()
                Exit For
            End If
        Next
    End Sub

    Private Sub txtSearch_TextChanged(sender As Object, e As EventArgs) Handles txtSearch.TextChanged
        PopulateList(txtSearch.Text)
        UpdateOkEnabled()
    End Sub

    Private Sub lvVoices_SelectedIndexChanged(sender As Object, e As EventArgs) Handles lvVoices.SelectedIndexChanged
        UpdateOkEnabled()
    End Sub

    Private Sub UpdateOkEnabled()
        btnOK.Enabled = lvVoices.SelectedItems.Count > 0
    End Sub

    Private Sub lvVoices_DoubleClick(sender As Object, e As EventArgs) Handles lvVoices.DoubleClick
        If lvVoices.SelectedItems.Count = 0 Then Return
        AcceptSelection()
    End Sub

    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        AcceptSelection()
    End Sub

    Private Sub AcceptSelection()
        If lvVoices.SelectedItems.Count = 0 Then Return
        SelectedVoice = TryCast(lvVoices.SelectedItems(0).Tag, VoiceInfo)
        DialogResult = DialogResult.OK
        Close()
    End Sub

End Class