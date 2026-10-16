Option Strict On
Option Explicit On
Option Infer On

Imports UglyToad.PdfPig
Imports UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor

Namespace Import

    ''' <summary>
    ''' Reads PDF text with PdfPig's ContentOrderTextExtractor (chosen over page.Text in the
    ''' phase 2 extraction spike because it keeps visual line breaks). Needs the PdfPig package
    ''' in Omskep.Core. Thin on purpose: all decisions live in <see cref="DocumentImporter"/>.
    ''' </summary>
    Public NotInheritable Class PdfPigPageReader
        Implements IPdfPageReader

        Public Function ReadPages(pdfPath As String) As IReadOnlyList(Of String) Implements IPdfPageReader.ReadPages
            Dim pages As New List(Of String)()
            Try
                Using doc As PdfDocument = PdfDocument.Open(pdfPath)
                    For Each pg In doc.GetPages()
                        pages.Add(ContentOrderTextExtractor.GetText(pg))
                    Next
                End Using
            Catch ex As Exception When IsPasswordRequired(ex)
                Throw New PdfPasswordRequiredException()
            End Try
            Return pages
        End Function

        ' Matched by name so this file does not depend on PdfPig's exception namespace.
        Private Shared Function IsPasswordRequired(ex As Exception) As Boolean
            Return ex.GetType().Name = "PdfDocumentEncryptedException"
        End Function

        Private Function NullIfBlank(s As String) As String
            Return If(String.IsNullOrWhiteSpace(s), Nothing, s)
        End Function

        Public Function ReadMetadata(pdfPath As String) As PdfDocumentMetadata Implements IPdfPageReader.ReadMetadata
            Using document As PdfDocument = PdfDocument.Open(pdfPath)
                Dim info = document.Information
                Return New PdfDocumentMetadata(NullIfBlank(info.Title), NullIfBlank(info.Author), NullIfBlank(info.CreationDate), NullIfBlank(info.ModifiedDate))
            End Using
        End Function
    End Class

End Namespace
