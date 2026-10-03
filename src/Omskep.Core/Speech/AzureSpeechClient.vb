Imports System.IO
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Voices

Namespace Speech

    Public NotInheritable Class AzureSpeechClient
        Implements ISpeechClient

        Public Const KeyHeaderName As String = "Ocp-Apim-Subscription-Key"
        Public Shared ReadOnly DefaultListTimeout As TimeSpan = TimeSpan.FromSeconds(60)

        Private ReadOnly _http As HttpClient
        Private ReadOnly _listTimeout As TimeSpan

        ''' <param name="http">Shared, long-lived. Prefer AzureHttpClientFactory.CreateClient().</param>
        Public Sub New(http As HttpClient, Optional listTimeout As TimeSpan? = Nothing)
            If http Is Nothing Then Throw New ArgumentNullException(NameOf(http))
            _http = http
            _listTimeout = If(listTimeout.HasValue AndAlso listTimeout.Value > TimeSpan.Zero, listTimeout.Value, DefaultListTimeout)
        End Sub

        Public Async Function GetVoicesAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceListResult) Implements ISpeechClient.GetVoicesAsync
            Dim host = AzureRegion.Normalize(region)
            If host Is Nothing Then Throw New ArgumentException("The region is not a valid Azure region id.", NameOf(region))
            If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentException("A key is required.", NameOf(key))

            ' Do not rely on HttpClient to notice an already-cancelled token: it differs between .NET versions.
            cancellationToken.ThrowIfCancellationRequested()

            Dim uri As New Uri("https://" & host & ".tts.speech.microsoft.com/cognitiveservices/voices/list")
            Using request As New HttpRequestMessage(HttpMethod.Get, uri)
                Try
                    request.Headers.Add(KeyHeaderName, key.Trim())
                Catch ex As FormatException
                    ' A key that cannot even be sent is a bad key. Nothing goes on the wire.
                    Return New VoiceListResult(AzureOutcome.Rejected, 0, "The key contains characters that cannot be sent.", Nothing)
                End Try
                request.Headers.UserAgent.ParseAdd("Omskep")

                Dim raw = Await SendAsync(request, _listTimeout, cancellationToken).ConfigureAwait(False)

                ' If the caller cancelled while the response was arriving, discard it: nothing should act on a cancelled fetch.
                cancellationToken.ThrowIfCancellationRequested()
                If raw.Outcome <> AzureOutcome.Ok Then
                    Return New VoiceListResult(raw.Outcome, raw.StatusCode, raw.Detail, Nothing)
                End If

                Try
                    Return New VoiceListResult(AzureOutcome.Ok, raw.StatusCode, String.Empty, VoiceListParser.Parse(raw.Body))
                Catch ex As JsonException
                    Return New VoiceListResult(AzureOutcome.Failed, raw.StatusCode, "Azure returned a voice list that could not be read.", Nothing)
                End Try
            End Using
        End Function

        ' ---- shared plumbing: phase 3's synthesis call reuses this ----

        Private NotInheritable Class RawResponse
            Public ReadOnly Property Outcome As AzureOutcome
            Public ReadOnly Property StatusCode As Integer
            Public ReadOnly Property Detail As String
            Public ReadOnly Property Body As Byte()

            Public Sub New(outcome As AzureOutcome, statusCode As Integer, detail As String, body As Byte())
                Me.Outcome = outcome
                Me.StatusCode = statusCode
                Me.Detail = detail
                Me.Body = body
            End Sub
        End Class

        ''' <summary>
        ''' Sends one request and classifies the result. The body is read only for HTTP 200. A caller cancellation
        ''' propagates as OperationCanceledException; our own timeout is a Transient outcome. Detail never includes
        ''' exception messages (they can echo URLs or headers), only a type name or status code.
        ''' </summary>
        Private Async Function SendAsync(request As HttpRequestMessage, timeout As TimeSpan, cancellationToken As CancellationToken) As Task(Of RawResponse)
            Using linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                linked.CancelAfter(timeout)

                Dim response As HttpResponseMessage
                Try
                    response = Await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(False)
                Catch ex As HttpRequestException
                    ' DNS, refused, TLS or dropped connection: no HTTP response at all.
                    Return New RawResponse(AzureOutcome.Offline, 0, "Could not connect to Azure (" & ex.GetType().Name & ").", Nothing)
                Catch ex As OperationCanceledException When Not cancellationToken.IsCancellationRequested
                    Return New RawResponse(AzureOutcome.Transient, 0, "Azure did not respond in time.", Nothing)
                End Try

                Using response
                    Dim status = CInt(response.StatusCode)
                    Dim outcome = AccessEvaluator.OutcomeFromHttpStatus(status)
                    If outcome <> AzureOutcome.Ok Then
                        Return New RawResponse(outcome, status, "HTTP " & status.ToString(), Nothing)
                    End If

                    Try
                        Dim body = Await response.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(False)
                        Return New RawResponse(AzureOutcome.Ok, status, String.Empty, body)
                    Catch ex As Exception When TypeOf ex Is HttpRequestException OrElse TypeOf ex Is IOException
                        Return New RawResponse(AzureOutcome.Transient, status, "The response from Azure was cut off.", Nothing)
                    Catch ex As OperationCanceledException When Not cancellationToken.IsCancellationRequested
                        Return New RawResponse(AzureOutcome.Transient, status, "Azure did not finish responding in time.", Nothing)
                    End Try
                End Using
            End Using
        End Function

    End Class

End Namespace