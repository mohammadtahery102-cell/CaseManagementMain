# BACKUP_COVERAGE_TIER3_REPORT.md

**Phases 4 & 5 — implementation and validation.**
Companion to `BACKUP_COVERAGE_TIER3_AUDIT.md`.

Date: 30 August 2026
Approved scope: **Full Category A** (7 new tables + 2 defect fixes) · center remapping **added** · merge policy **fresh-install only**
Build: ✅ succeeds, no new warnings. Tests: ✅ **190 passed, 0 failed**.

---

## 1. Tables added — 7

| Group | Tables |
|---|---|
| **Workflow runtime** (2) | EntWorkflowInstance · EntWorkflowHistory |
| **Approval runtime** (2) | EntApprovalRequest · EntApprovalAction |
| **Tasks** (1) | EntTask |
| **HR module completion** (2) | AdmJobApplication · AdmDriverContract |

## 2. Defects fixed — 2

Both were in tables **already backed up**, silently restoring *wrong* references rather than missing data.

| ID | Table | Was | Now |
|---|---|---|---|
| **D-1** 🔴 | `TblCaseTransferHistory` (Tier 2) | `CasID` copied raw → transfer records attached to the wrong case or to none | remapped through `casIdMap`; `UserID` and both center columns remapped too |
| **D-2** 🟠 | `EntRecordVersion` (Tier 1) | `(EntityName, EntityID)` copied raw → record snapshots attached to wrong records | polymorphic remap by `EntityName`; `ChangedByUserID` and `CenterID` remapped |

### Why these mattered

`InsertCaseRow` deliberately omits `CasID`, so cases are renumbered on restore. The live database shows how severe that is:

```
TblCase    COUNT = 1661   MIN(CasID) = 2   MAX(CasID) = 5066
TblFamily  COUNT = 3802                    MAX(FamID) = 12623
```

IDs are heavily non-contiguous after years of deletions. A backup's `CasID = 5066` simply does not exist after restore — the transfer record, a **legal record of custody**, lands on a different case or vanishes.

---

## 3. Coverage increase

| | Before Tier 3 | After Tier 3 |
|---|---|---|
| `BackupHelper` export | 40 | **47** |
| `AccountingBackupHelper` export | 11 | 11 |
| **Total covered** | **51 / 73 (70%)** | **58 / 73 (79%)** |
| Remaining excluded | 22 | 15 |

Cumulative across all three tiers: **28 → 58 of 73 tables**.

---

## 4. How the remapping works

### 4.1 Which identifiers change, and which do not

| Identifier | Preserved? | Why |
|---|---|---|
| `TblCase.CasID` | ❌ reassigned | `InsertCaseRow` skips the column |
| `TblFamily.FamID` | ❌ reassigned | `MergeChildTable` assigns new ids |
| `TblUsers.UserID` | ❌ reassigned | `MergeUsers` skips the column; seeded `admin` already holds id 1 |
| `TblCenter.CenterID` | ⚠️ usually | `MergeCenters` matches on `CenterCode` |
| Config/runtime PKs (`WorkflowID`, `ChainID`, `InstanceID`, `RequestID`, `EmployeeID`, `TxnID`…) | ✅ preserved | full replace; `InsertRow` writes **all** columns including the PK |

**The consequence that kept this change small:** because parents keep their ids under full replace, intra-group chains need *no* remapping — `EntWorkflowInstance`→`EntWorkflowHistory`, `EntApprovalRequest`→`EntApprovalAction`, `AdmEmployee`→`AdmLeave`/`AdmMission` are all automatically correct. Only references crossing into `TblCase`, `TblUsers` and `TblCenter` needed translation.

### 4.2 New infrastructure

`RestoreMaps` holds four old→new dictionaries. `casIdMap`/`famIdMap` come from the existing merge logic; user and center maps are built by `BuildNaturalKeyMap` through the same natural keys the merge code already trusts for de-duplication (`Username`, `CenterCode`).

`RestoreRemappedTable` performs a full replace while translating declared reference columns. Failure handling is deliberately different per column type, because the right answer differs:

| Column type | If it cannot be resolved | Rationale |
|---|---|---|
| **Case** (`CasID`, polymorphic `EntityID`) | **skip the row**, counted | A dangling case link is corruption. Losing a row is recoverable; a wrong link is not detectable later. |
| **User** (`AssignedToUserID`, …) | set **NULL** | The task/approval is still worth keeping; only the attribution is unknown. Columns are nullable. |
| **Center** | keep original value | Row surfaces under an unknown center — visible, not a silent error. |

`TryMapEntityId` switches on `EntityName` using the entity map from `VersionService.cs:31–39`; `TblApplicant` needs no translation (full replace preserves its PK), and unknown entity types cause the row to be skipped rather than guessed.

Classic restore passes `maps: null` — that path preserves original ids by construction, so no translation is correct there.

---

## 5. Validation results

### 5.1 The test was initially incapable of detecting the bug

Worth recording explicitly. The drill seeds cases into an empty database, producing contiguous ids `1…N`. On a fresh-install restore they are renumbered `1…N` again — **identical** — so remapping was a no-op and every new assertion passed *whether or not the fix worked*.

Fixed by reproducing production conditions: the seed now deletes several cases (children removed explicitly, so the result does not depend on `PRAGMA foreign_keys` state), creating a genuine id gap. A precondition assertion guards the whole block:

```csharp
Assert.AreNotEqual(summary.AnchorCasIdBefore, anchorIdAfter,
    "پیش‌شرطِ آزمون: شناسهٔ پرونده باید بعدِ بازیابی عوض شده باشد…");
```

If the gap ever stops materialising, this fails loudly instead of letting the remapping assertions pass vacuously.

### 5.2 Negative control — proof the test detects the defect

Remapping was temporarily disabled (`maps = null`) and the drill re-run:

```
Failed DisasterRecovery_FullRestore_AllCoveredCategories_Verified
Assert.AreEqual failed. Expected:<1>. Actual:<0>.
Tier 3 (D-1): سابقهٔ انتقال باید بعدِ بازیابی به همان پرونده وصل بماند، نه به شناسهٔ خام.
```

The transfer record pointed at a case that no longer exists — exactly the D-1 defect. The change was then reverted and the build re-verified. **The suite provably catches this class of regression.**

### 5.3 What the drill now asserts

All assertions resolve through **`Code`/`Username`**, never raw ids — the only way to prove a reference still points at the same *thing* after renumbering.

| # | Assertion | Confirms |
|---|---|---|
| 0 | anchor case id **changed** across restore | the test is meaningful |
| 1 | transfer history joins to the anchor case by `Code`; zero orphans | **D-1 fixed** |
| 2 | record version joins to the anchor case | **D-2 fixed** |
| 3 | workflow instance joins to the anchor case; history joins to its instance | **workflow survives** |
| 4 | approval request joins to correct case **and** correct user; action joins to its request and user | **approvals survive** |
| 5 | task joins to correct case **and** assigned user; zero orphaned tasks | **tasks survive** |
| 6 | `AdmEmployee`, `AdmJobApplication`, `AdmDriverContract` rows present | **HR records and contracts survive** |

Plus the Tier 2 assertions retained: permission matrix survives, revoked permissions stay revoked, user overrides remap correctly, role assignments survive.

### 5.4 Test totals

| Suite | Result |
|---|---|
| Backup / restore / encryption / auto-backup / accounting-backup | **39 / 39** |
| Permission + history: `AdminPermissionTests`, `AccountingPermissionTests`, `AiPermissionTests`, `LockServiceTests`, `RecordHistoryWiringTests`, `ApplicantDocumentTests` | **45 / 45** |
| `DevCenterSafetyTests`, `FrmSettingsDeleteCasesTests`, `IntegrityTests`, `RepairTests`, `SyncEngineFoundationTests`, `OfflineSyncFoundationTests` | **106 / 106** |
| **Total** | **190 passed, 0 failed** |

One fixture bug was found and fixed along the way: the initial gap size of 5 deleted *every* case in the tests that seed only 5, breaking 10 tests. Gap size is now proportional (`Math.Max(1, Math.Min(5, caseCount / 3))`).

The test fixture also now calls `AdminInitializer.EnsureAdminObjects()`, matching `Program.cs:54` — without it the `Adm*` tables never existed in tests, so Tier 2's HR coverage had never actually been exercised.

---

## 6. Remaining exclusions — 15 tables

### Category B — deferred (6)

| Tables | Rows | Note |
|---|---|---|
| EntSecurityEvent, EntErrorLog, EntRuleLog | 0 / 1 / 0 | Append-only diagnostic logs. Now cheap to add — they need only `Username`/`Center` remapping, and that machinery exists. |
| AiConversation, AiMessage, AiIntentLog | n/a | Assistant chat history; module is Phase 1 and its schema may still change. |

### Category C — excluded by design (9)

| Table(s) | Why inclusion would be harmful |
|---|---|
| SyncOutbox, SyncState, SyncBaseline, SyncConflict, SyncFile, SyncFileDownload | Device-local transport state; restoring another machine's queue would replay foreign operations against the server |
| EntRecordLock | Live edit locks — would lock records for users who are not editing |
| AiCaseSearchIndex | FTS5 index maintained by triggers `Trg_TblCase_AI_Insert/Update/Delete`; rebuilds itself from `TblCase` |
| TblAuditLogs | Verified dead — no `INSERT` anywhere in the codebase, only read by `DevCenterService.cs:1049` |

---

## 7. Risk assessment

### Closed

| Risk | Severity | Status |
|---|---|---|
| Case transfer history (legal custody record) restored against wrong cases | 🔴 Critical — **silent corruption** | ✅ closed, negative-control proven |
| Record versions attached to wrong records | 🟠 High | ✅ closed |
| Workflow position lost — cases silently revert to "no workflow" | 🔴 Critical | ✅ closed |
| Pending financial approvals and their audit trail lost | 🔴 Critical | ✅ closed |
| Assigned task queue lost | 🔴 Critical | ✅ closed |
| Recruitment and driver-contract records unprotected | 🟠 High | ✅ closed — HR module now fully covered |
| Center references drifting if a custom center exists | 🟡 Medium | ✅ closed via `CenterCode` map, retroactively hardening Tier 2's `TblReminder`/`Adm*` restores |

### Residual

| Risk | Level | Note |
|---|---|---|
| Category B logs and AI history unprotected | 🟡 Low | Currently ~0 rows; the remapping machinery now exists, so adding them is small |
| `EntityID` for `TblDocs`/`TblAssistance` targets cannot be remapped | 🟢 Very low | No map is captured for those child tables, so such rows are **skipped, not mis-pointed**. The shipped code only ever writes `EntityName = "TblCase"` (`EnterpriseInitializer.EntityCase`), so no real data is affected today. If those entity types ever get used, `MergeChildTable` would need to expose its maps. |
| User attribution set to NULL when a user is missing from the backup | 🟢 Very low | Deliberate: keeps the record, drops only the unverifiable attribution |
| New `Adm*`/`Ent*` tables added later won't be backed up automatically | 🟡 Low | The export list is explicit by design (so exclusions stay deliberate); adding a module means adding its tables here |

### Recommendation

Category B is now a genuinely small change — the remapping helpers it needs already exist and are tested. The three `Ent*` logs in particular are the natural next step, since security-event history is exactly what an investigator wants after an incident, and it is the one remaining gap where losing data has a compliance dimension rather than merely an operational one.
