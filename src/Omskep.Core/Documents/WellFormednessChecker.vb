Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Xml

Namespace Documents

    ''' <summary>
    ''' The outcome of checking a document. Immutable. Line and column are 1-based and count UTF-16
    ''' characters (not bytes); they are 0 when the document is well-formed.
    ''' </summary>
    Public NotInheritable Class CheckResult

        Public Shared ReadOnly Ok As New CheckResult(True, 0, 0, String.Empty, 0, 0, Nothing)

        Public ReadOnly Property IsWellFormed As Boolean

        ''' <summary>Where the parser gave up (the first error only).</summary>
        Public ReadOnly Property Line As Integer
        Public ReadOnly Property Column As Integer

        Public ReadOnly Property Message As String

        ''' <summary>Where the element that is probably still open was started; 0 when there is no hint.
        ''' The parser reports an unclosed tag at the next closing tag, often many lines after the mistake.</summary>
        Public ReadOnly Property CauseLine As Integer
        Public ReadOnly Property CauseColumn As Integer
        Public ReadOnly Property CauseElement As String

        Private Sub New(isWellFormed As Boolean, line As Integer, column As Integer, message As String,
                        causeLine As Integer, causeColumn As Integer, causeElement As String)
            Me.IsWellFormed = isWellFormed
            Me.Line = line
            Me.Column = column
            Me.Message = message
            Me.CauseLine = causeLine
            Me.CauseColumn = causeColumn
            Me.CauseElement = causeElement
        End Sub

        Friend Shared Function Malformed(line As Integer, column As Integer, message As String,
                                         causeLine As Integer, causeColumn As Integer, causeElement As String) As CheckResult
            Return New CheckResult(False, line, column, message, causeLine, causeColumn, causeElement)
        End Function

        Public ReadOnly Property HasCause As Boolean
            Get
                Return CauseLine > 0
            End Get
        End Property

        ''' <summary>One line for the status bar.</summary>
        Public ReadOnly Property StatusText As String
            Get
                If IsWellFormed Then Return "Well-formed"
                Dim text As String = "Line " & Line.ToString(Globalization.CultureInfo.InvariantCulture) & ": " & Message
                If HasCause Then
                    text &= " (the <" & CauseElement & "> opened on line " &
                            CauseLine.ToString(Globalization.CultureInfo.InvariantCulture) & " is still open)"
                End If
                Return text
            End Get
        End Property

    End Class

    ''' <summary>
    ''' Checks that a document is well-formed XML (not that it is valid SSML; Azure decides that).
    ''' Pure and stateless. DTDs are prohibited, so no entity can be expanded and nothing is fetched.
    ''' Only reports: it never edits the text. Never throws: every failure is a malformed result.
    ''' </summary>
    Public NotInheritable Class WellFormednessChecker

        Private NotInheritable Class OpenElement
            Public ReadOnly Name As String
            Public ReadOnly Line As Integer
            Public ReadOnly Column As Integer

            Public Sub New(name As String, line As Integer, column As Integer)
                Me.Name = name
                Me.Line = line
                Me.Column = column
            End Sub
        End Class

        Private Sub New()
        End Sub

        Public Shared Function Check(documentText As String) As CheckResult
            If String.IsNullOrWhiteSpace(documentText) Then
                Return CheckResult.Malformed(1, 1, "The document is empty.", 0, 0, Nothing)
            End If

            Dim settings As New XmlReaderSettings() With {
                .DtdProcessing = DtdProcessing.Prohibit,
                .XmlResolver = Nothing
            }
            Dim openElements As New Stack(Of OpenElement)()

            Try
                Using reader As XmlReader = XmlReader.Create(New StringReader(documentText), settings)
                    Dim info As IXmlLineInfo = TryCast(reader, IXmlLineInfo)
                    While reader.Read()
                        Select Case reader.NodeType
                            Case XmlNodeType.Element
                                If Not reader.IsEmptyElement Then
                                    Dim startLine As Integer = If(info Is Nothing, 0, info.LineNumber)
                                    Dim startColumn As Integer = If(info Is Nothing, 0, info.LinePosition)
                                    openElements.Push(New OpenElement(reader.Name, startLine, startColumn))
                                End If
                            Case XmlNodeType.EndElement
                                If openElements.Count > 0 Then openElements.Pop()
                        End Select
                    End While
                End Using
                Return CheckResult.Ok
            Catch ex As XmlException
                Return Explain(documentText, ex, openElements)
            Catch ex As Exception
                Return CheckResult.Malformed(1, 1, "The document could not be checked: " & ex.Message, 0, 0, Nothing)
            End Try
        End Function

        Private Shared Function Explain(documentText As String, ex As XmlException, openElements As Stack(Of OpenElement)) As CheckResult
            Dim line As Integer = Math.Max(1, ex.LineNumber)
            Dim column As Integer = Math.Max(1, ex.LinePosition)
            Dim message As String = Regex.Replace(ex.Message, "\s*Line \d+, position \d+\.\s*$", String.Empty)

            ' A tag-structure error is reported right after "</" (an end tag that does not match) or at
            ' the very end of the text (elements never closed). Only then is the innermost open element a
            ' sensible suspect; for any other kind of error (a stray "&", a half-typed attribute) it is not.
            Dim offset As Integer = OffsetOf(documentText, line, column)
            Dim afterEndTagOpener As Boolean = offset >= 2 AndAlso documentText(offset - 2) = "<"c AndAlso documentText(offset - 1) = "/"c
            Dim atEnd As Boolean = documentText.Substring(Math.Min(offset, documentText.Length)).Trim().Length = 0

            If (afterEndTagOpener OrElse atEnd) AndAlso openElements.Count > 0 Then
                Dim suspect As OpenElement = openElements.Peek()
                If suspect.Line > 0 Then
                    Return CheckResult.Malformed(line, column, message, suspect.Line, suspect.Column, suspect.Name)
                End If
            End If
            Return CheckResult.Malformed(line, column, message, 0, 0, Nothing)
        End Function

        Private Shared Function OffsetOf(text As String, line As Integer, column As Integer) As Integer
            Dim index As Integer = 0
            Dim current As Integer = 1
            While current < line
                Dim newline As Integer = text.IndexOf(ChrW(10), index)
                If newline < 0 Then Return text.Length
                index = newline + 1
                current += 1
            End While
            Return Math.Min(text.Length, index + column - 1)
        End Function

    End Class

End Namespace
