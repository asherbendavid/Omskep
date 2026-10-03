Imports System.IO
Imports System.Net.Http
Imports System.Text
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Speech

Namespace Speech

    <TestClass>
    Public Class AzureSpeechClientTests

        Private Const Key As String = "SECRETKEY0123456789abcdef"
        Private Const Region As String = "southafricanorth"
        Private Const OneVoice As String = "[{""ShortName"":""af-ZA-WillemNeural"",""Locale"":""af-ZA"",""DisplayName"":""Willem"",""Gender"":""Male"",""VoiceType"":""Neural""}]"

        Private Shared Function NewClient(handler As Support.FakeHttpMessageHandler, Optional timeoutMs As Integer = 0) As AzureSpeechClient
            Dim timeout As TimeSpan? = Nothing
            If timeoutMs > 0 Then timeout = TimeSpan.FromMilliseconds(timeoutMs)
            Return New AzureSpeechClient(New HttpClient(handler), timeout)
        End Function

        Private Shared Async Function ListAsync(client As AzureSpeechClient) As Task(Of VoiceListResult)
            Return Await client.GetVoicesAsync(Region, Key, CancellationToken.None)
        End Function

        Private Shared Function BigVoiceListJson() As String
            Dim sb As New StringBuilder("[")
            For i = 0 To 599
                If i > 0 Then sb.Append(","c)
                Dim n = i.ToString("D3")
                sb.Append("{""Name"":""Microsoft Server Speech Text to Speech Voice (xx-XX, Voice").Append(n).Append("Neural)"",")
                sb.Append("""DisplayName"":""Voice").Append(n).Append(""",""LocalName"":""Voice").Append(n).Append(""",")
                sb.Append("""ShortName"":""xx-XX-Voice").Append(n).Append("Neural"",""Gender"":""Female"",""Locale"":""xx-XX"",")
                sb.Append("""LocaleName"":""Padding language name for realistic size"",""SampleRateHertz"":""48000"",""VoiceType"":""Neural"",""Status"":""GA"",")
                sb.Append("""StyleList"":[""cheerful"",""sad"",""angry"",""excited"",""friendly"",""hopeful"",""shouting"",""terrified"",""unfriendly"",""whispering"",""calm"",""fearful"",""gentle"",""serious""],")
                sb.Append("""RolePlayList"":[""Girl"",""Boy"",""YoungAdultFemale"",""YoungAdultMale"",""OlderAdultFemale"",""OlderAdultMale"",""SeniorFemale"",""SeniorMale""],")
                sb.Append("""SecondaryLocaleList"":[""xx-XX"",""yy-YY"",""zz-ZZ"",""ww-WW"",""vv-VV"",""uu-UU""],")
                sb.Append("""VoiceTag"":{""TailoredScenarios"":[""Audiobook"",""Conversation"",""News"",""Narration"",""CustomerService""],""VoicePersonalities"":[""Friendly"",""Warm"",""Pleasant"",""Confident""]},")
                sb.Append("""WordsPerMinute"":""157""}")
            Next
            Return sb.Append("]"c).ToString()
        End Function

        ' ---- success ----

        <TestMethod>
        Public Async Function Success_ParsesVoices_AndSendsTheExpectedRequest() As Task
            Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
            Dim r = Await ListAsync(NewClient(handler))

            Assert.AreEqual(AzureOutcome.Ok, r.Outcome)
            Assert.IsTrue(r.IsSuccess)
            Assert.AreEqual(200, r.StatusCode)
            Assert.HasCount(1, r.Voices)
            Assert.AreEqual("af-ZA-WillemNeural", r.Voices(0).ShortName)

            Assert.HasCount(1, handler.Requests)
            Dim sent = handler.Requests(0)
            Assert.AreEqual("GET", sent.Method)
            Assert.AreEqual("https://southafricanorth.tts.speech.microsoft.com/cognitiveservices/voices/list", sent.Url)
            Assert.AreEqual(Key, sent.KeyHeader)
            Assert.AreEqual("Omskep", sent.UserAgent)
            Assert.IsFalse(sent.Url.Contains(Key, StringComparison.Ordinal))
        End Function

        <TestMethod>
        Public Async Function Success_FullSizeVoiceList_430kCharacters_Parses() As Task
            Dim json = BigVoiceListJson()
            Assert.IsGreaterThan(400000, json.Length, "test payload should be realistic in size")
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(200, json)))
            Assert.AreEqual(AzureOutcome.Ok, r.Outcome)
            Assert.HasCount(600, r.Voices)
        End Function

        <TestMethod>
        Public Async Function Success_EmptyList_IsOkWithNoVoices() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(200, "[]")))
            Assert.AreEqual(AzureOutcome.Ok, r.Outcome)
            Assert.IsEmpty(r.Voices)
        End Function

        <TestMethod>
        Public Async Function RegionAndKey_AreNormalizedBeforeUse() As Task
            Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
            Dim client = NewClient(handler)
            Await client.GetVoicesAsync("  SouthAfricaNorth ", "  " & Key & vbCrLf, CancellationToken.None)
            Assert.AreEqual("https://southafricanorth.tts.speech.microsoft.com/cognitiveservices/voices/list", handler.Requests(0).Url)
            Assert.AreEqual(Key, handler.Requests(0).KeyHeader)
        End Function

        ' ---- 401: bad key or wrong region ----

        <TestMethod>
        Public Async Function Http401_EmptyBody_IsRejected() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(401, "")))
            Assert.AreEqual(AzureOutcome.Rejected, r.Outcome)
            Assert.AreEqual(401, r.StatusCode)
            Assert.IsFalse(r.IsSuccess)
            Assert.IsEmpty(r.Voices)
            Assert.AreEqual("HTTP 401", r.Detail)
        End Function

        <TestMethod>
        Public Async Function Http401_WithBody_IsStillRejected_AndBodyIsNotCopiedIntoDetail() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(401, "{""error"":""leaky " & Key & """}")))
            Assert.AreEqual(AzureOutcome.Rejected, r.Outcome)
            Assert.IsFalse(r.Detail.Contains("leaky", StringComparison.Ordinal))
        End Function

        ' ---- other statuses ----

        <TestMethod>
        Public Async Function Http429_408_And5xx_AreTransient_NeverRejected() As Task
            For Each code In {408, 429, 500, 502, 503, 504}
                Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(code, "")))
                Assert.AreEqual(AzureOutcome.Transient, r.Outcome, code.ToString())
                Assert.AreEqual(code, r.StatusCode)
            Next
        End Function

        <TestMethod>
        Public Async Function OtherStatuses_AreFailed_IncludingRedirects() As Task
            For Each code In {204, 301, 302, 400, 403, 404}
                Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(code, "")))
                Assert.AreEqual(AzureOutcome.Failed, r.Outcome, code.ToString())
                Assert.AreEqual(code, r.StatusCode)
            Next
        End Function

        ' ---- offline: a connection-level failure, not an HTTP response ----

        <TestMethod>
        Public Async Function ConnectionFailure_IsOffline_WithNoStatusCode() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Throwing(Function() New HttpRequestException("No such host is known."))))
            Assert.AreEqual(AzureOutcome.Offline, r.Outcome)
            Assert.AreEqual(0, r.StatusCode)
            Assert.IsEmpty(r.Voices)
        End Function

        <TestMethod>
        Public Async Function ConnectionFailure_WithInnerSocketException_IsOffline() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Throwing(
                Function() New HttpRequestException("failed", New Net.Sockets.SocketException(10061)))))
            Assert.AreEqual(AzureOutcome.Offline, r.Outcome)
        End Function

        <TestMethod>
        Public Async Function Offline_And_Rejected_AreDistinctOutcomes() As Task
            Dim offline = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Throwing(Function() New HttpRequestException("x"))))
            Dim rejected = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(401, "")))
            Assert.AreNotEqual(rejected.Outcome, offline.Outcome)
        End Function

        ' ---- timeout and cancellation ----

        <TestMethod>
        Public Async Function Timeout_IsTransient_NotAnException() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Hanging(), 50))
            Assert.AreEqual(AzureOutcome.Transient, r.Outcome)
            Assert.AreEqual(0, r.StatusCode)
            Assert.IsTrue(r.Detail.Contains("in time", StringComparison.Ordinal))
        End Function

        <TestMethod>
        Public Async Function CallerCancellation_PropagatesAsOperationCanceled() As Task
            Dim client = NewClient(Support.FakeHttpMessageHandler.Hanging())
            Using cts As New CancellationTokenSource()
                Dim pending = client.GetVoicesAsync(Region, Key, cts.Token)
                cts.CancelAfter(30)
                Dim cancelled As Boolean = False
                Try
                    Await pending
                Catch ex As OperationCanceledException
                    cancelled = True
                End Try
                Assert.IsTrue(cancelled)
            End Using
        End Function

        <TestMethod>
        Public Async Function AlreadyCancelledToken_Throws_AndSendsNothing() As Task
            Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
            Dim client = NewClient(handler)
            Using cts As New CancellationTokenSource()
                cts.Cancel()
                Dim cancelled As Boolean = False
                Try
                    Await client.GetVoicesAsync(Region, Key, cts.Token)
                Catch ex As OperationCanceledException
                    cancelled = True
                End Try
                Assert.IsTrue(cancelled)
                Assert.IsEmpty(handler.Requests)
            End Using
        End Function

        <TestMethod>
        Public Async Function CancelledWhileResponseArrives_ResultIsDiscarded() As Task
            Using cts As New CancellationTokenSource()
                Dim handler As New Support.FakeHttpMessageHandler(
                    Function(req, ct) As Task(Of HttpResponseMessage)
                        cts.Cancel()   ' the user hits Cancel just as the answer lands
                        Return Task.FromResult(New HttpResponseMessage(Net.HttpStatusCode.OK) With {.Content = New StringContent(OneVoice)})
                    End Function)
                Dim client = NewClient(handler)
                Dim cancelled As Boolean = False
                Try
                    Await client.GetVoicesAsync(Region, Key, cts.Token)
                Catch ex As OperationCanceledException
                    cancelled = True
                End Try
                Assert.IsTrue(cancelled)
            End Using
        End Function

        ' ---- bad bodies ----

        <TestMethod>
        Public Async Function MalformedBodies_AreFailed_NotCrashes() As Task
            For Each body In {"", "null", "{}", "<html>gateway</html>", "[{", "{""voices"":[]}"}
                Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.Responding(200, body)))
                Assert.AreEqual(AzureOutcome.Failed, r.Outcome, "body: " & body)
                Assert.AreEqual(200, r.StatusCode)
                Assert.IsEmpty(r.Voices)
            Next
        End Function

        <TestMethod>
        Public Async Function BodyCutOffMidDownload_IsTransient() As Task
            Dim r = Await ListAsync(NewClient(Support.FakeHttpMessageHandler.RespondingWith(New Support.FailingContent(), 200)))
            Assert.AreEqual(AzureOutcome.Transient, r.Outcome)
            Assert.AreEqual(200, r.StatusCode)
        End Function

        ' ---- must never: send the key somewhere wrong, or leak it ----

        <TestMethod>
        Public Async Function InvalidRegion_Throws_AndNothingIsSent() As Task
            For Each bad In {Nothing, "", "evil.com/", "a.b", "x#", "a b", "x?y"}
                Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
                Dim client = NewClient(handler)
                Dim threw As Boolean = False
                Try
                    Await client.GetVoicesAsync(bad, Key, CancellationToken.None)
                Catch ex As ArgumentException
                    threw = True
                    Assert.IsFalse(ex.Message.Contains(Key, StringComparison.Ordinal))
                End Try
                Assert.IsTrue(threw, "should throw for: " & If(bad, "(Nothing)"))
                Assert.IsEmpty(handler.Requests)
            Next
        End Function

        <TestMethod>
        Public Async Function BlankKey_Throws_AndNothingIsSent() As Task
            For Each bad In {Nothing, "", "   "}
                Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
                Dim client = NewClient(handler)
                Dim threw As Boolean = False
                Try
                    Await client.GetVoicesAsync(Region, bad, CancellationToken.None)
                Catch ex As ArgumentException
                    threw = True
                End Try
                Assert.IsTrue(threw)
                Assert.IsEmpty(handler.Requests)
            Next
        End Function

        <TestMethod>
        Public Async Function KeyThatCannotBeSent_IsRejected_AndNothingIsSent() As Task
            Dim handler = Support.FakeHttpMessageHandler.Responding(200, OneVoice)
            Dim r = Await NewClient(handler).GetVoicesAsync(Region, "abc" & vbCr & vbLf & "X-Evil: 1", CancellationToken.None)
            Assert.AreEqual(AzureOutcome.Rejected, r.Outcome)
            Assert.AreEqual(0, r.StatusCode)
            Assert.IsEmpty(handler.Requests)
        End Function

        <TestMethod>
        Public Async Function Detail_NeverContainsTheKey_OnAnyOutcome() As Task
            Dim handlers As Support.FakeHttpMessageHandler() = {
                Support.FakeHttpMessageHandler.Responding(200, OneVoice),
                Support.FakeHttpMessageHandler.Responding(401, Key),
                Support.FakeHttpMessageHandler.Responding(429, Key),
                Support.FakeHttpMessageHandler.Responding(403, Key),
                Support.FakeHttpMessageHandler.Responding(200, "garbage " & Key),
                Support.FakeHttpMessageHandler.Throwing(Function() New HttpRequestException("could not reach host with key " & Key)),
                Support.FakeHttpMessageHandler.RespondingWith(New Support.FailingContent(), 200)
            }
            For Each h In handlers
                Dim r = Await ListAsync(NewClient(h))
                Assert.IsFalse(r.Detail.Contains(Key, StringComparison.Ordinal), r.Outcome.ToString())
                Assert.IsFalse(r.Detail.Contains("could not reach host", StringComparison.Ordinal), r.Outcome.ToString())
            Next
        End Function

        ' ---- construction and the shared HttpClient ----

        <TestMethod>
        Public Sub Constructor_NullHttpClient_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing))
        End Sub

        Private Shared Sub Build(http As HttpClient)
            Dim unused = New AzureSpeechClient(http)
        End Sub

        <TestMethod>
        Public Sub Factory_DisablesRedirects_AndLeavesTimeoutToTheClient()
            Using handler = AzureHttpClientFactory.CreateHandler()
                Assert.IsFalse(handler.AllowAutoRedirect)
            End Using
            Using http = AzureHttpClientFactory.CreateClient()
                Assert.AreEqual(Timeout.InfiniteTimeSpan, http.Timeout)
            End Using
        End Sub

        <TestMethod>
        Public Async Function SameClient_CanBeCalledRepeatedly_AfterAFailure() As Task
            Dim calls As Integer = 0
            Dim handler As New Support.FakeHttpMessageHandler(
                Function(req, ct) As Task(Of Net.Http.HttpResponseMessage)
                    calls += 1
                    If calls = 1 Then Throw New HttpRequestException("down")
                    Return Task.FromResult(New HttpResponseMessage(Net.HttpStatusCode.OK) With {.Content = New StringContent(OneVoice)})
                End Function)
            Dim client = NewClient(handler)

            Assert.AreEqual(AzureOutcome.Offline, (Await ListAsync(client)).Outcome)
            Assert.AreEqual(AzureOutcome.Ok, (Await ListAsync(client)).Outcome)
        End Function

    End Class

End Namespace