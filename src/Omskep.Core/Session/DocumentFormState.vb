Option Strict On
Option Explicit On
Option Infer On

Imports Omskep.Core.Access
Imports Omskep.Core.Documents

Namespace Session

    ''' <summary>
    ''' What the main window shows for the open document, combined with the access state. Pure, so the
    ''' form only copies values across. Kept separate from <see cref="MainFormState"/> (which is about
    ''' access) so that class and its tests stay unchanged.
    ''' </summary>
    Public NotInheritable Class DocumentFormState

        ''' <summary>The editor accepts typing. False while locked.</summary>
        Public ReadOnly Property EditorEnabled As Boolean

        ''' <summary>New, Open and the paste commands: anything that starts or changes content.</summary>
        Public ReadOnly Property NewOpenPasteEnabled As Boolean

        ''' <summary>Save is on whenever there are unsaved changes, even while locked or malformed:
        ''' edits must never be trapped by a key problem or a markup error.</summary>
        Public ReadOnly Property SaveEnabled As Boolean

        Public ReadOnly Property SaveAsEnabled As Boolean

        ''' <summary>Export is allowed: access allows it AND the document is well-formed.</summary>
        Public ReadOnly Property ExportEnabled As Boolean

        ''' <summary>Why export is blocked by the document (empty when it is not, or when access is the reason).</summary>
        Public ReadOnly Property ExportBlockedReason As String

        Public ReadOnly Property WindowTitle As String

        ''' <summary>For the status bar: the well-formedness result.</summary>
        Public ReadOnly Property CheckText As String

        ''' <summary>For the status bar: "up to N characters".</summary>
        Public ReadOnly Property CountText As String

        Private Sub New(editorEnabled As Boolean, newOpenPasteEnabled As Boolean, saveEnabled As Boolean,
                        saveAsEnabled As Boolean, exportEnabled As Boolean, exportBlockedReason As String,
                        windowTitle As String, checkText As String, countText As String)
            Me.EditorEnabled = editorEnabled
            Me.NewOpenPasteEnabled = newOpenPasteEnabled
            Me.SaveEnabled = saveEnabled
            Me.SaveAsEnabled = saveAsEnabled
            Me.ExportEnabled = exportEnabled
            Me.ExportBlockedReason = exportBlockedReason
            Me.WindowTitle = windowTitle
            Me.CheckText = checkText
            Me.CountText = countText
        End Sub

        ''' <param name="billableUpperBound">From <see cref="BillableCharacterCounter"/>.</param>
        ''' <param name="provider">Number formatting; the current culture when omitted.</param>
        Public Shared Function From(access As AccessState, document As DocumentModel, billableUpperBound As Integer,
                                    Optional provider As IFormatProvider = Nothing) As DocumentFormState
            If access Is Nothing Then Throw New ArgumentNullException(NameOf(access))
            If document Is Nothing Then Throw New ArgumentNullException(NameOf(document))
            Dim culture As IFormatProvider = If(provider, Globalization.CultureInfo.CurrentCulture)

            Dim pristine As Boolean = document.SaveState = SaveState.Pristine
            Dim documentAllowsExport As Boolean = document.IsWellFormed AndAlso Not pristine

            Dim reason As String = String.Empty
            If Not documentAllowsExport Then
                If pristine Then
                    reason = "There is nothing to export yet."
                ElseIf document.CheckState = CheckState.Unchecked Then
                    reason = "Checking the markup..."
                ElseIf document.LastCheck IsNot Nothing Then
                    reason = "Fix the markup error on line " &
                             document.LastCheck.Line.ToString(Globalization.CultureInfo.InvariantCulture) & " first."
                End If
            End If

            Dim title As String = document.DisplayName & If(document.HasUnsavedChanges, "*", "") & " - Omskep"

            Dim check As String
            If document.LastCheck IsNot Nothing Then
                check = document.LastCheck.StatusText
            ElseIf pristine Then
                check = String.Empty
            Else
                check = "Checking..."
            End If

            Dim count As String = "up to " & Math.Max(0, billableUpperBound).ToString("N0", culture) & " characters"

            Return New DocumentFormState(
                editorEnabled:=access.CanEdit,
                newOpenPasteEnabled:=access.CanEdit,
                saveEnabled:=document.HasUnsavedChanges,
                saveAsEnabled:=Not pristine,
                exportEnabled:=access.CanExport AndAlso documentAllowsExport,
                exportBlockedReason:=reason,
                windowTitle:=title,
                checkText:=check,
                countText:=count)
        End Function

    End Class

End Namespace
