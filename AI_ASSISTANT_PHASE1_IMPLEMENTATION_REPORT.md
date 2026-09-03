# AI Assistant Phase 1 — Implementation Report

Status: **Implementation complete. This document is audit/reporting only — no code was changed while producing it.**
Parent documents: [`AI_ASSISTANT_MASTER_PLAN.md`](AI_ASSISTANT_MASTER_PLAN.md) · [`AI_ASSISTANT_PHASE1_SPEC.md`](AI_ASSISTANT_PHASE1_SPEC.md) · [`AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md`](AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md) · [`AI_ASSISTANT_PHASE1_FIXES.md`](AI_ASSISTANT_PHASE1_FIXES.md)

---

## 1. Regression Audit — Conclusions First

**1. Did AI Assistant Phase 1 introduce any regression? No.**
Across every test run performed (7 separate executions, isolated and combined), **zero test failures were ever attributable to AI Phase 1 code.** The 49 new AI tests passed every single time they ran (standalone and embedded in larger runs). No pre-existing test's assertions changed behavior because of anything added in this phase.

**2. Which failures are pre-existing (unrelated to this work)?**
- **`BackupRestoreEncryptedTests.CreateBackup_Restore_ToFreshInstall_DataMatchesOriginal`** fails ("`UNIQUE constraint failed: TblCase.FormNo`") when run in total isolation (reproduced twice, deterministically), but **passes** when run as part of the full suite (reproduced twice). Root-caused to `Helpers/BackupHelper.cs`, which — confirmed via `git diff HEAD` — already contained a large (272-line), **uncommitted, in-progress encrypted-backup feature that predates this session**. I did not touch this file.
- **An intermittent test-host-process crash** (`vstest.console.exe` exits with code 255/-1, mid-run, with no test failure reported) occurs unpredictably during full sequential runs. **This was independently reproduced running only the 400 pre-existing tests with zero AI code in the execution path** — proving it is not caused by this work. It is a pre-existing environment/test-infrastructure instability (see §5).
- **~30 other files** (`Accounting/*`, `Enterprise/FrmErrorLog.cs`, `FrmCase.cs`, `Helpers/CaseDomain.cs`, etc.) were already modified and uncommitted in the working tree before this session started. None of these were touched during this work; they are unrelated, pre-existing in-progress changes, noted here only for transparency about the baseline this audit ran against.

**3. Which failures are caused by the new AI implementation? None found.**

---

## 2. Final Test Numbers

| Scope | Total | Passed | Failed | Skipped | Evidence |
|---|---|---|---|---|---|
| New AI Phase 1 tests | 49 | 49 | 0 | 0 | Ran standalone (clean) and twice more embedded in larger runs (clean both times) |
| Pre-existing suite (excl. AI tests) | 400 | 398 | 0 | 2 | One complete, uninterrupted run (`Test Run Successful`, 11.8 min); the 2 skips are pre-existing and documented (unrelated ClosedXML/WebView2 rendering tests requiring a real browser engine) |
| **Combined (best evidence across all runs)** | **449** | **447** | **0** | **2** | See caveat below — no single uninterrupted 449-test run completed, but no test in either subset ever failed in any run |

**Caveat — full combined run not cleanly completed in this environment.** A single, uninterrupted, all-449-tests-in-one-process run was attempted twice; both times the **test host process itself** (not a specific test) terminated with exit code 255 partway through, with no failing test reported before the cutoff. This is the same pre-existing instability described in §1/§5, reproduced independently of AI code. The numbers above are assembled from multiple overlapping runs that are mutually consistent (no contradictions, no test ever flipped from pass to fail across runs) — but I did not obtain one single unbroken execution log covering all 449 tests end to end, and I am reporting that gap honestly rather than papering over it.

---

## 3. Build Result

- `CaseManagement.csproj` (Debug, AnyCPU): **Build succeeded**, 0 errors.
- `CaseManagement.Tests.csproj` (Debug): **Build succeeded**, 0 errors. Pre-existing `MSB3277` reference-version-conflict warnings (System.Memory) are unrelated to this work and were present before.
- No new compiler warnings were introduced by AI Phase 1 code beyond the pre-existing baseline.

---

## 4. Modified Files

| File | Change |
|---|---|
| `Helpers/DatabaseInitializer.cs` | Added 2 `EnsureColumn` calls for `TblReminder` (`CreatedByAI`, `SourceQueryText`) — additive, idempotent |
| `Enterprise/EnterpriseInitializer.cs` | Added 2 `AddPermission` seed calls (`AI.Search`, `AI.Reminders.Create`) to the existing permission list |
| `Program.cs` | Added one line wiring `AiInitializer.EnsureAiObjects()` into the startup sequence, after `OfflineSyncInitializer` |
| `FrmDashboard.cs` | Added one toolbar button ("دستیار هوشمند") next to the existing "جستجوی پیشرفته" button |
| `CaseManagement.csproj` | Added `<Compile Include>` entries for all 15 new main-project files (old-style csproj, no auto-glob) |

No existing method body was rewritten; every change is either a net-new file or a small, additive insertion into an existing method.

---

## 5. New Files

**Main project (`CaseManagement/`):**
- `Helpers/AiInitializer.cs` — schema creation, FTS5 fallback guard, retention purge, reminder/audit reconciliation check
- `AI/PersianNormalizer.cs`
- `AI/AiModels.cs`
- `AI/CaseSearchCore.cs`
- `AI/CaseReferenceResolver.cs`
- `AI/ReminderDateParser.cs`
- `AI/PersianNluEngine.cs`
- `AI/SqlParam.cs`
- `AI/AiOrchestrator.cs`
- `AI/AiResultCard.cs`
- `AI/Handlers/SearchCaseHandler.cs`
- `AI/Handlers/SearchFamilyHandler.cs`
- `AI/Handlers/NoRecentServiceHandler.cs`
- `AI/Handlers/CreateReminderHandler.cs`
- `FrmAiAssistant.cs`

**Test project (`CaseManagement.Tests/`):**
- `AiPersianNormalizerTests.cs` (7 tests)
- `AiPersianNluEngineTests.cs` (12 tests)
- `AiReminderDateParserTests.cs` (8 tests)
- `AiCaseReferenceResolverTests.cs` (7 tests)
- `AiPermissionTests.cs` (4 tests)
- `AiCenterIsolationTests.cs` (5 tests)
- `AiCreateReminderHandlerTests.cs` (6 tests)

---

## 6. Database Changes

All additive; no existing table, column, or row was renamed, dropped, or altered destructively.

- **New tables**: `AiConversation`, `AiMessage`, `AiIntentLog`, `AiCaseSearchIndex` (FTS5 virtual table, includes `CenterID` per the fixes-doc correction).
- **New triggers**: `Trg_TblCase_AI_Insert/Update/Delete` (keep `AiCaseSearchIndex` in sync with `TblCase`), created only if FTS5 is available (confirmed available in this build — see fixes doc §4 — but the create is still wrapped defensively).
- **New indexes**: `IX_AiConversation_Center`, `IX_AiMessage_Conversation`, `IX_AiIntentLog_Created`.
- **Extended table**: `TblReminder` gains `CreatedByAI INTEGER NOT NULL DEFAULT 0` and `SourceQueryText TEXT NULL`.
- **New permission rows**: `EntPermission`/`EntRolePermission` entries for `AI.Search` (all roles) and `AI.Reminders.Create` (Operator+).
- **Retention**: a 180-day rolling purge of `AiMessage`/`AiConversation`/`AiIntentLog`, configurable via `SettingsHelper` key `Ai.RetentionDays`, runs on every startup.
- **Sync**: none of the new tables were added to `Sync/SyncComparer.cs`'s table list — they remain local-only by design (per fixes doc §7), matching `TblReminder`'s own existing non-synced precedent.

---

## 7. New Features Implemented (Phase 1 MVP)

- Natural-language case search: name, father's name, tazkira (full or suffix-only, e.g. "آخر تذکره‌اش ۵۴"), province, district, phone, service status, and count queries ("چند یتیم... داریم؟").
- Natural-language family-member search (joined through `TblCase` for center scoping, since `TblFamily` has no `CenterID` of its own).
- "No recent service" query with a configurable day threshold (defaults to 90), comparing against `MAX(TblAssistance.AssistanceDate)` per case.
- Direct case lookup by `Code` → `FormNo` → name, in that order — **`CasID` is never interpreted from user text**, per the fixes-doc correction.
- Natural-language reminder creation: relative days/weeks (numeric or Persian number-words), "امروز"/"فردا", and absolute Jalali dates — reusing the existing `PersianDateHelper.ParsePersianDate`. A reminder is **never** written without a resolved future date (hard rule, independent of confidence score).
- Confidence scoring (0–1) with three behavior tiers: direct answer (≥0.70), answer + confirm prompt (0.40–0.69), clarifying question only (<0.40).
- `FrmAiAssistant`: RTL chat window with a query-history panel, inline result cards, and one-click "باز کردن پرونده" opening the real `FrmCase`.
- Every AI-created reminder is logged via the existing `AuditLogger` (`"AI:CreateReminder"`) and tagged `CreatedByAI = 1` / `SourceQueryText` for traceability.
- Permission-gated through the existing `PermissionService` (`AI.Search`, `AI.Reminders.Create`) — no new permission engine.
- Center isolation enforced at every layer: the FTS5 index itself, the structured SQL filter stage, and case-reference resolution.
- Persian/Arabic-Indic digit normalization as the first step of all text processing — required for every numeric extraction (tazkira, phone, dates) to function.
- **Fully offline**: zero network calls, zero new NuGet dependencies (FTS5 confirmed compiled into the deployed `SQLite.Interop.dll` via binary inspection before committing to this design).

---

## 8. Remaining Gaps / Explicitly Out of Phase 1 Scope

Per the approved spec, these were deliberately **not** built in this phase:

- Financial-history search, duplicate-beneficiary/risk detection, system-health insights, admin recommendations, bug-suggestion engine, and any cloud-LLM fallback — all deferred to Phases 2–4.
- `AiRiskFlag`, `AiInsightCache`, `AiFeedback` tables — not created (no consumer in Phase 1).
- The reminder-list "🤖 created by AI" visual badge in `FrmDashboard`'s reminder popup was **not** added, to avoid touching `CheckDueReminders()` (a shared, working method) for a purely cosmetic gain — `CreatedByAI`/`SourceQueryText` are still stored and queryable, just not yet surfaced in that specific popup. A deliberate, minimal-footprint scope trim.
- District matching uses a small, hardcoded "living dictionary" (~21 known city/district names, e.g. "مزار شریف") since `TblCase.District` has no backing lookup table in the schema. Uncommon districts will not be recognized until this list is expanded from real usage — flagged as expected in the architect review, not an oversight.
- The confidence-threshold bands are a first-pass heuristic, not tuned against real staff phrasing. Expect a tuning pass after initial rollout, as the architect review anticipated.

---

## 9. Known Pre-Existing Issues (Not Caused by This Work)

- **Intermittent full-suite test-host crash** (§1/§2): reproduces with zero AI code involved; not diagnosed to a specific root cause within this audit (it did not reproduce on every run — one full 400-test run completed cleanly under `/Blame`). Recommend investigating in a dedicated session, ideally outside whatever is causing the intermittency (possibly resource contention specific to this sandboxed environment).
- **`BackupRestoreEncryptedTests.CreateBackup_Restore_ToFreshInstall_DataMatchesOriginal`** fails only in total isolation, passes in full-suite context — traced to pre-existing, uncommitted, in-progress work in `Helpers/BackupHelper.cs` (272 uncommitted lines implementing encrypted backup export/import, present before this session). Not modified or fixed here, per the explicit "audit and reporting only, no code changes" instruction for this pass.

---

## 10. Production-Readiness Assessment

**Not yet recommended for production rollout without three follow-ups**, despite all automated AI tests passing cleanly:

1. **Get one clean, uninterrupted full-suite run outside this environment** (or after the pre-existing test-host instability in §9 is separately diagnosed) to have an unambiguous, single-pass green baseline — this audit's evidence is strong but assembled from multiple overlapping runs, not one unbroken pass.
2. **Manual UI smoke-test of `FrmAiAssistant`** in the actually-running application (chat bubble rendering, RTL layout of the `FlowLayoutPanel`, result-card click-through) — flagged as a known WinForms RTL risk in the architect review and not exercised by any automated test in this phase (automated tests cover the handlers/services directly, not the live form's rendering).
3. **A short real-user NLU tuning pass** — the pattern-based intent/entity extraction handles every example in the spec correctly (verified by test), but Persian/Dari phrasing variety in real use will surface gaps in the trigger-word and district dictionaries, as anticipated by the architect review.

With those three items closed, Phase 1 is functionally complete, additive-only against the existing schema, fully offline, and respects every security/isolation requirement (permissions, center scoping, parameterized SQL, audit logging) verified by 49 passing dedicated tests.
