Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Linq
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Export
Imports Omskep.Core.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Settings
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Export

    <TestClass>
    Public Class ExportId3FieldsBuilderTests

        Private Shared Function Chunk() As List(Of DocumentChunk)
            Return New List(Of DocumentChunk) From {
                New DocumentChunk("<speak xmlns='http://www.w3.org/2001/10/synthesis'><voice name='af-ZA-WillemNeural'>Hi.</voice></speak>", 1.0)
            }
        End Function

        <TestMethod>
        Public Sub Typed_comment_and_genre_always_win_over_the_computed_defaults()
            Dim doc = "<?omskep-meta" & vbLf & "Comment: My own note" & vbLf & "Genre: Sermon" & vbLf & "?>" & vbLf & "<speak/>"
            Dim fields = ExportId3FieldsBuilder.Build(doc, Chunk(), "+10%", "study.ssml")
            Assert.AreEqual("My own note", fields.Comment)
            Assert.AreEqual("Sermon", fields.Genre)
        End Sub

        <TestMethod>
        Public Sub Absent_comment_and_genre_fall_back_to_computed_defaults()
            Dim fields = ExportId3FieldsBuilder.Build("<speak/>", Chunk(), "+10%", "study.ssml")
            Assert.AreEqual("Audiobook", fields.Genre)
            Assert.IsTrue(fields.Comment.Contains("af-ZA-WillemNeural", StringComparison.Ordinal))
            Assert.IsTrue(fields.Comment.Contains("study.ssml", StringComparison.Ordinal))
        End Sub

        <TestMethod>
        Public Sub A_numeric_track_field_parses_to_the_tags_integer_type()
            Dim doc = "<?omskep-meta" & vbLf & "Track: 4" & vbLf & "?>" & vbLf & "<speak/>"
            Assert.AreEqual(4UI, ExportId3FieldsBuilder.Build(doc, Chunk(), "", "").Track)
        End Sub

        <TestMethod>
        Public Sub A_non_numeric_track_field_is_left_unset_not_an_error()
            Dim doc = "<?omskep-meta" & vbLf & "Track: four" & vbLf & "?>" & vbLf & "<speak/>"
            Assert.IsNull(ExportId3FieldsBuilder.Build(doc, Chunk(), "", "").Track)
        End Sub

    End Class

End Namespace
