Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Threading
Imports System.Security.Cryptography
Imports System.Text
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Speech
Imports Omskep.Core.Storage

Namespace Export

    Public Enum ChunkExportStatus
        Pending
        Done
        Failed
    End Enum

    ''' <summary>Live state for progress reporting only -- not stored on the final result.</summary>
    Public Enum ChunkLiveState
        Sending
        Retrying
    End Enum

    ''' <summary>Result of attempting one chunk. Audio is Nothing unless Status is Done.</summary>
    Public NotInheritable Class ChunkExportOutcome
        Public ReadOnly Property Status As ChunkExportStatus
        Public ReadOnly Property Audio As Byte()
        Public ReadOnly Property RetriesUsed As Integer
        ''' <summary>True if a retry happened after at least one request actually reached Azure -- the chunk may have been billed more than once.</summary>
        Public ReadOnly Property PossiblyBilledTwice As Boolean
        ''' <summary>Plain-language reason. Nothing unless Status is Failed.</summary>
        Public ReadOnly Property FailureReason As String
        ''' <summary>SHA-256 of the chunk's SSML at the moment it was synthesized, hex-encoded. Nothing unless
        ''' Status is Done. Lets a later resume attempt tell whether this chunk's text has changed since.</summary>
        Public ReadOnly Property ContentHash As String

        Private Sub New(status As ChunkExportStatus, audio As Byte(), retriesUsed As Integer, possiblyBilledTwice As Boolean, failureReason As String, contentHash As String)
            Me.Status = status
            Me.Audio = audio
            Me.RetriesUsed = retriesUsed
            Me.PossiblyBilledTwice = possiblyBilledTwice
            Me.FailureReason = failureReason
            Me.ContentHash = contentHash
        End Sub

        Friend Shared Function Pending() As ChunkExportOutcome
            Return New ChunkExportOutcome(ChunkExportStatus.Pending, Nothing, 0, False, Nothing, Nothing)
        End Function

        Friend Shared Function Done(audio As Byte(), retriesUsed As Integer, possiblyBilledTwice As Boolean, contentHash As String) As ChunkExportOutcome
            Return New ChunkExportOutcome(ChunkExportStatus.Done, audio, retriesUsed, possiblyBilledTwice, Nothing, contentHash)
        End Function

        Friend Shared Function Failed(retriesUsed As Integer, possiblyBilledTwice As Boolean, reason As String) As ChunkExportOutcome
            Return New ChunkExportOutcome(ChunkExportStatus.Failed, Nothing, retriesUsed, possiblyBilledTwice, reason, Nothing)
        End Function
    End Class

    Public Enum ExportStatus
        Complete
        Cancelled
        ''' <summary>Stopped at some chunk, or never started (export was not allowed). Call ExportAsync again --
        ''' pass this same result back as priorAttempt to pick up after the last chunk that finished.</summary>
        FailedResumable
        ''' <summary>Every chunk was synthesized; only the final file write failed (disk full, permissions, a
        ''' locked file). The audio is fine -- only saving failed. Call ExportAsync again with this result as
        ''' priorAttempt (same chunk list) to retry just the write; no chunk is re-synthesized.</summary>
        FailedWriteOnly
    End Enum

    ''' <summary>One export attempt's outcome. Chunks has exactly one entry per input chunk, in order, even on failure or cancellation.</summary>
    Public NotInheritable Class ExportResult
        Public ReadOnly Property Status As ExportStatus
        Public ReadOnly Property Chunks As IReadOnlyList(Of ChunkExportOutcome)
        ''' <summary>Set only when Status is Complete.</summary>
        Public ReadOnly Property OutputPath As String
        Public ReadOnly Property Message As String

        Private Sub New(status As ExportStatus, chunks As IReadOnlyList(Of ChunkExportOutcome), outputPath As String, message As String)
            Me.Status = status
            Me.Chunks = chunks
            Me.OutputPath = outputPath
            Me.Message = message
        End Sub

        Friend Shared Function Complete(chunks As IReadOnlyList(Of ChunkExportOutcome), outputPath As String, message As String) As ExportResult
            Return New ExportResult(ExportStatus.Complete, chunks, outputPath, message)
        End Function

        Friend Shared Function Cancelled(chunks As IReadOnlyList(Of ChunkExportOutcome), message As String) As ExportResult
            Return New ExportResult(ExportStatus.Cancelled, chunks, Nothing, message)
        End Function

        Friend Shared Function FailedResumable(chunks As IReadOnlyList(Of ChunkExportOutcome), message As String) As ExportResult
            Return New ExportResult(ExportStatus.FailedResumable, chunks, Nothing, message)
        End Function

        Friend Shared Function FailedWriteOnly(chunks As IReadOnlyList(Of ChunkExportOutcome), message As String) As ExportResult
            Return New ExportResult(ExportStatus.FailedWriteOnly, chunks, Nothing, message)
        End Function
    End Class

    Public NotInheritable Class ExportProgress
        Public ReadOnly Property ChunkIndex As Integer
        Public ReadOnly Property TotalChunks As Integer
        Public ReadOnly Property State As ChunkLiveState
        ''' <summary>0 on the first attempt; 1, 2, ... on each retry.</summary>
        Public ReadOnly Property RetryAttempt As Integer

        Public Sub New(chunkIndex As Integer, totalChunks As Integer, state As ChunkLiveState, retryAttempt As Integer)
            Me.ChunkIndex = chunkIndex
            Me.TotalChunks = totalChunks
            Me.State = state
            Me.RetryAttempt = retryAttempt
        End Sub
    End Class

    ''' <summary>
    ''' Synthesizes already-composed, already-chunked chunks to one MP3. Critical. Does not compose or
    ''' chunk -- callers run SsmlComposer and Chunker first and pass the result in.
    '''
    ''' Resume is same-session only, per the phase 3a decision: no manifest, no cross-restart state.
    ''' Pass a prior ExportAsync call's result back in as priorAttempt (same chunk list) to skip the
    ''' chunks it already finished and continue from the first one that did not.
    '''
    ''' Sanity check deviates from the starter prompt's "characters sent / 12,000" estimate: phase 3a's
    ''' own spike testing showed the estimated-vs-actual duration gap can be large even on ordinary
    ''' content, not just markup-heavy edge cases, so using it as a pass/fail gate risks failing a
    ''' perfectly good chunk. Instead this only rejects a response that is implausibly short in an
    ''' absolute sense (near-empty, i.e. likely truncated), regardless of what any duration estimate
    ''' said going in. Worth the developer's explicit sign-off before this is relied on for real exports.
    ''' </summary>
    Public NotInheritable Class ExportEngine

        Public Const MaxRetriesPerChunk As Integer = 2
        ''' <summary>A real MP3 response for even a few spoken words is several KB; a response under
        ''' this is treated as likely truncated rather than genuinely that short.</summary>
        Public Const MinPlausibleAudioBytes As Integer = 2000

        Private Shared ReadOnly DefaultRetryDelay As TimeSpan = TimeSpan.FromSeconds(3)

        Private ReadOnly _client As ISpeechClient
        Private ReadOnly _secrets As ISecretStore
        Private ReadOnly _session As AppSession
        Private ReadOnly _monitor As ConnectionMonitor
        Private ReadOnly _retryDelay As TimeSpan

        Public Sub New(client As ISpeechClient, secrets As ISecretStore, session As AppSession, monitor As ConnectionMonitor,
                       Optional retryDelay As TimeSpan? = Nothing)
            If client Is Nothing Then Throw New ArgumentNullException(NameOf(client))
            If secrets Is Nothing Then Throw New ArgumentNullException(NameOf(secrets))
            If session Is Nothing Then Throw New ArgumentNullException(NameOf(session))
            If monitor Is Nothing Then Throw New ArgumentNullException(NameOf(monitor))
            _client = client
            _secrets = secrets
            _session = session
            _monitor = monitor
            _retryDelay = If(retryDelay, DefaultRetryDelay)
        End Sub

        Public Async Function ExportAsync(chunks As IReadOnlyList(Of DocumentChunk), outputPath As String, cancellationToken As CancellationToken,
                                           Optional progress As IProgress(Of ExportProgress) = Nothing,
                                           Optional priorAttempt As ExportResult = Nothing) As Task(Of ExportResult)
            If chunks Is Nothing OrElse chunks.Count = 0 Then
                Return ExportResult.FailedResumable(New List(Of ChunkExportOutcome)(), "There is nothing to export.")
            End If
            If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("An output path is required.", NameOf(outputPath))

            If Not _session.Access.CanExport Then
                Return ExportResult.FailedResumable(New List(Of ChunkExportOutcome)(), _session.Access.Message)
            End If

            Dim outcomes As New List(Of ChunkExportOutcome)
            Dim startIndex As Integer = 0
            If priorAttempt IsNot Nothing AndAlso priorAttempt.Chunks IsNot Nothing AndAlso priorAttempt.Chunks.Count = chunks.Count Then
                For i As Integer = 0 To priorAttempt.Chunks.Count - 1
                    Dim priorOutcome As ChunkExportOutcome = priorAttempt.Chunks(i)
                    If priorOutcome.Status <> ChunkExportStatus.Done Then Exit For
                    If priorOutcome.ContentHash <> HashOf(chunks(i).Ssml) Then Exit For
                    outcomes.Add(priorOutcome)
                Next
                startIndex = outcomes.Count
            End If

            For i As Integer = startIndex To chunks.Count - 1
                If cancellationToken.IsCancellationRequested Then
                    PadWithPending(outcomes, chunks.Count)
                    Return ExportResult.Cancelled(outcomes, "Export cancelled.")
                End If

                Dim outcome As ChunkExportOutcome = Await SynthesizeChunkWithRetriesAsync(chunks(i), i, chunks.Count, cancellationToken, progress).ConfigureAwait(False)
                outcomes.Add(outcome)

                If outcome.Status = ChunkExportStatus.Failed Then
                    PadWithPending(outcomes, chunks.Count)
                    Return ExportResult.FailedResumable(
                        outcomes,
                        "Chunk " & (i + 1).ToString() & " of " & chunks.Count.ToString() & " failed: " & outcome.FailureReason &
                        " The chunks completed so far are kept; export again to pick up from here.")
                End If
            Next

            Dim joined As Byte() = JoinAudio(outcomes)
            Try
                AtomicFile.WriteAllBytes(outputPath, joined)
            Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                ' Every chunk succeeded; only the final write failed. Calling again with this result as
                ' priorAttempt will skip straight to retrying the write, no chunk is re-synthesized.
                Return ExportResult.FailedWriteOnly(outcomes, "Every chunk was synthesized, but the file could not be written: " & ex.Message)
            End Try

            Return ExportResult.Complete(outcomes, outputPath, "Export complete: " & chunks.Count.ToString() & " chunk(s), " & joined.Length.ToString("N0") & " bytes.")
        End Function

        Private Async Function SynthesizeChunkWithRetriesAsync(chunk As DocumentChunk, index As Integer, total As Integer,
                                                                 cancellationToken As CancellationToken, progress As IProgress(Of ExportProgress)) As Task(Of ChunkExportOutcome)
            Dim retries As Integer = 0
            Dim sendsAttempted As Integer = 0

            Do
                cancellationToken.ThrowIfCancellationRequested()

                Dim secret As SecretLoadResult = _secrets.Load()
                If secret.Status <> KeyState.Present OrElse String.IsNullOrEmpty(secret.Key) Then
                    Return ChunkExportOutcome.Failed(retries, sendsAttempted > 1, "No Azure key is currently available.")
                End If

                Dim state As ChunkLiveState = If(retries = 0, ChunkLiveState.Sending, ChunkLiveState.Retrying)
                progress?.Report(New ExportProgress(index, total, state, retries))

                sendsAttempted += 1
                Dim result As SynthesisResult = Await _client.SynthesizeAsync(chunk.Ssml, _session.Region, secret.Key, cancellationToken).ConfigureAwait(False)
                _monitor.Report(result.Outcome)

                If result.Outcome = AzureOutcome.Rejected Then
                    ' A bad key or region will not fix itself by retrying.
                    Return ChunkExportOutcome.Failed(retries, sendsAttempted > 1, "Azure rejected the key or region. Check both in Settings, then export again.")
                End If

                If result.Outcome = AzureOutcome.Ok Then
                    Dim implausibleReason As String = ImplausibilityReason(result.Audio)
                    If implausibleReason Is Nothing Then
                        Return ChunkExportOutcome.Done(result.Audio, retries, sendsAttempted > 1, HashOf(chunk.Ssml))
                    End If
                    If retries >= MaxRetriesPerChunk Then
                        Return ChunkExportOutcome.Failed(retries, sendsAttempted > 1, implausibleReason)
                    End If
                Else
                    If retries >= MaxRetriesPerChunk Then
                        Return ChunkExportOutcome.Failed(retries, sendsAttempted > 1, DescribeFailure(result))
                    End If
                End If

                retries += 1
                Dim delay As TimeSpan = If(result.RetryAfter.HasValue AndAlso result.RetryAfter.Value > _retryDelay, result.RetryAfter.Value, _retryDelay)
                Await Task.Delay(delay, cancellationToken).ConfigureAwait(False)
            Loop
        End Function

        Private Function HashOf(ssml As String) As String
            Using sha As SHA256 = SHA256.Create()
                Return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(ssml)))
            End Using
        End Function

        ''' <summary>Nothing when the audio is plausible; otherwise a user-facing reason. See the class remarks for why this does not use the duration estimate.</summary>
        Private Shared Function ImplausibilityReason(audio As Byte()) As String
            If audio Is Nothing OrElse audio.Length = 0 Then Return "Azure returned no audio for this chunk."
            If audio.Length < MinPlausibleAudioBytes Then Return "The returned audio (" & audio.Length.ToString("N0") & " bytes) looks too short to be real -- likely truncated."
            Return Nothing
        End Function

        Private Shared Function DescribeFailure(result As SynthesisResult) As String
            Select Case result.Outcome
                Case AzureOutcome.Offline
                    Return "Could not reach Azure."
                Case AzureOutcome.Transient
                    Return "Azure was temporarily unavailable (" & result.Detail & ")."
                Case AzureOutcome.Failed
                    Return "Azure returned an unexpected response (" & result.Detail & ")."
                Case Else
                    Return result.Detail
            End Select
        End Function

        Private Shared Sub PadWithPending(outcomes As List(Of ChunkExportOutcome), totalCount As Integer)
            While outcomes.Count < totalCount
                outcomes.Add(ChunkExportOutcome.Pending())
            End While
        End Sub

        ''' <summary>Plain byte concatenation -- confirmed seamless for headerless CBR MP3 chunks in the phase 0 spike.</summary>
        Private Shared Function JoinAudio(outcomes As IReadOnlyList(Of ChunkExportOutcome)) As Byte()
            Dim totalLength As Integer = 0
            For Each o As ChunkExportOutcome In outcomes
                totalLength += o.Audio.Length
            Next
            Dim joined(totalLength - 1) As Byte
            Dim position As Integer = 0
            For Each o As ChunkExportOutcome In outcomes
                Array.Copy(o.Audio, 0, joined, position, o.Audio.Length)
                position += o.Audio.Length
            Next
            Return joined
        End Function

    End Class

End Namespace
