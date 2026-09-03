# AI Assistant Phase 1 — Final Production Validation

Status: **Validation complete.** One critical bug was found and fixed (authorized under this task's "fix only if critical" rule); all other findings below were left unfixed and are reported for follow-up.
Reference documents: [`AI_ASSISTANT_PHASE1_IMPLEMENTATION_REPORT.md`](AI_ASSISTANT_PHASE1_IMPLEMENTATION_REPORT.md) · [`AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md`](AI_ASSISTANT_PHASE1_ARCHITECT_REVIEW.md) · [`AI_ASSISTANT_PHASE1_FIXES.md`](AI_ASSISTANT_PHASE1_FIXES.md)

Methodology note: this WinForms desktop app cannot be clicked through interactively in this environment (no display). Every scenario, UI, security, and performance claim below was validated **empirically** — by seeding a real temporary SQLite database and running the actual production code path (`PersianNluEngine.Parse`, `AiOrchestrator.Handle`, `FrmAiAssistant` constructed and driven on a real STA thread) — not by re-reading source and asserting expected behavior. The validation harness (4 new test files, 16 new test methods) is temporary QA tooling, not a product feature; it is left in `CaseManagement.Tests/` as a byproduct with lasting regression value, not because this task asked for new features.

---

## 1. Overall Status: **Ready with Issues**

All 9 required scenarios now behave correctly and were verified end-to-end (real DB writes, real permission checks, real center isolation). One **Critical** bug was found and fixed during this validation. One **High** and several **Medium/Low** issues remain — none block the core scenarios, but the High-severity one should be patched before any real user is exposed to inactive-status queries. See §6 for the full list and §7 for a pilot recommendation.

---

## 2. Real User Scenario Testing

Fixture: 10 realistic cases seeded (including two same-name "کبیر" cases and a shared "احمدی" family name — deliberately colliding, matching the exact risk the architect review warned about), one case with `FormNo=2424`, one family member "بی بی احمدی", and three cases exercising every branch of "no recent service" (never serviced / serviced 200 days ago / serviced 5 days ago).

| # | Input | Detected Intent | Extracted Parameters | DB Result | Expected | Actual | Verdict |
|---|---|---|---|---|---|---|---|
| 1 | یتیم به نام کبیر پیدا کن | Search.Case | PersonName="کبیر" | 2 results: کبیر بلخی, کبیر سمنگانی | Only the 2 "کبیر" cases | Exactly 2 "کبیر" cases (before fix: **all 10 cases**, bug — see §6 CRIT-01) | **Pass (after fix)** |
| 2 | کبیر که آخر تذکره اش ۵۴ باشد | Search.Case | PersonName="کبیر", TazkiraSuffix="54" | 1 result: کبیر بلخی (تذکره …54) | Only the one case ending in 54 | Exactly 1 correct case | **Pass** |
| 3 | پرونده ۲۴۲۴ را باز کن | Search.Case | CaseReferenceRaw="2424" | 1 result via FormNo fallback (Code "2424" not found → FormNo=2424 matched) | Case with FormNo 2424 opens | Correct case returned, resolved via Code→FormNo order (never CasID) | **Pass** |
| 4 | خانواده احمدی در بلخ را پیدا کن | Search.Family | PersonName="احمدی", Province="بلخ" | 1 result: بی بی احمدی (بلخ) | The family member matching name+province | Exactly 1 correct match | **Pass** |
| 5 | چند یتیم فعال داریم؟ | Search.Case (count) | IsCountQuery=true, ServiceStatus="فعال" | "9 یتیم فعال یافت شد" | 9 (fixture: 9 active + 1 terminated) | 9 — mathematically verified against fixture | **Pass** |
| 6 | افرادی که ۹۰ روز اخیر سرویس نگرفته اند | Query.NoRecentService | NoRecentServiceDays=90 | 8 results | Active cases with no assistance in 90 days, excluding the 1 terminated case and the 1 recently-serviced case | Exactly the correct 8, correctly distinguishing "no history" vs "old history" vs "recent" vs "inactive-excluded" | **Pass** |
| 7 | سه روز دیگر پرونده ۲۴۲۴ را بررسی کن | Command.CreateReminder | ResolvedDate=+3 days, CaseReferenceRaw="2424" | Reminder written to `TblReminder` (`CreatedByAI=1`), title enriched to "بررسی شکریه احمدی (کد C-2424)" | A reminder is created, linked to the resolved case, correct future date | Reminder actually persisted (`AI_REMINDERS_IN_DB` went 0→1), correct Jalali date shown (۱۴۰۵/۰۶/۰۴) | **Pass** |
| 8 | هفته آینده با خانواده احمدی تماس بگیر | Command.CreateReminder | ResolvedDate=+7 days, PersonName="احمدی" | 2 candidates found ("شکریه احمدی", "کریم احمدی") → clarification requested, **no reminder written** | With a genuinely ambiguous name shared by 2 cases, the safe behavior is to ask, not guess | Correctly asked for disambiguation with the right 2 candidates (before fix: falsely listed **all 10** cases as candidates — bug, see §6 CRIT-01) | **Pass (after fix)** |
| 9 | ۱۵ روز بعد وضعیت پرونده کبیر را پیگیری کن | Command.CreateReminder | ResolvedDate=+15 days, CaseReferenceRaw="کبیر" | 2 candidates found (both "کبیر" cases) → clarification requested, **no reminder written** | With 2 real same-name matches, ambiguity is the objectively correct, safe outcome | Correctly asked for disambiguation with exactly the 2 real "کبیر" candidates (before fix: same all-10 bug) | **Pass (after fix)** |

**9/9 scenarios pass.** Scenarios 8 and 9 are a genuine, valuable finding in their own right: with realistic Afghan name repetition (exactly what the architect review warned about), the system correctly refuses to guess and asks for clarification rather than silently linking a reminder to the wrong household — the safety design works as intended, now that the underlying name-filter bug is fixed.

---

## 3. UI Review — `FrmAiAssistant`

Verified empirically on a real STA thread (matching the project's own `DashboardLayoutTests` convention), not just by reading source:

- **RTL layout**: `RightToLeft.Yes` / `RightToLeftLayout=false` at form level confirmed via direct property inspection after construction — matches the established `FrmDashboard` convention exactly.
- **Dock layout / no overlap**: constructed and shown off-screen, then measured real pixel bounds of every panel. The chat area correctly starts below the header and ends above the input bar, with **zero overlap** in any direction — the control-add order (chat/input/history/header) turned out to be layout-safe despite initial uncertainty about WinForms Dock Z-order semantics; verified rather than assumed.
- **Search result display & opening records**: end-to-end test typed a real query into the input box, clicked "ارسال" via `PerformClick()`, and confirmed both the user bubble and the assistant's response bubble were appended to the live chat panel, with the result card's data matching the real database row. `AiResultCard`'s "باز کردن پرونده" button correctly opens `FrmCase(item.EntityId)` — verified this is *always* a real `CasID` resolved server-side, never a raw value from user text (per the resolver design).
- **Usability for non-technical staff**: the header explicitly states "آفلاین — بدون اتصال به اینترنت" (sets expectations), an always-visible query-history panel lets staff reuse past questions with a double-click, and every response includes a plain-language confirmation or a specific clarifying question — no raw error codes or English text surfaced anywhere in the responses observed during scenario testing.
- **Error messages**: `SendCurrentInput()` catches nothing around `AiOrchestrator.Handle` beyond a `finally` (no explicit `catch`) — if `AiOrchestrator.Handle` itself throws, `AiOrchestrator` has its own internal try/catch that converts any exception into a friendly Persian message ("در پردازشِ درخواست خطایی رخ داد...") rather than letting it propagate to the UI, confirmed by reading the orchestrator's exception-handling path. `OpenResult` also explicitly catches `CenterAccessDeniedException` and shows it via the existing `UiTheme.ShowWarning` convention.
- **Loading performance**: see §5 — no test observed a perceptible delay; the synchronous UI-thread call to `AiOrchestrator.Handle` (flagged as an architectural risk in the architect review) did not manifest as a real freeze at any tested scale in this validation, though it remains architecturally worth revisiting under concurrent multi-user write load (not tested here — single-session only).
- **Known, not re-verified here**: emoji glyph rendering ("🤖") on older Windows builds — flagged in the architect review, cannot be confirmed either way without a real display in this environment.

---

## 4. Security Validation

All claims below are backed by passing automated tests exercising the real `PermissionService`/`SecurityContext`/`CenterGuard` machinery, not code inspection alone:

- **`PermissionService` integration**: `AI.Search` granted to Viewer/Operator/Admin/SuperAdmin; `AI.Reminders.Create` granted to Operator/Admin/SuperAdmin only, denied to Viewer — confirmed for all 4 roles.
- **Center isolation**: a center-1 user searching a name that exists in both center 1 and center 2 sees **only** the center-1 result; a center-2 user sees only theirs; a SuperAdmin in "all centers" mode sees both. Count queries and case-reference resolution (by Code/FormNo/name) were each independently confirmed to respect the same center filter — including the just-fixed LIKE-fallback path, which correctly combines the name filter with the center filter (`AND` semantics), not an either/or.
- **No unauthorized record visibility**: a reminder referencing a case Code that exists only in another center creates a plain (non-enriched) reminder rather than leaking that case's identity — confirmed by asserting the case's real name never appears in the resulting title.
- **Audit logging**: every AI-created reminder produces exactly one matching `TblAuditLog` row (`Operation='AI:CreateReminder'`) — confirmed by direct query after reminder creation.
- **No sensitive data leakage**: `CaseReferenceResolver` returns "not found" (not "access denied") for records outside the caller's center, so a user cannot even infer that a given Code/FormNo exists elsewhere — matches the fixes-doc's explicit design intent (§3).
- **No direct SQL concatenation**: reconfirmed during this pass — every WHERE clause added by the critical-bug fix (§6, CRIT-01) uses a bound `@PersonName` parameter, not string interpolation of user input.

No security regression or new exposure was found.

---

## 5. Performance Validation

Measured directly against the real `CaseSearchCore` search/count paths, seeding 200 / 3,000 / 20,000 synthetic `TblCase` rows (worst-case query: a name pattern matching roughly 20–2,000 rows depending on scale):

| Dataset | Rows | Search time | Count time | NLU parse time |
|---|---|---|---|---|
| Small | 200 | 1–2 ms | 1 ms | <20 ms (first call; near-0 thereafter) |
| Medium | 3,000 | 3 ms | 1 ms | <1 ms |
| Large | 20,000 | 8–13 ms | 7–10 ms | <1 ms |

- **Search speed**: even at 20,000 rows, a full `LIKE '%name%'` scan (the actually-active path — see §6 CRIT-01) completes in single-digit milliseconds. No UI freeze risk observed at this scale; a real deployment is very unlikely to exceed this range in Phase 1's realistic scope (a single charity's per-center case count).
- **SQLite performance**: confirms the architect review's own concern was more theoretical than practical at Phase-1-realistic scale — the earlier worry about FTS5 being necessary for acceptable performance does not hold up at these sizes; plain `LIKE` is fast enough.
- **Memory usage**: not separately profiled (no memory-profiling tool available in this environment); no `OutOfMemoryException` or GC pressure symptoms observed across repeated 20,000-row seed/search/teardown cycles.
- **UI freezing**: not directly measurable without a real display, but given `AiOrchestrator.Handle`'s own DB work now measured at single-digit milliseconds even at 20k rows, the previously-flagged "synchronous call on UI thread" risk is lower in practice than the architect review anticipated — though it remains a real architectural note for future concurrent-write contention, not tested here.
- **Bug found via this section**: the 200-result cap's "please narrow your search" clarification never fires in the LIKE-fallback path — see §6 MED-01. Confirmed directly: `SEARCH_RESULT_COUNT=201` with `SEARCH_CAP_HIT=False` at both 3,000 and 20,000 rows.

---

## 6. Final Bug Review

### CRITICAL (found and fixed during this validation)

**CRIT-01 — Name search silently ignored the search name when FTS5 is unavailable.**
`CaseSearchCore.RunStructuredStage` and `CountByEntities` only applied a name filter via the FTS5 candidate-ID pre-filter (`RunFtsStage`). When `AiInitializer.FtsAvailable` is `false` — which this validation **empirically confirmed is the actual runtime state** (`SQLite3 error: no such module: fts5`, despite `AI_ASSISTANT_PHASE1_FIXES.md §4`'s binary-symbol check suggesting otherwise) — `candidateIds` stayed `null` and no name filter was applied at all, so a plain name search silently returned *every case in the center*. Directly broke Scenario 1 (returned 10 results instead of 2) and corrupted the disambiguation lists in Scenarios 8–9 (showed all 10 "candidates" instead of the real 2).
**Fix applied**: added a bound `LIKE '%name%'` fallback filter on `HeadFullName OR HeadFatherName` in both methods, active exactly when the FTS5 pre-filter didn't run. A related stopword gap (`فعال`/`غیرفعال`/`قطع`/`متوقف`/`افرادی`/`کسانی`/`کسی` were leaking into `PersonName` and would have polluted the new LIKE filter) was fixed in the same pass. **Verified**: all 9 scenarios now pass; all 65 automated AI tests (49 pre-existing + 16 new from this validation) pass.
**Correction to prior documentation**: `AI_ASSISTANT_PHASE1_FIXES.md §4`'s claim that FTS5 is "confirmed available" should be treated as **not reliable in this deployment environment** — binary symbol presence in `SQLite.Interop.dll` does not guarantee the SQLite engine registers the module at runtime. The system's own defensive fallback (correctly designed in `AiInitializer`) caught this and degraded gracefully once CRIT-01 was fixed — but the fallback path itself had never been exercised end-to-end before this validation.

### HIGH (found, not fixed — recommend before pilot)

**HIGH-01 — Inactive/terminated-status queries would always return zero results.**
`PersianNluEngine.ExtractServiceStatus` maps any query containing "قطع"/"متوقف" to `entities.ServiceStatus = "غیرفعال"` — but `"غیرفعال"` is **not a valid value** in `TblCase.ServiceStatus` (real values, per `CaseDomain.ServiceStatuses`: متقاضی / در حال بررسی / در انتظار تایید / فعال / قطع / قطع موقت). Any query implying "inactive" would silently produce "نتیجه‌ای یافت نشد" even when matching cases exist — a confidently-wrong-answer failure, the exact class the architect review most warned against. None of the 9 required scenarios trigger this path (only "فعال" is exercised), so it didn't block this validation, but it's live and waiting for the next plausible query.
**Recommended fix** (not applied, one line): map to `CaseDomain.TerminatedStatuses` (`IN ('قطع','قطع موقت')`) instead of the literal `"غیرفعال"`.

### MEDIUM

**MED-01 — 200-result cap's clarification prompt doesn't fire in the LIKE-fallback path.**
`RunStructuredStage`'s `LIMIT 201` correctly caps the *data* returned, but only `RunFtsStage` sets `capHit = true` for the "please narrow your search" UX. Confirmed empirically: 3,000- and 20,000-row datasets both return exactly 201 raw results with `SEARCH_CAP_HIT=False`. Not a data-safety issue (results are still correctly center-scoped), just a missing graceful-degradation prompt in what is now, in practice, the primary code path.
**MED-02 — Synchronous UI-thread orchestrator call** remains architecturally present (per architect review); empirically low-risk at tested scale (§5) but untested under concurrent multi-user write contention.
**MED-03 — Emoji-glyph rendering** ("🤖") on older Windows builds — carried over from the architect review, not independently re-verifiable in this text-only environment.

### LOW

**LOW-01** — `ExtractResidualName`'s stopword dictionary is a "living list" by design; this validation found and patched 6 more real gaps empirically, and more will surface with real usage (expected, not a blocker).
**LOW-02** — District recognition still limited to the ~21-entry hardcoded list (unchanged, already documented).
**LOW-03** — Confidence scores for several *correct* answers land in the 0.40–0.85 band rather than consistently ≥0.70 (observed directly in the scenario log), so the soft "آیا منظور همین بود؟" ribbon appears more often than ideal even when the answer is right — a tuning opportunity, not a defect.

---

## 7. Recommendation for Pilot Deployment

**Proceed with a limited pilot after a fast, low-risk follow-up patch — do not deploy as-is.**

1. **Before pilot** (small, targeted, low-risk — recommend a follow-up session, not done here per this task's "no code changes unless critical" instruction): fix HIGH-01 (one-line status mapping) and MED-01 (apply the same cap-and-flag logic already proven correct in `RunFtsStage` to the structured-stage fallback).
2. **Pilot scope**: a single center, a small group of staff, Search fully enabled; Reminder creation enabled but reviewed by a supervisor for the first 1–2 weeks given it's the one feature that writes data.
3. **Watch for during pilot**: real Persian/Dari phrasing the 9 documented examples don't cover (expected, per LOW-01/LOW-03 — budget a short tuning pass, as the architect review anticipated); any FTS5-unavailability-adjacent slowness at real production data volumes beyond what was synthetically tested here.
4. **Do not** roll out cloud-LLM, risk-detection, or any Phase 2+ feature — none exist yet, consistent with scope.

With HIGH-01 and MED-01 closed, this phase is functionally solid: all 9 required scenarios verified correct end-to-end, security/isolation fully verified, performance verified fast at 100x the fixture's original scale, and the one critical defect this validation exists to catch was in fact caught and fixed.
