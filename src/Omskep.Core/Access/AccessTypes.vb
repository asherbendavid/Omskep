Namespace Access

    ''' <summary>What Omskep knows about the stored Azure key at startup.</summary>
    Public Enum KeyState
        ''' <summary>No settings file, or no key inside it (also: key cleared by the user).</summary>
        Absent
        ''' <summary>settings.json exists but is not valid JSON.</summary>
        SettingsFileDamaged
        ''' <summary>A key blob is present but failed to decrypt (CryptographicException).</summary>
        BlobUnreadable
        ''' <summary>A key was loaded successfully.</summary>
        Present
        ''' <summary>settings.json could not be read (locked or in use) even after retries. Nothing was changed.</summary>
        SettingsUnreadable
    End Enum

    ''' <summary>Whether a usable voice list is cached for the CURRENT region.</summary>
    Public Enum CacheStatus
        ''' <summary>No cache, empty cache, unparseable cache, or cache for a different region.</summary>
        Missing
        Present
    End Enum

    ''' <summary>Result of the most recent Azure call, classified.</summary>
    Public Enum AzureOutcome
        ''' <summary>No Azure call has been made this session.</summary>
        None
        Ok
        ''' <summary>HTTP 401: bad key OR wrong region (indistinguishable).</summary>
        Rejected
        ''' <summary>Connection-level failure (no HTTP response at all).</summary>
        Offline
        ''' <summary>Retry-later failure: HTTP 408, 429, 5xx, or a timeout. Never implies a bad key.</summary>
        Transient
        ''' <summary>Anything else unexpected: other 4xx, malformed response body.</summary>
        Failed
    End Enum

    Public Enum AccessMode
        ''' <summary>Everything disabled except Settings.</summary>
        Locked
        ''' <summary>Editing allowed, export blocked.</summary>
        EditOnly
        ''' <summary>Everything allowed.</summary>
        Full
    End Enum

    Public Enum AccessReason
        Ready
        NoKey
        SettingsFileDamaged
        KeyUnreadable
        SettingsUnreadable
        NeedsConnectionTest
        KeyRejected
        Offline
        AzureTemporarilyUnavailable
        AzureUnexpectedResponse
        NoVoicesAvailable
    End Enum

    ''' <summary>User-facing text. None of these may ever contain the key.</summary>
    Public Module AccessMessages
        Public Const KeyRejected As String = "Azure rejected the key or region. Check both."
        Public Const NoKey As String = "Enter your Azure key and region in Settings to get started."
        Public Const SettingsFileDamaged As String =
            "The settings file could not be read, so it is being treated as empty. Enter your Azure key and region in Settings."
        Public Const KeyUnreadable As String =
            "The saved Azure key could not be read. Enter it again in Settings."
        Public Const SettingsUnreadable As String =
            "The settings file is in use by another program right now. Close anything that may be using it, then try again."
        Public Const NeedsConnectionTest As String =
            "Use Test connection in Settings to download the voice list."
        Public Const Offline As String =
            "Could not reach Azure. Check your internet connection, your firewall, and that the region name is spelled correctly, then try again."
        Public Const AzureTemporarilyUnavailable As String =
            "Azure is temporarily busy or unavailable. Your key has been kept. Wait a moment and try again."
        Public Const AzureUnexpectedResponse As String =
            "Azure returned an unexpected response. Your key has been kept. Try again, and check the region if it keeps happening."
        Public Const NoVoicesAvailable As String =
            "Azure returned no voices for this region. Check the region in Settings."
        Public Const ExportBlockedKeyRejected As String =
            "Azure rejected the key or region. Check both. Editing still works; export is blocked until this is fixed in Settings."
    End Module

    ''' <summary>The app-wide answer to "what may the user do right now, and what do we tell them?"</summary>
    Public NotInheritable Class AccessState
        Public ReadOnly Property Mode As AccessMode
        Public ReadOnly Property Reason As AccessReason
        ''' <summary>Empty when Mode is Full.</summary>
        Public ReadOnly Property Message As String

        Public ReadOnly Property CanEdit As Boolean
            Get
                Return Mode <> AccessMode.Locked
            End Get
        End Property

        Public ReadOnly Property CanExport As Boolean
            Get
                Return Mode = AccessMode.Full
            End Get
        End Property

        ''' <summary>Settings is reachable in every state. Kept explicit so a test can pin it.</summary>
        Public ReadOnly Property CanOpenSettings As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub New(mode As AccessMode, reason As AccessReason, message As String)
            Me.Mode = mode
            Me.Reason = reason
            Me.Message = message
        End Sub
    End Class

    ''' <summary>
    ''' LastOutcome is the most recent Azure result (drives the message when locked).
    ''' KeyRejected is sticky: set by a 401, cleared only by a success or a key/region change,
    ''' so a later offline blip cannot silently re-enable export.
    ''' </summary>
    Public NotInheritable Class ConnectionSnapshot
        Public ReadOnly Property LastOutcome As AzureOutcome
        Public ReadOnly Property KeyRejected As Boolean

        Public Shared ReadOnly Empty As New ConnectionSnapshot(AzureOutcome.None, False)

        Public Sub New(lastOutcome As AzureOutcome, keyRejected As Boolean)
            Me.LastOutcome = lastOutcome
            Me.KeyRejected = keyRejected
        End Sub
    End Class

End Namespace
