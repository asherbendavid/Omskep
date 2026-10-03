Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Voices

Namespace Speech

    ''' <summary>
    ''' Azure Speech REST calls. Phase 1 needs only the voice list; phase 3 adds synthesis here,
    ''' reusing the same outcome classification.
    ''' </summary>
    Public Interface ISpeechClient
        ''' <summary>
        ''' Never throws for network or HTTP problems: they come back as an Outcome. Throws ArgumentException for an
        ''' invalid region or blank key (programming errors), and OperationCanceledException if the caller cancels.
        ''' </summary>
        Function GetVoicesAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceListResult)
    End Interface

    ''' <summary>
    ''' Outcome feeds ConnectionMonitor.Report. StatusCode is 0 when no HTTP response arrived.
    ''' Detail is short, technical and never contains the key, the URL or any response body.
    ''' </summary>
    Public NotInheritable Class VoiceListResult
        Public ReadOnly Property Outcome As AzureOutcome
        Public ReadOnly Property StatusCode As Integer
        Public ReadOnly Property Detail As String
        ''' <summary>Never Nothing: empty unless Outcome is Ok.</summary>
        Public ReadOnly Property Voices As IReadOnlyList(Of VoiceInfo)

        Public ReadOnly Property IsSuccess As Boolean
            Get
                Return Outcome = AzureOutcome.Ok
            End Get
        End Property

        Public Sub New(outcome As AzureOutcome, statusCode As Integer, detail As String, voices As IReadOnlyList(Of VoiceInfo))
            Me.Outcome = outcome
            Me.StatusCode = statusCode
            Me.Detail = detail
            Me.Voices = If(voices, Array.Empty(Of VoiceInfo)())
        End Sub
    End Class

End Namespace
