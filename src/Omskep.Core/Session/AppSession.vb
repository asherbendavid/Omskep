Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Secrets
Imports Omskep.Core.Settings
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Session

    ''' <summary>
    ''' The app-wide brain behind the main window and the Settings page. Holds the lockout state, runs the
    ''' Settings actions, and decides what gets saved. The forms only call it and display what it says.
    '''
    ''' Save policy: a typed key or changed region is saved ONLY when the connection test succeeds with voices.
    ''' Any other result (rejected, offline, busy, unexpected) changes nothing, so a possibly-working saved key is
    ''' never replaced by one that could not be verified. A candidate's failure is never reported to the connection
    ''' monitor, so it cannot block export for the working key.
    '''
    ''' Threading: StateChanged may be raised on any thread (an await resumes on the caller's context, which in
    ''' WinForms is the UI thread, but do not rely on it). Handlers must marshal to the UI thread themselves.
    ''' </summary>
    Public NotInheritable Class AppSession

        Private ReadOnly _settings As SettingsStore
        Private ReadOnly _secrets As ISecretStore
        Private ReadOnly _voices As VoiceService
        Private ReadOnly _monitor As ConnectionMonitor

        Private _keyState As KeyState = KeyState.Absent
        Private _region As String = AzureSettings.DefaultRegion

        Public Event StateChanged As EventHandler

        Public Sub New(settings As SettingsStore, secrets As ISecretStore, voices As VoiceService, monitor As ConnectionMonitor)
            If settings Is Nothing Then Throw New ArgumentNullException(NameOf(settings))
            If secrets Is Nothing Then Throw New ArgumentNullException(NameOf(secrets))
            If voices Is Nothing Then Throw New ArgumentNullException(NameOf(voices))
            If monitor Is Nothing Then Throw New ArgumentNullException(NameOf(monitor))
            _settings = settings
            _secrets = secrets
            _voices = voices
            _monitor = monitor
        End Sub

        ''' <summary>Wires up the real file-backed stores and Azure client for one settings folder.</summary>
        Public Shared Function Create(folder As String, protector As ISecretProtector, http As HttpClient) As AppSession
            Dim settings As New SettingsStore(folder)
            Dim monitor As New ConnectionMonitor()
            Dim voices As New VoiceService(New AzureSpeechClient(http), New VoiceCacheStore(folder), monitor)
            Return New AppSession(settings, New FileSecretStore(settings, protector), voices, monitor)
        End Function

        ' ---- what the UI reads ----

        Public ReadOnly Property Access As AccessState
            Get
                Return AccessEvaluator.Evaluate(_keyState, _voices.CacheStatusFor(_region), _monitor.Snapshot)
            End Get
        End Property

        Public ReadOnly Property KeyIsSaved As Boolean
            Get
                Return _keyState = KeyState.Present
            End Get
        End Property

        Public ReadOnly Property Region As String
            Get
                Return _region
            End Get
        End Property

        Public ReadOnly Property IsBusy As Boolean
            Get
                Return _voices.IsBusy
            End Get
        End Property

        Public ReadOnly Property VoiceCount As Integer
            Get
                Return _voices.Catalog.Count
            End Get
        End Property

        Public ReadOnly Property Catalog As VoiceCatalog
            Get
                Return _voices.Catalog
            End Get
        End Property

        ''' <summary>True when locked for a reason that trying again can fix (not a wrong or missing key).</summary>
        Public ReadOnly Property CanRetry As Boolean
            Get
                Dim a = Access
                If a.Mode <> AccessMode.Locked Then Return False
                Select Case a.Reason
                    Case AccessReason.SettingsUnreadable, AccessReason.NeedsConnectionTest, AccessReason.Offline,
                         AccessReason.AzureTemporarilyUnavailable, AccessReason.AzureUnexpectedResponse
                        Return True
                    Case Else
                        Return False
                End Select
            End Get
        End Property

        ' ---- startup ----

        ''' <summary>
        ''' Reads the key and cache from disk. Only if a key is saved and no usable cache exists does it fetch,
        ''' once. With a cache it never touches the network. Safe to call again as "try again".
        ''' </summary>
        Public Async Function StartupAsync(cancellationToken As CancellationToken) As Task
            If _voices.IsBusy Then Return

            LoadFromDisk()
            RaiseStateChanged()

            If _keyState <> KeyState.Present Then Return
            If _voices.CacheStatusFor(_region) = CacheStatus.Present Then Return

            Dim stored = _secrets.Load()
            If stored.Status <> KeyState.Present Then
                _keyState = stored.Status
                RaiseStateChanged()
                Return
            End If

            Await RefreshStoredAsync(_region, stored.Key, "Connected.", cancellationToken).ConfigureAwait(False)
        End Function

        Public Function RetryAsync(cancellationToken As CancellationToken) As Task
            Return StartupAsync(cancellationToken)
        End Function

        Private Sub LoadFromDisk()
            _keyState = _secrets.Load().Status
            Dim fromFile = _settings.Load().Value.Azure.Region
            _region = If(AzureRegion.Normalize(fromFile), AzureSettings.DefaultRegion)
            _voices.LoadCache(_region)
        End Sub

        ' ---- Settings actions ----

        ''' <summary>
        ''' The Settings "Test connection" button. With a typed key and/or a changed region this tests the candidate and
        ''' saves it only on success. With neither it simply re-checks the stored key (same as Refresh voices).
        ''' </summary>
        Public Async Function TestConnectionAsync(regionText As String, typedKey As String, cancellationToken As CancellationToken) As Task(Of ActionResult)
            Dim region = AzureRegion.Normalize(regionText)
            If region Is Nothing Then Return ActionResult.Fail(ConnectionMessages.InvalidRegion)

            Dim typed = If(typedKey, String.Empty).Trim()
            Dim hasTyped = typed.Length > 0
            If hasTyped Then
                Dim problem = KeyRules.Problem(typed)
                If problem IsNot Nothing Then Return ActionResult.Fail(problem)
            End If

            Dim stored = _secrets.Load()
            Dim regionChanged = Not String.Equals(region, _region, StringComparison.OrdinalIgnoreCase)

            If Not hasTyped AndAlso Not regionChanged Then
                If stored.Status <> KeyState.Present Then Return ActionResult.Fail(ConnectionMessages.NeedKey)
                Return Await RefreshStoredAsync(region, stored.Key, "Connected.", cancellationToken).ConfigureAwait(False)
            End If

            Dim candidate As String = Nothing
            If hasTyped Then
                candidate = typed
            ElseIf stored.Status = KeyState.Present Then
                candidate = stored.Key
            End If
            If candidate Is Nothing Then Return ActionResult.Fail(ConnectionMessages.NeedKey)

            If _voices.IsBusy Then Return ActionResult.Fail(ConnectionMessages.Busy)

            Dim pending = _voices.TestAsync(region, candidate, cancellationToken)
            RaiseStateChanged()
            Dim result As VoiceListResult
            Try
                result = Await pending.ConfigureAwait(False)
            Catch ex As InvalidOperationException
                Return ActionResult.Fail(ConnectionMessages.Busy)
            Finally
                RaiseStateChanged()
            End Try

            If result.Outcome <> AzureOutcome.Ok Then
                Return ActionResult.Fail(ConnectionMessages.ForFailure(result, ConnectionMessages.NothingChanged))
            End If
            If result.Voices.Count = 0 Then
                Return ActionResult.Fail(AccessMessages.NoVoicesAvailable & ConnectionMessages.NothingChanged)
            End If

            Try
                If hasTyped Then _secrets.Save(typed)
                If regionChanged Then _settings.Update(Sub(s) s.Azure.Region = region)
            Catch ex As Exception When IsStorageProblem(ex)
                Return ActionResult.Fail(ConnectionMessages.SaveFailed)
            End Try

            _region = region
            _keyState = KeyState.Present
            _voices.CredentialsChanged()
            Dim cacheSaved = _voices.Adopt(result, region)
            RaiseStateChanged()
            Return ActionResult.Ok(ConnectionMessages.Connected("Connected.", _voices.Catalog.Count, _voices.Catalog.Locales.Count, cacheSaved))
        End Function

        ''' <summary>The "Refresh voices" button: re-downloads the list with the stored key and region.</summary>
        Public Async Function RefreshVoicesAsync(cancellationToken As CancellationToken) As Task(Of ActionResult)
            Dim stored = _secrets.Load()
            If stored.Status <> KeyState.Present Then Return ActionResult.Fail(ConnectionMessages.NeedKey)
            Return Await RefreshStoredAsync(_region, stored.Key, "Voice list updated.", cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Removes the saved key. Region, voice defaults and the voice cache are left alone.</summary>
        Public Function ClearKey() As ActionResult
            Try
                _secrets.Clear()
            Catch ex As Exception When IsStorageProblem(ex)
                Return ActionResult.Fail(ConnectionMessages.ClearFailed)
            End Try
            _keyState = KeyState.Absent
            _voices.CredentialsChanged()
            RaiseStateChanged()
            Return ActionResult.Ok(ConnectionMessages.KeyRemoved)
        End Function

        ' ---- internals ----

        Private Async Function RefreshStoredAsync(region As String, key As String, successPrefix As String, cancellationToken As CancellationToken) As Task(Of ActionResult)
            If _voices.IsBusy Then Return ActionResult.Fail(ConnectionMessages.Busy)

            Dim pending = _voices.RefreshAsync(region, key, cancellationToken)
            RaiseStateChanged()
            Dim outcome As VoiceRefreshResult
            Try
                outcome = Await pending.ConfigureAwait(False)
            Catch ex As InvalidOperationException
                Return ActionResult.Fail(ConnectionMessages.Busy)
            Finally
                RaiseStateChanged()
            End Try

            Dim result = outcome.Result
            If result.Outcome = AzureOutcome.Ok AndAlso result.Voices.Count > 0 Then
                Return ActionResult.Ok(ConnectionMessages.Connected(successPrefix, _voices.Catalog.Count, _voices.Catalog.Locales.Count, outcome.CacheSaved))
            End If
            If result.Outcome = AzureOutcome.Ok Then
                Return ActionResult.Fail(AccessMessages.NoVoicesAvailable & ConnectionMessages.NothingChanged)
            End If
            Return ActionResult.Fail(ConnectionMessages.ForFailure(result, ConnectionMessages.NothingChanged))
        End Function

        Private Shared Function IsStorageProblem(ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is CryptographicException
        End Function

        Private Sub RaiseStateChanged()
            RaiseEvent StateChanged(Me, EventArgs.Empty)
        End Sub

    End Class

End Namespace
