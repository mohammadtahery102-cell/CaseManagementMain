# AI Assistant — Phase 1 (MVP) Implementation Spec

Status: **Planning only. No code implemented.**
Parent document: [`AI_ASSISTANT_MASTER_PLAN.md`](AI_ASSISTANT_MASTER_PLAN.md) — this spec expands that plan's Phase 0 (Foundation) + Phase 1 (MVP) into exact, buildable detail.

Every SQL statement, method signature, and pattern below is written to match what's **already in the codebase verbatim** (verified against `Helpers/DatabaseInitializer.cs`, `Helpers/CenterGuard.cs`, `Enterprise/PermissionService.cs`, `Enterprise/EnterpriseInitializer.cs`, `Helpers/AuditLogger.cs`, `FrmAdvancedSearch.cs`, `FrmDashboard.cs`) — not invented conventions.

## Hard constraints for this phase

- **Fully offline.** No `CloudLlmBridge`, no `PiiRedactionService`, no network call of any kind. Everything in this spec runs against the local `CaseDB.sqlite` only.
- **Persian RTL.** All new forms use the exact same `RightToLeft.Yes` / `RightToLeftLayout` pattern already used across the app.
- **No new NuGet dependencies.** SQLite FTS5 is native to the already-referenced `System.Data.SQLite`; everything else is plain C#.
- **Production safe.** All schema changes are additive (`CREATE TABLE IF NOT EXISTS` / idempotent `EnsureColumn`), all queries parameterized, no existing table/column renamed, no existing method rewritten.
- **Compatible with existing SQLite architecture.** New tables live in a new `AiInitializer.cs` called from the same startup sequence as `EnterpriseInitializer`; the one existing-table change (`TblReminder`) is done via the existing private `EnsureColumn` helper, in the same file it already lives in.

---

## 1. Database Changes

### 1.1 Extend `TblReminder` (existing table — additive only)

Added inside `Helpers/DatabaseInitializer.cs`, in the same block that already calls `EnsureColumn` for `TblReminder`-related migrations:

```csharp
EnsureColumn(con, "TblReminder", "CreatedByAI",     "INTEGER NOT NULL DEFAULT 0");
EnsureColumn(con, "TblReminder", "SourceQueryText",  "TEXT NULL");
```

Resulting effective schema (unchanged columns untouched):

```sql
CREATE TABLE TblReminder (
    ReminderID      INTEGER PRIMARY KEY AUTOINCREMENT,
    Title           TEXT NOT NULL,
    Note            TEXT NULL,
    RemindAt        TEXT NOT NULL,          -- "yyyy-MM-dd HH:mm", unchanged format
    IsDone          INTEGER NOT NULL DEFAULT 0,
    IsNotified      INTEGER NOT NULL DEFAULT 0,
    CenterID        INTEGER NULL,
    CreatedBy       TEXT NULL,
    CreatedAt       TEXT NOT NULL DEFAULT (datetime('now')),
    CreatedByAI     INTEGER NOT NULL DEFAULT 0,   -- NEW
    SourceQueryText TEXT NULL                     -- NEW
);
```

No other existing table is touched in Phase 1.

### 1.2 New file: `Helpers/AiInitializer.cs`

Called once from the same place `EnterpriseInitializer.Initialize(con)` is currently called (app startup, after core schema is ready). Uses the same `CREATE TABLE IF NOT EXISTS` idiom as every other initializer — no `EnsureColumn` needed since these are brand-new tables.

```sql
CREATE TABLE IF NOT EXISTS AiConversation (
    ConversationID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID         INTEGER NULL,
    CenterID       INTEGER NULL,
    Title          TEXT NULL,
    StartedAt      TEXT NOT NULL DEFAULT (datetime('now')),
    LastMessageAt  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS AiMessage (
    MessageID      INTEGER PRIMARY KEY AUTOINCREMENT,
    ConversationID INTEGER NOT NULL REFERENCES AiConversation(ConversationID) ON DELETE CASCADE,
    Sender         TEXT NOT NULL,        -- 'User' | 'Assistant'
    MessageText    TEXT NOT NULL,
    IntentDetected TEXT NULL,            -- e.g. 'Search.Case'
    EntitiesJson   TEXT NULL,
    Confidence     REAL NULL,
    ExecutionMs    INTEGER NULL,
    CreatedAt      TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS AiIntentLog (
    IntentLogID  INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID       INTEGER NULL,
    CenterID     INTEGER NULL,
    RawQuery     TEXT NOT NULL,
    ParsedIntent TEXT NULL,
    Confidence   REAL NULL,
    Success      INTEGER NOT NULL,    -- 0/1
    ErrorNote    TEXT NULL,
    CreatedAt    TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Full-text search index over case + family identity fields (see Section 6)
CREATE VIRTUAL TABLE IF NOT EXISTS AiCaseSearchIndex USING fts5(
    CasID UNINDEXED,
    FullName, FatherName, GrandFatherName,
    HeadTazkiraNo, Province, District, Phone,
    tokenize = 'unicode61 remove_diacritics 2'
);
```

Triggers (also in `AiInitializer.cs`) keep the index in sync automatically — no rebuild job needed in Phase 1:

```sql
CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Insert AFTER INSERT ON TblCase BEGIN
    INSERT INTO AiCaseSearchIndex (CasID, FullName, FatherName, GrandFatherName, HeadTazkiraNo, Province, District, Phone)
    SELECT NEW.CasID, NEW.FullName, NEW.FatherName, NEW.GrandFatherName, NEW.HeadTazkiraNo, NEW.Province, NEW.District, NEW.Phone;
END;

CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Update AFTER UPDATE ON TblCase BEGIN
    DELETE FROM AiCaseSearchIndex WHERE CasID = OLD.CasID;
    INSERT INTO AiCaseSearchIndex (CasID, FullName, FatherName, GrandFatherName, HeadTazkiraNo, Province, District, Phone)
    SELECT NEW.CasID, NEW.FullName, NEW.FatherName, NEW.GrandFatherName, NEW.HeadTazkiraNo, NEW.Province, NEW.District, NEW.Phone;
END;

CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Delete AFTER DELETE ON TblCase BEGIN
    DELETE FROM AiCaseSearchIndex WHERE CasID = OLD.CasID;
END;
```

> Note: exact `TblCase` column names for name fields (`FullName`/`FatherName`/`GrandFatherName`/`Phone`) must be confirmed against the live schema before coding — the survey above returned the truncated `CREATE TABLE` (35 columns, only key ones shown). This is a one-line lookup against `DatabaseInitializer.cs` at implementation time, not a design change.

A one-time backfill (`INSERT INTO AiCaseSearchIndex SELECT ... FROM TblCase`) runs inside `AiInitializer.cs` guarded by `SELECT COUNT(*) FROM AiCaseSearchIndex` — only populates if empty, so it's safe to run on every startup.

---

## 2. New Tables (summary)

`AiConversation`, `AiMessage`, `AiIntentLog`, `AiCaseSearchIndex` (FTS5) — that's it for Phase 1. `AiRiskFlag`, `AiInsightCache`, `AiFeedback` from the master plan are **not** created in Phase 1 (they belong to Phase 2/4 and have no consumer yet).

---

## 3. New Services

All new files under a new `AI/` folder, matching the existing `Enterprise/`, `Sync/` module-folder convention.

| File | Responsibility |
|---|---|
| `AI/PersianNormalizer.cs` | Static helper: unify ك→ک, ي→ی, strip diacritics/tatweel, normalize space/half-space/ZWNJ, trim honorifics. Used by both NLU and the FTS5 triggers' input path (query-time only — data is stored as-is, normalization happens when building the FTS `MATCH` query). |
| `AI/AiEntities.cs` | POCO: `PersonName`, `FatherName`, `GrandFatherName`, `TazkiraNo`, `TazkiraSuffix`, `Province`, `District`, `Phone`, `ServiceStatus`, `IsCountQuery`, `NoServiceSinceDays`, `RelativeDateExpression`, `CaseReference`. |
| `AI/AiIntent.cs` | `const string` keys: `Search.Case`, `Search.Family`, `Query.NoRecentService`, `Command.CreateReminder`. |
| `AI/PersianNluEngine.cs` | Tokenizes normalized input, scores against per-intent keyword/pattern sets, extracts `AiEntities`. Returns `(string Intent, AiEntities Entities, double Confidence)`. |
| `AI/ReminderDateParser.cs` | Converts Persian relative/absolute date phrases into a `DateTime` matching `TblReminder.RemindAt`'s `"yyyy-MM-dd HH:mm"` format. See Section 7. |
| `AI/AiResponse.cs` | POCO: `ResponseText`, `List<AiResultItem> Results`, `Intent`, `Confidence`. `AiResultItem`: `EntityType` ('Case'/'Family'), `EntityId`, `DisplayTitle`, `DisplaySubtitle`. |
| `AI/IAiIntentHandler.cs` | `AiResponse Handle(AiEntities entities, string rawQuery);` |
| `AI/Handlers/SearchCaseHandler.cs` | Implements `Search.Case` (incl. count-style queries — see Section 6). |
| `AI/Handlers/SearchFamilyHandler.cs` | Implements `Search.Family`. |
| `AI/Handlers/NoRecentServiceHandler.cs` | Implements `Query.NoRecentService`. |
| `AI/Handlers/CreateReminderHandler.cs` | Implements `Command.CreateReminder`. See Section 7. |
| `AI/AiOrchestrator.cs` | `AiResponse Handle(string rawText)`: permission check → NLU → dispatch → log → return. Owns `AiConversation`/`AiMessage`/`AiIntentLog` persistence. |
| `AI/AiPermissionGate.cs` | Thin wrapper: `EnsureCanSearch()`, `EnsureCanCreateReminder()`, each calling `PermissionService.Require("AI.Search" / "AI.Reminders.Create")`. |
| `AI/AiAuditLogger.cs` | `LogReminderCreated(int reminderId, string title, string remindAt, string rawQuery)` → calls existing `AuditLogger.Log("AI:CreateReminder", "TblReminder", reminderId, null, "Title=...; RemindAt=...; SourceQuery=...")`. |
| `Helpers/AiInitializer.cs` | Schema creation (Section 1.2), called from startup. |

No `RiskDetectionEngine`, `DataQualityEngine`, `SystemHealthAnalyzer`, `CloudLlmBridge`, or `PiiRedactionService` in Phase 1 — explicitly deferred (Section 9).

---

## 4. New Forms

| Form/Control | Purpose |
|---|---|
| `FrmAiAssistant.cs` | Main chat window. Modal, opened from `FrmDashboard`. |
| `AI/AiResultCard.cs` (UserControl) | One result row (case or family), embedded inside the chat log. |

No `FrmAiRiskDashboard`, `FrmAiSystemHealth`, `FrmAiAdminInsights`, `FrmAiSettings` in Phase 1 — those depend on tables/engines not built yet.

---

## 5. New Permissions

Two new `AddPermission(...)` calls added to the existing seed list inside `Enterprise/EnterpriseInitializer.cs`, using the method already defined there — no new permission engine, no new tables:

```csharp
AddPermission(con, "AI.Search",           "جستجوی هوشمند (زبان طبیعی)", "دستیار هوشمند", 610, true, true, true);
AddPermission(con, "AI.Reminders.Create", "ایجاد یادآوری با دستور طبیعی", "دستیار هوشمند", 611, true, true, false);
```

(`AddPermission(con, key, name, category, sortOrder, admin, operatorRole, viewer)` — `SuperAdmin` is always granted internally by the method.)

| Key | Viewer | Operator | Admin | SuperAdmin |
|---|---|---|---|---|
| `AI.Search` | ✅ | ✅ | ✅ | ✅ |
| `AI.Reminders.Create` | ❌ | ✅ | ✅ | ✅ |

`Manager` role: not present as a distinct role in the current `EntPermission`/role model (roles seen in `EnterpriseInitializer.cs` are `SuperAdmin`/`Admin`/`Operator`/`Viewer`) — the master plan's `Manager` tier collapses into `Admin` for Phase 1 until/unless a real `Manager` role exists in `TblUsers.Role`. No new role is invented in this phase.

Enforcement: `AiOrchestrator.Handle(...)` calls `AiPermissionGate.EnsureCanSearch()` before running NLU at all, and `CreateReminderHandler` calls `AiPermissionGate.EnsureCanCreateReminder()` before any write — both throw the same way `PermissionService.Require(...)` already does elsewhere, so the existing permission-denied UX is reused unchanged.

---

## 6. Search Architecture

Two-stage, both stages fully local:

**Stage 1 — candidate narrowing (FTS5).** For any query containing a name-like token, `SearchCaseHandler` runs:
```sql
SELECT CasID FROM AiCaseSearchIndex WHERE AiCaseSearchIndex MATCH @NormalizedQuery LIMIT 200;
```
using `PersianNormalizer` on the query text first (so ي/ك variants and half-space differences don't cause misses). This replaces a full unindexed `LIKE '%...%'` scan.

**Stage 2 — structured filtering (reuse existing pattern).** `FrmAdvancedSearch.cs`'s `AddLikeFilter`/`AddExactFilter` are private instance methods on a `Form`, so they can't be called directly from a service class — Phase 1 does **not** touch or duplicate `FrmAdvancedSearch` (per the "never rewrite working code" rule). Instead, `SearchCaseHandler` implements the *identical* pattern as its own small private helpers, applied to the Stage-1 candidate `CasID` set plus the entities extracted by `PersianNluEngine`:

```csharp
var sql = new StringBuilder("SELECT * FROM TblCase WHERE 1=1 AND IsArchived = 0");
var cmd = new SQLiteCommand(...);
if (candidateIds != null) sql.Append(" AND CasID IN (" + string.Join(",", candidateIds) + ")");
AddLikeFilter(sql, cmd, "Province", "@Province", entities.Province);
AddLikeFilter(sql, cmd, "District", "@District", entities.District);
AddExactFilter(sql, cmd, "HeadTazkiraNo", "@Tazkira", entities.TazkiraNo);
AddExactFilter(sql, cmd, "ServiceStatus", "@Status", entities.ServiceStatus);
int cid = SecurityContext.CenterFilterId;
if (cid != 0) sql.Append(" AND CenterID = @CenterID");
```

This is deliberately the same `WHERE 1=1` + parameterized-append idiom `FrmAdvancedSearch` already uses, so any future maintainer reading either file recognizes the pattern immediately.

**Tazkira-suffix matching** ("کبیر که آخر تذکره‌اش 54 باشد"): `PersianNluEngine` extracts a `TazkiraSuffix` entity (digits following "آخر"/"آخرش"/"آخر تذکره") and `SearchCaseHandler` applies `AND HeadTazkiraNo LIKE @Suffix` with `@Suffix = "%54"` — a suffix-anchored `LIKE`, not a full scan, since it's combined with the FTS5 name pre-filter whenever a name is also present.

**Count-style queries** ("در ولایت بلخ چند یتیم فعال داریم؟"): `PersianNluEngine` sets `entities.IsCountQuery = true` when the query starts with/contains "چند". `SearchCaseHandler` then runs the same filtered query wrapped in `SELECT COUNT(*)` instead of `SELECT *`, and `AiOrchestrator` renders a plain-text count answer instead of result cards.

**Center scoping**: identical to `FrmAdvancedSearch` — `SecurityContext.CenterFilterId` (0 = SuperAdmin sees all centers, otherwise the user's own `CenterID`) appended to every query. No AI query path bypasses this.

`SearchFamilyHandler` follows the same two-stage approach against `TblFamily` (joined to `TblCase` for `CenterID`), using `MemberName`/tazkira/phone columns.

---

## 7. Reminder Architecture

**`ReminderDateParser`** — pattern set covering the spec's examples and their natural variants:

| Persian phrase pattern | Resolves to |
|---|---|
| "سه روز دیگر", "۳ روز دیگر" | `DateTime.Today.AddDays(3)`, default time 09:00 |
| "یک هفته بعد", "هفته بعد" | `+7 days` |
| "دو هفته دیگر" | `+14 days` |
| "فردا" | `+1 day` |
| "امروز" | today, default 17:00 (end of day) |
| absolute Jalali date, e.g. "1404/06/10" | converted via existing Jalali↔Gregorian conversion already used elsewhere in the app (reused, not reimplemented) |
| no date phrase found | handler returns a clarifying question instead of guessing (`AiResponse` with `ResponseText = "چه زمانی؟"`, no write) |

Number words ("سه", "یک", "دو") are parsed via a small fixed Persian-number-word dictionary (1–31 covers all realistic reminder horizons) — no general number-parsing library needed.

**`CreateReminderHandler` flow:**

1. `ReminderDateParser` resolves `entities.RelativeDateExpression` → `DateTime remindAt`. If it can't resolve with reasonable confidence, return a clarifying question — **never guess and silently create a wrong-dated reminder.**
2. If the query also names a case/family ("پرونده ۲۴۲۴", "خانواده احمدی"), reuse `PersianNluEngine`'s entity extraction + `SearchCaseHandler`'s lookup to resolve a `CasID`; if ambiguous (multiple matches), ask which one instead of picking the first.
3. Build `Title` from the query (e.g. "بررسی پرونده 2424" / "تماس با خانواده احمدی" / "مدارک پرونده 321 ناقص است") — a short deterministic template per intent sub-pattern, not free text summarization.
4. Insert into `TblReminder`:
   ```sql
   INSERT INTO TblReminder (Title, Note, RemindAt, IsDone, IsNotified, CenterID, CreatedBy, CreatedByAI, SourceQueryText)
   VALUES (@Title, @Note, @RemindAt, 0, 0, @CenterID, @CreatedBy, 1, @RawQuery);
   ```
   `@CenterID = SecurityContext.CurrentCenterId`, `@CreatedBy = current username` (matching the existing `TEXT`-typed `CreatedBy` column — not a `TblUsers` FK, consistent with current schema).
5. Call `AiAuditLogger.LogReminderCreated(...)`.
6. Return a confirmation `AiResponse` ("یادآوری برای ۲۵ آب ساعت ۰۹:۰۰ ثبت شد.") with a result card linking to the reminder / the referenced case.

**Notification path — zero changes required.** `FrmDashboard.StartReminderTimer()` / `CheckDueReminders()` already polls `TblReminder WHERE IsDone = 0 AND IsNotified = 0 AND RemindAt <= now`, with no filter on how the row was created. An AI-created reminder is picked up and shown exactly like a manually created one the next time the timer ticks. The only UI addition (Section 8) is showing a small "🤖" badge on reminders where `CreatedByAI = 1`, for trust/transparency — purely cosmetic, no behavior change.

---

## 8. UI Mockup Descriptions

### `FrmAiAssistant`

- Form-level: `RightToLeft = RightToLeft.Yes; RightToLeftLayout = false;` (matches every other form in the app).
- Size ≈ 480×640, `FormBorderStyle = FixedDialog` or `Sizable` (consistent with other utility dialogs like `FrmAdvancedSearch`), opened via `ShowDialog(this)` from `FrmDashboard`.
- Layout, top to bottom:
  - **Header panel** (Dock=Top, ~40px): "دستیار هوشمند" title, small subtitle "آفلاین — بدون اتصال به اینترنت" (reinforces the offline guarantee to the user).
  - **Chat log** (Dock=Fill): a `Panel` with `AutoScroll = true` containing a `FlowLayoutPanel` (`FlowDirection.TopDown`, `WrapContents = false`, `RightToLeft = Yes`, `RightToLeftLayout = true` — opts in individually, matching the existing `TabControl` RTL pattern). Each turn is a rounded bubble `Panel`: user messages right-aligned with accent background, assistant messages left-aligned with neutral background. Assistant turns that return results append one or more `AiResultCard` controls directly below the text bubble, inside the same flow panel.
  - **Input panel** (Dock=Bottom, ~48px): RTL `TextBox` (placeholder text: "برای مثال: کبیر بلخی یا سه روز دیگر پرونده 2424 را بررسی کن") + "ارسال" `Button`, Enter-key submits.
- Empty/first-open state: a few example-query chips (tappable) showing sample phrasings from this spec's in-scope intents, so non-technical staff aren't staring at a blank box.

### `AiResultCard` (UserControl)

- Compact card, ~440×72, RTL layout.
- Top line: bold name (`FullName` or `MemberName`), right-aligned.
- Second line, smaller/gray: father's name · tazkira · province/district (whichever entities matched), e.g. "پدر: X · تذکره: ...54 · بلخ".
- Right side: status pill (رنگ سبز/خاکستری for فعال/غیرفعال), matching whatever status-pill styling already exists on `FrmDashboard`/case list views.
- Bottom-right: "باز کردن پرونده" `Button` → `using (var frm = new FrmCase(caseId)) frm.ShowDialog(this);` (or `FrmFamily` for family results) — same instantiation pattern already used elsewhere (e.g. `FrmDashboard`'s advanced-search button).
- If the card represents a reminder confirmation instead of a case/family, it shows the reminder title/time and a small "🤖 ایجادشده توسط دستیار" badge instead of an "open" button.

### `FrmDashboard` entry point

One additional line in the existing toolbar-button list:
```csharp
toolButtons.Controls.Add(CreateToolButton("دستیار هوشمند", "🤖",
    delegate { using (var frm = new FrmAiAssistant()) frm.ShowDialog(this); }));
```
Placed next to the existing "جستجوی پیشرفته" button so the two search entry points sit together.

---

## 9. Explicitly Out of Scope for Phase 1

(Carried over from the master plan, restated here so implementation doesn't drift):

- Cloud LLM / `CloudLlmBridge` / `PiiRedactionService` — Phase 4.
- `RiskDetectionEngine`, `DataQualityEngine`, duplicate-beneficiary detection, "پرونده‌های نیاز به بررسی" — Phase 2.
- `FinancialHistoryHandler` ("آخرین کمک‌های نقدی خانواده احمدی") — Phase 3.
- `SystemHealthAnalyzer`, `AdminRecommendationEngine`, `AiBugSuggestionEngine` — Phase 3/4.
- `AiFeedback` loop, `AiRiskFlag`, `AiInsightCache` tables — not created in Phase 1 (no consumer yet).
- Voice input, messaging-bot bridges — Phase 5.

---

## 10. Exact Implementation Order

| # | Step | Depends on | Est. days |
|---|---|---|---|
| 1 | `Helpers/AiInitializer.cs`: new tables + FTS5 index + triggers + one-time backfill; wire into startup sequence next to `EnterpriseInitializer.Initialize` | — | 1.5 |
| 2 | `TblReminder` `EnsureColumn` additions in `DatabaseInitializer.cs` | — | 0.25 |
| 3 | Seed `AI.Search` / `AI.Reminders.Create` in `EnterpriseInitializer.cs`; confirm `PermissionService.HasPermission` resolves correctly for all 4 roles | 1, 2 (schema must exist first) | 0.5 |
| 4 | `AI/PersianNormalizer.cs` | — | 1 |
| 5 | `AI/AiEntities.cs`, `AI/AiIntent.cs`, `AI/AiResponse.cs`, `AI/IAiIntentHandler.cs` (data contracts) | 4 | 0.5 |
| 6 | `AI/PersianNluEngine.cs` (tokenizer, intent classifier, entity extraction incl. tazkira-suffix + count-query detection) | 4, 5 | 3.5 |
| 7 | `AI/ReminderDateParser.cs` | 4 | 2 |
| 8 | `AI/AiPermissionGate.cs`, `AI/AiAuditLogger.cs` | 3 | 0.5 |
| 9 | `AI/Handlers/SearchCaseHandler.cs` (FTS5 stage 1 + filter stage 2 + count-query branch) | 1, 6 | 2 |
| 10 | `AI/Handlers/SearchFamilyHandler.cs` | 9 (shares helpers) | 1 |
| 11 | `AI/Handlers/NoRecentServiceHandler.cs` | 9 | 1 |
| 12 | `AI/Handlers/CreateReminderHandler.cs` | 7, 8, 9 (case-reference resolution) | 1.5 |
| 13 | `AI/AiOrchestrator.cs` (ties 6–12 together, persists `AiConversation`/`AiMessage`/`AiIntentLog`) | 6–12 | 1 |
| 14 | `AI/AiResultCard.cs` UserControl | — | 1 |
| 15 | `FrmAiAssistant.cs` | 13, 14 | 2 |
| 16 | `FrmDashboard.cs` toolbar entry point + reminder "🤖" badge | 15 | 0.5 |
| 17 | End-to-end manual QA against every in-scope example query; regression pass confirming existing search/reminder/dashboard behavior is unaffected (per `CLAUDE.md`) | all | 2 |

**Total: ~18.75 developer-days** (matches the master plan's combined Phase 0 + Phase 1 estimate of 16–22 days).

---

## 11. Development Effort (rollup)

| Category | Days |
|---|---|
| Schema + permissions (steps 1–3) | 2.25 |
| NLU core (steps 4–8) | 7.5 |
| Intent handlers (steps 9–12) | 5.5 |
| Orchestration + UI (steps 13–16) | 4.5 |
| QA / regression (step 17) | 2 |
| **Total** | **~18.75 days** |

Add ~1–2 days of buffer if real staff phrasing during step 17 reveals NLU gaps not covered by the initial pattern set (expected and budgeted-for in the master plan, not a sign of a flawed design).
