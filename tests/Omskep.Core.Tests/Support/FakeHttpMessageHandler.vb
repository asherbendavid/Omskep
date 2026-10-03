Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Threading

Namespace Support

    Public NotInheritable Class RecordedRequest
        Public ReadOnly Property Method As String
        Public ReadOnly Property Url As String
        Public ReadOnly Property KeyHeader As String
        Public ReadOnly Property UserAgent As String

        Public Sub New(method As String, url As String, keyHeader As String, userAgent As String)
            Me.Method = method
            Me.Url = url
            Me.KeyHeader = keyHeader
            Me.UserAgent = userAgent
        End Sub
    End Class

    ''' <summary>A scriptable HttpMessageHandler: no network, records what the client sent.</summary>
    Public NotInheritable Class FakeHttpMessageHandler
        Inherits HttpMessageHandler

        Private ReadOnly _responder As Func(Of HttpRequestMessage, CancellationToken, Task(Of HttpResponseMessage))

        Public ReadOnly Property Requests As New List(Of RecordedRequest)()

        Public Sub New(responder As Func(Of HttpRequestMessage, CancellationToken, Task(Of HttpResponseMessage)))
            _responder = responder
        End Sub

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Dim keyValues As IEnumerable(Of String) = Nothing
            Dim keyHeader As String = Nothing
            If request.Headers.TryGetValues("Ocp-Apim-Subscription-Key", keyValues) Then keyHeader = String.Join(",", keyValues)
            Requests.Add(New RecordedRequest(request.Method.Method, request.RequestUri.AbsoluteUri, keyHeader, request.Headers.UserAgent.ToString()))
            Return _responder(request, cancellationToken)
        End Function

        Public Shared Function Responding(status As Integer, body As String) As FakeHttpMessageHandler
            Return RespondingBytes(status, Encoding.UTF8.GetBytes(body))
        End Function

        Public Shared Function RespondingBytes(status As Integer, body As Byte()) As FakeHttpMessageHandler
            Return New FakeHttpMessageHandler(
                Function(req, ct)
                    Dim msg As New HttpResponseMessage(CType(status, HttpStatusCode)) With {.Content = New ByteArrayContent(body)}
                    Return Task.FromResult(msg)
                End Function)
        End Function

        Public Shared Function RespondingWith(content As HttpContent, status As Integer) As FakeHttpMessageHandler
            Return New FakeHttpMessageHandler(
                Function(req, ct)
                    Return Task.FromResult(New HttpResponseMessage(CType(status, HttpStatusCode)) With {.Content = content})
                End Function)
        End Function

        ''' <summary>Fails like a dead connection: the exception is thrown before any response exists.</summary>
        Public Shared Function Throwing(exceptionFactory As Func(Of Exception)) As FakeHttpMessageHandler
            Return New FakeHttpMessageHandler(
                Function(req, ct) As Task(Of HttpResponseMessage)
                    Throw exceptionFactory()
                End Function)
        End Function

        ''' <summary>Never answers; only cancellation (caller or timeout) ends it.</summary>
        Public Shared Function Hanging() As FakeHttpMessageHandler
            Return New FakeHttpMessageHandler(
                Async Function(req, ct) As Task(Of HttpResponseMessage)
                    Await Task.Delay(Timeout.Infinite, ct)
                    Return Nothing
                End Function)
        End Function

    End Class

    ''' <summary>Body that dies mid-read, like a connection dropped during a download.</summary>
    Public NotInheritable Class FailingContent
        Inherits HttpContent

        Protected Overrides Function SerializeToStreamAsync(stream As Stream, context As TransportContext) As Task
            Return Task.FromException(New IOException("connection reset"))
        End Function

        Protected Overrides Function TryComputeLength(ByRef length As Long) As Boolean
            length = -1
            Return False
        End Function
    End Class

End Namespace
