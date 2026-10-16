Option Strict On
Option Explicit On
Option Infer On

Imports TagLibSharp2.Core
Imports TagLibSharp2.Id3.Id3v2
Imports TagLibSharp2.Mpeg

Namespace Export
    Public NotInheritable Class Id3Fields
        Public ReadOnly Property Title As String
        Public ReadOnly Property Artist As String
        Public ReadOnly Property Album As String
        Public ReadOnly Property Track As UInteger?
        Public ReadOnly Property Year As String
        Public ReadOnly Property Copyright As String
        Public ReadOnly Property Comment As String
        Public ReadOnly Property Genre As String

        Public Sub New(title As String, artist As String, album As String, track As UInteger?,
                       year As String, copyright As String, comment As String, genre As String)
            Me.Title = title : Me.Artist = artist : Me.Album = album : Me.Track = track
            Me.Year = year : Me.Copyright = copyright : Me.Comment = comment : Me.Genre = genre
        End Sub
    End Class

    Public NotInheritable Class Id3StampResult
        Public ReadOnly Property Succeeded As Boolean
        Public ReadOnly Property Message As String

        Private Sub New(succeeded As Boolean, message As String)
            Me.Succeeded = succeeded
            Me.Message = message
        End Sub

        Public Shared Function Ok() As Id3StampResult
            Return New Id3StampResult(True, Nothing)
        End Function

        Public Shared Function Failed(message As String) As Id3StampResult
            Return New Id3StampResult(False, message)
        End Function
    End Class

    ''' <summary>Stamps ID3 tags onto an already-exported, untagged MP3 file in place.</summary>
    Public NotInheritable Class Id3Stamper
        Public Shared Function Stamp(mp3Path As String, fields As Id3Fields) As Id3StampResult
            Dim readResult As Mp3FileReadResult = Mp3File.ReadFromFile(mp3Path)
            If Not readResult.IsSuccess Then
                Return Id3StampResult.Failed("Could not read the exported MP3 to tag it: " & readResult.Error)
            End If

            Using mp3 As Mp3File = readResult.File
                ' Title/Artist/etc. each auto-create an Id3v2Tag via Mp3File's own setters, but Copyright
                ' has no such convenience property on Mp3File -- only on Id3v2Tag itself -- so it needs the
                ' tag to already exist, explicitly, rather than relying on setter-order to create it first.
                If mp3.Id3v2Tag Is Nothing Then mp3.Id3v2Tag = New Id3v2Tag()

                mp3.Title = fields.Title
                mp3.Artist = fields.Artist
                mp3.Album = fields.Album
                mp3.Track = fields.Track
                mp3.Year = fields.Year
                mp3.Genre = fields.Genre
                mp3.Comment = fields.Comment
                mp3.Id3v2Tag.Copyright = fields.Copyright

                Dim writeResult As FileWriteResult = mp3.SaveToFile(mp3Path)
                If Not writeResult.IsSuccess Then
                    Return Id3StampResult.Failed("The audio exported successfully, but the ID3 tags could not be written: " & writeResult.Error)
                End If
            End Using

            Return Id3StampResult.Ok()
        End Function
    End Class


End Namespace
