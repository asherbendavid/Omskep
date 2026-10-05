Option Strict On
Option Explicit On
Option Infer On

Imports System.Globalization
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Session

Namespace Session

    <TestClass>
    Public Class DocumentFormStateTests

        Private Shared ReadOnly Full As AccessState = New AccessState(AccessMode.Full, AccessReason.Ready, String.Empty)
        Private Shared ReadOnly EditOnly As AccessState = New AccessState(AccessMode.EditOnly, AccessReason.KeyRejected, "blocked")
        Private Shared ReadOnly Locked As AccessState = New AccessState(AccessMode.Locked, AccessReason.NoKey, "enter key")

        Private Shared Function TestPath(fileName As String) As String
            Return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Omskep O'Brien", fileName)
        End Function

        Private Shared Function State(access As AccessState, doc As DocumentModel, Optional count As Integer = 0) As DocumentFormState
            Return DocumentFormState.From(access, doc, count, CultureInfo.InvariantCulture)
        End Function

        Private Shared Function WellFormedDirty() As DocumentModel
            Dim m As New DocumentModel()
            m.Edited()
            m.CheckCompleted(m.Revision, CheckResult.Ok)
            Return m
        End Function

        Private Shared Function MalformedDirty() As DocumentModel
            Dim m As New DocumentModel()
            m.Edited()
            m.CheckCompleted(m.Revision, WellFormednessChecker.Check("<speak>" & ChrW(10) & "a & b" & ChrW(10) & "</speak>"))
            Return m
        End Function

        ' ---- pristine / new ----

        <TestMethod>
        Public Sub A_new_untouched_document_has_nothing_to_save_or_export()
            Dim st = State(Full, New DocumentModel())
            Assert.IsFalse(st.SaveEnabled)
            Assert.IsFalse(st.SaveAsEnabled)
            Assert.IsFalse(st.ExportEnabled)
            Assert.AreEqual("There is nothing to export yet.", st.ExportBlockedReason)
            Assert.AreEqual("Untitled - Omskep", st.WindowTitle)
            Assert.AreEqual(String.Empty, st.CheckText)
            Assert.IsTrue(st.EditorEnabled)
            Assert.IsTrue(st.NewOpenPasteEnabled)
        End Sub

        ' ---- the export gate ----

        <TestMethod>
        Public Sub Export_needs_both_access_and_a_well_formed_document()
            Assert.IsTrue(State(Full, WellFormedDirty()).ExportEnabled)
            Assert.IsFalse(State(EditOnly, WellFormedDirty()).ExportEnabled)
            Assert.IsFalse(State(Locked, WellFormedDirty()).ExportEnabled)
            Assert.IsFalse(State(Full, MalformedDirty()).ExportEnabled)
        End Sub

        <TestMethod>
        Public Sub A_malformed_document_blocks_export_and_names_the_line()
            Dim st = State(Full, MalformedDirty())
            Assert.IsFalse(st.ExportEnabled)
            Assert.AreEqual("Fix the markup error on line 2 first.", st.ExportBlockedReason)
            Assert.StartsWith("Line 2:", st.CheckText)
        End Sub

        <TestMethod>
        Public Sub A_document_still_being_checked_blocks_export_but_keeps_the_last_result_on_screen()
            Dim m As DocumentModel = WellFormedDirty()
            m.Edited()
            Dim st = State(Full, m)
            Assert.IsFalse(st.ExportEnabled)
            Assert.AreEqual("Checking the markup...", st.ExportBlockedReason)
            Assert.AreEqual("Well-formed", st.CheckText)
        End Sub

        <TestMethod>
        Public Sub An_edited_document_with_no_result_yet_says_checking()
            Dim m As New DocumentModel()
            m.Edited()
            Assert.AreEqual("Checking...", State(Full, m).CheckText)
        End Sub

        <TestMethod>
        Public Sub When_access_is_the_blocker_the_document_gives_no_extra_reason()
            Dim st = State(EditOnly, WellFormedDirty())
            Assert.AreEqual(String.Empty, st.ExportBlockedReason)
        End Sub

        ' ---- never trap edits ----

        <TestMethod>
        Public Sub Saving_is_allowed_for_a_malformed_document()
            Assert.IsTrue(State(Full, MalformedDirty()).SaveEnabled)
        End Sub

        <TestMethod>
        Public Sub Saving_stays_possible_when_the_app_becomes_locked()
            Dim st = State(Locked, MalformedDirty())
            Assert.IsFalse(st.EditorEnabled)
            Assert.IsFalse(st.NewOpenPasteEnabled)
            Assert.IsTrue(st.SaveEnabled)
            Assert.IsTrue(st.SaveAsEnabled)
        End Sub

        <TestMethod>
        Public Sub An_import_that_has_not_been_saved_can_be_saved()
            Dim m As New DocumentModel()
            m.Imported()
            Dim st = State(Full, m)
            Assert.IsTrue(st.SaveEnabled)
            Assert.IsTrue(st.SaveAsEnabled)
            Assert.AreEqual("Untitled* - Omskep", st.WindowTitle)
        End Sub

        <TestMethod>
        Public Sub A_clean_file_has_nothing_to_save_but_can_be_saved_as()
            Dim m As New DocumentModel()
            m.OpenedFile(TestPath("Heelal.ssml"))
            Dim st = State(Full, m)
            Assert.IsFalse(st.SaveEnabled)
            Assert.IsTrue(st.SaveAsEnabled)
            Assert.AreEqual("Heelal.ssml - Omskep", st.WindowTitle)
        End Sub

        <TestMethod>
        Public Sub The_title_shows_a_star_for_unsaved_changes_and_loses_it_on_save()
            Dim m As New DocumentModel()
            m.OpenedFile(TestPath("a.ssml"))
            m.Edited()
            Assert.AreEqual("a.ssml* - Omskep", State(Full, m).WindowTitle)
            m.Saved(TestPath("a.ssml"))
            Assert.AreEqual("a.ssml - Omskep", State(Full, m).WindowTitle)
        End Sub

        ' ---- counts ----

        <TestMethod>
        Public Sub The_count_is_shown_as_an_upper_bound_with_thousands_separators()
            Assert.AreEqual("up to 80,449 characters", State(Full, New DocumentModel(), 80449).CountText)
            Assert.AreEqual("up to 0 characters", State(Full, New DocumentModel(), 0).CountText)
            Assert.AreEqual("up to 0 characters", State(Full, New DocumentModel(), -5).CountText)
        End Sub

        ' ---- bad arguments ----

        <TestMethod>
        Public Sub Null_arguments_are_rejected()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing, New DocumentModel()))
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Full, Nothing))
        End Sub

        Private Shared Sub Build(access As AccessState, doc As DocumentModel)
            Dim unused = DocumentFormState.From(access, doc, 0)
        End Sub

    End Class

End Namespace
