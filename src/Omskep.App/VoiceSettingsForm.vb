Imports Omskep.Core.Session
Imports Omskep.Core.Voices

''' <summary>
''' Per-locale default voice and speaking-rate percent, separate from the key/region Settings window on
''' the developer's own call: key/region is set once and rarely touched again, while voice/rate tuning is
''' expected to happen regularly to keep things sounding right. Writes only through AppSession's
''' SetDefaultVoice/SetSpeakingRatePercent, which themselves only ever go through SettingsStore.Update.
''' </summary>
Public Class VoiceSettingsForm

    Private NotInheritable Class LocaleRow
        Public ReadOnly Property Locale As String
        Public ReadOnly Property VoiceCombo As ComboBox
        Public ReadOnly Property RateUpDown As NumericUpDown

        Public Sub New(locale As String, voiceCombo As ComboBox, rateUpDown As NumericUpDown)
            Me.Locale = locale
            Me.VoiceCombo = voiceCombo
            Me.RateUpDown = rateUpDown
        End Sub
    End Class

    Private ReadOnly _session As AppSession
    Private ReadOnly _rows As New List(Of LocaleRow)()

    Public Sub New(session As AppSession)
        InitializeComponent()
        _session = session
        BuildRows()
    End Sub

    ''' <summary>The form is a fixed, non-resizable size, so a row width tied to that known constant is
    ''' simpler and more reliable than reading pnlRows.ClientSize.Width at build time -- that reading
    ''' never accounted for the vertical scrollbar's own width, which (with the dozens of locales a real
    ''' catalog has) is present from the very first row, clipping the rate controls off the right edge
    ''' of every row.</summary>
    Private Const RowWidth As Integer = 460

    Private Sub BuildRows()
        Dim catalog As VoiceCatalog = _session.Catalog
        If catalog Is Nothing Then Return

        Dim currentVoices As IReadOnlyDictionary(Of String, String) = _session.DefaultVoices()
        Dim currentRates As IReadOnlyDictionary(Of String, Integer) = _session.SpeakingRatePercent()

        pnlRows.SuspendLayout()
        Try
            For Each locale As String In catalog.Locales
                Dim voices As IReadOnlyList(Of VoiceInfo) = catalog.VoicesFor(locale)
                If voices.Count = 0 Then Continue For

                Dim row As New Panel With {.Size = New Size(RowWidth, 30), .Margin = New Padding(0, 2, 0, 2)}

                Dim lblLocale As New Label With {.Text = locale, .Location = New Point(0, 7), .Size = New Size(70, 20), .TextAlign = ContentAlignment.MiddleLeft}

                Dim cboVoice As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Location = New Point(74, 3), .Size = New Size(260, 24)}
                For Each voice As VoiceInfo In voices
                    cboVoice.Items.Add(voice)
                Next
                cboVoice.DisplayMember = NameOf(VoiceInfo.ShortName)

                Dim selectedShortName As String = Nothing
                currentVoices.TryGetValue(locale, selectedShortName)
                Dim selectedVoice As VoiceInfo = voices.FirstOrDefault(Function(v) String.Equals(v.ShortName, selectedShortName, StringComparison.OrdinalIgnoreCase))
                cboVoice.SelectedItem = If(selectedVoice, voices(0))

                Dim lblRate As New Label With {.Text = "Rate %:", .Location = New Point(344, 7), .Size = New Size(50, 20), .TextAlign = ContentAlignment.MiddleLeft}

                Dim nudRate As New NumericUpDown With {.Location = New Point(396, 3), .Size = New Size(60, 24), .Minimum = -90, .Maximum = 100}
                Dim rate As Integer = 0
                currentRates.TryGetValue(locale, rate)
                nudRate.Value = Math.Max(nudRate.Minimum, Math.Min(nudRate.Maximum, rate))

                row.Controls.Add(lblLocale)
                row.Controls.Add(cboVoice)
                row.Controls.Add(lblRate)
                row.Controls.Add(nudRate)
                pnlRows.Controls.Add(row)

                _rows.Add(New LocaleRow(locale, cboVoice, nudRate))
            Next
        Finally
            pnlRows.ResumeLayout(True)
        End Try
    End Sub

    Private Sub btnOK_Click(sender As Object, e As EventArgs) Handles btnOK.Click
        Dim catalog As VoiceCatalog = _session.Catalog
        For Each row As LocaleRow In _rows
            Dim chosen As VoiceInfo = TryCast(row.VoiceCombo.SelectedItem, VoiceInfo)
            If chosen IsNot Nothing AndAlso (catalog Is Nothing OrElse catalog.Contains(chosen.ShortName)) Then
                _session.SetDefaultVoice(row.Locale, chosen.ShortName)
            End If
            _session.SetSpeakingRatePercent(row.Locale, CInt(row.RateUpDown.Value))
        Next
        DialogResult = DialogResult.OK
        Close()
    End Sub

End Class