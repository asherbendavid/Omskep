Imports Omskep.Core.Access
Imports Omskep.Core.Speech

Namespace Session

    ''' <summary>Plain, user-facing text for the Settings page. Nothing here can contain the key.</summary>
    Public Module ConnectionMessages

        Public Const InvalidRegion As String =
            "The region must be a plain region name such as southafricanorth (lowercase letters and digits only)."
        Public Const NeedKey As String = "Enter your Azure key first."
        Public Const Busy As String = "Another request to Azure is already running. Wait for it to finish."
        Public Const NothingChanged As String = " Nothing was changed."
        Public Const SaveFailed As String =
            "The connection worked, but your settings could not be saved. Check that the settings folder is writable, then try again."
        Public Const ClearFailed As String =
            "The key could not be removed because the settings file is in use or not writable. Try again."
        Public Const KeyRemoved As String = "The saved key has been removed."
        Public Const Testing As String = "Testing the connection..."

        Public Function Plural(count As Integer, singular As String, pluralForm As String) As String
            Return count.ToString() & " " & If(count = 1, singular, pluralForm)
        End Function

        ''' <param name="prefix">For example "Connected." or "Voice list updated."</param>
        Public Function Connected(prefix As String, voices As Integer, locales As Integer, cacheSaved As Boolean) As String
            Dim text = prefix & " " & Plural(voices, "voice", "voices") & " in " & Plural(locales, "locale", "locales") & "."
            If Not cacheSaved Then
                text &= " The voice list could not be saved to disk, so it will be downloaded again next time."
            End If
            Return text
        End Function

        ''' <summary>Message for a failed call. <paramref name="suffix"/> is appended (for example NothingChanged).</summary>
        Public Function ForFailure(result As VoiceListResult, suffix As String) As String
            Select Case result.Outcome
                Case AzureOutcome.Rejected
                    Return AccessMessages.KeyRejected & suffix
                Case AzureOutcome.Offline
                    Return AccessMessages.Offline & suffix
                Case AzureOutcome.Transient
                    Return "Azure is temporarily busy or unavailable. Wait a moment and try again." & suffix
                Case Else
                    Dim code = If(result.StatusCode > 0, " (HTTP " & result.StatusCode.ToString() & ")", String.Empty)
                    Return "Azure returned an unexpected response" & code & ". Check the region, then try again." & suffix
            End Select
        End Function

    End Module

End Namespace
