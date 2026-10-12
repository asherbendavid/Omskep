Imports System.Threading
Imports Omskep.Core.Access
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Support

    ''' <summary>Scriptable ISpeechClient: no HTTP at all. Records the region and key of every call.</summary>
    Public NotInheritable Class FakeSpeechClient
        Implements ISpeechClient

        Private ReadOnly _responder As Func(Of String, String, CancellationToken, Task(Of VoiceListResult))
        Private ReadOnly _synthesisResponder As Func(Of String, String, String, CancellationToken, Task(Of SynthesisResult))

        Public ReadOnly Property Calls As New List(Of String())()
        Public ReadOnly Property SynthesisCalls As New List(Of String())()

        Public Sub New(responder As Func(Of CancellationToken, Task(Of VoiceListResult)), Optional synthesisResponder As Func(Of String, String, String, CancellationToken, Task(Of SynthesisResult)) = Nothing)
            _responder = Function(region, key, ct) responder(ct)
            _synthesisResponder = If(synthesisResponder, AddressOf DefaultSynthesisResponder)
        End Sub

        ''' <summary>Responder that can look at the region and key it was called with.</summary>
        Public Sub New(responder As Func(Of String, String, CancellationToken, Task(Of VoiceListResult)), Optional synthesisResponder As Func(Of String, String, String, CancellationToken, Task(Of SynthesisResult)) = Nothing)
            _responder = responder
            _synthesisResponder = If(synthesisResponder, AddressOf DefaultSynthesisResponder)
        End Sub

        Public Function GetVoicesAsync(region As String, key As String, cancellationToken As CancellationToken) As Task(Of VoiceListResult) Implements ISpeechClient.GetVoicesAsync
            Calls.Add(New String() {region, key})
            Return _responder(region, key, cancellationToken)
        End Function

        Public Function SynthesizeAsync(ssml As String, region As String, key As String, cancellationToken As CancellationToken) As Task(Of SynthesisResult) Implements ISpeechClient.SynthesizeAsync
            SynthesisCalls.Add(New String() {ssml, region, key})
            Return _synthesisResponder(ssml, region, key, cancellationToken)
        End Function

        ''' <summary>
        ''' Used when a test exercises GetVoicesAsync only and never set a synthesis responder.
        ''' Throws so an accidental SynthesizeAsync call in such a test fails loudly instead of
        ''' silently returning a plausible-looking default.
        ''' </summary>
        Private Shared Function DefaultSynthesisResponder(ssml As String, region As String, key As String, cancellationToken As CancellationToken) As Task(Of SynthesisResult)
            Throw New NotImplementedException("This FakeSpeechClient was not given a synthesis responder.")
        End Function

        Public Shared Function Returning(result As VoiceListResult) As FakeSpeechClient
            Return New FakeSpeechClient(Function(ct) Task.FromResult(result))
        End Function

        ''' <summary>Never answers; only cancellation ends it.</summary>
        Public Shared Function Hanging() As FakeSpeechClient
            Return New FakeSpeechClient(
                Async Function(ct) As Task(Of VoiceListResult)
                    Await Task.Delay(Timeout.Infinite, ct)
                    Return Nothing
                End Function)
        End Function

        ''' <summary>A client whose SynthesizeAsync always returns the given result, regardless of ssml/region/key.</summary>
        Public Shared Function ReturningSynthesis(result As SynthesisResult) As FakeSpeechClient
            Return New FakeSpeechClient(
                Function(ct) Task.FromResult(New VoiceListResult(AzureOutcome.Ok, 200, String.Empty, New List(Of VoiceInfo)())),
                Function(ssml, region, key, ct) Task.FromResult(result))
        End Function

        ''' <summary>Never answers a synthesis call; only cancellation ends it.</summary>
        Public Shared Function HangingSynthesis() As FakeSpeechClient
            Return New FakeSpeechClient(
                Function(ct) Task.FromResult(New VoiceListResult(AzureOutcome.Ok, 200, String.Empty, New List(Of VoiceInfo)())),
                Async Function(ssml, region, key, ct) As Task(Of SynthesisResult)
                    Await Task.Delay(Timeout.Infinite, ct)
                    Return Nothing
                End Function)
        End Function

        Public Shared Function Voices(count As Integer, Optional locale As String = "xx-XX") As List(Of VoiceInfo)
            Dim list As New List(Of VoiceInfo)()
            For i = 0 To count - 1
                list.Add(New VoiceInfo() With {
                    .ShortName = locale & "-Voice" & i.ToString("D3") & "Neural",
                    .Locale = locale,
                    .DisplayName = "Voice" & i.ToString("D3"),
                    .Gender = "Female",
                    .VoiceType = "Neural"})
            Next
            Return list
        End Function

        Public Shared Function Ok(count As Integer, Optional locale As String = "xx-XX") As VoiceListResult
            Return New VoiceListResult(AzureOutcome.Ok, 200, String.Empty, Voices(count, locale))
        End Function

        Public Shared Function Fail(outcome As AzureOutcome, statusCode As Integer) As VoiceListResult
            Return New VoiceListResult(outcome, statusCode, If(statusCode = 0, "no connection", "HTTP " & statusCode.ToString()), Nothing)
        End Function

        Public Shared Function OkSynthesis(audio As Byte()) As SynthesisResult
            Return New SynthesisResult(AzureOutcome.Ok, 200, String.Empty, audio, Nothing)
        End Function

        Public Shared Function FailSynthesis(outcome As AzureOutcome, statusCode As Integer, Optional retryAfter As TimeSpan? = Nothing) As SynthesisResult
            Return New SynthesisResult(outcome, statusCode, If(statusCode = 0, "no connection", "HTTP " & statusCode.ToString()), Nothing, retryAfter)
        End Function

    End Class

End Namespace