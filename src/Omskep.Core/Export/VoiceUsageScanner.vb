Option Strict On
Option Explicit On
Option Infer On

Imports Omskep.Core.Documents
Imports TagLibSharp2.Core
Imports TagLibSharp2.Id3.Id3v2
Imports TagLibSharp2.Mpeg

Namespace Export
    Public NotInheritable Class VoiceUsageScanner
        Public Shared Function FindDistinctVoiceNames(chunks As IReadOnlyList(Of DocumentChunk)) As IReadOnlyList(Of String)
            Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
            Dim ordered As New List(Of String)
            For Each chunk In chunks
                Dim doc As XDocument
                Try
                    doc = XDocument.Parse(chunk.Ssml)
                Catch
                    Continue For ' defensive: a malformed chunk should never happen this late, but this scan must never throw
                End Try
                For Each voiceEl In doc.Descendants().Where(Function(e) e.Name.LocalName = "voice")
                    Dim nameAttr As XAttribute = voiceEl.Attribute("name")
                    If nameAttr IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(nameAttr.Value) Then
                        If seen.Add(nameAttr.Value) Then ordered.Add(nameAttr.Value)
                    End If
                Next
            Next
            Return ordered
        End Function
    End Class

End Namespace
