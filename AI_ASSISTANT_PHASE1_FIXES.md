# AI Assistant Phase 1 — Corrections

Status: **Planning only. No code implemented.**
Purpose: resolve every issue raised in [`AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md`](AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md) and produce the corrected design that [`AI_ASSISTANT_PHASE1_SPEC.md`](AI_ASSISTANT_PHASE1_SPEC.md) should be updated to reflect. This document supersedes the schema/architecture details in that spec wherever they conflict; it does not replace the spec's implementation-order/file-list structure.

Two items below (§4, §7) were re-verified directly against the live codebase and deployed binaries during this pass, not just reasoned about — findings are stated as confirmed facts, not assumptions.

---

## 1. Remove All References to Non-Existent Schema Fields

**Confirmed against `Helpers/DatabaseInitializer.cs`:** `TblCase` has no `FullName`, `FatherName`, or `GrandFatherName` columns. `TblFamily` has no `Phone` column. The real columns are:

| Wrong (in original spec) | Correct |
|---|---|
| `TblCase.FullName` | `TblCase.HeadFullName` |
| `TblCase.FatherName` | `TblCase.HeadFatherName` |
| `TblCase.GrandFatherName` | **does not exist — drop entirely, no substitute** |
| `TblFamily.Phone` | **does not exist** — phone lookup for a family goes through the parent case (`TblCase.Phone` / `TblCase.RelativePhone`), not a per-member column |

Corrected `AiCaseSearchIndex` (replaces the version in spec §1.2):

```sql
CREATE VIRTUAL TABLE IF NOT EXISTS AiCaseSearchIndex USING fts5(
    CasID UNINDEXED,
    CenterID UNINDEXED,           -- added, see §2
    HeadFullName, HeadFatherName,
    HeadTazkiraNo, Province, District, Phone,
    tokenize = 'unicode61 remove_diacritics 2'
);
```

Triggers updated to select `NEW.HeadFullName, NEW.HeadFatherName, ..., NEW.CenterID` accordingly (full corrected trigger SQL in §2).

Corrected `AiEntities` fields: `PersonName`, `FatherName` (maps to `HeadFatherName`/`MemberFatherName` depending on target table), `TazkiraNo`, `TazkiraSuffix`, `Province`, `District`, `Phone`, `ServiceStatus`, `IsCountQuery`, `NoServiceSinceDays`, `RelativeDateExpression`, `CaseReference`. **`GrandFatherName` removed.**

`TblFamily`'s real searchable columns are `MemberName`, `MemberFatherName`, `MemberTazkiraNo` (verified) — `SearchFamilyHandler` must target these exact names.

---

## 2. Fix Multi-Center Search Architecture

Root cause (from review §1/§4): the FTS5 candidate stage had no center awareness, so a 200-row cap could be entirely consumed by rows outside the user's center before Stage 2's `CenterID` filter ever ran.

**Fix — filter at Stage 1, not just Stage 2.** `AiCaseSearchIndex` now carries `CenterID UNINDEXED` (§1), populated by corrected triggers:

```sql
CREATE TRIGGER IF NOT EXISTS Trg_TblCase_AI_Insert AFTER INSERT ON TblCase BEGIN
    INSERT INTO AiCaseSearchIndex (CasID, CenterID, HeadFullName, HeadFatherName, HeadTazkiraNo, Province, District, Phone)
    SELECT NEW.CasID, NEW.CenterID, NEW.HeadFullName, NEW.HeadFatherName, NEW.HeadTazkiraNo, NEW.Province, NEW.District, NEW.Phone;
END;
-- UPDATE / DELETE triggers mirror this, keyed on CasID, same column set.
```

`SearchCaseHandler`'s Stage 1 query becomes:

```sql
SELECT CasID FROM AiCaseSearchIndex
WHERE AiCaseSearchIndex MATCH @NormalizedQuery
  AND (@CenterFilterId = 0 OR CenterID = @CenterFilterId)   -- 0 = SuperAdmin / IsAllCenters
LIMIT 200;
```

This makes the 200-row cap a **per-visible-scope** cap, not a global one — a center-scoped user's 200 slots are never consumed by other centers' matches. `@CenterFilterId` is `SecurityContext.CenterFilterId`, the same value already used everywhere else in the app; no new authorization concept introduced.

**`SearchFamilyHandler`** (unindexed in Phase 1, per spec §6/§9 — accepted scope) must join through `TblCase` for both the name filter *and* the center filter, since `TblFamily` has no `CenterID` column of its own:

```sql
SELECT f.* FROM TblFamily f
JOIN TblCase c ON c.CasID = f.CasID
WHERE 1=1 AND c.IsArchived = 0
  AND (@CenterFilterId = 0 OR c.CenterID = @CenterFilterId)
  AND f.MemberName LIKE @Name ...
```

This was implicit before; it is now an explicit, required part of the handler's query, called out so it isn't dropped during implementation.

**Cap-exhaustion UX (also closes the review's "no narrow-down flow" gap):** if Stage 1 returns exactly 200 rows (i.e., the cap was hit) and no `Province`/`District`/`TazkiraSuffix` entity was extracted, `SearchCaseHandler` does not silently return a top-N slice — it returns a clarifying response: "بیش از ۲۰۰ نتیجه — لطفاً ولایت یا تذکره را هم بگویید." This turns the former silent-false-negative failure mode into a visible, actionable one.

---

## 3. Record Resolution Strategy: CasID vs. Code vs. FormNo

Root cause (review §7): `CasID` is an internal `AUTOINCREMENT` primary key with no user-facing meaning and no guaranteed stability across synced copies (the presence of a separate `GlobalID` column on `TblCase` is evidence of this). `Code` and `FormNo` are the two columns with `UNIQUE` constraints and are what staff actually read off paper forms/case folders.

**Resolution order when a query contains a bare number** (e.g. "پرونده ۲۴۲۴", "فورم ۲۴۲۴"), applied in this exact sequence, stopping at the first hit:

1. **`Code` exact match** (`TblCase.Code = @token`) — `Code` is `TEXT`, so this also naturally covers alphanumeric codes ("A-2424") if the org uses them.
2. **`FormNo` exact match** (`TblCase.FormNo = @token`, parsed as integer) — only attempted if `Code` matched nothing.
3. **Never resolve directly against `CasID` from free-text user input.** `CasID` is only ever used internally — e.g., when a result card's "باز کردن پرونده" button passes the already-resolved `CasID` programmatically to open `FrmCase`. A raw number typed by a user is never interpreted as `CasID`.
4. If **both** `Code` and `FormNo` match different cases (possible, since they're independent unique constraints — e.g. `Code = "2424"` on one case and `FormNo = 2424` on a different case), do not silently pick one: return both as a disambiguation choice ("دو پرونده با این شماره یافت شد — کدام‌یک؟"), exactly like the multi-match handling in §7 of this document.
5. All of the above still passes through the center filter from §2 — a `Code`/`FormNo` match outside the caller's visible center(s) is treated as **no match**, not "match, but access denied" (avoids confirming to a user that a specific code exists in another center at all).

This resolution logic lives in a shared helper (`AI/CaseReferenceResolver.cs`, new — not previously listed in the spec's service table) used by both `SearchCaseHandler` (when a number appears alongside search intent) and `CreateReminderHandler` (when linking a reminder to a case).

---

## 4. SQLite FTS5 Availability — Verified

Review §3 flagged this as unverified and potentially fatal. **It has now been directly verified against the deployed binaries**, not inferred from the package version alone:

- `packages.config` pins `System.Data.SQLite.Core` / `Stub.System.Data.SQLite.Core.NetFramework` at **1.0.115.5**.
- Binary inspection of all four deployed `SQLite.Interop.dll` variants (`bin/Debug/x86`, `bin/Release/x86`, `bin/x64/Debug/x64`, `bin/x64/Release/x64`) confirms the FTS5 extension **is compiled in** — the interop DLLs contain the real FTS5 entry-point symbols `fts5_init`, `fts5_api_ptr`, `fts5_source_id`, and `fts5vocab` in every build/architecture combination the app ships.

**Conclusion: FTS5 is available in every configuration this app is built and deployed in. No `LIKE`-based fallback architecture is needed for Phase 1.** The review's 🔴 rating on this item is resolved — downgrade to informational.

**Residual, cheap safeguard (recommended, not required):** wrap the one-time `CREATE VIRTUAL TABLE ... USING fts5(...)` call in `AiInitializer.cs` in a try/catch that logs a clear, specific error ("FTS5 unavailable in this SQLite build") via the existing `ErrorLogger` rather than letting a raw `SQLiteException` surface from the shared startup sequence — this protects only against a *future* SQLite package downgrade, not the current, verified-good state.

---

## 5. Persian Digit Normalization

Root cause (review §5/§6): `PersianNormalizer` as specified handled ك/ي unification, diacritics, and spacing, but never converted Persian/Arabic-Indic digits to ASCII — silently breaking every tazkira-suffix, phone, and date extraction for the (default, on most Persian keyboards) Persian-numeral input mode.

**Fix — add as the first normalization step, before tokenization:**

```csharp
private static readonly Dictionary<char, char> DigitMap = new Dictionary<char, char> {
    {'۰','0'},{'۱','1'},{'۲','2'},{'۳','3'},{'۴','4'},{'۵','5'},{'۶','6'},{'۷','7'},{'۸','8'},{'۹','9'}, // Persian
    {'٠','0'},{'١','1'},{'٢','2'},{'٣','3'},{'٤','4'},{'٥','5'},{'٦','6'},{'٧','7'},{'٨','8'},{'٩','9'}  // Arabic-Indic
};
```

Applied character-by-character over the raw input as the very first step of `PersianNormalizer.Normalize(...)`, before ك/ي unification and space normalization. All downstream regex patterns (`TazkiraSuffix`, phone, `ReminderDateParser`'s absolute-date branch) operate only on the normalized (ASCII-digit) string — they never need their own digit-handling logic, keeping the fix in exactly one place.

`AiMessage.MessageText` and `AiIntentLog.RawQuery` continue to store the **original, un-normalized** input (for faithful audit/history) — normalization is applied to a working copy used only for parsing.

---

## 6. Confidence Scoring Rules

Root cause (review §6): `PersianNluEngine` was specified to return a `Confidence` double with no defined formula — the load-bearing "ask instead of guess" mechanism was unimplementable as written.

**Fix — concrete, additive scoring model**, computed per candidate intent, highest score wins:

| Signal | Points |
|---|---|
| Intent trigger keyword/phrase matched (e.g. "چند" for count, "یادآوری"/relative-date phrase for reminder) | +0.40 |
| Each distinct entity successfully extracted and *relevant to this intent* (name, tazkira, province, district, phone, resolved date) | +0.15 each, capped at +0.45 total |
| Case/family reference resolved unambiguously via §3's resolver (only relevant to `Command.CreateReminder`) | +0.15 |
| Penalty: more than one case/family candidate remains unresolved after §3 (ambiguous reference) | −0.25 |
| Penalty: `Command.CreateReminder` intent matched but `ReminderDateParser` produced no valid future `DateTime` | −0.50 (effectively forces clarification — see below) |

**Thresholds** (score clamped to `[0, 1]`):

- **≥ 0.70** → answer directly.
- **0.40 – 0.69** → answer, but append a refine/confirm prompt (e.g., show results *and* "آیا منظور همین بود؟").
- **< 0.40** → do not answer or write anything; return a clarifying question naming what's missing (e.g., "چه زمانی؟" if a reminder's date didn't resolve).

**Hard override, independent of score:** `Command.CreateReminder` **never** writes to `TblReminder` unless `ReminderDateParser` returned a valid, resolved, future `DateTime` — regardless of overall confidence score. This closes the review's "guessed defaults written without confirmation" gap at the rule level, not just the scoring level: a missing/invalid date is a hard stop, not a low-confidence-but-proceeding case.

This formula and its thresholds are simple enough to unit-test directly (fixed inputs → expected score), which the original spec's QA step (§10, step 17) did not have a concrete mechanism for.

---

## 7. Sync Behavior for All AI Tables

Root cause (review §3/§4/§6-of-"100k" section): the spec never stated whether `AiConversation`, `AiMessage`, `AiIntentLog`, `AiCaseSearchIndex`, or the two new `TblReminder` columns participate in the `Sync/` module.

**Verified against `Sync/SyncEngine.cs` and `Sync/SyncComparer.cs`:** the sync module is an **explicit whitelist**, not a generic "sync every table" mechanism — it only ever touches `TblCase` (referred to internally as "Guardians") and `TblFamily` ("Members"). `TblReminder` itself is confirmed **not** in this whitelist — reminders, AI-created or manual, are already local-only per machine; this is existing behavior Phase 1 does not change.

**Decision: none of the new AI tables are added to the sync whitelist.** `AiConversation`, `AiMessage`, `AiIntentLog`, and `AiCaseSearchIndex` remain strictly local to the machine that generated them. This is consistent with the existing precedent (`TblReminder`), requires no new code in `Sync/` (staying off the whitelist is the default — nothing to build), and directly avoids the review's flagged risk of beneficiary search history (names, tazkira digits, phone numbers typed as free text) silently replicating to every other center and the central SyncServer.

**Made explicit, not implicit:** `AiInitializer.cs` includes a one-line comment at each new table's `CREATE TABLE` stating this is intentional (`-- Local-only by design. Do NOT add to Sync/SyncComparer.cs's table list. See AI_ASSISTANT_PHASE1_FIXES.md §7.`) — closing the review's "silence" complaint by turning the decision into a durable, discoverable artifact rather than tribal knowledge.

**Consequence, accepted as Phase 1 scope:** cross-center or head-office analytics over `AiIntentLog`/`AiRiskFlag`-style data (Phase 2+) will require a *separate*, explicit aggregation/export mechanism later — not a byproduct of the existing sync pipeline. This is a Phase 2+ design question, not a Phase 1 gap.

---

## 8. Retention Policy for AI Logs

Root cause (review §2/"100k beneficiaries" section): `AiIntentLog` and `AiMessage`/`AiConversation` had no purge policy, growing unboundedly and inflating the whole-file `AutoBackups`/`SyncBackups` that already back up `CaseDB.sqlite` wholesale.

**Fix — fixed rolling retention window, enforced on startup:**

```sql
DELETE FROM AiMessage
WHERE ConversationID IN (
    SELECT ConversationID FROM AiConversation WHERE LastMessageAt < datetime('now', '-180 days')
);
DELETE FROM AiConversation WHERE LastMessageAt < datetime('now', '-180 days');
DELETE FROM AiIntentLog WHERE CreatedAt < datetime('now', '-180 days');
```

- **180 days** chosen to comfortably cover any plausible "what did I search last month" recall need while bounding growth; not a magic number — should be confirmed with the actual charity org's audit/record-keeping requirements before Phase 1 ships (a config value, `SettingsHelper`-backed, not a hardcoded literal, so it can be adjusted without a code change).
- Runs once per app start, inside `AiInitializer.cs`, immediately after schema creation/backfill — same lifecycle slot as the existing idempotent migration checks, so no new background timer/service is needed in Phase 1 (a dedicated `AiBackgroundScanner` timer is Phase 2 scope per the master plan; Phase 1 doesn't need it just for retention).
- `TblReminder.SourceQueryText` is **not** subject to this retention policy — it's tied to the reminder's own lifecycle (kept as long as the reminder row exists, deleted only if/when the reminder itself is deleted), which is separate, low-volume, and already governed by however `TblReminder` rows are currently retired.
- **`VACUUM`** is explicitly out of scope for Phase 1: SQLite doesn't reclaim file space from `DELETE`s automatically, so file size won't shrink even with retention in place — only growth is bounded. Scheduling periodic `VACUUM` is an operational/maintenance decision (likely alongside the existing `AutoBackups` job), not part of this feature, and is called out here so it isn't silently assumed solved.

---

## 9. Transaction Strategy for Reminder Creation

Root cause (review §3/§7): the original spec implied a single all-or-nothing transaction across parse → resolve → insert `TblReminder` → `AuditLogger.Log`. **Verified against `Helpers/AuditLogger.cs`: `AuditLogger.Log(...)` always opens its own new `SQLiteConnection` internally (`new DatabaseHelper().GetConnection()`) — it does not accept an external connection or transaction.** True single-transaction atomicity across the reminder insert *and* the audit-log write is therefore **not achievable without modifying `AuditLogger.cs` itself**, which every other module in the app currently calls unchanged — doing so would violate the project's minimal-diff/"never rewrite working code" rule for a shared, working component, for the benefit of one new feature.

**Corrected, honest strategy — two units, ordered, with a cheap reconciliation check instead of false atomicity:**

1. **Unit 1 (real DB transaction, single connection):** case-reference resolution (read-only) + the `TblReminder` INSERT, wrapped in one `SQLiteTransaction` on the AI module's own connection. This is already atomic by nature (SQLite auto-commits a single `INSERT` statement), so this "transaction" mainly matters if resolution ever needs to write anything in future phases — kept for that forward-compatibility, not because today's single INSERT needs it.
2. **Unit 2 (separate, by necessity):** `AuditLogger.Log("AI:CreateReminder", ...)`, called **immediately** after Unit 1 commits successfully — minimizing, not eliminating, the crash window between the two writes.
3. **Accepted residual risk:** a crash in the narrow window between Unit 1's commit and Unit 2's write leaves a `TblReminder` row with `CreatedByAI = 1` and no matching `TblAuditLog` entry. This is treated as a **detectable, reconcilable** inconsistency, not a correctness invariant that must never break: a lightweight startup check —
   ```sql
   SELECT ReminderID FROM TblReminder
   WHERE CreatedByAI = 1
     AND ReminderID NOT IN (SELECT EntityID FROM TblAuditLog WHERE EntityName = 'TblReminder' AND Operation = 'AI:CreateReminder');
   ```
   — run at the same startup point as §8's retention job, logs any orphaned rows to `EntErrorLog` for later review. This gives visibility into the rare failure case without pretending a false guarantee exists.

This is a materially different (and more honest) answer than the original review's "wrap it in a transaction" recommendation — that fix was not achievable as stated once `AuditLogger`'s real implementation is accounted for.

---

## 10. Re-Estimated Development Effort

The corrections above are **not polish — they close real correctness and safety gaps** (multi-center data integrity, a functional prerequisite for all numeric parsing, the core guess-vs-ask safety mechanism, and record-identity correctness for writes). Effort increases accordingly:

| Area | Original (spec §10) | Delta | Corrected |
|---|---|---|---|
| Schema + permissions | 2.25 | +0.5 (`CenterID` on FTS5 table + triggers; family-search center-safe join) | 2.75 |
| NLU core | 7.5 | +0.5 (digit normalization) +1.5 (confidence scoring formula, thresholds, hard-override rule, unit-testable) | 9.5 |
| Intent handlers | 5.5 | +1.5 (`CaseReferenceResolver`: Code/FormNo resolution + disambiguation UX, used by both search and reminder handlers) | 7.0 |
| Orchestration + UI | 4.5 | +0.25 (cap-exhaustion "narrow it down" response path) | 4.75 |
| Data lifecycle (new) | 0 | +0.5 (retention job, §8) +0.25 (sync-exclusion guard comments/verification, §7) +0.75 (reminder/audit reconciliation check, §9) | 1.5 |
| QA / regression | 2.0 | +1.0 (test cases for: Persian-numeral input, ambiguous Code/FormNo, cross-center probe queries, cap-exhaustion response, confidence-threshold boundaries) | 3.0 |
| FTS5 fallback contingency | *(implicit risk buffer)* | −0 (removed — availability confirmed in §4, no fallback path needed) | 0 |
| **Total** | **18.75** | **+6.75** | **≈ 25.5 developer-days** |

**Revised estimate: ~25–27 developer-days** (25.5 midpoint, rounded up slightly for integration friction between the newly-added `CaseReferenceResolver` and the two handlers that both depend on it). This is a **+36% increase** over the original 18.75-day estimate — the honest cost of fixing three factual spec bugs and four previously-undefined behaviors before, rather than after, they reach production.

---

*Corrections complete. No implementation performed. `AI_ASSISTANT_PHASE1_SPEC.md` should be revised to match §1–§9 before coding begins.*
