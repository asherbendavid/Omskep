Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Settings
Imports Omskep.Core.Tests.Documents

Namespace Settings

    <TestClass>
    Public Class WordWrapSettingTests
        Inherits ScratchFolderTests

        <TestMethod>
        Public Sub Word_wrap_is_on_when_nothing_is_saved()
            Dim store As New SettingsStore(NewFolder())
            Assert.IsTrue(store.Load().Value.WordWrap)
        End Sub

        <TestMethod>
        Public Sub Word_wrap_off_survives_a_save_and_a_reload()
            Dim folder As String = NewFolder()
            Dim store As New SettingsStore(folder)
            store.Update(Sub(s) s.WordWrap = False)
            Assert.IsFalse(New SettingsStore(folder).Load().Value.WordWrap)
        End Sub

        <TestMethod>
        Public Sub An_older_settings_file_without_the_field_defaults_to_on_and_keeps_its_other_values()
            Dim folder As String = NewFolder()
            File.WriteAllText(Path.Combine(folder, SettingsStore.FileName),
                              "{""schemaVersion"":1,""azure"":{""region"":""westeurope""},""defaultVoices"":{""af-ZA"":""af-ZA-AdriNeural""}}",
                              New UTF8Encoding(False))
            Dim loaded As AppSettings = New SettingsStore(folder).Load().Value
            Assert.IsTrue(loaded.WordWrap)
            Assert.AreEqual("westeurope", loaded.Azure.Region)
            Assert.AreEqual("af-ZA-AdriNeural", loaded.DefaultVoices("af-ZA"))
        End Sub

    End Class

End Namespace
