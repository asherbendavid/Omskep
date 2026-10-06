Option Strict On
Option Explicit On
Option Infer On

Imports System.Text

Namespace Import

    ''' <summary>
    ''' Turns a PDF (through an <see cref="IPdfPageReader"/>) or pasted text into a complete
    ''' SSML document. Holds no state between calls. Never throws for bad content: every
    ''' failure becomes an <see cref="ImportResult"/> with a message the user can act on.
    ''' Never removes words: pages with no readable text are reported by number.
    ''' </summary>
    Public NotInheritable Class DocumentImporter

        ''' <summary>A PDF with fewer non-whitespace characters than this in total is treated as
        ''' having no text (for example a scan whose only text layer is page numbers).</summary>
        Public Const MinimumTextCharacters As Integer = 20

        Private Const MaxListedPages As Integer = 10

        Private ReadOnly _reader As IPdfPageReader

        Public Sub New(reader As IPdfPageReader)
            If reader Is Nothing Then Throw New ArgumentNullException(NameOf(reader))
            _reader = reader
        End Sub

        ''' <exception cref="ArgumentException">locale or voice is blank (a caller bug).</exception>
        Public Function ImportPdf(pdfPath As String, locale As String, voiceShortName As String) As ImportResult
            DocumentComposer.RequireScaffold(locale, voiceShortName)

            Dim pages As IReadOnlyList(Of String) = Nothing
            Try
                pages = _reader.ReadPages(pdfPath)
            Catch ex As PdfPasswordRequiredException
                Return Failure(ImportOutcome.PasswordProtected,
                               "This PDF is password protected, so Omskep cannot read it. " &
                               "Remove the protection and try again, or paste the text instead.")
            Catch ex As Exception
                Return Failure(ImportOutcome.Unreadable,
                               "This file could not be read as a PDF (" & ex.Message & "). " &
                               "It may be damaged, not a PDF, or in use by another program.")
            End Try

            If pages Is Nothing OrElse pages.Count = 0 Then
                Return Failure(ImportOutcome.NoTextFound, NoTextMessage)
            End If

            Dim emptyPages As New List(Of Integer)()
            Dim keptBodies As New List(Of String)()
            Dim parts As New List(Of SanitizeResult)()
            Dim totalText As Integer = 0

            For i As Integer = 0 To pages.Count - 1
                Dim part As SanitizeResult = TextSanitizer.Sanitize(pages(i))
                parts.Add(part)
                Dim pageBody As String = part.Body.TrimEnd(ChrW(10), " "c)
                Dim textCount As Integer = CountNonWhitespace(pageBody)
                If textCount = 0 Then
                    emptyPages.Add(i + 1)
                Else
                    keptBodies.Add(pageBody)
                    totalText += textCount
                End If
            Next

            If keptBodies.Count = 0 OrElse totalText < MinimumTextCharacters Then
                Return Failure(ImportOutcome.NoTextFound, NoTextMessage)
            End If

            Dim lf As String = ChrW(10)
            Dim joined As String = String.Join(lf & lf, keptBodies)
            Dim sanitation As SanitizeResult = SanitizeResult.Combine(joined, parts)
            Dim document As String = DocumentComposer.Compose(joined, locale, voiceShortName)
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find(document)
            Dim outcome As ImportOutcome = If(emptyPages.Count > 0, ImportOutcome.ImportedWithEmptyPages, ImportOutcome.Imported)

            Return New ImportResult(outcome,
                                    Summarise("Imported " & Plural(keptBodies.Count, "page") & ".", emptyPages, sanitation, flags),
                                    document, pages.Count, emptyPages, sanitation, flags)
        End Function

        ''' <summary>Pasted text from Word or anywhere else. Same sanitizing as a PDF.</summary>
        ''' <exception cref="ArgumentException">locale or voice is blank (a caller bug).</exception>
        Public Function ImportText(pastedText As String, locale As String, voiceShortName As String) As ImportResult
            DocumentComposer.RequireScaffold(locale, voiceShortName)

            Dim sanitation As SanitizeResult = TextSanitizer.Sanitize(pastedText)
            Dim body As String = sanitation.Body.TrimEnd(ChrW(10), " "c)
            If CountNonWhitespace(body) = 0 Then
                Return Failure(ImportOutcome.NoTextFound, "There is no text to import.")
            End If

            Dim document As String = DocumentComposer.Compose(body, locale, voiceShortName)
            Dim flags As IReadOnlyList(Of ImportFlag) = ImportFlagFinder.Find(document)
            Return New ImportResult(ImportOutcome.Imported,
                                    Summarise("Imported the pasted text.", New List(Of Integer)(), sanitation, flags),
                                    document, 0, New List(Of Integer)(), sanitation, flags)
        End Function

        ' ---- helpers ----

        Private Const NoTextMessage As String =
            "No text could be read from this PDF. It looks like a scanned or image-only document, " &
            "and Omskep cannot read text from pictures. Use a PDF with selectable text, or paste the text instead."

        Private Shared Function Failure(outcome As ImportOutcome, message As String) As ImportResult
            Return New ImportResult(outcome, message, String.Empty, 0, New List(Of Integer)(),
                                    TextSanitizer.Sanitize(Nothing), New List(Of ImportFlag)())
        End Function

        Private Shared Function CountNonWhitespace(value As String) As Integer
            Dim n As Integer = 0
            For Each c As Char In value
                If Not Char.IsWhiteSpace(c) Then n += 1
            Next
            Return n
        End Function

        Private Shared Function Plural(count As Integer, noun As String) As String
            Return count.ToString(Globalization.CultureInfo.InvariantCulture) & " " & noun & If(count = 1, "", "s")
        End Function

        ''' <summary>" Cleaned up: removed 2 decorative symbols, corrected 3 quote marks." or an empty string when
        ''' nothing was changed. Shared with pasting so both report in the same words.</summary>
        Friend Shared Function CleanupSentence(sanitation As SanitizeResult) As String
            Dim cleaned As New List(Of String)()
            If sanitation.RemovedPrivateUseTotal > 0 Then
                cleaned.Add("removed " & Plural(sanitation.RemovedPrivateUseTotal, "decorative symbol"))
            End If
            If sanitation.RemovedInvalidCharacters > 0 Then
                cleaned.Add("removed " & Plural(sanitation.RemovedInvalidCharacters, "unusable character"))
            End If
            If sanitation.FixedArticleQuotes > 0 Then
                cleaned.Add("corrected " & Plural(sanitation.FixedArticleQuotes, "quote mark"))
            End If
            If cleaned.Count = 0 Then Return String.Empty
            Return " Cleaned up: " & String.Join(", ", cleaned) & "."
        End Function

        Private Shared Function Summarise(lead As String,
                                          emptyPages As List(Of Integer),
                                          sanitation As SanitizeResult,
                                          flags As IReadOnlyList(Of ImportFlag)) As String
            Dim sb As New StringBuilder(lead)

            If emptyPages.Count > 0 Then
                Dim listed As New List(Of String)()
                For k As Integer = 0 To Math.Min(emptyPages.Count, MaxListedPages) - 1
                    listed.Add(emptyPages(k).ToString(Globalization.CultureInfo.InvariantCulture))
                Next
                sb.Append(" ").Append(Plural(emptyPages.Count, "page")).Append(" had")
                sb.Append(" no readable text and ").Append(If(emptyPages.Count = 1, "was", "were")).Append(" skipped: ")
                sb.Append(String.Join(", ", listed))
                If emptyPages.Count > MaxListedPages Then
                    sb.Append(" and ").Append(emptyPages.Count - MaxListedPages).Append(" more")
                End If
                sb.Append(". Check them in the original PDF.")
            End If

            sb.Append(CleanupSentence(sanitation))

            Dim wide As Integer = 0
            Dim hyphens As Integer = 0
            For Each flag In flags
                If flag.Kind = ImportFlagKind.WideSpaceRun Then wide += 1 Else hyphens += 1
            Next
            Dim review As New List(Of String)()
            If wide > 0 Then review.Add(Plural(wide, "wide gap"))
            If hyphens > 0 Then review.Add(Plural(hyphens, "line-end hyphen"))
            If review.Count > 0 Then
                sb.Append(" For review: ").Append(String.Join(" and ", review)).Append(".")
            End If

            Return sb.ToString()
        End Function

    End Class

End Namespace
