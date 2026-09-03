# BACKUP_COVERAGE_TIER3_AUDIT.md

**Phases 1–3 — audit, classification and remapping analysis. No code modified.**

Date: 30 August 2026
Follows: `BACKUP_RESTORE_AUDIT.md` (Tier 1) · `BACKUP_COVERAGE_TIER2_AUDIT.md` / `_REPORT.md` (Tier 2)
Current coverage: **51 / 73 tables** (40 `BackupHelper` + 11 `AccountingBackupHelper`)
Evidence: full schema trace of all 22 remaining tables + live census of `bin/Debug/CaseDB.sqlite`

---

## 0. Two defects found in already-shipped work

The Tier 3 investigation surfaced problems in tables that are **already backed up**. These matter more than any of the 22 uncovered tables, because they silently produce *wrong* data rather than missing data.

### D-1 🔴 `TblCaseTransferHistory` restores case references that point at the wrong case

Added to backup in Tier 2 and restored with `RestoreWholeTable` — verbatim, including its raw `CasID` column. But `InsertCaseRow` **explicitly skips `CasID`** so cases receive fresh `AUTOINCREMENT` ids on restore (`BackupHelper.cs`, `InsertCaseRow`: `if (col == "CasID") continue;`).

This is not theoretical. The live database:

```
TblCase    COUNT = 1661    MIN(CasID) = 2    MAX(CasID) = 5066
TblFamily  COUNT = 3802                      MAX(FamID) = 12623
```

Ids are **heavily non-contiguous**. On a fresh-install restore the 1,661 cases are renumbered 1…1661, while the backup's transfer-history rows still reference ids in the 2…5066 range. Every row therefore lands on a different case or on no case at all.

`TblCaseTransferHistory` is the legal record of custody transfer between offices. Silently re-pointing it is worse than losing it.

### D-2 🟠 `EntRecordVersion` has the same class of problem

Backed up since Tier 1 and restored via `RestoreWholeTableIfEmpty`, it stores `EntityName` + `EntityID` — a polymorphic pointer into `TblCase` / `TblFamily` / `TblDocs` / `TblAssistance` / `TblApplicant` (map confirmed in `VersionService.cs:31–39`). Four of those five have their ids reassigned on restore, so restored record-version snapshots attach to the wrong records.

Lower severity than D-1 only because it is a history/audit feature rather than a legal record, and the table is currently empty.

**Both should be fixed in Tier 3 regardless of which new tables are added.**

---

## 1. Full inventory — 22 remaining tables

Legend for **Dependency chain**: `→` means "references". Bold marks a reference whose target id **changes during restore**.

### Enterprise runtime — 8 tables

| Table | Module | Purpose | Rows | Foreign keys | Dependency chain | Business impact | DR impact |
|---|---|---|---|---|---|---|---|
| **EntWorkflowInstance** | Workflow | A live workflow run attached to a record | 0 | `WorkflowID`→EntWorkflow (FK, CASCADE), `CurrentStateID`→EntWorkflowState, `EntityID` polymorphic, `CenterID` | EntWorkflow → **TblCase** → TblCenter | Cases lose their position in the workflow; staff cannot tell what stage a case is at | In-flight process state lost; cases silently revert to "no workflow" |
| **EntWorkflowHistory** | Workflow | Transition log per instance | 0 | `InstanceID`→EntWorkflowInstance (FK, CASCADE), `FromStateID`/`ToStateID`, `TransitionID` | EntWorkflowInstance → EntWorkflowState | Decision trail lost — cannot show who moved a case and when | Governance/audit trail gone |
| **EntApprovalRequest** | Approval | A pending or completed approval | 0 | `ChainID`→EntApprovalChain, `EntityID` polymorphic, `WorkflowInstanceID`, `TransitionID`, `TargetStateID`, **`RequestedByUserID`**, `CenterID` | EntApprovalChain → **TblCase** → **TblUsers** | Pending financial approvals vanish; money awaiting sign-off is orphaned | Approvals in flight are lost; requests must be re-raised |
| **EntApprovalAction** | Approval | Each approve/reject decision | 0 | `RequestID`→EntApprovalRequest (FK, CASCADE), **`ActionByUserID`** | EntApprovalRequest → **TblUsers** | Evidence of *who approved* is lost — an accountability record | Cannot prove who authorised a payment |
| **EntTask** | Tasks | Assigned staff work item | 0 | `EntityID` polymorphic, **`AssignedToUserID`**, **`CreatedByUserID`**, `SourceID`, `CenterID` | **TblCase** → **TblUsers** → TblCenter | Assigned work disappears; staff lose their queue | All outstanding tasks lost |
| **EntSecurityEvent** | Security | Security event log (denials, logins) | 0 | **`UserID`** (+ `Username` text), `EntityID`, `CenterID` | **TblUsers** → TblCenter | Security forensics lost | Cannot investigate past incidents |
| **EntErrorLog** | Diagnostics | Application error log | 1 | **`UserID`** (+ `Username` text), `CenterID` | **TblUsers** → TblCenter | Diagnostic history lost | Harder to debug recurring faults |
| **EntRuleLog** | Rules | Rule execution log | 0 | `RuleID`→EntRule, `EntityID`, `CenterID` | EntRule → **TblCase** | Rule audit lost | Cannot show why a rule fired |

### Admin / HR — 2 tables

| Table | Module | Purpose | Rows | Foreign keys | Dependency chain | Business impact | DR impact |
|---|---|---|---|---|---|---|---|
| **AdmJobApplication** | Recruitment | Full job application (38 fields: education, experience, references, salary) | n/a* | `CenterID` only | TblCenter | Entire recruitment pipeline lost | Applicant records unrecoverable |
| **AdmDriverContract** | Transport | Driver/vehicle contract with wage terms | n/a* | `TxnID`→AccTransaction (**cross-module**), `CenterID`, `FilePath` | AccTransaction (separate backup file) → TblCenter | Contractual and payment-linked records lost | Contract terms and the signed-file link lost |

\* module initialises on first use; not present in the sampled database.

### AI assistant — 3 tables

| Table | Module | Purpose | Rows | Foreign keys | Dependency chain | Business impact | DR impact |
|---|---|---|---|---|---|---|---|
| AiConversation | AI | Chat session | n/a | **`UserID`**, `CenterID` | **TblUsers** → TblCenter | Chat history lost | Low — conversational convenience |
| AiMessage | AI | Message within a conversation | n/a | `ConversationID`→AiConversation (FK, CASCADE) | AiConversation | Message history lost | Low |
| AiIntentLog | AI | NLU intent/accuracy log | n/a | **`UserID`**, `CenterID` | **TblUsers** | Quality-tuning data lost | Low |

### Excluded by design — 9 tables

| Table | Module | Purpose | Rows | Why it must stay out |
|---|---|---|---|---|
| SyncOutbox | Sync | Unsent operations **for this device** | 0 | Restoring another machine's queue replays foreign operations against the server |
| SyncState | Sync | Per-device sync cursor | 0 | Stale cursors corrupt delta computation |
| SyncBaseline | Sync | Last-known-server snapshot | 0 | Same — device-specific |
| SyncConflict | Sync | Unresolved conflicts | 0 | Transient, device-specific |
| SyncFile / SyncFileDownload | Sync | File transfer queue/state | 0 / 0 | Transient transport state |
| EntRecordLock | Enterprise | Live edit locks | 0 | Would lock records for users who are not editing |
| AiCaseSearchIndex | AI | FTS5 search index | n/a | **Auto-maintained by triggers** `Trg_TblCase_AI_Insert/Update/Delete` (`AiInitializer.cs:96–110`) — rebuilds itself from `TblCase`; restoring it as data would corrupt it |
| TblAuditLogs | Core | Second audit table | 0 | **Verified dead**: no `INSERT` anywhere in the codebase; only read by `DevCenterService.cs:1049`. Superseded by `TblAuditLog` |

---

## 2. Classification (Phase 2)

### Category A — Must backup (7 tables + 2 defect fixes)

| Item | Justification |
|---|---|
| **D-1 fix — `TblCaseTransferHistory` case remapping** | Already backed up but restores **wrong** references. A corrupted legal record is worse than a missing one. |
| **D-2 fix — `EntRecordVersion` entity remapping** | Same defect class, already-shipped table. |
| **AdmJobApplication** | Real HR business data with **zero** protection. Trivial to add: `CenterID` is its only reference. Closes the half-covered HR module flagged as an operational risk in the Tier 2 report. |
| **AdmDriverContract** | Contractual records with wage terms. Only `CenterID` + an optional `TxnID`. |
| **EntTask** | Assigned staff work. Losing the task queue stops people working, with no record of what was assigned. |
| **EntWorkflowInstance** | Where each case sits in its process. Without it, restored cases silently have no workflow position. |
| **EntWorkflowHistory** | The decision trail behind those positions. |
| **EntApprovalRequest** | Pending financial approvals — money awaiting sign-off. |
| **EntApprovalAction** | Who approved what. An accountability record. |

**Note on timing.** All the Enterprise runtime tables are currently 0 rows. That is precisely the argument for doing them **now**: the remapping infrastructure can be built and tested against a controlled dataset, rather than retrofitted once live approval and workflow data exists. This was the recommendation closing the Tier 2 report.

### Category B — Should backup (6 tables)

| Table | Justification for deferring |
|---|---|
| EntSecurityEvent, EntErrorLog, EntRuleLog | Append-only diagnostic logs. Valuable for forensics, but losing them does not break operations, and they need only `Username` remapping. |
| AiConversation, AiMessage, AiIntentLog | Assistant chat history. Genuinely low business value; the module is Phase 1 and its schema may still change. |

Restoring these would be straightforward once Category A's remapping helpers exist — they reuse the same machinery. Reasonable as a follow-up.

### Category C — Can stay excluded (9 tables)

The six `Sync*` tables, `EntRecordLock`, `AiCaseSearchIndex` and `TblAuditLogs`, for the reasons in §1. Backing any of these up would be **actively harmful**, not merely wasteful:

- `Sync*` would replay another device's pending operations against the live server.
- `EntRecordLock` would lock records for users who are not editing.
- `AiCaseSearchIndex` is trigger-maintained; restoring stale rows would desynchronise search from reality.
- `TblAuditLogs` has no writer — backing it up would preserve permanent emptiness.

---

## 3. Remapping analysis (Phase 3)

### 3.1 Which identifiers actually change during restore

This is the crux. Verified directly from the code:

| Identifier | Preserved on restore? | Mechanism |
|---|---|---|
| **`TblCase.CasID`** | ❌ **reassigned** | `InsertCaseRow` skips the `CasID` column; `casIdMap` tracks old→new |
| **`TblFamily.FamID`** | ❌ **reassigned** | `MergeChildTable` reassigns; `famIdMap` tracks old→new |
| `TblDocs.DocID`, `TblAssistance.AssistanceID` | ❌ reassigned | `MergeChildTable`; **no map currently captured** |
| **`TblUsers.UserID`** | ❌ **reassigned** | `MergeUsers` skips `UserID`; a seeded `admin` already occupies id 1 |
| `TblCenter.CenterID` | ⚠️ **usually** preserved | `MergeCenters` omits `CenterID`, but `INSERT OR IGNORE` on `CenterCode` means the 11 pre-seeded centers keep their seeded ids. Live DB is contiguous 1–11. **Diverges only if an office added a custom center.** |
| Config-table PKs (`WorkflowID`, `StateID`, `ChainID`, `RuleID`, `TemplateID`, `PackageID`, `EmployeeID`, `AccTransaction.TxnID`) | ✅ **preserved** | Restored by full replace; `InsertRow` writes **all** columns including the PK |

**The key consequence:** intra-group parent→child chains are automatically safe under full replace, because parents keep their ids. `EntWorkflowInstance`→`EntWorkflowHistory`, `EntApprovalRequest`→`EntApprovalAction`, `AiConversation`→`AiMessage`, and `AdmEmployee`→`AdmLeave`/`AdmMission` all need **no** remapping.

Only references that cross into `TblCase`, `TblFamily`, `TblUsers` (and possibly `TblCenter`) require translation.

### 3.2 Per-table remapping requirements

| Table | ID remap | Username remap | Case remap | Center remap |
|---|---|---|---|---|
| **TblCaseTransferHistory** (D-1) | — | `UserID` ⚠️ | ✅ **`CasID`** | `FromCenterID`/`ToCenterID` ⚠️ |
| **EntRecordVersion** (D-2) | — | — | ✅ **`EntityID`** (polymorphic) | — |
| **EntWorkflowInstance** | `WorkflowID`, `CurrentStateID` — safe (config preserved) | `StartedBy` is **text** — safe | ✅ **`EntityID`** | `CenterID` ⚠️ |
| **EntWorkflowHistory** | `InstanceID` — safe (parent preserved) | `ActionBy` is text — safe | — | — |
| **EntApprovalRequest** | `ChainID`, `TransitionID`, `TargetStateID` — safe | ✅ **`RequestedByUserID`** | ✅ **`EntityID`** | `CenterID` ⚠️ |
| **EntApprovalAction** | `RequestID` — safe (parent preserved) | ✅ **`ActionByUserID`** | — | — |
| **EntTask** | `SourceID` — context-dependent | ✅ **`AssignedToUserID`**, **`CreatedByUserID`** | ✅ **`EntityID`** | `CenterID` ⚠️ |
| **AdmJobApplication** | — | — | — | `CenterID` ⚠️ |
| **AdmDriverContract** | `TxnID`→AccTransaction — safe *(if accounting backup is also restored)* | — | — | `CenterID` ⚠️ |
| EntSecurityEvent / EntErrorLog / EntRuleLog *(Cat. B)* | `RuleID` safe | `UserID` — but a `Username` text column already exists alongside | `EntityID` | `CenterID` ⚠️ |
| AiConversation / AiIntentLog *(Cat. B)* | — | `UserID` | — | `CenterID` ⚠️ |
| AiMessage *(Cat. B)* | `ConversationID` — safe | — | — | — |

⚠️ = usually correct in practice, but not guaranteed. See §3.3.

### 3.3 The polymorphic `EntityID` problem

`EntWorkflowInstance`, `EntApprovalRequest`, `EntTask`, `EntRuleLog`, `EntSecurityEvent` and `EntRecordVersion` all store a pair (`EntityName`, `EntityID`). The supported targets are fixed (`VersionService.cs:31–39`):

```
TblCase → CasID   TblFamily → FamID   TblDocs → DocID
TblAssistance → AssistanceID          TblApplicant → ApplicantID
```

Remapping must therefore **switch on `EntityName`**, applying `casIdMap` for `TblCase`, `famIdMap` for `TblFamily`, and so on. `TblApplicant` needs no translation (restored by full replace, PK preserved). `TblDocs` and `TblAssistance` currently have **no map captured** during restore — in practice `EntityCase = "TblCase"` is the only value the shipped code writes, so a `TblCase`-only remap covers real data, with unknown entity types skipped rather than guessed.

**Proposed rule:** remap what we can resolve; **skip** rows whose entity cannot be resolved, and count them. Skipping loses a row; mis-pointing corrupts a record. Skipping is the safe failure.

### 3.4 Center remapping

`CenterID` appears on nearly every Category A table. `MergeCenters` omits `CenterID` and relies on `INSERT OR IGNORE` against the unique `CenterCode`, so the 11 pre-seeded centers retain their seeded ids and the live database is contiguous 1–11 — meaning center ids align **today**. They would diverge if an office created a custom center before the backup.

A `CenterCode`-based map is the robust fix and is cheap to build (11 rows). Recommended, and it also hardens the existing `TblReminder` and `Adm*` restores added in Tier 2.

---

## 4. Proposed scope for Phase 4

| Item | Tables |
|---|---|
| Defect fixes | `TblCaseTransferHistory` (D-1), `EntRecordVersion` (D-2) |
| New coverage | `EntTask`, `EntWorkflowInstance`, `EntWorkflowHistory`, `EntApprovalRequest`, `EntApprovalAction`, `AdmJobApplication`, `AdmDriverContract` |
| New infrastructure | polymorphic entity remap (`casIdMap`/`famIdMap`), `Username` remap extended to arbitrary columns, `CenterCode`-based center remap |

Projected coverage: **51 → 58 of 73 (79%)**, with the two silent-corruption defects closed.

Remaining after Tier 3: 15 tables — 6 Category B (deferred) + 9 Category C (excluded by design).

---

## Open questions before Phase 4

**Q1 — Scope.** Category A as proposed (7 new tables + 2 defect fixes), or narrower (defect fixes + the 2 HR tables only, which need no case/user remapping and carry real data today)?

**Q2 — Center remapping.** Add the `CenterCode`-based map now — which also retroactively hardens the Tier 2 `TblReminder`/`Adm*` restores — or accept the current "correct unless a custom center exists" behaviour and leave it?

**Q3 — Merge-restore policy for runtime tables.** Tier 2 established that *configuration* is restored only on a fresh install. Workflow instances, tasks and approvals are **operational data**, not configuration. My recommendation is to apply the same fresh-install-only rule (simple, consistent, no duplicates possible), rather than attempting to merge in-flight workflow state between offices — which would need conflict resolution this backup system does not have. Confirm?
