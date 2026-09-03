# BACKUP_COVERAGE_TIER4_IMPLEMENTATION.md

**Phases A and B implemented. Phase C (AI history) deliberately NOT started.**

Date: 30 August 2026
Follows: `BACKUP_COVERAGE_TIER4_AUDIT.md`
Build: ✅ succeeds, no new warnings. Tests: ✅ **232 passed, 0 failed**.

Constraints honoured: `SyncOutbox` untouched · no AI-history work · device identity handling untouched (only *excluded* from backup, never read or modified) · no new restore engine · no schema changes · no migrations.

---

## 1. Modified files

Two files.

| File | Change |
|---|---|
| `Helpers/BackupHelper.cs` | `SyncStateAllowedKeys` allow-list constant + `SyncStateAllowedKeysSql` + `IsSyncStateKeyAllowed` |
| | Key-filtered `SyncState` export (Phase A) |
| | New `MergeSyncState` restore method — runs in **both** restore modes |
| | 3 `LoadTableIfExists` calls for the Enterprise logs (Phase B) |
| | 3 `RestoreRemappedTable` calls in `RestoreConfigurationTables` (Phase B) |
| `CaseManagement.Tests/DisasterRecoveryDrillTests.cs` | Seed extended: 3 config keys + 2 sensitive keys, security event, error log, rule log |
| | 8 new assertions in the main drill (3 log tables, 3 config keys, **2 negative security assertions**) |
| | New test `Tier4_SensitiveSyncStateKeys_NeverEnterBackupFile` |
| | New test `Tier4_MergeRestore_DoesNotOverwriteLocalSyncConfiguration` |

**No new restore engine was created.** Phase B reuses `RestoreRemappedTable` with the same call shape already used for `EntTask` in Tier 3. Phase A reuses the `MergeAppSettings`/`MergeLookup` `INSERT OR IGNORE` pattern established in Tier 1.

---

## 2. Coverage before / after

| | Before Tier 4 | After Tier 4 |
|---|---|---|
| `BackupHelper` export | 47 | **51** |
| `AccountingBackupHelper` export | 11 | 11 |
| **Total covered** | **58 / 73 (79%)** | **62 / 73 (85%)** |
| Remaining uncovered | 15 | **11** |

Cumulative across all four tiers: **28 → 62 of 73**.

### Tables added — 4

| Table | Phase | Restore strategy | Remapping applied |
|---|---|---|---|
| `SyncState` *(3 allowed keys only)* | A | `INSERT OR IGNORE` on `StateKey` — **both** modes | none needed (key/value) |
| `EntSecurityEvent` | B | full replace, fresh install only | `UserID`, `CenterID`, polymorphic `EntityID` |
| `EntErrorLog` | B | full replace, fresh install only | `UserID`, `CenterID` |
| `EntRuleLog` | B | full replace, fresh install only | `CenterID`, polymorphic `EntityID` |

---

## 3. Phase A — exactly which keys are preserved

### ✅ Preserved (the complete allow-list)

| Key | Purpose | Why it must survive |
|---|---|---|
| `ServerUrl` | Sync server address | Without it the office **cannot reconnect** after disaster recovery until an admin re-enters it by hand |
| `AutoSyncEnabled` | Automatic sync on/off | Sync silently stops running |
| `AutoSyncIntervalMinutes` | Sync schedule | Reverts to default cadence |

### ❌ Never backed up, never restored

| Key | Category | Reason |
|---|---|---|
| `DeviceGuid` | Device identity | The identity the server uses to tell machines apart (`HttpFileSyncTransport.cs:95, 213`). Restoring it onto another machine creates two devices claiming one identity. |
| `RefreshToken` | **Auth credential** | A persisted authentication token (`HttpSyncTransport.cs:209`). Restoring it transfers a credential to a different machine. |
| `LastSyncAt`, `AutoSyncLastAttemptAt`, `AutoSyncLastResult`, `AutoSyncLastFailureAt`, `AutoSyncLastFailure` | Device telemetry | Execution state of *this* machine; meaningless on a fresh install. |

### Design decisions worth recording

**1. Allow-list, never deny-list.** If another developer adds a new `SyncState` key later, it is excluded **by default** rather than silently exported. A deny-list would have the opposite, dangerous default.

**2. Filtering happens at export, in the SQL itself.**

```sql
SELECT * FROM SyncState WHERE StateKey IN ('ServerUrl','AutoSyncEnabled','AutoSyncIntervalMinutes')
```

The credential never enters the backup file at all — not even in encrypted form. Backups get copied, moved and archived for years; the strongest protection is for the secret to never be in the artifact.

**3. Filtered again on restore (defence in depth).** `MergeSyncState` re-checks every key against the allow-list, because a backup taken *before* this change could still contain `DeviceGuid` or `RefreshToken`. Those must never land on a different machine, regardless of which version produced the file.

**4. Both restore modes supported, safely.** `INSERT OR IGNORE` on `StateKey` gives the correct behaviour in each:

| Mode | `SyncState` keys | Result |
|---|---|---|
| **Full restore** (fresh install) | absent | restored → office can reconnect immediately |
| **Merge restore** (live database) | already present | untouched → this office's server address is never overwritten by another office's |

---

## 4. Tests executed

### New tests — 2

| Test | Verifies |
|---|---|
| `Tier4_SensitiveSyncStateKeys_NeverEnterBackupFile` | Inspects the backup's own contents via `VerifyEncryptedBackup`: the 3 config keys are present, `DeviceGuid`/`RefreshToken` are **absent**, and the token value appears in no row |
| `Tier4_MergeRestore_DoesNotOverwriteLocalSyncConfiguration` | Merge restore leaves the local `ServerUrl` intact and creates no duplicate key |

### Extended — the disaster-recovery drill

| Requirement | Assertion |
|---|---|
| Security events survive | joins `EntSecurityEvent` → correct case (by `Code`) **and** correct user (by `Username`) |
| Error logs survive | joins `EntErrorLog` → correct user |
| Rule logs survive | joins `EntRuleLog` → correct case |
| SyncState configuration survives | all three keys restored with exact values |
| **DeviceGuid NOT restored** | `COUNT(*) = 0` |
| **RefreshToken NOT restored** | `COUNT(*) = 0` |

All reference assertions resolve through `Code`/`Username`, never raw ids — the drill deliberately creates an id gap, so a raw-copy regression cannot pass.

### Negative control — proof the security assertions actually work

The allow-list was temporarily widened to include `DeviceGuid` and `RefreshToken`, and the suite re-run:

```
Failed DisasterRecovery_FullRestore_AllCoveredCategories_Verified
  Assert.AreEqual failed. Expected:<0>. Actual:<1>.
  Tier 4 (امنیت): DeviceGuid هرگز نباید بازیابی شود.

Failed Tier4_SensitiveSyncStateKeys_NeverEnterBackupFile
  CollectionAssert.DoesNotContain failed.
  امنیت: هویتِ دستگاه هرگز نباید داخلِ فایلِ بکاپ نوشته شود.
```

Both failed exactly as intended — the assertions are not vacuous. The change was reverted, the allow-list verified back to precisely three keys, and the build re-run.

### Full results

| Suite | Result |
|---|---|
| Backup / restore / encryption / auto-backup / accounting-backup / DR drill | **41 / 41** |
| Permission + history: `AdminPermissionTests`, `AccountingPermissionTests`, `AiPermissionTests`, `LockServiceTests`, `RecordHistoryWiringTests`, `ApplicantDocumentTests` | **45 / 45** |
| Sync + integrity: `DevCenterSafetyTests`, `FrmSettingsDeleteCasesTests`, `IntegrityTests`, `RepairTests`, `SyncEngineFoundationTests`, `OfflineSyncFoundationTests`, `SyncFileTests`, `SyncConflictResolverTests` | **146 / 146** |
| **Total** | **232 passed, 0 failed** |

The sync suites were included deliberately: Phase A touches a table the sync subsystem reads on every operation, so regression there needed explicit coverage.

---

## 5. Risks

### Introduced by this change

| Risk | Level | Mitigation |
|---|---|---|
| A future `SyncState` key is sensitive and gets exported | 🟢 Very low | Allow-list means new keys are excluded by default; adding one is a deliberate act |
| A future key is *needed* but forgotten from the allow-list | 🟡 Low | Symptom is visible and benign — a setting reverts to default after DR. Documented in §3 |
| Log tables grow the backup file | 🟢 Very low | Currently ~1 row total; `EntSecurityEvent` grows with security events, not with case volume |
| Merge restore silently skips the three logs | 🟢 Accepted | Consistent with Tiers 2–3; prevents duplicate rows in append-only tables with no natural key |

### Not addressed (unchanged from the audit)

| Risk | Level | Note |
|---|---|---|
| `SyncOutbox` loss → silent divergence with head office | 🟡 Medium | **Explicitly out of scope per instruction.** Upload reads only from the outbox and no rebuild path exists, so post-DR the office and head office can diverge until a full re-sync. **Best closed procedurally** — see §7 |
| AI history unprotected | 🔵 Low | Phase C deliberately not started |

---

## 6. Remaining uncovered tables — 11

### Deferred — 3 (Phase C, not started per instruction)

`AiConversation` · `AiMessage` · `AiIntentLog` — assistant chat history. The module is Phase 1 and `AI_ASSISTANT_MASTER_PLAN.md` describes further tables (`AiRiskFlag`, `AiInsightCache`, `AiFeedback`) not yet built, so the export list would likely need revisiting.

### Excluded by design — 8

| Table | Reason |
|---|---|
| `SyncOutbox` | Device-local queue of unsent operations; replaying another install's queue is unsafe. **Untouched per instruction.** |
| `SyncBaseline` | Per-device cursor; absence already handled as "first sync" (`SyncConflictAnalyzer.cs:230–237`) |
| `SyncConflict` | Transient; re-detected on next sync |
| `SyncFile`, `SyncFileDownload` | Transfer state; re-derived by `MediaScanner` |
| `EntRecordLock` | Would lock records for users who are not editing |
| `AiCaseSearchIndex` | FTS5 index maintained by triggers `Trg_TblCase_AI_Insert/Update/Delete`; rebuilds itself |
| `TblAuditLogs` | Verified dead — no `INSERT` anywhere; read only by `DevCenterService.cs:1049` |

**Every table where loss causes business or compliance harm is now covered.** The 8 remaining exclusions are cases where backing the table up would be harmful or pointless, not gaps.

---

## 7. Recommended follow-up (not code)

The `SyncOutbox` gap is real but is best closed in the **recovery runbook**, not in the backup engine: after any disaster recovery, an administrator should run a **full re-sync** to reconcile the office with head office, because pending unsent operations are intentionally not restored.

No such runbook step exists today, so this risk is currently neither mitigated nor documented. It is out of scope here — flagging it so it is not lost.
