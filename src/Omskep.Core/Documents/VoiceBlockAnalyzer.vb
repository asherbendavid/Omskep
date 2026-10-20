Option Strict On
Option Explicit On
Option Infer On

Imports System.Xml.Linq
Imports Omskep.Core.Voices

Namespace Documents

    ''' <summary>One &lt;voice&gt; element from composed SSML: its short name, resolved locale (catalog
    ''' first, falling back to the "ll-CC-..." shape SsmlComposer itself uses -- Nothing if neither
    ''' works), whether that short name actually exists in the current catalog, and its full spoken text
    ''' (all descendant text, exactly what SsmlComposer sends to Azure for this block).</summary>
    Public NotInheritable Class VoiceBlockInfo

        Public ReadOnly Property ShortName As String
        Public ReadOnly Property Locale As String
        Public ReadOnly Property ExistsInCatalog As Boolean
        Public ReadOnly Property Text As String

        Public Sub New(shortName As String, locale As String, existsInCatalog As Boolean, text As String)
            Me.ShortName = shortName
            Me.Locale = locale
            Me.ExistsInCatalog = existsInCatalog
            Me.Text = text
        End Sub

    End Class

    ''' <summary>
    ''' Walks every &lt;voice&gt; element in already-composed SSML once, producing the per-block facts
    ''' both the export-time voice-name gate and the script-mismatch warning need. Pure, Important tier
    ''' (same reasoning as both features it feeds): this never edits anything, it only reports. Assumes
    ''' well-formed, non-nested input -- true of anything SsmlComposer.Compose has already succeeded on,
    ''' which is the only caller.
    ''' </summary>
    Public NotInheritable Class VoiceBlockAnalyzer

        Private Sub New()
        End Sub

        Public Shared Function Analyze(composedSsml As String, catalog As VoiceCatalog) As IReadOnlyList(Of VoiceBlockInfo)
            Dim results As New List(Of VoiceBlockInfo)()
            If String.IsNullOrEmpty(composedSsml) Then Return results

            Dim doc As XDocument
            Try
                doc = XDocument.Parse(composedSsml)
            Catch
                ' Defensive only: the only caller only ever passes text SsmlComposer.Compose already
                ' approved as well-formed.
                Return results
            End Try

            If doc.Root Is Nothing Then Return results
            Dim ns As XNamespace = doc.Root.Name.Namespace

            For Each voiceEl As XElement In doc.Root.Descendants(ns + "voice")
                Dim shortName As String = If(voiceEl.Attribute("name")?.Value, String.Empty)
                If shortName.Length = 0 Then Continue For

                Dim fromCatalog As VoiceInfo = catalog?.TryFind(shortName)
                Dim exists As Boolean = fromCatalog IsNot Nothing
                Dim locale As String = If(exists, fromCatalog.Locale, GuessLocaleFromShortName(shortName))

                results.Add(New VoiceBlockInfo(shortName, locale, exists, voiceEl.Value))
            Next

            Return results
        End Function

        ''' <summary>"af-ZA-WillemNeural" -&gt; "af-ZA": the first two hyphen-separated parts, matching
        ''' SsmlComposer's own locale convention. Nothing if the short name doesn't have that shape.</summary>
        Private Shared Function GuessLocaleFromShortName(shortName As String) As String
            Dim parts As String() = shortName.Split("-"c)
            If parts.Length < 3 Then Return Nothing
            Return parts(0) & "-" & parts(1)
        End Function

    End Class

End Namespace
