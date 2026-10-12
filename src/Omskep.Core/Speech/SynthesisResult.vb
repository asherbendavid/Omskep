Imports Omskep.Core.Access

Namespace Speech

    ''' <summary>
    ''' Result of one SynthesizeAsync call. Audio is Nothing unless Outcome is Ok. RetryAfter is the
    ''' Retry-After Azure sent (429/503 typically), or Nothing if none was present -- never assume a
    ''' Transient outcome implies one. Detail never contains the key.
    ''' </summary>
    Public NotInheritable Class SynthesisResult
        Public ReadOnly Property Outcome As AzureOutcome
        Public ReadOnly Property StatusCode As Integer
        Public ReadOnly Property Detail As String
        Public ReadOnly Property Audio As Byte()
        Public ReadOnly Property RetryAfter As TimeSpan?

        Public Sub New(outcome As AzureOutcome, statusCode As Integer, detail As String, audio As Byte(), retryAfter As TimeSpan?)
            Me.Outcome = outcome
            Me.StatusCode = statusCode
            Me.Detail = detail
            Me.Audio = audio
            Me.RetryAfter = retryAfter
        End Sub
    End Class

End Namespace
