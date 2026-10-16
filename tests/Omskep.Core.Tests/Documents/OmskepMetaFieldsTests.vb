Option Strict On
Option Explicit On
Option Infer On

Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Documents

Namespace Documents

    <TestClass>
    Public Class OmskepMetaFieldsTests

        <TestMethod>
        Public Sub A_document_with_no_block_treats_every_field_as_null()
            Dim fields = OmskepMetaFields.Parse("<speak><voice name='x'>Hi.</voice></speak>")
            Assert.IsNull(fields.Value("Title"))
            Assert.IsNull(fields.Value("Artist"))
        End Sub

        <TestMethod>
        Public Sub Empty_or_whitespace_input_treats_every_field_as_null()
            Assert.IsNull(OmskepMetaFields.Parse("").Value("Title"))
            Assert.IsNull(OmskepMetaFields.Parse(Nothing).Value("Title"))
        End Sub

        <TestMethod>
        Public Sub Basic_fields_are_parsed_and_available_case_insensitively()
            Dim xml = "<?omskep-meta" & vbLf &
                      "Title: Die Heelal: Toeval? (English)" & vbLf &
                      "Artist: J. Smith" & vbLf &
                      "Track: 4" & vbLf &
                      "Year: 2026" & vbLf &
                      "?>" & vbLf &
                      "<speak><voice name='x'>Hi.</voice></speak>"
            Dim fields = OmskepMetaFields.Parse(xml)

            Assert.AreEqual("Die Heelal: Toeval? (English)", fields.Value("Title"))
            Assert.AreEqual("J. Smith", fields.Value("artist"))
            Assert.AreEqual("4", fields.Value("TRACK"))
            Assert.AreEqual("2026", fields.Value("Year"))
        End Sub

        <TestMethod>
        Public Sub A_value_containing_a_colon_keeps_everything_after_the_first_one()
            Dim xml = "<?omskep-meta" & vbLf & "Album: Die Heelal: Toeval?" & vbLf & "?>" & vbLf & "<speak/>"
            Assert.AreEqual("Die Heelal: Toeval?", OmskepMetaFields.Parse(xml).Value("Album"))
        End Sub

        <TestMethod>
        Public Sub A_blank_value_and_an_absent_field_are_both_null()
            Dim xml = "<?omskep-meta" & vbLf & "Album:" & vbLf & "Copyright:   " & vbLf & "?>" & vbLf & "<speak/>"
            Dim fields = OmskepMetaFields.Parse(xml)
            Assert.IsNull(fields.Value("Album"))
            Assert.IsNull(fields.Value("Copyright"))
            Assert.IsNull(fields.Value("Genre"))   ' not present in the block at all
        End Sub

        <TestMethod>
        Public Sub A_line_with_no_colon_is_silently_ignored()
            Dim xml = "<?omskep-meta" & vbLf & "this is just a note" & vbLf & "Title: Kept" & vbLf & "?>" & vbLf & "<speak/>"
            Dim fields = OmskepMetaFields.Parse(xml)
            Assert.AreEqual("Kept", fields.Value("Title"))
        End Sub

        <TestMethod>
        Public Sub An_unrecognised_field_name_is_still_parsed_not_rejected()
            ' The parser recognises every field name per the spec -- "unrecognised" only means
            ' "Omskep doesn't wire this one up yet," not "the parser can't read it."
            Dim xml = "<?omskep-meta" & vbLf & "Composer: Handel" & vbLf & "?>" & vbLf & "<speak/>"
            Assert.AreEqual("Handel", OmskepMetaFields.Parse(xml).Value("Composer"))
        End Sub

        <TestMethod>
        Public Sub The_first_occurrence_of_a_duplicated_key_wins_even_if_blank()
            Dim xml = "<?omskep-meta" & vbLf &
                      "Artist:" & vbLf &
                      "Artist: Should be ignored" & vbLf &
                      "?>" & vbLf & "<speak/>"
            Assert.IsNull(OmskepMetaFields.Parse(xml).Value("Artist"))
        End Sub

        <TestMethod>
        Public Sub The_first_occurrence_wins_when_both_are_non_blank()
            Dim xml = "<?omskep-meta" & vbLf &
                      "Artist: First" & vbLf &
                      "Artist: Second" & vbLf &
                      "?>" & vbLf & "<speak/>"
            Assert.AreEqual("First", OmskepMetaFields.Parse(xml).Value("Artist"))
        End Sub

        <TestMethod>
        Public Sub Only_a_single_leading_space_in_the_value_is_trimmed_not_all_leading_whitespace()
            Dim xml = "<?omskep-meta" & vbLf & "Title:   Indented" & vbLf & "?>" & vbLf & "<speak/>"
            ' One space after the colon is the separator; the rest is the value's own content.
            Assert.AreEqual("  Indented", OmskepMetaFields.Parse(xml).Value("Title"))
        End Sub

        <TestMethod>
        Public Sub Trailing_whitespace_on_a_value_is_trimmed()
            Dim xml = "<?omskep-meta" & vbLf & "Title: Trailing   " & vbLf & "?>" & vbLf & "<speak/>"
            Assert.AreEqual("Trailing", OmskepMetaFields.Parse(xml).Value("Title"))
        End Sub

        <TestMethod>
        Public Sub Windows_line_endings_in_the_block_are_handled_the_same_as_unix_ones()
            Dim xml = "<?omskep-meta" & vbCrLf & "Title: CRLF" & vbCrLf & "?>" & vbCrLf & "<speak/>"
            Assert.AreEqual("CRLF", OmskepMetaFields.Parse(xml).Value("Title"))
        End Sub

        <TestMethod>
        Public Sub A_field_name_is_never_null_or_empty_safe_to_query()
            Dim fields = OmskepMetaFields.Parse("<speak/>")
            Assert.IsNull(fields.Value(Nothing))
        End Sub

    End Class

End Namespace
