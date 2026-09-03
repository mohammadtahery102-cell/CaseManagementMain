# Architect Review — AI Assistant Phase 1 Spec

Status: **Review only. No code implemented. No changes made to the spec.**
Reviewed document: [`AI_ASSISTANT_PHASE1_SPEC.md`](AI_ASSISTANT_PHASE1_SPEC.md)
Reviewer stance: senior architect, pre-implementation gate review.

Every finding below was checked against the live schema (`Helpers/DatabaseInitializer.cs`) and connection layer (`DAL/DatabaseHelper.cs`), not assumed from the spec's own description. Several findings are **factual errors in the spec**, not hypothetical risks — these are marked **[SPEC BUG]** and must be fixed before a single line of code is written.

Severity key: 🔴 Critical (blocks implementation or breaks production) · 🟠 High (fix before/during Phase 1) · 🟡 Medium (fix before scale-up) · ⚪ Note (accepted scope / low priority)

---

## 1. Security Risks

- 🔴 **Cross-center data leakage through the FTS5 candidate stage [SPEC BUG].** `AiCaseSearchIndex` as designed has **no `CenterID` column**, and Stage 1 (`SELECT CasID FROM AiCaseSearchIndex WHERE ... MATCH ... LIMIT 200`) runs before any center filter is applied. Center filtering only happens in Stage 2. For a center-scoped user this isn't just an info-leak risk, it's a **ranking correctness bug**: if another center has more name matches, the 200-row cap can be consumed entirely by rows the user can never see, silently evicting the true in-center match. Must fix: add `CenterID UNINDEXED` to the FTS5 table, populate it in the sync triggers, and filter `WHERE CenterID = @CenterID AND ... MATCH ...` at Stage 1 whenever `SecurityContext.CenterFilterId != 0`.
- 🟠 **No read-side permission or visibility model for `AiConversation`/`AiMessage`.** The spec defines write gates (`AI.Search`, `AI.Reminders.Create`) but never says who can *read back* past conversations, whether one user can see another's chat history, or whether a manager/admin screen would show all users' queries. Query text alone ("خانواده احمدی" repeatedly, or a query about a specific abuse-flagged case) is sensitive even without opening the file — for a system handling vulnerable children's data, "who searched for whom" needs an explicit access model, not silence.
- 🟠 **Searches are not audited; only reminder writes are.** `AuditLogger.Log` is only called from `CreateReminderHandler`. A search for a specific beneficiary is a real access event and currently leaves only an `AiIntentLog` row with no described retention or access control — inconsistent with the seriousness the rest of the app gives audit logging (`TblAuditLog`, `EntSecurityEvent`).
- 🟠 **No explicit cross-center rejection in reminder case-linking.** `CreateReminderHandler` step 2 resolves a case reference via the same search path, which *is* center-filtered — but this is implicit, inherited behavior, not a stated check. A defense-in-depth explicit `if (!SecurityContext.CanAccessCenter(resolvedCase.CenterID)) reject` should be stated in the handler design itself, the same way `CenterGuard.EnsureCaseAccess` is used elsewhere in the app, not left to "well, the underlying query happens to filter."
- 🟡 **New PII surface with no retention/minimization policy.** `AiMessage.MessageText` and `TblReminder.SourceQueryText` store raw user input verbatim — names, tazkira digits, phone numbers typed in natural language — in new tables with no purge job, no encryption (matching the rest of the DB, which the project's own `SYSTEM_AUDIT_REPORT.md` already flags as plaintext), and no stated retention limit. This *expands* the PII footprint even though Phase 1 has no cloud egress.
- ⚪ Parameterized SQL discipline is otherwise sound and consistently described — no injection risk in the filter-building pattern itself.

---

## 2. Performance Risks

- 🟠 **Every interaction becomes a synchronous write, including plain searches.** Logging every turn to `AiMessage` + `AiIntentLog` turns a read-only search into a write transaction. Combined with the DB's non-WAL rollback-journal mode (see Section 3), this contends with the existing `PRAGMA busy_timeout=8000` write path used elsewhere (sync, reminder polling). The spec never states these writes happen off the UI thread — if they run inline with the search call, a lock wait under contention stalls the chat UI for up to 8 seconds.
- 🟠 **`SearchFamilyHandler` has no index backing.** The FTS5 index only covers `TblCase`. Family search — one of the four in-scope Phase 1 intents — falls back to unindexed `LIKE '%...%'` over `TblFamily`, undermining the "Search Architecture" section's own stated purpose. This is fine at hundreds of rows, not fine at the scale discussed in Section 6 below.
- 🟡 **FTS5 triggers add write amplification to the app's hottest table.** `TblCase` is edited constantly by `FrmCase` and related forms. Three new `AFTER INSERT/UPDATE/DELETE` triggers on every case write is a real, measurable tax on an existing, unrelated workflow — acceptable at small scale, worth a benchmark before shipping (`CLAUDE.md`: "never break existing features").
- 🟡 **`LIMIT 200` is arbitrary and will silently drop true matches for common names.** Afghan naming conventions repeat first names and father-names heavily across unrelated families. A flat cap without a stated fallback ("if a province/tazkira entity is also present, don't cap; if not, cap and say 'narrow it down'") will produce confusing false negatives even at moderate scale.
- ⚪ Debounce/live-typing isn't a concern — submission is Enter/button-triggered, not per-keystroke, per the spec's own UI description.

---

## 3. SQLite Risks

- 🔴 **FTS5 availability is assumed, not verified [SPEC BUG].** The spec states FTS5 is "native to the already-referenced `System.Data.SQLite`" — but FTS5 support depends on how that specific `SQLite.Interop.dll` was compiled (`SQLITE_ENABLE_FTS5`); many NuGet builds ship with only FTS3/4, or none. If unverified, `CREATE VIRTUAL TABLE ... USING fts5` throws at startup inside the shared initializer sequence — a **fatal, app-breaking failure**, not a degraded feature. This must be verified against the actual installed package version before any schema work starts, with a plain-`LIKE` fallback plan ready if it isn't available.
- 🟠 **No confirmed WAL mode; only `busy_timeout` is set.** The existing connection layer sets `PRAGMA busy_timeout=8000` but no `journal_mode=WAL` was found. Rollback-journal mode locks the whole file for the write's duration. Adding three more concurrent writers (chat logging, FTS5 triggers, reminder inserts) on top of the existing sync/reminder-timer writers increases lock contention risk. This should be revisited — either move to WAL before Phase 1 ships, or explicitly accept and document the contention risk.
- 🟠 **Trigger-based FTS5 sync may not survive the `Sync/` module's merge path.** The triggers assume normal single-row `INSERT`/`UPDATE`/`DELETE` traffic from the UI. The `Sync/` module (`HttpSyncTransport`, conflict resolution, bulk backup restore visible in `AutoBackups`/`SyncBackups`) may perform bulk merges, `ATTACH DATABASE` imports, or bulk `INSERT ... SELECT` that behave differently under `AFTER` triggers, or that intentionally bypass triggers. This compatibility was never checked against the actual sync implementation — must be verified, not assumed.
- 🟡 **No transaction boundary around the reminder-creation flow.** Parse → resolve case → insert `TblReminder` → `AuditLogger.Log` is described as four sequential steps with no shared transaction. A crash between steps 3 and 4 leaves a reminder with no audit trail; the rest of the codebase demonstrably wraps multi-step writes in `SQLiteTransaction` (`DatabaseInitializer.cs` pattern) — this flow should match that pattern.
- 🟡 **Backfill guard (`SELECT COUNT(*) FROM AiCaseSearchIndex`) is not a real migration-version check.** An interrupted first run (partial backfill) leaves `COUNT(*) > 0`, so the guard skips completion forever. If the app has an existing schema-version mechanism, reuse it instead of a row-count heuristic.

---

## 4. Multi-Center Risks

- 🔴 Same FTS5-missing-`CenterID` issue as Section 1 — repeated here because in a multi-center deployment this is the dominant failure mode, not an edge case: any center with a larger case count or more common names will systematically starve other centers' search results once total candidates exceed 200.
- 🟠 **`AiConversation.CenterID` is nullable with no stated meaning for `NULL`.** Unlike `TblReminder.CenterID` (nullable for reasons inherited from existing behavior), this is a *brand-new* table — there's no excuse to inherit that looseness. It should be `NOT NULL`, populated from `SecurityContext.CurrentCenterId` at creation, full stop.
- 🟡 **SuperAdmin cross-center count queries need an explicit test, not an inference.** `SecurityContext.CenterFilterId == 0` for `IsAllCenters` correctly flows into the count-query path per the spec's reuse of the existing filter — but this needs to be a stated acceptance test ("Admin at Center A asking 'چند یتیم فعال داریم' never includes Center B"), not something the reader has to derive from the general filter logic.
- ⚪ Flat, non-per-center permission grants (`AI.Search`/`AI.Reminders.Create` apply org-wide per role) match the app's existing permission model exactly — this is consistent, not a regression, even though it means no per-center pilot rollout is possible in Phase 1. Worth noting as accepted scope.

---

## 5. RTL Persian UI Risks

- 🟠 **`FlowLayoutPanel` + `RightToLeft.Yes` + `RightToLeftLayout = true` for chat bubbles is a known WinForms trouble spot.** WinForms' RTL mirroring of dynamically-added, variable-height child controls inside an `AutoScroll` flow panel has long-standing layout/anchoring quirks (text clipping, mis-mirrored padding, scroll-position jumps on append). This should be a short prototype spike before committing UI build days to it, not assumed to "just work" because the form-level RTL flag is set correctly elsewhere in the app.
- 🟠 **No digit-normalization step, despite three features depending on it [SPEC BUG].** `PersianNormalizer` is specified to handle ك/ي unification, diacritics, and spacing — but not Persian/Arabic numeral conversion (۰١٢... / ٠١٢... → 0-9). Every one of tazkira-suffix matching, phone-number extraction, and `ReminderDateParser`'s absolute-date parsing silently breaks if a user types digits in Persian numerals, which is the *default* input mode on most Persian-language keyboards. This is not a UI polish item — it's a functional prerequisite missing from the normalizer's own spec.
- 🟡 **Bidi rendering of mixed Persian text + digits inside RTL bubbles** (tazkira numbers, phone numbers, dates embedded in Persian sentences) is a classic WinForms/GDI+ bidi rendering hazard — numbers and adjacent punctuation can visually reorder. No mitigation is described (e.g., wrapping numeric spans, or verifying `TextRenderer` behavior) — needs explicit visual QA, not just functional QA.
- 🟡 **Placeholder text in the input `TextBox` isn't a built-in WinForms feature on .NET Framework 4.7.2** — requires manual owner-draw/focus-simulation code, not a property assignment. Small, but the spec presents it as if it's free.
- ⚪ **Emoji glyphs ("🤖") for the toolbar button/badge risk tofu-box rendering** on older Windows builds or machines without emoji fonts installed — plausible in this deployment context (field offices, possibly older hardware). Reuse the existing icon convention (`"⌕"` used for advanced search) instead of introducing a new glyph family.

---

## 6. Search Accuracy Risks

- 🔴 **`GrandFatherName` does not exist in the schema [SPEC BUG].** Verified against `DatabaseInitializer.cs`: `TblCase` has `HeadFullName` and `HeadFatherName` only — there is no grandfather-name column anywhere on the table. The Phase 1 spec's `AiEntities.GrandFatherName` and the FTS5 table's `GrandFatherName` column both reference a field that doesn't exist. This isn't a "confirm at implementation time" footnote as the spec hedged it — it's a wrong assumption that must be corrected now: drop `GrandFatherName`, correct `FullName`→`HeadFullName`, `FatherName`→`HeadFatherName` throughout.
- 🟠 **No confidence-scoring formula is defined anywhere.** `PersianNluEngine` is specified to return a `Confidence` double, and the reminder flow explicitly depends on "if confidence is low, ask instead of guessing" — but no scoring model, formula, or threshold is given. This is the single load-bearing safety mechanism of the whole Tier-1 design, and it's currently unimplementable as written.
- 🟠 **The tazkira-suffix pattern set is narrow and will miss common equivalent phrasings.** The spec anchors on three trigger phrases ("آخر", "آخرش", "آخر تذکره"). Real speech has many more ("آخرین رقمش...", "به ۵۴ ختم می‌شه", "شماره‌اش ۵۴ هست"). This is an inherent risk of the hand-written-pattern-set approach chosen for Tier 1 — acceptable as a starting point, but the spec should say explicitly that this pattern list is a living dictionary requiring an expansion pass after the QA step, not a fixed, "done" artifact.
- 🟡 **No "too many results" UX path.** With heavily repeated Afghan given/father names, unqualified queries will often return large, low-precision result sets. The `FrmAiAssistant` mockup only describes single-shot result cards — no narrowing/refine prompt ("۴۳ نتیجه یافت شد؛ ولایت را هم بگویید") is designed.

---

## 7. Reminder Risks

- 🔴 **Ambiguous identifier resolution: "پرونده ۲۴۲۴" is assumed to mean `CasID` [SPEC BUG].** `CasID` is an internal `AUTOINCREMENT` primary key with no user-facing meaning. The columns that actually carry a `UNIQUE` constraint and are meant to be human-referenced are `Code` and `FormNo`. A user saying "پرونده ۲۴۲۴" almost certainly means the case *code/form number* they see on paper or in `FrmCase`, not the internal row ID — which may not even match across machines before full sync reconciliation (note the presence of a separate `GlobalID` column, suggesting `CasID` stability across synced copies is not guaranteed). `CreateReminderHandler` must resolve against `Code`/`FormNo` first, falling back to `CasID` only if the case-search entity extraction already found a distinct record another way.
- 🟠 **Guessed default times with no pre-write confirmation.** "سه روز دیگر" → 09:00, "امروز" → 17:00 are reasonable defaults, but they're applied and *written* before the user sees them — the confirmation is post-hoc ("یادآوری ثبت شد"), not a pre-write check. A wrongly-guessed time creates a silently wrong reminder that only surfaces when (or if) it fires. A one-tap "لغو" affordance on the confirmation card is implied but not designed.
- 🟠 **No idempotency/duplicate-reminder check.** Re-running the same command (user unsure it saved) silently creates a second `TblReminder` row. No dedup-by-similarity check is described.
- 🟡 **No validation against past-dated results.** If `ReminderDateParser` misresolves to a past date, nothing stops the insert, and the existing `CheckDueReminders()` timer will pick it up and notify immediately — a wrong parse becomes an instant, confusing notification instead of a caught error. Should validate `remindAt > DateTime.Now` before insert and treat a past result as a parse failure requiring clarification.
- ⚪ `CreatedBy` correctly reuses the existing free-text convention — but the spec should name the *exact* existing accessor for "current username" rather than leaving it as "current username" prose, to avoid an implementer inventing a new one.

---

## Direct Answers

### What can go wrong?
The three most damaging failure modes, in order:
1. **A center-scoped user's search silently returns wrong/incomplete results because another center's matches consumed the FTS5 candidate cap** (Sections 1, 4) — a correctness bug that looks like "the AI is bad at search," eroding trust exactly the way the master plan itself warned against.
2. **The app fails to start** if FTS5 isn't actually compiled into the deployed SQLite provider (Section 3) — this is a binary fail/pass unknown that hasn't been checked.
3. **A reminder silently attaches to the wrong case** because "پرونده ۲۴۲۴" was resolved against the wrong identifier (Section 7) — for a system whose entire pitch is trustworthy AI-initiated writes, this is the worst possible category of bug: wrong, confident, and silent.

### What would I change before implementation?
In priority order: (1) fix the three factual spec bugs — missing `GrandFatherName` column, missing `CenterID` on the FTS5 index, `CasID` vs `Code`/`FormNo` reminder resolution; (2) verify FTS5 is actually available in the shipped SQLite build, with a `LIKE`-based fallback design ready; (3) add digit normalization to `PersianNormalizer` — nothing numeric works without it; (4) define the confidence-scoring formula concretely; (5) wrap the reminder-creation flow in a transaction; (6) decide, explicitly, whether the three new `Ai*` tables participate in `Sync` at all, and document the answer; (7) confirm WAL vs. rollback-journal mode is the right call given the new write volume.

### Which parts are overengineered?
- The full FTS5 two-stage architecture may be more than this deployment's real scale needs (see below) — worth a sizing gate ("do centers realistically exceed a few thousand active cases?") before paying its complexity and risk cost in Phase 1.
- `AiConversation` as a persisted, titled, multi-turn thread entity is speculative infrastructure: Phase 1's handlers are stateless (`Handle(entities, rawQuery)` per call, no context carry-over), so there's no feature in this phase that actually uses conversation continuity. A flat, append-only log (which `AiIntentLog` already mostly is) would serve Phase 1 without the extra table and bookkeeping (`Title`, `LastMessageAt`).
- The day-by-day, half-day-granularity implementation schedule presents false precision for the one component whose scope is inherently open-ended — Persian NLU pattern coverage (Section 6). A fixed "3.5 days" box invites schedule risk more than it aids planning.

### Which parts are underengineered?
- Confidence scoring (undefined formula) — the core safety mechanism of the entire design.
- Digit normalization — a functional prerequisite, currently absent.
- Family search indexing — one of four in-scope intents has no performance design at all.
- Multi-step write atomicity (no transaction).
- Data lifecycle: no retention/purge policy for `AiIntentLog`/`AiMessage`, no stated sync participation.
- Read-side access control for conversation/query history.
- "Too many results" / ambiguous-match UX — only the happy path (few, clear results) is designed.

### What would break at 100,000 beneficiaries?
- `SearchFamilyHandler`'s unindexed `LIKE` scan over what would likely be 100k–500k+ `TblFamily` rows becomes a multi-second full scan per query if run synchronously — a real UI freeze risk if not explicitly moved off the UI thread (not specified either way).
- The FTS5 `LIMIT 200` cap becomes actively harmful at this scale: common-name collisions across a much larger case pool make silent false negatives frequent rather than occasional, unless province/tazkira entities are always required to narrow the query — not currently enforced.
- `AiIntentLog`/`AiMessage` grow unbounded with no purge policy, inflating `CaseDB.sqlite`'s file size indefinitely — and since the app already does whole-file `AutoBackups`/`SyncBackups`, this directly grows backup time and storage with no corresponding user value after a few months.
- If the new `Ai*` tables are swept into `Sync` without a deliberate decision, 100k beneficiaries' worth of search history — potentially containing names, tazkira digits, and phone numbers typed as free text — replicates across every center and the central SyncServer. This is the single biggest unaddressed question in the spec.

### What would break in multi-center deployments?
- The FTS5-missing-`CenterID` bug (Sections 1 & 4) is the dominant failure mode specifically *because* multiple centers share one local database file distinguished only by a `CenterID` column with no database-level enforcement — there is no row-level security in SQLite, so this is 100% dependent on every query path remembering to filter, and the FTS5 stage currently doesn't.
- Reminder case-linking without an explicit cross-center rejection check relies on the same implicit filtering — one missed `AND CenterID = ...` in a future handler and a staff member at Center A can create a reminder against Center B's case.
- Sync-triggered bulk merges (Section 3) are the realistic scenario, in a multi-center deployment, where the FTS5 triggers' single-row-insert assumption gets stress-tested — this is precisely when data from *other* centers gets written into a local file in bulk, which is also the scenario most likely to desync the FTS5 index from `TblCase` if the trigger design doesn't hold up under whatever bulk mechanism `Sync/` actually uses.

---

*Review complete. No implementation performed. Recommend addressing the 🔴 items above before Phase 1 coding begins; 🟠 items should be resolved during Phase 1, not deferred.*
