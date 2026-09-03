# BACKUP_COVERAGE_TIER4_AUDIT.md

**Phase 1 — audit only. No code modified, no migrations created.**

Date: 30 August 2026
Follows: Tier 1 → Tier 3 (`BACKUP_RESTORE_AUDIT.md`, `BACKUP_COVERAGE_TIER2_*`, `BACKUP_COVERAGE_TIER3_*`)
Verified current coverage: **58 / 73 tables** (47 `BackupHelper` + 11 `AccountingBackupHelper`) — recomputed from source, not assumed
Remaining uncovered: **15 tables**
Existing infrastructure: `RestoreMaps` (case / family / user / center), `RestoreRemappedTable`, `RestoreWholeTableIfEmpty`, `isFreshInstall` detection, disaster-recovery drill + negative control, 190 passing tests

---

## 0. Headline finding — one table is misclassified, in both directions

Tiers 1–3 recorded all six `Sync*` tables as "excluded by design". **That is correct for five of them and wrong for `SyncState`.**

`SyncState` is not a sync cursor table. It is a **key/value configuration store**, and its keys fall into three different categories with opposite correct answers:

| Key | Written by | Category | Correct treatment |
|---|---|---|---|
| `ServerUrl` | `HttpSyncTransport.cs:32` | **Configuration** | ✅ **should be backed up** |
| `AutoSyncEnabled` | `BackgroundSyncManager.cs:43` | **Configuration** | ✅ should be backed up |
| `AutoSyncIntervalMinutes` | `BackgroundSyncManager.cs:44` | **Configuration** | ✅ should be backed up |
| `DeviceGuid` | `HttpSyncTransport.cs:33` | **Device identity** | ❌ **must NOT be restored** |
| `RefreshToken` | `HttpSyncTransport.cs:34` | **Auth credential** | ❌ **must NOT be restored** |
| `LastSyncAt`, `AutoSyncLastAttemptAt`, `AutoSyncLastResult`, `AutoSyncLastFailureAt`, `AutoSyncLastFailure` | `SyncOutboxService.cs:356`, `BackgroundSyncManager.cs:45–54` | Device telemetry | ➖ harmless either way; recommend excluding |

Two consequences, both currently wrong:

1. **Losing `ServerUrl` is a real operational gap.** After disaster recovery the office cannot reach the sync server until an administrator re-enters the address by hand (`FrmServerConnection.cs:17` notes the address is read only from `SyncState`). For a provincial office with limited support access, that is a genuine outage — not a data-loss issue, but a "we cannot reconnect" issue.
2. **Blanket-including `SyncState` would be a security regression.** `RefreshToken` is a persisted authentication credential written in plain text (`HttpSyncTransport.cs:209`), and `DeviceGuid` is the identity the server uses to distinguish machines (`HttpFileSyncTransport.cs:95, 213`). Restoring either onto a different machine transfers a credential and creates two devices claiming one identity.

**`SyncState` therefore needs key-level selective backup — the only table in the entire database where table-level include/exclude is the wrong granularity.**

---

## 1. Remaining uncovered tables

Row counts from the live `bin/Debug/CaseDB.sqlite`. Tables marked *(not present)* initialise on first use of their module.

| # | Table | Module | Rows | Business-critical? | Data loss acceptable? |
|---|---|---|---|---|---|
| 1 | **SyncState** | Sync | 0 | ⚠️ **Partly** — config yes, credentials no | ❌ for `ServerUrl` · ✅ for the rest |
| 2 | **EntSecurityEvent** | Security | 0 | ✅ Yes — compliance/forensics | ❌ No |
| 3 | **EntErrorLog** | Diagnostics | 1 | ➖ Moderate | ⚠️ Tolerable |
| 4 | **EntRuleLog** | Rules | 0 | ➖ Moderate | ⚠️ Tolerable |
| 5 | **AiConversation** | AI | *(not present)* | ❌ No | ✅ Yes |
| 6 | **AiMessage** | AI | *(not present)* | ❌ No | ✅ Yes |
| 7 | **AiIntentLog** | AI | *(not present)* | ❌ No | ✅ Yes |
| 8 | **SyncOutbox** | Sync | 0 | ⚠️ Situational | ⚠️ See §3.2 |
| 9 | **SyncBaseline** | Sync | 0 | ❌ No | ✅ Yes — self-heals |
| 10 | **SyncConflict** | Sync | 0 | ❌ No | ✅ Yes |
| 11 | **SyncFile** | Sync | 0 | ❌ No | ✅ Yes |
| 12 | **SyncFileDownload** | Sync | 0 | ❌ No | ✅ Yes |
| 13 | **EntRecordLock** | Enterprise | 0 | ❌ No — harmful if restored | ✅ Yes |
| 14 | **AiCaseSearchIndex** | AI | *(not present)* | ❌ No — derived | ✅ Yes — rebuilt by triggers |
| 15 | **TblAuditLogs** | Core | 0 | ❌ No — dead table | ✅ Yes |

---

## 2. Remapping requirements

Recall which identifiers change during restore (verified in Tier 3): `CasID`, `FamID`, `UserID` are reassigned; `CenterID` usually survives via `CenterCode`; primary keys of full-replace tables are preserved.

**Crucially, the `Sync*` tables reference records by `EntityGlobalID` — a TEXT GUID that survives restore unchanged** (`InsertCaseRow` inserts every column except `CasID`, so `GlobalID` carries through). They therefore need **no** case remapping. Their exclusion is a semantic decision, not a referential-integrity limitation — a distinction the earlier tiers did not draw.

| Table | CaseID remap | UserID remap | CenterID remap | No remap | Notes |
|---|---|---|---|---|---|
| SyncState | — | — | — | ✅ | key/value only |
| EntSecurityEvent | ✅ `EntityID` (polymorphic) | ✅ `UserID` | ✅ `CenterID` | — | also has `Username` TEXT — attribution survives even if `UserID` fails |
| EntErrorLog | — | ✅ `UserID` | ✅ `CenterID` | — | also has `Username` TEXT |
| EntRuleLog | ✅ `EntityID` (polymorphic) | — | ✅ `CenterID` | — | `RuleID` safe (config preserved) |
| AiConversation | — | ✅ `UserID` | ✅ `CenterID` | — | |
| AiMessage | — | — | — | ✅ | `ConversationID` safe (parent preserved) |
| AiIntentLog | — | ✅ `UserID` | ✅ `CenterID` | — | |
| SyncOutbox | — | ✅ `UserID` | ✅ `CenterID` | — | entity refs are GUIDs → no case remap |
| SyncBaseline / SyncConflict / SyncFile / SyncFileDownload | — | — | ⚠️ `CenterID` | — | GUID-based |
| EntRecordLock | ✅ `EntityID` | ✅ `UserID` | ✅ `CenterID` | — | irrelevant — excluded |
| AiCaseSearchIndex | — | — | — | ✅ | derived, never restored |
| TblAuditLogs | — | — | — | ✅ | dead |

**Every remapping type needed by Tier 4 already exists.** `RestoreRemappedTable` with `userColumns` / `centerColumns` / `polymorphicEntity` covers all seven candidate tables with no new infrastructure.

---

## 3. Restore-mode safety

### 3.1 Full vs merge restore

| Table | Full restore | Merge restore | Recommended |
|---|---|---|---|
| SyncState *(config keys only)* | ✅ safe | ⚠️ would overwrite local server address | **Fresh install only** |
| EntSecurityEvent | ✅ safe | ⚠️ duplicates (no natural key) | **Fresh install only** |
| EntErrorLog | ✅ safe | ⚠️ duplicates | **Fresh install only** |
| EntRuleLog | ✅ safe | ⚠️ duplicates | **Fresh install only** |
| AiConversation / AiMessage / AiIntentLog | ✅ safe | ⚠️ duplicates | **Fresh install only** |

All seven candidates are append-only with no natural unique key, so they follow the rule already established in Tiers 2–3: **restore on fresh install, skip on merge**. No new policy decision is required, and no duplicate-row risk arises.

### 3.2 `SyncOutbox` — the one genuinely debatable case

Verified mechanics: upload selects **only** from the outbox —

```
SyncOutboxService.cs:285
SELECT * FROM SyncOutbox WHERE State IN (@P, @F) ORDER BY OutboxID LIMIT @L
```

— and there is **no rebuild/re-enqueue path anywhere in the codebase** (searched for `Enqueue`, `RebuildOutbox`, `ReQueue` — no matches).

So if the outbox is lost, unsent operations are **not** re-derived. Note carefully what is and is not lost:

- ✅ The underlying business records (`TblCase`, `TblFamily`, …) **are** backed up and restored — no business data is destroyed.
- ❌ The marker saying "these changes still need pushing to the server" is lost, so the central server never receives them automatically. The office and head office stay divergent until someone notices and forces a re-sync.

**Recommendation: still exclude.** Restoring a queue of pending operations onto a re-installed machine means replaying operations whose `DeviceGuid` and auth context no longer match, against a server that may already have received some of them. The correct remedy for post-DR divergence is a deliberate full re-sync, not queue replay. This should be documented as a **known recovery procedure**, not silently accepted — see §6.

---

## 4. Dependency graph

```
TblCase ──GlobalID(TEXT, preserved)──┐
                                      ├── SyncOutbox.EntityGlobalID
TblFamily ─GlobalID─────────────────┤   SyncBaseline.EntityGlobalID
TblDocs ───GlobalID─────────────────┤   SyncConflict.EntityGlobalID
TblAssistance ─GlobalID─────────────┘   SyncFile / SyncFileDownload.EntityGlobalID
        (GUID survives restore → no remapping needed)

TblCase.CasID (REASSIGNED) ──► EntSecurityEvent.EntityID   [polymorphic]
                              └► EntRuleLog.EntityID        [polymorphic]

TblUsers.UserID (REASSIGNED) ─► EntSecurityEvent.UserID  (+ Username TEXT fallback)
                              ├► EntErrorLog.UserID      (+ Username TEXT fallback)
                              ├► AiConversation.UserID
                              ├► AiIntentLog.UserID
                              └► SyncOutbox.UserID

TblCenter.CenterID (usually preserved) ─► all seven candidates

EntRule.RuleID (preserved) ───► EntRuleLog.RuleID
AiConversation.ConversationID (preserved) ──► AiMessage.ConversationID   [parent→child, safe]

TblCase ══triggers══► AiCaseSearchIndex   (Trg_TblCase_AI_Insert/Update/Delete)
                                          → self-rebuilding, must never be restored

SyncState  ── standalone key/value, no FK ── but mixes:
    config (ServerUrl, AutoSync*)  |  identity (DeviceGuid)  |  credential (RefreshToken)
```

---

## 5. Risk classification and recovery ranking

Ranked by **recovery importance** — what hurts most if missing after a disaster.

| Rank | Table | Risk | Consequence of loss after DR |
|---|---|---|---|
| **1** | **SyncState** *(config keys)* | 🟠 **HIGH** | Office **cannot reconnect to the sync server** until an admin manually re-enters the address. Blocks all data flow to head office. Cheapest fix in the whole tier. |
| **2** | **EntSecurityEvent** | 🟠 **HIGH** | Security forensics gone. Permission denials, login anomalies and access events cannot be investigated after an incident — the one remaining gap with a **compliance** dimension rather than merely operational. |
| **3** | **EntErrorLog** | 🟡 **MEDIUM** | Diagnostic history lost; recurring faults become harder to trace. Has 1 row today, will grow. |
| **4** | **EntRuleLog** | 🟡 **MEDIUM** | Cannot demonstrate why a business rule fired — weakens the audit story around automated decisions. |
| **5** | **SyncOutbox** | 🟡 **MEDIUM** | Unsent operations not replayed → silent divergence with head office until a full re-sync. Business data itself survives. Mitigation is procedural, not technical. |
| **6** | **AiConversation / AiMessage / AiIntentLog** | 🔵 **LOW** | Assistant chat history lost. No operational impact; module is Phase 1 and its schema may still change. |
| **7** | **SyncBaseline** | 🔵 **LOW** | Self-heals — absence is already handled as "first sync" (`SyncConflictAnalyzer.cs:230–237`). |
| **8** | **SyncConflict** | 🔵 **LOW** | Unresolved conflict records lost; re-detected on next sync. |
| **9** | **SyncFile / SyncFileDownload** | 🔵 **LOW** | Transfer state re-derived by `MediaScanner` on next run. |
| **10** | **EntRecordLock** | 🔵 **LOW** | Restoring would be **actively harmful** — locks records for users who are not editing. |
| **11** | **AiCaseSearchIndex** | 🔵 **LOW** | Restoring would be **actively harmful** — trigger-maintained; stale rows desynchronise search from reality. |
| **12** | **TblAuditLogs** | 🔵 **LOW** | Dead table — no `INSERT` anywhere in the codebase; read only by `DevCenterService.cs:1049`. Backing it up preserves permanent emptiness. |

---

## 6. Proposed Tier 4 implementation plan

Three phases, ordered by value-to-risk. **Nothing implemented yet — this is the proposal.**

### Phase A — `SyncState` selective config backup
**Value: highest · Risk: 🟢 LOW**

Export only an allow-list of configuration keys (`ServerUrl`, `AutoSyncEnabled`, `AutoSyncIntervalMinutes`), restoring them fresh-install-only. `DeviceGuid`, `RefreshToken` and all telemetry keys are **explicitly excluded** in code with the reason recorded.

- Needs a small key-filtered export (the only such case in the codebase) — new but contained
- Risk: getting the allow-list wrong. Mitigated by making it an explicit allow-list, never a deny-list, so a future key added by another developer is excluded by default rather than accidentally exported
- Security note: this phase *reduces* risk — it makes explicit that credentials must never enter a backup

### Phase B — Enterprise logs
**Value: high · Risk: 🟢 LOW**

Add `EntSecurityEvent`, `EntErrorLog`, `EntRuleLog` via the **existing** `RestoreRemappedTable` with `userColumns` / `centerColumns` / `polymorphicEntity`.

- Zero new infrastructure — same call shape already used for `EntTask` in Tier 3
- Both `EntSecurityEvent` and `EntErrorLog` carry a `Username` TEXT column, so attribution degrades gracefully even if a user cannot be resolved
- Risk: essentially the Tier 3 pattern repeated; covered by extending the existing drill

### Phase C — AI history *(optional)*
**Value: low · Risk: 🟡 LOW–MEDIUM**

Add `AiConversation`, `AiMessage`, `AiIntentLog`.

- The only elevated risk in the tier: these tables do not exist in the sampled database, the module is Phase 1, and `AI_ASSISTANT_MASTER_PLAN.md` describes further tables (`AiRiskFlag`, `AiInsightCache`, `AiFeedback`) not yet built — so the export list would likely need revisiting
- `LoadTableIfExists` degrades safely, but churn is likely
- **Recommendation: defer** until the AI module's schema settles

### Projected outcome

| | Now | After A+B | After A+B+C |
|---|---|---|---|
| Tables covered | 58 / 73 (79%) | **62 / 73 (85%)** | **65 / 73 (89%)** |
| Remaining | 15 | 11 | 8 |

After Phase A+B, the remaining 11 are: 5 `Sync*` transport tables, `EntRecordLock`, `AiCaseSearchIndex`, `TblAuditLogs` (all **excluded by design**), plus the 3 AI tables (deferred). **Every table where loss causes business or compliance harm would be covered.**

### Not proposed for backup — final

| Table | Reason |
|---|---|
| SyncOutbox, SyncBaseline, SyncConflict, SyncFile, SyncFileDownload | Device-local transport state; replaying another install's queue is unsafe |
| SyncState `DeviceGuid` / `RefreshToken` | Device identity and auth credential — transferring them is a security regression |
| EntRecordLock | Would lock records for users who are not editing |
| AiCaseSearchIndex | Trigger-maintained derived index |
| TblAuditLogs | Verified dead — no writer exists |

### Recommended non-code deliverable

The `SyncOutbox` gap (§3.2) is best closed **procedurally, not technically**: after any disaster recovery, an administrator should run a **full re-sync** to reconcile the office with head office, because pending unsent operations are not restored by design. This belongs in the recovery runbook. Worth noting that no such runbook step exists today — the risk is currently neither mitigated nor documented.

---

## Open questions before Phase 2

**Q1 — Scope.** Phase A + B (recommended), all three phases, or Phase A only?

**Q2 — `SyncState` allow-list.** Confirm the three configuration keys (`ServerUrl`, `AutoSyncEnabled`, `AutoSyncIntervalMinutes`) are the right set, and that `DeviceGuid` / `RefreshToken` must never be backed up. This is a security-relevant decision and I do not want to assume it.

**Q3 — `SyncOutbox` runbook step.** Should I draft the post-recovery re-sync procedure as documentation, or is that owned elsewhere?
