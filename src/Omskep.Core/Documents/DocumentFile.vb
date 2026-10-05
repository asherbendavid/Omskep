Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text
Imports Omskep.Core.Storage

Namespace Documents

    ''' <summary>A file could not be opened or saved. The message is written for the user.</summary>
    Public Class DocumentFileException
        Inherits Exception

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        Public Sub New(message As String, innerException As Exception)
            MyBase.New(message, innerException)
        End Sub
    End Class

    ''' <summary>
    ''' Reads and writes the text of a document. UTF-8 without a byte-order mark on save, LF line endings,
    ''' written through <see cref="AtomicFile"/> so a crash or a failure leaves the old file intact.
    ''' Loading never edits markup: only line endings are normalised. Failures become
    ''' <see cref="DocumentFileException"/> with a message the user can act on.
    ''' </summary>
    Public NotInheritable Class DocumentFile

        ''' <summary>Larger files are refused: a study is well under a megabyte.</summary>
        Public Const MaxBytes As Integer = 20 * 1024 * 1024

        Private Sub New()
        End Sub

        Public Shared Function Load(path As String, Optional maxBytes As Integer = MaxBytes) As String
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A file path is required.", NameOf(path))

            Dim bytes As Byte()
            Try
                Dim info As New FileInfo(path)
                If Not info.Exists Then
                    Throw New FileNotFoundException("the file does not exist")
                End If
                If info.Length > maxBytes Then
                    Return Refuse("That file is too large to be a study document.")
                End If
                bytes = File.ReadAllBytes(path)
            Catch ex As Exception When IsFileProblem(ex)
                Throw New DocumentFileException("The file could not be opened (" & ex.Message & ").", ex)
            End Try

            Dim text As String = Decode(bytes)
            If text.Contains(ChrW(0)) Then
                Return Refuse("That file contains binary data, so it is not a text document.")
            End If
            Return text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        End Function

        ''' <summary>Writes the text atomically. Line endings are written as LF.</summary>
        ''' <exception cref="DocumentFileException">The text could not be encoded or the file could not be written.</exception>
        Public Shared Sub Save(path As String, text As String)
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A file path is required.", NameOf(path))
            If text Is Nothing Then Throw New ArgumentNullException(NameOf(text))

            Dim bytes As Byte()
            Try
                bytes = New UTF8Encoding(False, True).GetBytes(text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf))
            Catch ex As EncoderFallbackException
                Throw New DocumentFileException(
                    "The text contains a character that cannot be saved (an unpaired surrogate). " &
                    "Your text is still in the editor.", ex)
            End Try

            Try
                AtomicFile.WriteAllBytes(path, bytes)
            Catch ex As Exception When IsFileProblem(ex)
                Throw New DocumentFileException(
                    "The file could not be saved (" & ex.Message & "). Your text is still in the editor; " &
                    "try Save As to another location.", ex)
            End Try
        End Sub

        Private Shared Function Decode(bytes As Byte()) As String
            Try
                If bytes.Length >= 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF Then
                    Return New UTF8Encoding(False, True).GetString(bytes, 3, bytes.Length - 3)
                End If
                If bytes.Length >= 2 AndAlso bytes(0) = &HFF AndAlso bytes(1) = &HFE Then
                    Return New UnicodeEncoding(False, False, True).GetString(bytes, 2, bytes.Length - 2)
                End If
                If bytes.Length >= 2 AndAlso bytes(0) = &HFE AndAlso bytes(1) = &HFF Then
                    Return New UnicodeEncoding(True, False, True).GetString(bytes, 2, bytes.Length - 2)
                End If
                Return New UTF8Encoding(False, True).GetString(bytes)
            Catch ex As DecoderFallbackException
                Throw New DocumentFileException(
                    "That file is not UTF-8 text. Open it in Notepad, choose Save As and set the encoding " &
                    "to UTF-8, then open it here.", ex)
            End Try
        End Function

        ' Always throws; returns String only so callers can write "Return Refuse(...)".
        Private Shared Function Refuse(message As String) As String
            Throw New DocumentFileException(message)
        End Function

        Private Shared Function IsFileProblem(ex As Exception) As Boolean
            Return TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse
                   TypeOf ex Is NotSupportedException OrElse TypeOf ex Is ArgumentException OrElse
                   TypeOf ex Is Security.SecurityException
        End Function

    End Class

End Namespace
