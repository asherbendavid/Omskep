Imports System.Net.Http
Imports System.Threading

Namespace Speech

    ''' <summary>
    ''' One long-lived HttpClient for the whole app. Redirects are off so the subscription-key header can never
    ''' be forwarded to another host. The per-call timeout lives in AzureSpeechClient, hence the infinite client timeout.
    ''' </summary>
    Public Module AzureHttpClientFactory

        Public Function CreateHandler() As SocketsHttpHandler
            Return New SocketsHttpHandler() With {
                .AllowAutoRedirect = False,
                .PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            }
        End Function

        Public Function CreateClient() As HttpClient
            Return New HttpClient(CreateHandler()) With {.Timeout = Timeout.InfiniteTimeSpan}
        End Function

    End Module

End Namespace
