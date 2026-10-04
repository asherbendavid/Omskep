Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access
Imports Omskep.Core.Session

Namespace Session

    <TestClass>
    Public Class SettingsFormStateTests

        Private Shared Function S(Optional keySaved As Boolean = False, Optional replacing As Boolean = False,
                                  Optional own As Boolean = False, Optional other As Boolean = False,
                                  Optional typed As Integer = 0, Optional regionValid As Boolean = True) As SettingsFormState
            Return SettingsFormState.From(keySaved, replacing, own, other, typed, regionValid)
        End Function

        <TestMethod>
        Public Sub NoKeySaved_ShowsKeyBox_AndNeedsATypedKeyToTest()
            Dim empty = S()
            Assert.IsTrue(empty.ShowKeyBox)
            Assert.IsFalse(empty.ShowSavedPanel)
            Assert.IsFalse(empty.TestEnabled)
            Assert.IsFalse(empty.RefreshEnabled)
            Assert.IsFalse(empty.ReplaceEnabled)
            Assert.IsFalse(empty.ClearEnabled)

            Assert.IsTrue(S(typed:=5).TestEnabled)
        End Sub

        <TestMethod>
        Public Sub InvalidRegion_DisablesTest_EvenWithAKey()
            Assert.IsFalse(S(typed:=5, regionValid:=False).TestEnabled)
            Assert.IsFalse(S(keySaved:=True, regionValid:=False).TestEnabled)
        End Sub

        <TestMethod>
        Public Sub KeySaved_ShowsSavedPanel_WithReplaceClearRefreshAndTest()
            Dim st = S(keySaved:=True)
            Assert.IsFalse(st.ShowKeyBox)
            Assert.IsTrue(st.ShowSavedPanel)
            Assert.IsTrue(st.ReplaceEnabled)
            Assert.IsTrue(st.ClearEnabled)
            Assert.IsTrue(st.RefreshEnabled)
            Assert.IsTrue(st.TestEnabled)
        End Sub

        <TestMethod>
        Public Sub Replacing_ShowsKeyBoxAgain_AndHidesReplaceAndClear()
            Dim st = S(keySaved:=True, replacing:=True)
            Assert.IsTrue(st.ShowKeyBox)
            Assert.IsFalse(st.ShowSavedPanel)
            Assert.IsFalse(st.ReplaceEnabled)
            Assert.IsFalse(st.ClearEnabled)
            Assert.IsTrue(st.RefreshEnabled)
        End Sub

        <TestMethod>
        Public Sub OwnRequestRunning_TestBecomesCancel_AndEverythingElseIsLocked()
            Dim st = S(keySaved:=True, own:=True, typed:=3)
            Assert.IsTrue(st.TestIsCancel)
            Assert.IsTrue(st.TestEnabled)
            Assert.IsFalse(st.InputsEnabled)
            Assert.IsFalse(st.RefreshEnabled)
            Assert.IsFalse(st.ReplaceEnabled)
            Assert.IsFalse(st.ClearEnabled)
            Assert.IsTrue(st.ShowProgress)
        End Sub

        <TestMethod>
        Public Sub OwnRequestRunning_StaysCancellable_EvenIfInputsBecomeInvalid()
            Assert.IsTrue(S(own:=True, regionValid:=False).TestEnabled)
        End Sub

        <TestMethod>
        Public Sub OtherRequestRunning_DisablesEverything_AndOffersNoCancel()
            Dim st = S(keySaved:=True, other:=True, typed:=3)
            Assert.IsFalse(st.TestIsCancel)
            Assert.IsFalse(st.TestEnabled)
            Assert.IsFalse(st.InputsEnabled)
            Assert.IsFalse(st.RefreshEnabled)
            Assert.IsTrue(st.ShowProgress)
        End Sub

        <TestMethod>
        Public Sub NothingRunning_NoProgress_InputsEnabled()
            Dim st = S(keySaved:=True)
            Assert.IsFalse(st.ShowProgress)
            Assert.IsTrue(st.InputsEnabled)
            Assert.IsFalse(st.TestIsCancel)
        End Sub

        <TestMethod>
        Public Sub KeyBox_AndSavedPanel_AreNeverBothShown_Nor_BothHidden()
            For Each saved In {False, True}
                For Each replacing In {False, True}
                    Dim st = S(keySaved:=saved, replacing:=replacing)
                    Assert.AreNotEqual(st.ShowKeyBox, st.ShowSavedPanel)
                Next
            Next
        End Sub

    End Class

    <TestClass>
    Public Class MainFormStateTests

        Private Shared Function Access(mode As AccessMode, reason As AccessReason, message As String) As AccessState
            Return New AccessState(mode, reason, message)
        End Function

        <TestMethod>
        Public Sub Full_EnablesEverything_NoBanner_ReadyStatus()
            Dim st = MainFormState.From(Access(AccessMode.Full, AccessReason.Ready, String.Empty), False, False, 556)
            Assert.IsTrue(st.EditEnabled)
            Assert.IsTrue(st.ExportEnabled)
            Assert.IsFalse(st.ShowBanner)
            Assert.IsFalse(st.ShowRetry)
            Assert.AreEqual("Ready. 556 voices available.", st.StatusText)
        End Sub

        <TestMethod>
        Public Sub EditOnly_BlocksExportOnly_ShowsBannerWithMessage()
            Dim st = MainFormState.From(Access(AccessMode.EditOnly, AccessReason.KeyRejected, "blocked"), False, False, 10)
            Assert.IsTrue(st.EditEnabled)
            Assert.IsFalse(st.ExportEnabled)
            Assert.IsTrue(st.ShowBanner)
            Assert.AreEqual("blocked", st.BannerText)
            Assert.IsTrue(st.StatusText.Contains("export is blocked", StringComparison.Ordinal))
        End Sub

        <TestMethod>
        Public Sub Locked_DisablesEditAndExport_ShowsBanner()
            Dim st = MainFormState.From(Access(AccessMode.Locked, AccessReason.NoKey, "enter key"), False, False, 0)
            Assert.IsFalse(st.EditEnabled)
            Assert.IsFalse(st.ExportEnabled)
            Assert.IsTrue(st.ShowBanner)
            Assert.AreEqual("enter key", st.BannerText)
        End Sub

        <TestMethod>
        Public Sub Retry_ShownOnlyWhenAllowed_AndDisabledWhileBusy()
            Dim a = Access(AccessMode.Locked, AccessReason.Offline, "offline")
            Assert.IsTrue(MainFormState.From(a, False, True, 0).ShowRetry)
            Assert.IsTrue(MainFormState.From(a, False, True, 0).RetryEnabled)
            Assert.IsFalse(MainFormState.From(a, True, True, 0).RetryEnabled)
            Assert.IsFalse(MainFormState.From(a, False, False, 0).ShowRetry)
        End Sub

        <TestMethod>
        Public Sub Busy_ShowsContactingAzure()
            Dim st = MainFormState.From(Access(AccessMode.Locked, AccessReason.Offline, "x"), True, True, 0)
            Assert.AreEqual("Contacting Azure...", st.StatusText)
        End Sub

        <TestMethod>
        Public Sub SingularVoice_IsNotPluralised()
            Dim st = MainFormState.From(Access(AccessMode.Full, AccessReason.Ready, String.Empty), False, False, 1)
            Assert.AreEqual("Ready. 1 voice available.", st.StatusText)
        End Sub

        <TestMethod>
        Public Sub NullAccess_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(Sub() Build(Nothing))
        End Sub

        Private Shared Sub Build(a As AccessState)
            Dim unused = MainFormState.From(a, False, False, 0)
        End Sub

    End Class

End Namespace
