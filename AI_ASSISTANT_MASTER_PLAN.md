# AI Assistant Master Plan — سیستم مدیریت خیریه (CaseManagement)

Status: **Planning only. No code implemented.**
Scope: Architecture and implementation plan for a native, in-app AI Assistant module.
Grounded against the actual codebase as of this writing (see "Existing Foundation" below). No table, class, or convention is renamed; everything is additive, per `CLAUDE.md`.

---

## 0. Existing Foundation This Plan Builds On

The plan deliberately reuses what already exists instead of inventing parallel systems:

| Need | Already exists | Plan's approach |
|---|---|---|
| DB engine | SQLite (`CaseDB.sqlite`), code-first via `DatabaseInitializer.cs` + `EnsureColumn` | Same pattern for all new tables/columns |
| Data access | Single `DatabaseHelper` (ADO.NET, parameterized SQL) | AI services call through it; no new ORM |
| Multi-center scoping | `TblCenter`, `CenterID` on ~most tables, `CenterGuard`, `SecurityContext.IsAllCenters` | Every AI query/write is center-scoped by default |
| Roles/permissions | `EntPermission` / `EntRolePermission` / `EntUserPermission`, resolved by `PermissionService` | New AI permission keys added to the *same* tables, no parallel permission system |
| Audit logging | `AuditLogger` → `TblAuditLog` | Every AI-initiated write is logged with an `AI:` operation prefix |
| Error/security logging | `ErrorLogger` → `EntErrorLog`; `SecurityAudit` → `EntSecurityEvent` | Source data for the "AI System Health" feature — read-only |
| Reminders | `TblReminder` (`Title`, `Note`, `RemindAt`, `IsDone`, `IsNotified`, `CenterID`) already fully functional | Extended with nullable `CreatedByAI`, `SourceQueryText` columns — not replaced |
| Search | `FrmAdvancedSearch` (structured multi-field filter) | New NL Search Engine sits *on top of* it and can delegate to it |
| Sync/remote channel | `Sync/` (`HttpSyncTransport`, `SyncOutboxService`, background sync) talking to a remote SyncServer | Reused as the *only* egress path if an optional cloud LLM is ever enabled |
| Background jobs | `BackgroundSyncManager` (periodic timer-based) | Same pattern reused for AI risk/quality scans |

Everything below is new, but nothing below requires renaming or restructuring existing code.

---

## 1. Complete Architecture

```
                       ┌────────────────────────────┐
                       │        FrmAiAssistant       │  (chat UI, RTL, docked/modal)
                       │  FrmAiRiskDashboard         │
                       │  FrmAiSystemHealth          │
                       │  FrmAiAdminInsights         │
                       │  FrmAiSettings              │
                       └──────────────┬───────────────┘
                                      │  IAiOrchestrator.Handle(text, SecurityContext)
                       ┌──────────────▼───────────────┐
                       │        AiOrchestrator         │  orchestration + permission gate
                       └──────────────┬───────────────┘
              ┌───────────────────────┼────────────────────────┐
              ▼                       ▼                        ▼
   ┌────────────────────┐  ┌──────────────────────┐  ┌───────────────────────┐
   │  PersianNluEngine   │  │   IntentRegistry /     │  │  CloudLlmBridge (opt.) │
   │  (normalize, tokenize,│ │   IAiIntentHandler set │  │  via SyncServer proxy  │
   │   extract entities)  │  │  (one class per intent)│  │  PiiRedactionService   │
   └──────────┬──────────┘  └───────────┬───────────┘  └───────────┬───────────┘
              │                          │                          │
              │           ┌──────────────┼──────────────┐           │
              ▼           ▼              ▼              ▼           ▼
        SearchCaseHandler  CreateReminderHandler  FinancialHistoryHandler ...
              │                          │                          │
              └──────────────┬───────────┴──────────────┬───────────┘
                              ▼                          ▼
                     DatabaseHelper (existing)   CenterGuard (existing)
                              │
                              ▼
                        CaseDB.sqlite
                   (+ new AiXxx tables, + FTS5 index)

   Background (independent of chat):
   AiBackgroundScanner → RiskDetectionEngine, DataQualityEngine,
                           SystemHealthAnalyzer, AdminRecommendationEngine
                           → write AiRiskFlag / AiInsightCache
                           → read EntErrorLog, TblAuditLog, SyncOutbox, SyncConflict
```

**Two-tier NLU design (core architectural decision):**

- **Tier 1 — Local Persian NLU Engine (always on, offline-capable, default).** A deterministic normalizer + intent classifier + entity extractor built specifically for this domain's vocabulary (یتیم، تذکره، ولایت، ولسوالی، کمک نقدی…). Handles all example queries in the spec without any network call or external dependency. This is the workhorse — most charity staff queries are structurally simple ("کبیر بلخی", "یتیمان فعال سمنگان").
- **Tier 2 — Cloud LLM fallback (optional, opt-in, admin-gated).** Only invoked when Tier 1 confidence is below threshold or the query is genuinely open-ended ("چه کسانی احتمال تکراری بودن دارند؟" needs reasoning, not just filtering). Routed through the existing SyncServer as a proxy (never called directly from a field laptop), after a `PiiRedactionService` strips tazkira numbers, phone numbers, and full names unless explicitly authorized. Fully disable-able per center for offline-only deployments.

This mirrors the project's existing offline-first philosophy (Sync module already assumes intermittent connectivity) and avoids sending beneficiary PII off-premises by default — a real concern for vulnerable populations in Afghanistan.

---

## 2. Database Design

New tables use the `Ai` prefix, matching the existing `Ent`/`Acc`/`Sync` module-prefix convention. All created via `EnsureColumn`/`CREATE TABLE IF NOT EXISTS` in a new `Helpers/AiInitializer.cs`, called from the same startup sequence as `EnterpriseInitializer`.

### 2.1 New tables

```sql
-- Conversation history (for context, audit, and re-opening past chats)
CREATE TABLE IF NOT EXISTS AiConversation (
    ConversationID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID         INTEGER NOT NULL,
    CenterID       INTEGER,
    Title          TEXT,
    StartedAt      TEXT NOT NULL,
    LastMessageAt  TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AiMessage (
    MessageID       INTEGER PRIMARY KEY AUTOINCREMENT,
    ConversationID  INTEGER NOT NULL REFERENCES AiConversation(ConversationID),
    Sender          TEXT NOT NULL,      -- 'User' | 'Assistant'
    MessageText     TEXT NOT NULL,
    IntentDetected  TEXT,               -- e.g. 'Search.Case'
    EntitiesJson    TEXT,               -- extracted entities, for debugging/audit
    Confidence      REAL,
    UsedCloudLLM    INTEGER NOT NULL DEFAULT 0,
    ExecutionMs     INTEGER,
    CreatedAt       TEXT NOT NULL
);

-- Every parsed query, successful or not — powers NLU quality tracking and admin tuning
CREATE TABLE IF NOT EXISTS AiIntentLog (
    IntentLogID   INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID        INTEGER,
    CenterID      INTEGER,
    RawQuery      TEXT NOT NULL,
    ParsedIntent  TEXT,
    Confidence    REAL,
    Success       INTEGER NOT NULL,   -- 0/1
    ErrorNote     TEXT,
    CreatedAt     TEXT NOT NULL
);

-- Risk Detection Engine + Data Quality Engine output (human-reviewed, never auto-acted)
CREATE TABLE IF NOT EXISTS AiRiskFlag (
    FlagID       INTEGER PRIMARY KEY AUTOINCREMENT,
    EntityType   TEXT NOT NULL,     -- 'Case' | 'Family' | 'Assistance' | 'Document'
    EntityID     INTEGER NOT NULL,
    RiskType     TEXT NOT NULL,     -- 'DuplicateBeneficiary' | 'SuspiciousPayment' |
                                     -- 'InactiveCase' | 'MissingDocument' | 'IncompleteRecord'
    Severity     TEXT NOT NULL,     -- 'Low' | 'Medium' | 'High'
    Description  TEXT NOT NULL,
    DetailsJson  TEXT,              -- e.g. matched duplicate CaseIDs, similarity score
    Status       TEXT NOT NULL DEFAULT 'Open',  -- 'Open' | 'Reviewed' | 'Dismissed'
    CenterID     INTEGER,
    DetectedAt   TEXT NOT NULL,
    ReviewedBy   INTEGER,
    ReviewedAt   TEXT
);

-- Cached periodic admin/analytics output (avoids recomputing on every dashboard open)
CREATE TABLE IF NOT EXISTS AiInsightCache (
    InsightID    INTEGER PRIMARY KEY AUTOINCREMENT,
    InsightType  TEXT NOT NULL,    -- 'ModuleGapAnalysis' | 'SystemRiskSummary' | 'UsagePattern'
    CenterID     INTEGER,          -- NULL = cross-center (SuperAdmin only)
    Content      TEXT NOT NULL,    -- rendered markdown/text
    GeneratedAt  TEXT NOT NULL,
    ExpiresAt    TEXT NOT NULL
);

-- Optional Phase-4 feedback loop for tuning Tier-1 NLU
CREATE TABLE IF NOT EXISTS AiFeedback (
    FeedbackID   INTEGER PRIMARY KEY AUTOINCREMENT,
    MessageID    INTEGER NOT NULL REFERENCES AiMessage(MessageID),
    Rating       INTEGER NOT NULL,   -- -1 / +1
    Comment      TEXT,
    CreatedAt    TEXT NOT NULL
);
```

### 2.2 Extending existing tables (additive only)

```sql
-- TblReminder: mark AI-originated reminders, keep the original phrase for audit/trust
ALTER TABLE TblReminder ADD COLUMN CreatedByAI INTEGER NOT NULL DEFAULT 0;
ALTER TABLE TblReminder ADD COLUMN SourceQueryText TEXT;
```

No other existing table is altered. Financial/duplicate/inactivity analysis reads `TblCase`, `TblFamily`, `TblAssistance`, `AccTransaction`, etc. — it does not need new columns on them.

### 2.3 Index & search strategy

- **Structured filters** (province, district, status, tazkira, phone) already have natural filter paths through `FrmAdvancedSearch`'s existing queries — add covering indexes only where missing: `CREATE INDEX IF NOT EXISTS IX_TblCase_Province_District ON TblCase(Province, District);`, `IX_TblFamily_Tazkira ON TblFamily(MemberTazkiraNo)`.
- **Fuzzy/name search** ("کبیر", "کبیر بلخی"): use **SQLite FTS5** virtual tables (native to `System.Data.SQLite`, no new dependency — satisfies the "no unnecessary libraries" rule):
  ```sql
  CREATE VIRTUAL TABLE IF NOT EXISTS AiCaseSearchIndex USING fts5(
      CaseID UNINDEXED, FullName, FatherName, GrandFatherName,
      HeadTazkiraNo, Province, District, Notes,
      tokenize = 'unicode61 remove_diacritics 2'
  );
  ```
  Populated/kept in sync via triggers on `TblCase`/`TblFamily` insert/update/delete (`AFTER INSERT/UPDATE/DELETE`), so it never drifts and requires no separate rebuild job.
- **Persian normalization before indexing and before querying**: unify ك→ک, ي→ی, remove diacritics/tatweel, normalize half-space vs. space vs. ZWNJ, and strip common honorifics. This normalization function is shared by both the FTS trigger population and the query-time entity extractor — critical, since "کبیر" typed with an Arabic ي will silently miss otherwise.
- **Performance strategy**: fuzzy match (Levenshtein/Jaro-Winkler) is used only as a *second pass* over the small FTS5 candidate set (typically <100 rows) returned by the trigram/prefix match — never a full-table scan. Duplicate-detection background job runs off the UI thread on a timer (like `BackgroundSyncManager`), batches by center, and only re-scans records changed since its last run (`ModifiedAt` watermark).

---

## 3. Required Tables (summary)

New: `AiConversation`, `AiMessage`, `AiIntentLog`, `AiRiskFlag`, `AiInsightCache`, `AiFeedback` (Phase 4), `AiCaseSearchIndex` (FTS5 virtual table).
Extended: `TblReminder` (+2 nullable columns).
Reused unchanged: `TblCase`, `TblFamily`, `TblDocs`, `TblAssistance`, `AccTransaction`, `TblCenter`, `TblUsers`, `EntPermission`/`EntRolePermission`/`EntUserPermission`, `TblAuditLog`, `EntErrorLog`, `EntSecurityEvent`, `SyncOutbox`, `SyncConflict`.

---

## 4. Required Services

All under a new `AI/` folder (matching `Enterprise/`, `Sync/` conventions), each a small, testable class:

| Service | Responsibility |
|---|---|
| `IAiOrchestrator` / `AiOrchestrator` | Entry point. Validates permission, calls NLU, dispatches to handler, logs, returns `AiResponse`. |
| `PersianNormalizer` | Character/diacritic/half-space normalization (shared by search + NLU). |
| `PersianNluEngine` | Tokenization, intent classification (pattern + keyword scoring), entity extraction (name, tazkira digits, province/district via `TblLookup`, relative dates, amounts). |
| `IntentRegistry` | Maps intent string → `IAiIntentHandler` implementation. |
| `IAiIntentHandler` (interface) + handlers: `SearchCaseHandler`, `SearchFamilyHandler`, `ServiceStatusQueryHandler`, `FinancialHistoryHandler`, `CreateReminderHandler`, `SystemHealthQueryHandler`, `AdminRecommendationHandler`, `DuplicateQueryHandler`, `ProblemCaseQueryHandler` | One class per intent family; each only touches the tables it needs, each center-scoped. |
| `ReminderEngine` | Parses Persian relative/absolute dates ("سه روز دیگر", "یک هفته بعد", "دوشنبه آینده") into `RemindAt`; writes via existing `TblReminder` insert path so the existing notification/escalation code picks it up unchanged. |
| `RiskDetectionEngine` | Duplicate-beneficiary matching (fuzzy name + father-name + tazkira + DOB proximity), suspicious-payment heuristics (amount outliers, frequency anomalies, round-number clustering) against `AccTransaction`/`TblAssistance`. |
| `DataQualityEngine` | Missing required docs (`TblDocs` vs. checklist), incomplete mandatory fields, stale records. |
| `SystemHealthAnalyzer` | Aggregates `EntErrorLog`, `TblAuditLog`, `SyncOutbox`/`SyncConflict` into human-readable answers ("کدام فرم‌ها بیشترین مشکل را دارند؟"). Read-only. |
| `AdminRecommendationEngine` | Usage-pattern + gap analysis (e.g., module X has high error rate but low permission coverage) → suggestions. Read-only, advisory. |
| `AiBugSuggestionEngine` | Groups recent exceptions by stack signature, proposes likely root cause + suggested fix area as text. **Never modifies code.** |
| `PiiRedactionService` | Strips/masks PII before any Tier-2 cloud call; configurable per field. |
| `CloudLlmBridge` | Optional. Sends redacted prompt to SyncServer's `/ai/complete` proxy endpoint; enforces timeout, retry, and hard-disable when offline. |
| `AiPermissionGate` | Thin wrapper over existing `PermissionService`, checking new `AI.*` permission keys before any handler runs. |
| `AiAuditLogger` | Wraps existing `AuditLogger`; tags every AI write with `Operation = "AI:" + intent`. |
| `AiBackgroundScanner` | Timer-driven (same pattern as `BackgroundSyncManager`) periodic invocation of `RiskDetectionEngine`/`DataQualityEngine`/`SystemHealthAnalyzer`, writing to `AiRiskFlag`/`AiInsightCache`. |

---

## 5. Required APIs

This is a WinForms desktop app with no internal REST layer — "APIs" here means (a) the internal C# service contracts above, kept small and mockable for testing, and (b) the one external HTTP contract needed for the optional cloud tier, added to the existing SyncServer rather than calling a third-party LLM endpoint directly from client machines:

```
POST {SyncServerBaseUrl}/api/ai/complete
Headers: existing sync auth (same as HttpSyncTransport)
Body:    { "redactedPrompt": "...", "intentHint": "...", "centerId": 12 }
Response:{ "intent": "...", "entities": {...}, "answerText": "...", "confidence": 0.86 }
```

- SyncServer holds the actual LLM API key server-side (never on a field laptop).
- SyncServer itself is responsible for its own redaction re-check, rate limiting per center, and logging of outbound calls — this plan only defines the client-side contract; SyncServer-side implementation is a separate, smaller companion plan when Phase 4 is scheduled.
- No other new external API surface is needed — everything else is local.

---

## 6. Required UI Screens

| Screen | Purpose | Min. role |
|---|---|---|
| `FrmAiAssistant` | Main RTL chat panel (right-aligned bubbles, Persian). Result messages render as `AiResultCard` rows (name/tazkira/province/status + "باز کردن پرونده" button that opens `FrmCase`/`FrmFamily` directly). Reachable from a persistent "دستیار هوشمند" button on `FrmDashboard`'s toolbar, matching existing dashboard entry-point conventions. | Viewer |
| `AiResultCard` (UserControl) | Reusable result row, embeddable in chat and in dashboard widgets. | — |
| `FrmAiRiskDashboard` | List/filter `AiRiskFlag` by type/severity/status; review/dismiss actions (never auto-resolves). | Manager |
| `FrmAiSystemHealth` | Error trend charts, top-failing forms, sync failure counts — backed by `SystemHealthAnalyzer`. | Admin |
| `FrmAiAdminInsights` | Recommendation feed from `AdminRecommendationEngine` + `AiBugSuggestionEngine`. | SuperAdmin |
| `FrmAiSettings` | Enable/disable Tier-2 cloud LLM per center, redaction field toggles, view `AiIntentLog` success rate, manage AI permission shortcuts (delegates to existing `FrmPermissionMatrix`). | SuperAdmin |

All screens follow the existing `FrmXxx` naming and RTL/theme conventions already in the codebase (no new UI framework).

---

## 7. Required Permissions

Added as new rows in the *existing* `EntPermission` table (no new permission engine), resolved by the existing `PermissionService` priority chain:

| Permission key | Grants | Default roles |
|---|---|---|
| `AI.Search` | Use NL search, open results | Viewer, Operator, Manager, Admin, SuperAdmin |
| `AI.Reminders.Create` | Create reminders via NL command | Operator, Manager, Admin, SuperAdmin |
| `AI.Analytics.View` | View `FrmAiRiskDashboard`, financial/duplicate insights | Manager, Admin, SuperAdmin |
| `AI.Diagnostics.View` | View `FrmAiSystemHealth` (error/log analysis) | Admin, SuperAdmin |
| `AI.Admin.FullAccess` | View `FrmAiAdminInsights`, bug-suggestion feed, cross-center analytics, `FrmAiSettings` | SuperAdmin |
| `AI.CloudLLM.Use` | Allow Tier-2 cloud fallback for this user's queries (even if enabled center-wide, per-user opt-out possible) | Configurable, default off until explicitly enabled |

Enforcement point: `AiPermissionGate` is called at the *top* of `AiOrchestrator.Handle(...)`, before NLU even runs, and again inside each handler for the specific entity/center it touches (defense in depth, consistent with `CenterGuard` already being called in multiple layers elsewhere in the codebase).

---

## 8. Implementation Phases

**Phase 0 — Foundation (no user-visible feature yet)**
`AiInitializer` schema, `PersianNormalizer`, `AiCaseSearchIndex` FTS5 + triggers, `AiOrchestrator` skeleton, `AI.*` permission rows, `AiAuditLogger`. Exit criteria: internal console/test harness can run a query end-to-end against real data.

**Phase 1 — MVP: Natural Language Search + Reminders**
`PersianNluEngine` (name/tazkira/province/district/phone/status intents), `SearchCaseHandler`, `SearchFamilyHandler`, `ServiceStatusQueryHandler`, `ReminderEngine` + `CreateReminderHandler`, `FrmAiAssistant`, `AiResultCard`. This alone covers 6 of the 8 example queries in the spec.

**Phase 2 — Data Quality & Risk (read-only insights)**
`DataQualityEngine`, `RiskDetectionEngine` (duplicates, inactive cases, missing docs), `AiBackgroundScanner`, `FrmAiRiskDashboard`.

**Phase 3 — Financial Insight + System Health**
`FinancialHistoryHandler`, suspicious-payment heuristics in `RiskDetectionEngine`, `SystemHealthAnalyzer`, `FrmAiSystemHealth`.

**Phase 4 — Advanced: Admin Assistant, Bug Suggestions, Cloud LLM**
`AdminRecommendationEngine`, `AiBugSuggestionEngine`, `PiiRedactionService` + `CloudLlmBridge` + SyncServer `/api/ai/complete` endpoint, `FrmAiAdminInsights`, `FrmAiSettings`, `AiFeedback` loop.

**Phase 5 — Stretch (future, not scoped in detail here)**
Voice input for low-literacy field staff, predictive review-priority scoring, WhatsApp/Telegram bridge for remote centers, cross-center trend dashboards for SuperAdmin.

Each phase is independently shippable and reversible — Phase *N+1* never requires changing Phase *N*'s schema or code, only adding to it (matches `CLAUDE.md`'s "extend, never replace").

---

## 9. MVP Version (Phase 1 scope, concretely)

- Chat-style search: name, father's name, tazkira (including partial: "آخر تذکره‌اش 54"), province, district, phone, active/inactive service status.
- One NL reminder command pattern set: relative days/weeks, fixed dates, tied to a case/family ID if mentioned.
- Every result opens the real form (`FrmCase`, `FrmFamily`) — no dead-end text answers.
- Fully offline (Tier 1 only). No cloud dependency, no new external attack surface.
- Center-scoped and permission-gated from day one.
- Every query logged to `AiIntentLog`, every reminder write logged to `AiAuditLogger` — trust and auditability built in, not bolted on later.

## 10. Advanced Version (Phase 4 scope, concretely)

- Open-ended reasoning queries ("چه کسانی احتمال تکراری بودن دارند؟", "بزرگترین ریسک سیستم چیست؟") via Tier-2 cloud LLM with redaction.
- Root-cause bug suggestions from log clustering, presented as advisory text only.
- Cross-center SuperAdmin analytics and module-gap recommendations.
- User feedback loop (`AiFeedback`) feeding back into Tier-1 pattern/keyword tuning (manual review, not auto-retraining — there's no ML training pipeline in this plan, by design, to keep it auditable and dependency-light).

---

## 11. Estimated Development Effort

Assuming one primary developer (matches the project's current single-maintainer pattern), effort in developer-days, sequential:

| Phase | Estimate |
|---|---|
| Phase 0 — Foundation | 4–6 days |
| Phase 1 — MVP (Search + Reminders + Chat UI) | 12–16 days |
| Phase 2 — Risk/Quality engines + dashboard | 8–10 days |
| Phase 3 — Financial insight + System health | 8–10 days |
| Phase 4 — Admin assistant + Bug suggestions + Cloud LLM | 12–15 days |
| **MVP-only total (Phase 0+1)** | **~16–22 days** |
| **Full plan through Phase 4** | **~44–57 days** |

These are focused implementation estimates; add ~20% for the project's existing "verify build / verify existing features still work" discipline after each phase (per `CLAUDE.md`), and more if UAT with real charity staff surfaces NLU gaps in Phase 1 (likely, given dialectal variation — budget a short tuning pass after first real-world use).

---

## 12. Risks

| Risk | Mitigation |
|---|---|
| Sending beneficiary PII (names, tazkira, phone) to a cloud LLM | Tier-1 local-first design; `PiiRedactionService` before any Tier-2 call; opt-in per center; SyncServer as sole egress point, never direct-from-client |
| False positives in duplicate/fraud detection eroding staff trust | All `AiRiskFlag` output is advisory only, requires human review/dismiss; never auto-merges or auto-blocks records |
| Persian/Dari NLU ambiguity (ك/ي vs ک/ی, half-space, dialectal spelling, name variants) | Shared `PersianNormalizer`; fuzzy second pass; Phase-1 UAT tuning pass budgeted explicitly |
| Multi-center data leakage through AI answers | `AiPermissionGate` + `CenterGuard` enforced at orchestrator entry *and* inside every handler (defense in depth) |
| Breaking existing features while integrating (violates `CLAUDE.md`) | All new tables/columns only; `TblReminder` change is two nullable columns; existing forms/services untouched; each phase independently testable |
| SQLite/UI-thread performance under fuzzy search or background scans at scale | FTS5 pre-filter before fuzzy pass; background jobs run off UI thread on a timer with incremental watermarks, mirroring `BackgroundSyncManager` |
| Offline field centers with no connectivity | Tier 1 fully functional offline by design; Tier 2 fails closed (silently unavailable, not an error) when offline |
| Cloud LLM cost at scale if enabled broadly | Tier-2 gated by permission + explicit center opt-in; rate limiting enforced server-side at SyncServer proxy |
| Over-trust in AI-created reminders/records | `CreatedByAI` flag surfaced in UI wherever reminders are shown; source query text kept for traceability; staff can edit/delete like any reminder |
| Scope creep beyond this plan during implementation | Phased delivery with explicit exit criteria per phase; each phase reviewed against `CLAUDE.md` before merge |

---

## 13. Recommended Technology Stack

- **Language/runtime**: C# on the existing .NET Framework 4.7.2 — no framework upgrade, preserves WinForms/SQLite compatibility as mandated.
- **Local NLU**: hand-written normalizer + pattern/keyword intent classifier + lightweight fuzzy matcher (Levenshtein/Jaro-Winkler implemented in-project, not a new package) — zero new runtime dependencies, fully auditable, no training data or ML pipeline needed for Tier 1.
- **Search indexing**: SQLite **FTS5** virtual tables — native to the already-referenced `System.Data.SQLite`, no new dependency.
- **Optional cloud LLM (Tier 2 only)**: Anthropic Claude, via the existing SyncServer as a reverse proxy — Haiku-class model for fast intent/entity fallback, Sonnet-class for longer admin-recommendation summarization. Chosen over building a local LLM because it avoids shipping model weights to WinForms client machines and keeps the sensitive-data boundary at the server, which the sync architecture already assumes.
- **Background execution**: existing timer-based pattern from `BackgroundSyncManager` — no new job scheduler/dependency.
- **UI**: WinForms, existing RTL/theme setup — no new UI framework.
- **Logging/audit**: existing `AuditLogger`/`ErrorLogger`/`SecurityAudit` — extended, not replaced.

No new NuGet packages are strictly required for Phases 0–3. Phase 4's `CloudLlmBridge` needs only a plain HTTPS call (via `HttpClient`, already implicitly available) to the SyncServer's own new endpoint — the Anthropic SDK/API key lives server-side in SyncServer, not in this WinForms project.

---

## Final Answer: How I'd Build This

If I were building the best AI Assistant for a charity system serving Afghan orphans and families, I would not start with a chatbot wrapped around a cloud LLM. I'd start with a **local, deterministic Persian understanding engine** tuned tightly to this domain's actual vocabulary — because the majority of real queries ("کبیر بلخی", "یتیمان فعال سمنگان") are structured lookups in disguise, not open-ended reasoning, and this population's data is too sensitive to default to sending off-premises, and connectivity in many of these centers cannot be assumed. Get that Tier-1 engine excellent — correct, fast, fully offline, auditable — before ever touching a cloud model.

I would treat every AI-initiated write (a reminder, a flag) as provisional and visible, never silent or authoritative: staff must always see that the AI proposed it, and must be able to override it with one click, because the moment an underpaid, overworked charity worker stops trusting the assistant, they will stop using it and revert to spreadsheets.

I would build the risk/duplicate/fraud detection as **advisory-only, human-in-the-loop** from day one, resisting the temptation to auto-merge or auto-block, because a false positive against a real orphan's file is a much worse failure than a missed true positive — the cost asymmetry has to shape the design, not just the disclaimer text.

And I would only add the cloud LLM tier once the local engine is proven, gated behind an explicit per-center opt-in and routed through the server infrastructure that already exists for sync — because the right sequencing is *earn trust locally, then extend capability remotely*, not the reverse.
