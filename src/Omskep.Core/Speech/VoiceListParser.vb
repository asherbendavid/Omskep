Imports System.IO
Imports System.Text.Json
Imports Omskep.Core.Voices

Namespace Speech

    ''' <summary>The few fields we read from Azure's voice list. Everything else (styles, roles, samples) is ignored.</summary>
    Public NotInheritable Class AzureVoiceDto
        Public Property ShortName As String
        Public Property Locale As String
        Public Property DisplayName As String
        Public Property Gender As String
        Public Property VoiceType As String
    End Class

    Public Module VoiceListParser

        Private ReadOnly Options As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

        ''' <summary>
        ''' Parses the voices/list body. Entries without a ShortName or Locale are skipped, duplicates collapse,
        ''' and the result is sorted by locale then name. Throws JsonException if the body is not a JSON array.
        ''' </summary>
        Public Function Parse(json As Byte()) As IReadOnlyList(Of VoiceInfo)
            If json Is Nothing Then Throw New ArgumentNullException(NameOf(json))

            Dim dtos As List(Of AzureVoiceDto)
            Using stream As New MemoryStream(json, False)
                dtos = JsonSerializer.Deserialize(Of List(Of AzureVoiceDto))(stream, Options)
            End Using
            If dtos Is Nothing Then Throw New JsonException("The voice list was null.")

            Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim result As New List(Of VoiceInfo)()
            For Each d In dtos
                If d Is Nothing Then Continue For
                If String.IsNullOrWhiteSpace(d.ShortName) OrElse String.IsNullOrWhiteSpace(d.Locale) Then Continue For
                If Not seen.Add(d.ShortName.Trim()) Then Continue For
                result.Add(New VoiceInfo() With {
                    .ShortName = d.ShortName.Trim(),
                    .Locale = d.Locale.Trim(),
                    .DisplayName = If(String.IsNullOrWhiteSpace(d.DisplayName), d.ShortName.Trim(), d.DisplayName.Trim()),
                    .Gender = d.Gender,
                    .VoiceType = d.VoiceType})
            Next

            result.Sort(Function(a, b)
                            Dim byLocale = String.Compare(a.Locale, b.Locale, StringComparison.OrdinalIgnoreCase)
                            If byLocale <> 0 Then Return byLocale
                            Return String.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase)
                        End Function)
            Return result
        End Function

    End Module

End Namespace
