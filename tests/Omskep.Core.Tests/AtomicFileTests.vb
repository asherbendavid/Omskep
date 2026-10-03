Imports System.IO
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Storage

Namespace Storage

    <TestClass>
    Public Class AtomicFileTests

        <TestMethod>
        Public Sub Write_NewFile_CreatesMissingFolderAndContent()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "sub", "a.json")
                AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("hello"))
                Assert.AreEqual("hello", File.ReadAllText(p))
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_ExistingFile_ReplacesContent_AndLeavesNoTemp()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "old")
                AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"))
                Assert.AreEqual("new", File.ReadAllText(p))
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_StaleTempFileFromEarlierCrash_IsOverwritten()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "old")
                File.WriteAllText(p & AtomicFile.TempSuffix, "half-writt")
                AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"))
                Assert.AreEqual("new", File.ReadAllText(p))
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_WhenTempCannotBeCreated_ThrowsAndOriginalIsIntact()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "original")
                Directory.CreateDirectory(p & AtomicFile.TempSuffix)   ' makes the temp write fail

                Dim threw As Boolean = False
                Try
                    AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"))
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    threw = True
                End Try

                Assert.IsTrue(threw)
                Assert.AreEqual("original", File.ReadAllText(p))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_FailureForNewFile_LeavesNothingBehind()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                Directory.CreateDirectory(p)   ' target path is a folder: neither replace nor move can succeed
                Dim threw As Boolean = False
                Try
                    AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"), Nothing, New Integer() {0})   ' no waiting in tests
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    threw = True
                End Try
                Assert.IsTrue(threw)
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        ' ---- transient swap failures (antivirus / indexer holding the target briefly) ----

        <TestMethod>
        Public Sub Write_TransientSwapFailures_AreRetried_ThenSucceed()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "old")
                Dim calls As Integer = 0
                Dim swap As Action(Of String, String) =
                    Sub(src As String, dst As String)
                        calls += 1
                        If calls = 1 Then Throw New IOException("Unable to remove the file to be replaced.")
                        If calls = 2 Then Throw New UnauthorizedAccessException("Access denied.")
                        File.Move(src, dst, True)
                    End Sub

                AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"), swap, New Integer() {0, 0, 0})

                Assert.AreEqual(3, calls)
                Assert.AreEqual("new", File.ReadAllText(p))
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_SwapKeepsFailing_GivesUpAfterConfiguredRetries_OriginalIntact()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "original")
                Dim calls As Integer = 0
                Dim swap As Action(Of String, String) =
                    Sub(src As String, dst As String)
                        calls += 1
                        Throw New IOException("Unable to remove the file to be replaced.")
                    End Sub

                Assert.ThrowsExactly(Of IOException)(
                    Sub() AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"), swap, New Integer() {0, 0}))

                Assert.AreEqual(3, calls)   ' first try plus two retries
                Assert.AreEqual("original", File.ReadAllText(p))
                Assert.IsFalse(File.Exists(p & AtomicFile.TempSuffix))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_NonTransientSwapFailure_IsNotRetried()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "a.json")
                File.WriteAllText(p, "original")
                Dim calls As Integer = 0
                Dim swap As Action(Of String, String) =
                    Sub(src As String, dst As String)
                        calls += 1
                        Throw New FileNotFoundException("temp file vanished")
                    End Sub

                Assert.ThrowsExactly(Of FileNotFoundException)(
                    Sub() AtomicFile.WriteAllBytes(p, Encoding.UTF8.GetBytes("new"), swap, New Integer() {0, 0, 0}))

                Assert.AreEqual(1, calls)
                Assert.AreEqual("original", File.ReadAllText(p))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_NullRetryList_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(
                Sub() AtomicFile.WriteAllBytes("x.bin", New Byte() {1}, Nothing, Nothing))
        End Sub

        <TestMethod>
        Public Sub Write_LargePayload_RoundTripsExactly()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "big.bin")
                Dim data(500000) As Byte
                Dim rng As New Random(42)
                rng.NextBytes(data)
                AtomicFile.WriteAllBytes(p, data)
                Assert.IsTrue(File.ReadAllBytes(p).SequenceEqual(data))
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_EmptyContent_WritesEmptyFile()
            Using t As New Support.TempDir()
                Dim p = Path.Combine(t.FolderPath, "empty.bin")
                AtomicFile.WriteAllBytes(p, New Byte() {})
                Assert.AreEqual(0L, New FileInfo(p).Length)
            End Using
        End Sub

        <TestMethod>
        Public Sub Write_BadArguments_Throw()
            Assert.ThrowsExactly(Of ArgumentException)(Sub() AtomicFile.WriteAllBytes("  ", New Byte() {1}))
            Assert.ThrowsExactly(Of ArgumentException)(Sub() AtomicFile.WriteAllBytes(Nothing, New Byte() {1}))
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() AtomicFile.WriteAllBytes("x.bin", Nothing))
        End Sub

    End Class

End Namespace