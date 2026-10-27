Imports System.IO
Imports System.Reflection
Imports System.Text

''' <summary>
''' Loads and parses the hand-editable SSML reference-panel content from its embedded
''' resource file. Tolerant of hand-editing quirks the same way omskep-meta is: a block
''' missing a Title, Snippet or Note simply yields an empty string for that field, never
''' an error, at runtime. Malformed structure (an [Entry] with no matching [/Entry]) is a
''' build-time concern caught by ReferencePanelContentTests, not something this loader
''' needs to defend against at runtime, since the resource is embedded and never
''' user-edited after the app ships.
''' </summary>
Public NotInheritable Class ReferencePanelContent

    Private Sub New()
    End Sub

    Public Const ResourceName As String = "Omskep.App.SsmlReferenceEntries.txt"

    Private Const EntryStart As String = "[Entry]"
    Private Const EntryEnd As String = "[/Entry]"
    Private Const TitleKey As String = "Title:"
    Private Const SnippetKey As String = "Snippet:"
    Private Const NoteKey As String = "Note:"

    ''' <summary>Loads entries from the embedded resource, in file order.</summary>
    Public Shared Function LoadEntries() As IReadOnlyList(Of (Title As String, Snippet As String, Note As String))
        Using stream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            If stream Is Nothing Then
                Return Array.Empty(Of (Title As String, Snippet As String, Note As String))()
            End If
            Using reader As New StreamReader(stream, Encoding.UTF8)
                Return Parse(reader.ReadToEnd())
            End Using
        End Using
    End Function

    ''' <summary>Parses raw resource text into entries, in file order. Exposed for direct testing
    ''' without needing the embedded resource to be built.</summary>
    Public Shared Function Parse(text As String) As IReadOnlyList(Of (Title As String, Snippet As String, Note As String))
        Dim results As New List(Of (Title As String, Snippet As String, Note As String))
        If String.IsNullOrEmpty(text) Then
            Return results
        End If

        Dim lines() As String = text.Replace(vbCrLf, vbLf).Split(vbLf)
        Dim i As Integer = 0

        While i < lines.Length
            If lines(i).Trim() = EntryStart Then
                i += 1
                Dim title As String = String.Empty
                Dim snippetLines As New List(Of String)
                Dim noteLines As New List(Of String)
                Dim mode As Integer = 0 ' 0 = none, 1 = snippet, 2 = note

                While i < lines.Length AndAlso lines(i).Trim() <> EntryEnd
                    Dim line As String = lines(i)
                    Dim trimmed As String = line.Trim()

                    If trimmed.StartsWith(TitleKey) Then
                        title = trimmed.Substring(TitleKey.Length).Trim()
                        mode = 0
                    ElseIf trimmed = SnippetKey Then
                        mode = 1
                    ElseIf trimmed = NoteKey Then
                        mode = 2
                    ElseIf mode = 1 Then
                        snippetLines.Add(line)
                    ElseIf mode = 2 Then
                        noteLines.Add(line)
                    End If
                    i += 1
                End While

                results.Add((title, JoinTrimmed(snippetLines), JoinTrimmed(noteLines)))
            End If
            i += 1
        End While

        Return results
    End Function

    ' Trims exactly the leading/trailing blank line the [Entry]/Snippet:/Note: framing
    ' naturally introduces, without touching a meaningful blank line in the middle of a
    ' hand-typed value.
    Private Shared Function JoinTrimmed(lines As List(Of String)) As String
        While lines.Count > 0 AndAlso lines(0).Trim().Length = 0
            lines.RemoveAt(0)
        End While
        While lines.Count > 0 AndAlso lines(lines.Count - 1).Trim().Length = 0
            lines.RemoveAt(lines.Count - 1)
        End While
        Return String.Join(vbCrLf, lines)
    End Function

End Class