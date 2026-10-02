Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Omskep.Core.Access

Namespace Access

    <TestClass>
    Public Class AccessEvaluatorTests

        Private Const Combined As String = "Azure rejected the key or region. Check both."

        Private Shared Function Snap(last As AzureOutcome, Optional rejected As Boolean = False) As ConnectionSnapshot
            Return New ConnectionSnapshot(last, rejected)
        End Function

        Private Shared Sub AssertLocked(s As AccessState, reason As AccessReason)
            Assert.AreEqual(AccessMode.Locked, s.Mode)
            Assert.AreEqual(reason, s.Reason)
            Assert.IsFalse(s.CanEdit)
            Assert.IsFalse(s.CanExport)
            Assert.IsTrue(s.CanOpenSettings)
            Assert.IsFalse(String.IsNullOrWhiteSpace(s.Message))
        End Sub

        ' ---- Rows 1-3: no usable key => locked, even if a stale cache exists ----

        <TestMethod>
        Public Sub NoKey_IsLocked_EvenWithCache()
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Absent, CacheStatus.Present, ConnectionSnapshot.Empty), AccessReason.NoKey)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Absent, CacheStatus.Missing, ConnectionSnapshot.Empty), AccessReason.NoKey)
        End Sub

        <TestMethod>
        Public Sub DamagedSettingsFile_IsLocked_WithPlainMessage()
            AssertLocked(AccessEvaluator.Evaluate(KeyState.SettingsFileDamaged, CacheStatus.Present, ConnectionSnapshot.Empty), AccessReason.SettingsFileDamaged)
        End Sub

        <TestMethod>
        Public Sub UnreadableBlob_IsLocked_WithPlainMessage()
            AssertLocked(AccessEvaluator.Evaluate(KeyState.BlobUnreadable, CacheStatus.Present, Snap(AzureOutcome.Ok)), AccessReason.KeyUnreadable)
        End Sub

        ' ---- Row 3/6: key + cache => unlocked ----

        <TestMethod>
        Public Sub KeyAndCache_NoCallYet_IsFull()
            Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Present, ConnectionSnapshot.Empty)
            Assert.AreEqual(AccessMode.Full, s.Mode)
            Assert.IsTrue(s.CanEdit)
            Assert.IsTrue(s.CanExport)
            Assert.AreEqual(String.Empty, s.Message)
        End Sub

        <TestMethod>
        Public Sub KeyAndCache_OfflineTransientFailed_StayFull()
            For Each o In {AzureOutcome.Offline, AzureOutcome.Transient, AzureOutcome.Failed, AzureOutcome.Ok}
                Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Present, Snap(o))
                Assert.AreEqual(AccessMode.Full, s.Mode, o.ToString())
            Next
        End Sub

        ' ---- Row 5: rejected later => keep editing, block export ----

        <TestMethod>
        Public Sub KeyAndCache_Rejected_EditOnly_ExportBlocked()
            Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Present, Snap(AzureOutcome.Rejected, True))
            Assert.AreEqual(AccessMode.EditOnly, s.Mode)
            Assert.AreEqual(AccessReason.KeyRejected, s.Reason)
            Assert.IsTrue(s.CanEdit)
            Assert.IsFalse(s.CanExport)
            Assert.IsTrue(s.Message.StartsWith(Combined, StringComparison.Ordinal))
        End Sub

        <TestMethod>
        Public Sub StandingRejection_SurvivesALaterOfflineResult()
            Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Present, Snap(AzureOutcome.Offline, True))
            Assert.IsFalse(s.CanExport)
            Assert.IsTrue(s.CanEdit)
        End Sub

        ' ---- Rows 4, 6, 7, 12: key but no cache ----

        <TestMethod>
        Public Sub KeyNoCache_EachOutcome_LocksWithItsOwnReason()
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, ConnectionSnapshot.Empty), AccessReason.NeedsConnectionTest)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Ok)), AccessReason.NoVoicesAvailable)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Rejected, True)), AccessReason.KeyRejected)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Offline)), AccessReason.Offline)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Transient)), AccessReason.AzureTemporarilyUnavailable)
            AssertLocked(AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Failed)), AccessReason.AzureUnexpectedResponse)
        End Sub

        <TestMethod>
        Public Sub KeyNoCache_Rejected_UsesExactCombinedMessage()
            Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Rejected, True))
            Assert.AreEqual(Combined, s.Message)
        End Sub

        <TestMethod>
        Public Sub KeyNoCache_OfflineMessage_DiffersFromRejectedMessage()
            Dim off = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Offline))
            Dim rej = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Rejected, True))
            Assert.AreNotEqual(rej.Message, off.Message)
            Assert.IsFalse(off.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
        End Sub

        <TestMethod>
        Public Sub Transient_NeverClaimsKeyIsBadOrQuotaSpent()
            Dim s = AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Missing, Snap(AzureOutcome.Transient))
            Assert.IsFalse(s.Message.Contains("rejected", StringComparison.OrdinalIgnoreCase))
            Assert.IsFalse(s.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            Assert.IsTrue(s.Message.Contains("kept", StringComparison.OrdinalIgnoreCase))
        End Sub

        ' ---- Exhaustive invariants over every combination ----

        <TestMethod>
        Public Sub AllCombinations_HoldInvariants()
            For Each k In [Enum].GetValues(Of KeyState)()
                For Each c In [Enum].GetValues(Of CacheStatus)()
                    For Each o In [Enum].GetValues(Of AzureOutcome)()
                        For Each rej In {False, True}
                            Dim s = AccessEvaluator.Evaluate(k, c, Snap(o, rej))
                            Dim label = $"{k}/{c}/{o}/{rej}"
                            Assert.IsTrue(s.CanOpenSettings, label)
                            If s.CanExport Then Assert.IsTrue(s.CanEdit, label)
                            If k <> KeyState.Present Then Assert.AreEqual(AccessMode.Locked, s.Mode, label)
                            If k = KeyState.Present AndAlso c = CacheStatus.Missing Then Assert.AreEqual(AccessMode.Locked, s.Mode, label)
                            If s.Mode = AccessMode.Full Then
                                Assert.AreEqual(String.Empty, s.Message, label)
                            Else
                                Assert.IsFalse(String.IsNullOrWhiteSpace(s.Message), label)
                            End If
                        Next
                    Next
                Next
            Next
        End Sub

        <TestMethod>
        Public Sub NullSnapshot_Throws()
            Assert.ThrowsExactly(Of ArgumentNullException)(
                Sub() AccessEvaluator.Evaluate(KeyState.Present, CacheStatus.Present, Nothing))
        End Sub

        ' ---- Row 9/10: cache validity ----

        <TestMethod>
        Public Sub CacheStatusFor_MatchingRegion_IsPresent_CaseInsensitive()
            Assert.AreEqual(CacheStatus.Present, AccessEvaluator.CacheStatusFor("southafricanorth", 556, "SouthAfricaNorth"))
        End Sub

        <TestMethod>
        Public Sub CacheStatusFor_DifferentRegion_IsMissing()
            Assert.AreEqual(CacheStatus.Missing, AccessEvaluator.CacheStatusFor("westeurope", 556, "southafricanorth"))
        End Sub

        <TestMethod>
        Public Sub CacheStatusFor_EmptyOrBlank_IsMissing()
            Assert.AreEqual(CacheStatus.Missing, AccessEvaluator.CacheStatusFor("southafricanorth", 0, "southafricanorth"))
            Assert.AreEqual(CacheStatus.Missing, AccessEvaluator.CacheStatusFor(Nothing, 5, "southafricanorth"))
            Assert.AreEqual(CacheStatus.Missing, AccessEvaluator.CacheStatusFor("southafricanorth", 5, ""))
            Assert.AreEqual(CacheStatus.Missing, AccessEvaluator.CacheStatusFor("southafricanorth", -1, "southafricanorth"))
        End Sub

        ' ---- HTTP classification: the 429 decision ----

        <TestMethod>
        Public Sub Http200_IsOk_And401_IsRejected()
            Assert.AreEqual(AzureOutcome.Ok, AccessEvaluator.OutcomeFromHttpStatus(200))
            Assert.AreEqual(AzureOutcome.Rejected, AccessEvaluator.OutcomeFromHttpStatus(401))
        End Sub

        <TestMethod>
        Public Sub Http429_408_And5xx_AreTransient_NotRejected()
            For Each code In {408, 429, 500, 502, 503, 504, 599}
                Assert.AreEqual(AzureOutcome.Transient, AccessEvaluator.OutcomeFromHttpStatus(code), code.ToString())
            Next
        End Sub

        <TestMethod>
        Public Sub OtherStatuses_AreFailed()
            For Each code In {0, 204, 301, 400, 403, 404, 600}
                Assert.AreEqual(AzureOutcome.Failed, AccessEvaluator.OutcomeFromHttpStatus(code), code.ToString())
            Next
        End Sub

    End Class

    <TestClass>
    Public Class ConnectionMonitorTests

        <TestMethod>
        Public Sub NewMonitor_IsEmpty()
            Dim m As New ConnectionMonitor()
            Assert.AreEqual(AzureOutcome.None, m.Snapshot.LastOutcome)
            Assert.IsFalse(m.Snapshot.KeyRejected)
        End Sub

        <TestMethod>
        Public Sub Rejected_IsSticky_AcrossOfflineTransientFailed()
            Dim m As New ConnectionMonitor()
            m.Report(AzureOutcome.Rejected)
            For Each o In {AzureOutcome.Offline, AzureOutcome.Transient, AzureOutcome.Failed}
                m.Report(o)
                Assert.IsTrue(m.Snapshot.KeyRejected, o.ToString())
                Assert.AreEqual(o, m.Snapshot.LastOutcome)
            Next
        End Sub

        <TestMethod>
        Public Sub Ok_ClearsRejection()
            Dim m As New ConnectionMonitor()
            m.Report(AzureOutcome.Rejected)
            m.Report(AzureOutcome.Ok)
            Assert.IsFalse(m.Snapshot.KeyRejected)
            Assert.AreEqual(AzureOutcome.Ok, m.Snapshot.LastOutcome)
        End Sub

        <TestMethod>
        Public Sub Reset_ClearsEverything()
            Dim m As New ConnectionMonitor()
            m.Report(AzureOutcome.Rejected)
            m.Reset()
            Assert.AreEqual(AzureOutcome.None, m.Snapshot.LastOutcome)
            Assert.IsFalse(m.Snapshot.KeyRejected)
        End Sub

        <TestMethod>
        Public Sub ReportingNone_IsIgnored()
            Dim m As New ConnectionMonitor()
            m.Report(AzureOutcome.Rejected)
            m.Report(AzureOutcome.None)
            Assert.AreEqual(AzureOutcome.Rejected, m.Snapshot.LastOutcome)
            Assert.IsTrue(m.Snapshot.KeyRejected)
        End Sub

        <TestMethod>
        Public Sub Transient_WithoutPriorRejection_NeverSetsRejected()
            Dim m As New ConnectionMonitor()
            m.Report(AzureOutcome.Transient)
            Assert.IsFalse(m.Snapshot.KeyRejected)
        End Sub

    End Class

End Namespace