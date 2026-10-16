Option Strict On
Option Explicit On
Option Infer On

Imports System.Text.RegularExpressions

Namespace Documents

    ''' <summary>
    ''' Reads the optional "omskep-meta" processing instruction from a document's XML prolog --
    ''' see docs/omskep-meta-spec.md for the full format definition: one "Key: Value" per line,
    ''' split on the first colon, keys case-insensitive, first occurrence of a key wins, a blank
    ''' or missing value is null, and anything else (a line with no colon, an unrecognised or
    ''' repeated key) is silently ignored. This parser recognises every field name, not only the
    ''' ones Omskep currently wires up into the export dialog, per the spec's own future-proofing.
    ''' </summary>
    Public NotInheritable Class OmskepMetaFields

        Private ReadOnly _values As IReadOnlyDictionary(Of String, String)

        Private Sub New(values As IReadOnlyDictionary(Of String, String))
            _values = values
        End Sub

        Public Shared ReadOnly Property Empty As OmskepMetaFields = New OmskepMetaFields(New Dictionary(Of String, String)())

        ''' <summary>The value of the named field (matched case-insensitively), or Nothing if the
        ''' field is absent from the block or present with a blank value -- the spec treats both
        ''' as the same thing: null.</summary>
        Public Function Value(fieldName As String) As String
            If fieldName Is Nothing Then Return Nothing
            Dim result As String = Nothing
            _values.TryGetValue(fieldName.Trim().ToLowerInvariant(), result)
            Return result
        End Function

        ''' <summary>
        ''' Finds the first "omskep-meta" processing instruction in the text and parses its content
        ''' per the spec. Returns OmskepMetaFields.Empty if none is found, which is equivalent to a
        ''' document that defines every field as null.
        ''' </summary>
        ''' <remarks>
        ''' This is a plain text scan for "&lt;?omskep-meta ... ?&gt;", not an XML-prolog-aware
        ''' parse: it does not verify that the processing instruction actually precedes the
        ''' document's root element. Keeping the parser separate from well-formedness lets it work
        ''' on a document that is not currently well-formed XML (for example, mid-edit) -- the live
        ''' well-formedness checker and SsmlComposer's prolog-stripping step are what enforce that
        ''' this block is only ever written, and only ever stripped, from the prolog.
        ''' </remarks>
        Public Shared Function Parse(xmlText As String) As OmskepMetaFields
            If String.IsNullOrEmpty(xmlText) Then Return Empty

            Dim match As Match = PiPattern.Match(xmlText)
            If Not match.Success Then Return Empty

            Dim content As String = match.Groups(1).Value
            Dim values As New Dictionary(Of String, String)()

            For Each rawLine As String In content.Replace(vbCrLf, vbLf).Split(CChar(vbLf))
                Dim line As String = rawLine.Trim()
                If line.Length = 0 Then Continue For

                Dim colonIndex As Integer = line.IndexOf(":"c)
                If colonIndex < 0 Then Continue For

                Dim key As String = line.Substring(0, colonIndex).Trim().ToLowerInvariant()
                If key.Length = 0 OrElse values.ContainsKey(key) Then Continue For ' first occurrence wins

                Dim rawValue As String = line.Substring(colonIndex + 1)
                If rawValue.StartsWith(" ", StringComparison.Ordinal) Then rawValue = rawValue.Substring(1)
                Dim value As String = rawValue.TrimEnd()

                values(key) = If(value.Length = 0, Nothing, value)
            Next

            Return New OmskepMetaFields(values)
        End Function

        ' Content captured non-greedily so the match stops at the first "?>", matching the
        ' processing-instruction grammar (its content can never contain that sequence).
        Private Shared ReadOnly PiPattern As New Regex(
            "<\?omskep-meta\s(.*?)\?>",
            RegexOptions.Singleline Or RegexOptions.CultureInvariant)

    End Class

End Namespace
