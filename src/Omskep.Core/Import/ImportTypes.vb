Option Strict On
Option Explicit On
Option Infer On

Namespace Import

    ''' <summary>How an import ended. Everything except the two "Imported" values has no document.</summary>
    Public Enum ImportOutcome
        ''' <summary>Every page had text.</summary>
        Imported
        ''' <summary>Imported, but some pages had no readable text (see EmptyPages).</summary>
        ImportedWithEmptyPages
        ''' <summary>No usable text: a scanned or image-only PDF, or nothing pasted.</summary>
        NoTextFound
        PasswordProtected
        ''' <summary>Damaged, not a PDF, missing, or locked by another program.</summary>
        Unreadable
    End Enum

    ''' <summary>Thrown by an <see cref="IPdfPageReader"/> when the PDF needs a password to open.</summary>
    Public Class PdfPasswordRequiredException
        Inherits Exception

        Public Sub New()
            MyBase.New("The PDF needs a password to open.")
        End Sub
    End Class

    ''' <summary>
    ''' Reads the text of a PDF, one string per page, in page order. A page with no text
    ''' yields an empty string. Throws <see cref="PdfPasswordRequiredException"/> for a
    ''' password-protected file; any other failure may throw anything.
    ''' </summary>
    Public Interface IPdfPageReader
        Function ReadPages(pdfPath As String) As IReadOnlyList(Of String)
    End Interface

    Public Enum ImportFlagKind
        ''' <summary>Three or more spaces in a row: usually a header or footer gap.</summary>
        WideSpaceRun
        ''' <summary>A line ending in a hyphen after a letter: possibly a word split across lines.</summary>
        LineEndHyphen
    End Enum

    ''' <summary>A place the user should look at. Never an edit. Line and column are 1-based
    ''' and count UTF-16 characters, not bytes.</summary>
    Public NotInheritable Class ImportFlag

        Public ReadOnly Property Kind As ImportFlagKind
        Public ReadOnly Property Line As Integer
        Public ReadOnly Property Column As Integer
        Public ReadOnly Property Length As Integer

        Public Sub New(kind As ImportFlagKind, line As Integer, column As Integer, length As Integer)
            Me.Kind = kind
            Me.Line = line
            Me.Column = column
            Me.Length = length
        End Sub

    End Class

    Public NotInheritable Class ImportResult

        Public ReadOnly Property Outcome As ImportOutcome

        ''' <summary>Text for the user: what happened, and what to check.</summary>
        Public ReadOnly Property Message As String

        ''' <summary>The complete SSML document, or an empty string when the import failed.</summary>
        Public ReadOnly Property Document As String

        ''' <summary>Pages in the PDF (0 for pasted text).</summary>
        Public ReadOnly Property PageCount As Integer

        ''' <summary>1-based numbers of pages that had no readable text and were skipped.</summary>
        Public ReadOnly Property EmptyPages As IReadOnlyList(Of Integer)

        ''' <summary>What the sanitizer changed, summed over all pages.</summary>
        Public ReadOnly Property Sanitation As SanitizeResult

        ''' <summary>Places to review, in document coordinates (line 1 is the speak tag).</summary>
        Public ReadOnly Property Flags As IReadOnlyList(Of ImportFlag)

        Public Sub New(outcome As ImportOutcome,
                       message As String,
                       document As String,
                       pageCount As Integer,
                       emptyPages As IReadOnlyList(Of Integer),
                       sanitation As SanitizeResult,
                       flags As IReadOnlyList(Of ImportFlag))
            Me.Outcome = outcome
            Me.Message = message
            Me.Document = document
            Me.PageCount = pageCount
            Me.EmptyPages = emptyPages
            Me.Sanitation = sanitation
            Me.Flags = flags
        End Sub

        Public ReadOnly Property Succeeded As Boolean
            Get
                Return Outcome = ImportOutcome.Imported OrElse Outcome = ImportOutcome.ImportedWithEmptyPages
            End Get
        End Property

    End Class

End Namespace
