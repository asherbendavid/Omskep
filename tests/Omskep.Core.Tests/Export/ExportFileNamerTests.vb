Option Strict On
Option Explicit On
Option Infer On

Imports System.IO
Imports System.Linq
Imports System.Threading
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Documents
Imports Omskep.Core.Export
Imports Omskep.Core.Secrets
Imports Omskep.Core.Session
Imports Omskep.Core.Settings
Imports Omskep.Core.Speech
Imports Omskep.Core.Voices

Namespace Export

    <TestClass>
    Public Class ExportFileNamerTests

        <TestMethod>
        Public Sub An_empty_folder_gets_the_plain_alpha_name()
            Using dir As New Support.TempDir()
                Assert.AreEqual("Study Alpha.mp3", ExportFileNamer.DefaultFileName("Study", dir.FolderPath))
            End Using
        End Sub

        <TestMethod>
        Public Sub An_existing_alpha_file_bumps_to_the_next_number()
            Using dir As New Support.TempDir()
                File.WriteAllText(Path.Combine(dir.FolderPath, "Study Alpha.mp3"), "")
                Assert.AreEqual("Study Alpha (2).mp3", ExportFileNamer.DefaultFileName("Study", dir.FolderPath))
            End Using
        End Sub

        <TestMethod>
        Public Sub A_gap_in_the_numbering_is_filled_rather_than_skipped()
            Using dir As New Support.TempDir()
                File.WriteAllText(Path.Combine(dir.FolderPath, "Study Alpha.mp3"), "")
                File.WriteAllText(Path.Combine(dir.FolderPath, "Study Alpha (3).mp3"), "")
                ' (2) is free even though (3) is taken -- the function offers the first free slot, not max+1.
                Assert.AreEqual("Study Alpha (2).mp3", ExportFileNamer.DefaultFileName("Study", dir.FolderPath))
            End Using
        End Sub

        <TestMethod>
        Public Sub An_unsaved_document_gets_the_untitled_stem()
            Using dir As New Support.TempDir()
                Assert.AreEqual("Untitled Alpha.mp3", ExportFileNamer.DefaultFileName(Nothing, dir.FolderPath))
                Assert.AreEqual("Untitled Alpha.mp3", ExportFileNamer.DefaultFileName("   ", dir.FolderPath))
            End Using
        End Sub

        <TestMethod>
        Public Sub Illegal_filename_characters_in_the_document_name_are_replaced()
            Using dir As New Support.TempDir()
                Assert.AreEqual("Die Heelal_ Toeval_ Alpha.mp3", ExportFileNamer.DefaultFileName("Die Heelal: Toeval?", dir.FolderPath))
            End Using
        End Sub

    End Class
End Namespace
