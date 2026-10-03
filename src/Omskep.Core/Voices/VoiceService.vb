Imports System.IO
Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Speech

Namespace Voices

    ''' <summary>What a refresh did. CacheSaved is False if nothing was written (failure, no voices, or a disk problem).</summary>
    Public NotInheritable Class VoiceRefreshResult
        Public ReadOnly Property Result As VoiceListResult
        Public ReadOnly Property CacheSaved As Boolean

        Public Sub New(result As VoiceListResult, cacheSaved As Boolean)
            Me.Result = result
            Me.CacheSaved = cacheSaved
        End Sub
    End Class

    ''' <summary>
    ''' Owns the app's current voice list: loads the cache at startup, fetches from Azure, keeps the cache and the
    ''' in-memory catalog in step, and reports results of the STORED key to the ConnectionMonitor.
    '''
    ''' Two paths, deliberately separate:
    '''   TestAsync    - for a key the user is typing. No cache writes, no monitor reports, so a bad candidate key
    '''                  can never mark the working key as rejected. Adopt it once the key has been saved.
    '''   RefreshAsync - for the stored key (startup, "Refresh voices"). Success replaces the cache; any failure keeps
    '''                  the old cache and is reported to the monitor.
    ''' Only one request runs at a time.
    ''' </summary>
    Public NotInheritable Class VoiceService

        Private ReadOnly _client As ISpeechClient
        Private ReadOnly _cache As VoiceCacheStore
        Private ReadOnly _monitor As ConnectionMonitor
        Private ReadOnly _utcNow As Func(Of DateTime)
        Private ReadOnly _lock As New Object()

        Private _catalog As VoiceCatalog = VoiceCatalog.Empty
        Private _catalogRegion As String
        Private _busy As Integer

        Public Sub New(client As ISpeechClient, cache As VoiceCacheStore, monitor As ConnectionMonitor, Optional utcNow As Func(Of DateTime) = Nothing)
            If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
            If cache Is Nothing Then Throw New ArgumentNullException(NameOf(cache))
            If monitor Is Nothing Then Throw New ArgumentNullException(NameOf(monitor))
            _client = client
            _cache = cache
            _monitor = monitor
            If utcNow Is Nothing Then
                _utcNow = Function() DateTime.UtcNow
            Else
                _utcNow = utcNow
            End If
        End Sub

        ''' <summary>The current voices (empty until a cache is loaded or a fetch is adopted).</summary>
        Public ReadOnly Property Catalog As VoiceCatalog
            Get
                SyncLock _lock
                    Return _catalog
                End SyncLock
            End Get
        End Property

        Public ReadOnly Property IsBusy As Boolean
            Get
                Return Volatile.Read(_busy) = 1
            End Get
        End Property

        ''' <summary>Startup: reads the cache file and uses it only if it has voices for the current region.</summary>
        Public Function LoadCache(currentRegion As String) As CacheStatus
            Dim loaded = _cache.Load()
            Dim status = CacheStatus.Missing
            If loaded.Status = VoiceCacheLoadStatus.Loaded Then status = loaded.Cache.StatusFor(currentRegion)

            SyncLock _lock
                If status = CacheStatus.Present Then
                    _catalog = New VoiceCatalog(loaded.Cache.Voices)
                    _catalogRegion = AzureRegion.Normalize(currentRegion)
                Else
                    _catalog = VoiceCatalog.Empty
                    _catalogRegion = Nothing
                End If
            End SyncLock
            Return status
        End Function

        ''' <summary>Feeds the lockout state machine: Present only if voices are held for this region.</summary>
        Public Function CacheStatusFor(currentRegion As String) As CacheStatus
            SyncLock _lock
                Dim region = AzureRegion.Normalize(currentRegion)
                If _catalog.Count > 0 AndAlso region IsNot Nothing AndAlso String.Equals(_catalogRegion, region, StringComparison.OrdinalIgnoreCase) Then
                    Return CacheStatus.Present
                End If
                Return CacheStatus.Missing
            End SyncLock
        End Function

        ''' <summary>Tries a (possibly new) key. Changes nothing: no cache write, no monitor report.</summary>
        Public Function TestAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceListResult)
            Return FetchAsync(region, key, cancellationToken)
        End Function

        ''' <summary>
        ''' Fetches with the stored key. A non-empty success replaces the cache and catalog. Everything else keeps the
        ''' old cache and catalog, and the outcome goes to the monitor. A caller cancellation propagates and changes nothing.
        ''' </summary>
        Public Async Function RefreshAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceRefreshResult)
            Dim result = Await FetchAsync(region, key, cancellationToken).ConfigureAwait(False)

            If result.Outcome = AzureOutcome.Ok AndAlso result.Voices.Count > 0 Then
                Return New VoiceRefreshResult(result, Adopt(result, region))
            End If

            _monitor.Report(result.Outcome)
            Return New VoiceRefreshResult(result, False)
        End Function

        ''' <summary>
        ''' Makes a successful test result current: sets the catalog, saves the cache for this region and reports Ok.
        ''' Call it only after the key that produced the result has been saved. Returns False if the cache file could
        ''' not be written; the voices still work for this session.
        ''' </summary>
        Public Function Adopt(result As VoiceListResult, region As String) As Boolean
            If result Is Nothing Then Throw New ArgumentNullException(NameOf(result))
            If result.Outcome <> AzureOutcome.Ok OrElse result.Voices.Count = 0 Then
                Throw New ArgumentException("Only a successful, non-empty voice list can be adopted.", NameOf(result))
            End If
            Dim normalized = AzureRegion.Normalize(region)
            If normalized Is Nothing Then Throw New ArgumentException("The region is not a valid Azure region id.", NameOf(region))

            SyncLock _lock
                _catalog = New VoiceCatalog(result.Voices)
                _catalogRegion = normalized
                _monitor.Report(AzureOutcome.Ok)

                Dim cache As New VoiceCache() With {
                    .Region = normalized,
                    .FetchedUtc = _utcNow(),
                    .Voices = New List(Of VoiceInfo)(result.Voices)
                }
                Try
                    _cache.Save(cache)
                    Return True
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    Return False
                End Try
            End SyncLock
        End Function

        ''' <summary>Call when the stored key or region changes: earlier results say nothing about the new ones.</summary>
        Public Sub CredentialsChanged()
            _monitor.Reset()
        End Sub

        Private Async Function FetchAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceListResult)
            If Interlocked.CompareExchange(_busy, 1, 0) <> 0 Then
                Throw New InvalidOperationException("A voice list request is already running.")
            End If
            Try
                Return Await _client.GetVoicesAsync(region, key, cancellationToken).ConfigureAwait(False)
            Finally
                Interlocked.Exchange(_busy, 0)
            End Try
        End Function

    End Class

End Namespace
