Option Strict On
Option Explicit On
Option Infer On

Imports System.IO

Namespace Documents

    ''' <summary>Whether the document matches what is on disk.</summary>
    Public Enum SaveState
        ''' <summary>A new, untitled document nobody has touched. Nothing to save or lose.</summary>
        Pristine
        ''' <summary>Matches the file on disk.</summary>
        Clean
        ''' <summary>Has changes that exist nowhere else (an imported or edited document, saved or not).</summary>
        Dirty
    End Enum

    ''' <summary>Whether the markup of the current text is known to be well-formed.</summary>
    Public Enum CheckState
        ''' <summary>The text changed and the result for it has not arrived yet.</summary>
        Unchecked
        WellFormed
        Malformed
    End Enum

    ''' <summary>
    ''' Tracks which document is open and what is known about it. Holds no text: the editor owns that.
    ''' Pure, no UI; use from the UI thread only.
    '''
    ''' Two things are tracked separately on purpose. A malformed document is still an ordinary, saveable
    ''' document (saving a work in progress is never blocked); the check state only gates export.
    '''
    ''' Each change to the content bumps <see cref="Revision"/>. A check started for one revision is ignored
    ''' if the text has changed since, so a slow result can never mark newer text as checked.
    ''' </summary>
    Public NotInheritable Class DocumentModel

        Public ReadOnly Property FilePath As String
        Public ReadOnly Property SaveState As SaveState
        Public ReadOnly Property CheckState As CheckState
        Public ReadOnly Property LastCheck As CheckResult
        Public ReadOnly Property Revision As Integer

        Public Sub New()
            NewDocument()
        End Sub

        Public ReadOnly Property DisplayName As String
            Get
                Return If(FilePath Is Nothing, "Untitled", Path.GetFileName(FilePath))
            End Get
        End Property

        Public ReadOnly Property IsWellFormed As Boolean
            Get
                Return CheckState = CheckState.WellFormed
            End Get
        End Property

        Public ReadOnly Property HasUnsavedChanges As Boolean
            Get
                Return SaveState = SaveState.Dirty
            End Get
        End Property

        ''' <summary>A fresh untitled document (the caller puts the scaffold into the editor).</summary>
        Public Sub NewDocument()
            StartContent(Nothing, SaveState.Pristine)
        End Sub

        ''' <summary>A file was opened from disk.</summary>
        Public Sub OpenedFile(path As String)
            RequirePath(path)
            StartContent(path, SaveState.Clean)
        End Sub

        ''' <summary>A PDF or pasted text became a new, unsaved document.</summary>
        Public Sub Imported()
            StartContent(Nothing, SaveState.Dirty)
        End Sub

        ''' <summary>The text changed in any way.</summary>
        Public Sub Edited()
            _SaveState = SaveState.Dirty
            _CheckState = CheckState.Unchecked
            _Revision += 1
        End Sub

        ''' <summary>The text was written to disk (the text itself did not change).</summary>
        Public Sub Saved(path As String)
            RequirePath(path)
            _FilePath = path
            _SaveState = SaveState.Clean
        End Sub

        ''' <summary>The editor undid back to the saved content. Ignored for a document with no file.</summary>
        Public Sub ReachedSavePoint()
            If FilePath IsNot Nothing Then _SaveState = SaveState.Clean
        End Sub

        ''' <summary>Applies a check result if it was computed for the current text.</summary>
        ''' <returns>False when the text has changed since (the result is stale and was ignored).</returns>
        Public Function CheckCompleted(forRevision As Integer, result As CheckResult) As Boolean
            If result Is Nothing Then Throw New ArgumentNullException(NameOf(result))
            If forRevision <> Revision Then Return False
            _LastCheck = result
            _CheckState = If(result.IsWellFormed, CheckState.WellFormed, CheckState.Malformed)
            Return True
        End Function

        Private Sub StartContent(path As String, save As SaveState)
            _FilePath = path
            _SaveState = save
            _CheckState = CheckState.Unchecked
            _LastCheck = Nothing
            _Revision += 1
        End Sub

        Private Shared Sub RequirePath(path As String)
            If String.IsNullOrWhiteSpace(path) Then
                Throw New ArgumentException("A file path is required.", NameOf(path))
            End If
        End Sub

    End Class

End Namespace
