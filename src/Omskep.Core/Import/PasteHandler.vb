Option Strict On
Option Explicit On
Option Infer On

Imports Omskep.Core.Documents

Namespace Import

    Public Enum PasteKind
        ''' <summary>Nothing usable on the clipboard; Message says why.</summary>
        NothingToPaste
        ''' <summary>The document was blank: Text is a complete new document to put in its place.</summary>
        ReplaceDocument
        ''' <summary>Text is to be inserted at the caret.</summary>
        InsertText
    End Enum

    ''' <summary>What the form should do for a paste. Immutable.</summary>
    Public NotInheritable Class PastePlan

        Public ReadOnly Property Kind As PasteKind
        Public ReadOnly Property Text As String
        Public ReadOnly Property Message As String

        Public Sub New(kind As PasteKind, text As String, message As String)
            Me.Kind = kind
            Me.Text = text
            Me.Message = message
        End Sub

    End Class

    ''' <summary>
    ''' Decides what a paste does, so the clipboard code in the form stays trivial:
    ''' Paste (sanitizing) and Paste as plain text (raw). Pure; the clipboard text is passed in.
    ''' </summary>
    Public NotInheritable Class PasteHandler

        ' The paste path never reads a PDF; this reader makes that explicit.
        Private NotInheritable Class NoPdfReader
            Implements IPdfPageReader

            Public Function ReadPages(pdfPath As String) As IReadOnlyList(Of String) Implements IPdfPageReader.ReadPages
                Throw New NotSupportedException("Pasting does not read PDFs.")
            End Function
        End Class

        Private Sub New()
        End Sub

        ''' <param name="currentDocument">The text now in the editor.</param>
        ''' <param name="clipboardText">The clipboard text, or Nothing when it holds none.</param>
        ''' <param name="plainText">True for Paste as plain text: insert exactly what was copied (only line endings are
        ''' normalised and NUL characters, which no document can hold, are dropped). False for the normal Paste:
        ''' sanitize, and turn a blank document into a complete imported one.</param>
        Public Shared Function Plan(currentDocument As String, clipboardText As String, locale As String,
                                    voiceShortName As String, plainText As Boolean) As PastePlan
            If String.IsNullOrWhiteSpace(clipboardText) Then
                Return New PastePlan(PasteKind.NothingToPaste, String.Empty, "The clipboard has no text.")
            End If

            Dim lf As String = ChrW(10)
            If plainText Then
                Dim raw As String = clipboardText.Replace(vbCrLf, lf).Replace(vbCr, lf).Replace(ChrW(0), String.Empty)
                Return New PastePlan(PasteKind.InsertText, raw, "Pasted as plain text.")
            End If

            If DocumentScaffold.IsBlank(currentDocument) Then
                Dim imported As ImportResult = New DocumentImporter(New NoPdfReader()).ImportText(clipboardText, locale, voiceShortName)
                If Not imported.Succeeded Then
                    Return New PastePlan(PasteKind.NothingToPaste, String.Empty, imported.Message)
                End If
                Return New PastePlan(PasteKind.ReplaceDocument, imported.Document, imported.Message)
            End If

            Dim sanitation As SanitizeResult = TextSanitizer.Sanitize(clipboardText)
            If String.IsNullOrWhiteSpace(sanitation.Body) Then
                Return New PastePlan(PasteKind.NothingToPaste, String.Empty, "There was no text left to paste after cleaning.")
            End If
            Return New PastePlan(PasteKind.InsertText, sanitation.Body, "Pasted." & DocumentImporter.CleanupSentence(sanitation))
        End Function

    End Class

End Namespace
