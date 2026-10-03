Namespace Access

    ''' <summary>
    ''' Pure functions: no I/O, no clock, no UI. Every branch of the lockout state machine
    ''' lives here so each can be unit tested.
    ''' </summary>
    Public Module AccessEvaluator

        Public Function Evaluate(key As KeyState, cache As CacheStatus, connection As ConnectionSnapshot) As AccessState
            If connection Is Nothing Then Throw New ArgumentNullException(NameOf(connection))

            Select Case key
                Case KeyState.Absent
                    Return Locked(AccessReason.NoKey, AccessMessages.NoKey)
                Case KeyState.SettingsFileDamaged
                    Return Locked(AccessReason.SettingsFileDamaged, AccessMessages.SettingsFileDamaged)
                Case KeyState.BlobUnreadable
                    Return Locked(AccessReason.KeyUnreadable, AccessMessages.KeyUnreadable)
                Case KeyState.SettingsUnreadable
                    Return Locked(AccessReason.SettingsUnreadable, AccessMessages.SettingsUnreadable)
                Case KeyState.Present
                    If cache = CacheStatus.Present Then
                        Return EvaluateWithCache(connection)
                    End If
                    Return EvaluateWithoutCache(connection)
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(key))
            End Select
        End Function

        ' Key + cached voices: editing always allowed. Only a sticky rejection blocks export.
        ' Offline / transient / failed never lock anything while a cache exists.
        Private Function EvaluateWithCache(connection As ConnectionSnapshot) As AccessState
            If connection.KeyRejected Then
                Return New AccessState(AccessMode.EditOnly, AccessReason.KeyRejected, AccessMessages.ExportBlockedKeyRejected)
            End If
            Return New AccessState(AccessMode.Full, AccessReason.Ready, String.Empty)
        End Function

        ' Key but no usable cache: locked, and the message follows the latest Azure result.
        Private Function EvaluateWithoutCache(connection As ConnectionSnapshot) As AccessState
            Select Case connection.LastOutcome
                Case AzureOutcome.None
                    Return Locked(AccessReason.NeedsConnectionTest, AccessMessages.NeedsConnectionTest)
                Case AzureOutcome.Ok
                    Return Locked(AccessReason.NoVoicesAvailable, AccessMessages.NoVoicesAvailable)
                Case AzureOutcome.Rejected
                    Return Locked(AccessReason.KeyRejected, AccessMessages.KeyRejected)
                Case AzureOutcome.Offline
                    Return Locked(AccessReason.Offline, AccessMessages.Offline)
                Case AzureOutcome.Transient
                    Return Locked(AccessReason.AzureTemporarilyUnavailable, AccessMessages.AzureTemporarilyUnavailable)
                Case AzureOutcome.Failed
                    Return Locked(AccessReason.AzureUnexpectedResponse, AccessMessages.AzureUnexpectedResponse)
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(connection))
            End Select
        End Function

        Private Function Locked(reason As AccessReason, message As String) As AccessState
            Return New AccessState(AccessMode.Locked, reason, message)
        End Function

        ''' <summary>
        ''' A cache counts only if it has voices AND was fetched for the region currently selected.
        ''' </summary>
        Public Function CacheStatusFor(cachedRegion As String, cachedVoiceCount As Integer, currentRegion As String) As CacheStatus
            If cachedVoiceCount <= 0 Then Return CacheStatus.Missing
            If String.IsNullOrWhiteSpace(cachedRegion) OrElse String.IsNullOrWhiteSpace(currentRegion) Then Return CacheStatus.Missing
            If String.Equals(cachedRegion.Trim(), currentRegion.Trim(), StringComparison.OrdinalIgnoreCase) Then
                Return CacheStatus.Present
            End If
            Return CacheStatus.Missing
        End Function

        ''' <summary>
        ''' Classifies an HTTP status from Azure Speech. Only 401 means "key or region rejected".
        ''' 429 is deliberately Transient: Microsoft's TTS FAQ says it is usually regional/voice
        ''' capacity, not exhausted quota, so it must never be read as a bad key or a spent quota.
        ''' </summary>
        Public Function OutcomeFromHttpStatus(statusCode As Integer) As AzureOutcome
            Select Case statusCode
                Case 200
                    Return AzureOutcome.Ok
                Case 401
                    Return AzureOutcome.Rejected
                Case 408, 429
                    Return AzureOutcome.Transient
                Case 500 To 599
                    Return AzureOutcome.Transient
                Case Else
                    Return AzureOutcome.Failed
            End Select
        End Function

    End Module

    ''' <summary>
    ''' Session-lifetime record of Azure results. Phase 3's export code calls Report(...) after
    ''' every Azure call so a mid-export 401 blocks further exports without touching the stored key.
    ''' Thread-safe; callers marshal to the UI thread themselves before touching controls.
    ''' </summary>
    Public NotInheritable Class ConnectionMonitor
        Private ReadOnly _lock As New Object()
        Private _last As AzureOutcome = AzureOutcome.None
        Private _rejected As Boolean

        Public ReadOnly Property Snapshot As ConnectionSnapshot
            Get
                SyncLock _lock
                    Return New ConnectionSnapshot(_last, _rejected)
                End SyncLock
            End Get
        End Property

        Public Sub Report(outcome As AzureOutcome)
            If outcome = AzureOutcome.None Then Return
            SyncLock _lock
                _last = outcome
                If outcome = AzureOutcome.Ok Then
                    _rejected = False
                ElseIf outcome = AzureOutcome.Rejected Then
                    _rejected = True
                End If
                ' Offline / Transient / Failed leave a standing rejection in place.
            End SyncLock
        End Sub

        ''' <summary>Call when the key or region changes: old results say nothing about the new ones.</summary>
        Public Sub Reset()
            SyncLock _lock
                _last = AzureOutcome.None
                _rejected = False
            End SyncLock
        End Sub
    End Class

End Namespace
