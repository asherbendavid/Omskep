Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace Settings

    ''' <summary>The "azure" section of settings.json.</summary>
    Public NotInheritable Class AzureSettings
        Public Const DefaultRegion As String = "southafricanorth"

        <JsonPropertyName("region")>
        Public Property Region As String = DefaultRegion

        ''' <summary>Base64 of the DPAPI-protected key. Never the key itself. Nothing when no key is saved.</summary>
        <JsonPropertyName("keyBlob")>
        Public Property KeyBlob As String

        ''' <summary>Fields this version does not know about, kept so a newer file survives a round trip.</summary>
        <JsonExtensionData>
        Public Property ExtensionData As Dictionary(Of String, JsonElement)
    End Class

    ''' <summary>
    ''' Contents of settings.json. Add new settings as new properties with safe defaults; old files
    ''' simply lack them. Bump CurrentSchemaVersion only for changes an old reader cannot ignore.
    ''' </summary>
    Public NotInheritable Class AppSettings
        Public Const CurrentSchemaVersion As Integer = 1

        <JsonPropertyName("schemaVersion")>
        Public Property SchemaVersion As Integer = CurrentSchemaVersion

        <JsonPropertyName("azure")>
        Public Property Azure As AzureSettings = New AzureSettings()

        ''' <summary>Locale (e.g. "af-ZA") to voice ShortName (e.g. "af-ZA-WillemNeural").</summary>
        <JsonPropertyName("defaultVoices")>
        Public Property DefaultVoices As Dictionary(Of String, String) = New Dictionary(Of String, String)()

        ''' <summary>Locale to speaking-rate preset, in whole percent (e.g. "he-IL": -10).</summary>
        <JsonPropertyName("speakingRatePercent")>
        Public Property SpeakingRatePercent As Dictionary(Of String, Integer) = New Dictionary(Of String, Integer)()

        <JsonExtensionData>
        Public Property ExtensionData As Dictionary(Of String, JsonElement)

        ''' <summary>Repairs nulls left by hand-edited or partial files so callers never null-check.</summary>
        Friend Sub Normalize()
            If Azure Is Nothing Then Azure = New AzureSettings()
            If String.IsNullOrWhiteSpace(Azure.Region) Then Azure.Region = AzureSettings.DefaultRegion
            If DefaultVoices Is Nothing Then DefaultVoices = New Dictionary(Of String, String)()
            If SpeakingRatePercent Is Nothing Then SpeakingRatePercent = New Dictionary(Of String, Integer)()
        End Sub
    End Class

End Namespace
