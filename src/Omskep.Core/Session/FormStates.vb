Imports Omskep.Core.Access

Namespace Session

    ''' <summary>Which Settings controls are visible and enabled. Pure, so the form only copies values across.</summary>
    Public NotInheritable Class SettingsFormState
        Public ReadOnly Property ShowKeyBox As Boolean
        Public ReadOnly Property ShowSavedPanel As Boolean
        Public ReadOnly Property InputsEnabled As Boolean
        Public ReadOnly Property TestEnabled As Boolean
        ''' <summary>While this form's own request runs, the Test button becomes Cancel.</summary>
        Public ReadOnly Property TestIsCancel As Boolean
        Public ReadOnly Property RefreshEnabled As Boolean
        Public ReadOnly Property ReplaceEnabled As Boolean
        Public ReadOnly Property ClearEnabled As Boolean
        Public ReadOnly Property ShowProgress As Boolean

        Private Sub New(showKeyBox As Boolean, showSavedPanel As Boolean, inputsEnabled As Boolean, testEnabled As Boolean,
                        testIsCancel As Boolean, refreshEnabled As Boolean, replaceEnabled As Boolean, clearEnabled As Boolean, showProgress As Boolean)
            Me.ShowKeyBox = showKeyBox
            Me.ShowSavedPanel = showSavedPanel
            Me.InputsEnabled = inputsEnabled
            Me.TestEnabled = testEnabled
            Me.TestIsCancel = testIsCancel
            Me.RefreshEnabled = refreshEnabled
            Me.ReplaceEnabled = replaceEnabled
            Me.ClearEnabled = clearEnabled
            Me.ShowProgress = showProgress
        End Sub

        ''' <param name="ownRequestRunning">A request started from this form (cancellable).</param>
        ''' <param name="otherRequestRunning">A request started elsewhere, such as the startup fetch (not cancellable here).</param>
        Public Shared Function From(keySaved As Boolean, replacing As Boolean, ownRequestRunning As Boolean,
                                    otherRequestRunning As Boolean, typedKeyLength As Integer, regionValid As Boolean) As SettingsFormState
            Dim busy = ownRequestRunning OrElse otherRequestRunning
            Dim showSaved = keySaved AndAlso Not replacing
            Dim canTest = regionValid AndAlso (typedKeyLength > 0 OrElse keySaved)

            Return New SettingsFormState(
                showKeyBox:=Not showSaved,
                showSavedPanel:=showSaved,
                inputsEnabled:=Not busy,
                testEnabled:=ownRequestRunning OrElse (Not otherRequestRunning AndAlso canTest),
                testIsCancel:=ownRequestRunning,
                refreshEnabled:=keySaved AndAlso Not busy,
                replaceEnabled:=showSaved AndAlso Not busy,
                clearEnabled:=showSaved AndAlso Not busy,
                showProgress:=busy)
        End Function
    End Class

    ''' <summary>What the main window shows for a given access state. Pure.</summary>
    Public NotInheritable Class MainFormState
        Public ReadOnly Property EditEnabled As Boolean
        Public ReadOnly Property ExportEnabled As Boolean
        Public ReadOnly Property ShowBanner As Boolean
        Public ReadOnly Property BannerText As String
        Public ReadOnly Property ShowRetry As Boolean
        Public ReadOnly Property RetryEnabled As Boolean
        Public ReadOnly Property StatusText As String

        Private Sub New(editEnabled As Boolean, exportEnabled As Boolean, showBanner As Boolean, bannerText As String,
                        showRetry As Boolean, retryEnabled As Boolean, statusText As String)
            Me.EditEnabled = editEnabled
            Me.ExportEnabled = exportEnabled
            Me.ShowBanner = showBanner
            Me.BannerText = bannerText
            Me.ShowRetry = showRetry
            Me.RetryEnabled = retryEnabled
            Me.StatusText = statusText
        End Sub

        Public Shared Function From(access As AccessState, isBusy As Boolean, canRetry As Boolean, voiceCount As Integer) As MainFormState
            If access Is Nothing Then Throw New ArgumentNullException(NameOf(access))

            Dim status As String
            If isBusy Then
                status = "Contacting Azure..."
            Else
                Select Case access.Mode
                    Case AccessMode.Full
                        status = "Ready. " & ConnectionMessages.Plural(voiceCount, "voice", "voices") & " available."
                    Case AccessMode.EditOnly
                        status = "Editing only: export is blocked. Open Settings to fix the key or region."
                    Case Else
                        status = "Locked. Open Settings."
                End Select
            End If

            Return New MainFormState(
                editEnabled:=access.CanEdit,
                exportEnabled:=access.CanExport,
                showBanner:=access.Mode <> AccessMode.Full,
                bannerText:=access.Message,
                showRetry:=canRetry,
                retryEnabled:=Not isBusy,
                statusText:=status)
        End Function
    End Class

End Namespace
