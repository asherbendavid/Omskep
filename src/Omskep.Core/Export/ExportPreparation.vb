Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports Omskep.Core.Documents
Imports Omskep.Core.Session

Namespace Export

    Public NotInheritable Class ExportPreparationResult
        Public ReadOnly Property Succeeded As Boolean
        Public ReadOnly Property FailureMessage As String
        Public ReadOnly Property Chunks As IReadOnlyList(Of DocumentChunk)
        Public ReadOnly Property BillableUpperBound As Integer
        Public ReadOnly Property Fields As Id3Fields
        Public ReadOnly Property DefaultFileName As String

        Private Sub New(succeeded As Boolean, failureMessage As String, chunks As IReadOnlyList(Of DocumentChunk),
                     billableUpperBound As Integer, fields As Id3Fields, defaultFileName As String)
            Me.Succeeded = succeeded
            Me.FailureMessage = failureMessage
            Me.Chunks = chunks
            Me.BillableUpperBound = billableUpperBound
            Me.Fields = fields
            Me.DefaultFileName = defaultFileName
        End Sub

        Public Shared Function Failed(message As String) As ExportPreparationResult
            Return New ExportPreparationResult(False, message, Nothing, 0, Nothing, Nothing)
        End Function

        Public Shared Function Ok(chunks As IReadOnlyList(Of DocumentChunk), billableUpperBound As Integer,
                               fields As Id3Fields, defaultFileName As String) As ExportPreparationResult
            Return New ExportPreparationResult(True, Nothing, chunks, billableUpperBound, fields, defaultFileName)
        End Function
    End Class

    ''' <summary>Everything that has to happen between "Export" being clicked and the confirmation
    ''' dialog actually opening: re-running the export gate (never trusting the menu's last-enabled
    ''' state), composing and chunking the document, resolving the omskep-meta/computed ID3 fields,
    ''' and picking the default export filename.</summary>
    Public NotInheritable Class ExportPreparation

        Public Shared Function Prepare(session As AppSession, model As DocumentModel, documentText As String,
                                    outputFolder As String, sourceFileName As String) As ExportPreparationResult
            Dim billable0 As Integer = BillableCharacterCounter.CountUpperBound(documentText)
            Dim ds As DocumentFormState = DocumentFormState.From(session.Access, model, billable0)
            If Not ds.ExportEnabled Then
                Return ExportPreparationResult.Failed(If(String.IsNullOrEmpty(ds.ExportBlockedReason), "Export is not currently available.", ds.ExportBlockedReason))
            End If

            Dim composition As CompositionResult = SsmlComposer.Compose(documentText, session.SpeakingRatePercent())
            If Not composition.IsSuccess Then
                Return ExportPreparationResult.Failed("The document could not be composed for export: " & composition.FailureReason)
            End If

            Dim chunking As ChunkingResult = Chunker.Chunk(composition.Ssml)
            If Not chunking.IsSuccess Then
                Return ExportPreparationResult.Failed("The document could not be split for export: " & chunking.FailureReason)
            End If

            Dim upperBound As Integer = Chunker.TotalBillableCharacters(chunking.Chunks)
            Dim rateDescription As String = DescribeRates(session.SpeakingRatePercent())
            Dim fields As Id3Fields = ExportId3FieldsBuilder.Build(documentText, chunking.Chunks, rateDescription, sourceFileName)

            Dim stem As String = If(model.FilePath IsNot Nothing, Path.GetFileNameWithoutExtension(model.FilePath), Nothing)
            Dim defaultName As String = ExportFileNamer.DefaultFileName(stem, outputFolder)

            Return ExportPreparationResult.Ok(chunking.Chunks, upperBound, fields, defaultName)
        End Function

        ''' <summary>Describes the saved speaking-rate settings for the ID3 Comment. Deliberately reports
        ''' every locale that has a non-default saved rate, not only locales actually used in this
        ''' particular document -- scoping it to just-the-locales-present would need cross-referencing
        ''' each chunk's voice back to a locale, which nothing in Core currently exposes a clean way to
        ''' do (locale is resolved internally during SsmlComposer's own prosody-wrapping step). Simpler
        ''' and still accurate; worth revisiting only if this ever reads as misleading in practice.</summary>
        Private Shared Function DescribeRates(ratesByLocale As IReadOnlyDictionary(Of String, Integer)) As String
            If ratesByLocale Is Nothing OrElse ratesByLocale.Count = 0 Then Return "default"
            Return String.Join(", ", ratesByLocale.
            OrderBy(Function(kv) kv.Key, StringComparer.Ordinal).
            Select(Function(kv) $"{kv.Key} {(If(kv.Value >= 0, "+", ""))}{kv.Value}%"))
        End Function

    End Class

End Namespace
