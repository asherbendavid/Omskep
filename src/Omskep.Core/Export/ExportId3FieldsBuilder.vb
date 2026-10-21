Option Strict On
Option Explicit On
Option Infer On

Imports Omskep.Core.Documents
Imports TagLibSharp2.Core
Imports TagLibSharp2.Id3.Id3v2
Imports TagLibSharp2.Mpeg

Namespace Export
    Public NotInheritable Class ExportId3FieldsBuilder
        Public Shared Function Build(savedDocumentText As String, chunks As IReadOnlyList(Of DocumentChunk),
                                      speakingRateDescription As String, sourceFileName As String) As Id3Fields
            Dim meta As OmskepMetaFields = OmskepMetaFields.Parse(savedDocumentText)

            Dim track As UInteger? = Nothing
            Dim trackText As String = meta.Value("Track")
            If trackText IsNot Nothing Then
                Dim parsed As UInteger
                If UInteger.TryParse(trackText, parsed) Then track = parsed
            End If

            Dim voices As String = String.Join(", ", VoiceUsageScanner.FindDistinctVoiceNames(chunks).OrderBy(Function(v) v, StringComparer.Ordinal))
            Dim computedComment As String = $"Made with Omskep. This narration is synthetic (AI-generated) speech. Source: {sourceFileName}. Voices: {voices}."

            Return New Id3Fields(
                title:=meta.Value("Title"),
                artist:=meta.Value("Artist"),
                album:=meta.Value("Album"),
                track:=track,
                year:=meta.Value("Year"),
                copyright:=meta.Value("Copyright"),
                comment:=If(meta.Value("Comment"), computedComment),
                genre:=If(meta.Value("Genre"), "Audiobook"))
        End Function
    End Class
End Namespace
