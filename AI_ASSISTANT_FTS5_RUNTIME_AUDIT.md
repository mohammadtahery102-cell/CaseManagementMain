# AI Assistant — FTS5 Runtime Compatibility Audit

Status: **Audit complete. No functional code changes were required — the existing fallback was already correct.** New regression tests were added to prove this, not assume it.

Scope discipline: this document only investigates FTS5 runtime compatibility, per the request. No new features were added.

---

## Trigger

The user observed Visual Studio break on:
```
System.Data.SQLite.SQLiteException: SQL logic error
no such module: fts5
```
inside `AiInitializer.Exec(...)`, and reported it as "AI Assistant startup fails inside AiInitializer." Instruction: don't assume FTS5 is available; investigate actual runtime behavior, not just re-assert prior claims.

**Headline finding: the app does not fail.** What the user saw is Visual Studio's first-chance exception break (a debugger setting, already addressed in the prior turn) firing on an exception that is caught one stack frame later. This audit verifies that claim with fresh, independent evidence rather than repeating it.

---

## 1. Does `AiInitializer` correctly fall back when FTS5 is unavailable?

**Yes — verified by code trace and by an independent runtime probe, not by re-reading the same claim.**

`Helpers/AiInitializer.cs:84-129`: every FTS5-dependent statement (`CREATE VIRTUAL TABLE ... USING fts5`, the 3 `CREATE TRIGGER` statements, the backfill `INSERT`) lives inside one `try` block. The virtual-table `CREATE` is the *first* statement in that block, so when it throws, none of the trigger/backfill statements ever execute — there is exactly one throw site reached at runtime, not four. The `catch (Exception ex)` sets `FtsAvailable = false` and logs via `Debug.WriteLine`; it never rethrows.

New test `FtsAvailable_ReflectsActualEnvironment_NotAssumed` (`AiFts5UnavailableRuntimeTests.cs`) does not trust `AiInitializer`'s own report — it independently attempts `CREATE VIRTUAL TABLE __fts5_probe__ USING fts5(x)` on a fresh connection, outside any `AiInitializer` code path, and asserts the result matches `AiInitializer.FtsAvailable`. **Passed.** In this environment, both the independent probe and `AiInitializer.FtsAvailable` agree: FTS5 is not registered.

---

## 2. Is the exception fully swallowed and does startup continue?

**Yes — verified by calling the exact production entry point with no protective wrapper of my own.**

`Program.cs:45-82` wraps the whole startup sequence (`DatabaseInitializer` → `AccountingInitializer` → `EnterpriseInitializer` → `OfflineSyncInitializer` → `AiInitializer.EnsureAiObjects()` → `ErrorLogger.Install()` → ...) in one outer `try/catch` — but that outer catch is a second line of defense, not the one that actually activates here, because `AiInitializer.EnsureAiObjects()` already recovers internally (§1) and returns normally. Execution genuinely continues into `ErrorLogger.Install()` and everything after it.

New test `EnsureAiObjects_NeverThrows_RegardlessOfFts5Outcome` calls `AiInitializer.EnsureAiObjects()` **twice in a row with no try/catch around the call**, exactly mirroring what Program.cs does, plus a second call simulating an app restart (the FTS5 `CREATE VIRTUAL TABLE` fails identically every time, since the underlying SQLite build never registers the module — this is not a one-time or intermittent failure, it is deterministic on this machine). **Both calls returned normally. Passed.**

The retention-purge and reminder/audit-reconciliation logic that runs *after* the FTS5 try/catch (`AiInitializer.cs:131-166`) only touches `AiMessage`, `AiConversation`, `AiIntentLog`, `TblReminder`, `TblAuditLog` — none of which depend on `AiCaseSearchIndex` — so there is no second failure point downstream of the FTS5 catch block.

---

## 3. Does any AI search path still depend on FTS5?

**No unguarded dependency found — verified by an exhaustive grep of the entire `AI/` folder, not a sample.**

```
grep "AiCaseSearchIndex|FtsAvailable" across AI/*.cs, AI/Handlers/*.cs
```
returned exactly 4 matches, all in `CaseSearchCore.cs`:
- Two guard checks: `if (!string.IsNullOrWhiteSpace(entities.PersonName) && AiInitializer.FtsAvailable)` (in `SearchByEntities` and `CountByEntities`).
- Two references to the table name, both *inside* `RunFtsStage`, which is only ever reachable through those two guards.

No other file — not `CaseReferenceResolver.cs`, not any of the four handlers, not `AiOrchestrator.cs`, not `FrmAiAssistant.cs` — references `AiCaseSearchIndex` at all. `CaseReferenceResolver` delegates to `CaseSearchCore.SearchByEntities` for name lookups, so it inherits the same guard rather than duplicating (or missing) it.

Four new tests exercise every layer with FTS5 confirmed unavailable in this environment and assert **correct results**, not just "no crash": `Search_WorksCorrectly_WithoutFts5`, `Count_WorksCorrectly_WithoutFts5`, `CaseReferenceResolver_WorksCorrectly_WithoutFts5`, and `FullOrchestrator_EndToEnd_WorksCorrectly_WithoutFts5` (drives the real `AiOrchestrator.Handle` entry point end to end). All passed, each asserting the exact expected row(s), not merely a non-empty result.

---

## 4–6. Fix startup if it can fail / make FTS5 completely optional / force the LIKE path when unavailable

**No code change was required.** Items 1–3 above establish, with independent verification rather than assumption, that:
- Startup cannot fail from this cause (§2).
- FTS5 is already fully optional — `CREATE VIRTUAL TABLE` is skipped-on-failure (via the exception path, which is functionally equivalent to a skip since `IF NOT EXISTS` combined with the catch means no partial state is left behind), `FtsAvailable` is set correctly, and the LIKE-based path is already forced whenever it's `false` (§1, §3).
- This was already the state of the code *before* this audit — the critical version of this exact bug (name search silently ignoring the filter in the LIKE-fallback path) was found and fixed in the prior final-validation pass (see `AI_ASSISTANT_PHASE1_FINAL_VALIDATION.md`, CRIT-01/HIGH-01/MED-01). This audit re-verified that fix independently and found it holds.

No regression, no gap, nothing to patch.

---

## 7. Automated tests for environments without FTS5

Added `CaseManagement.Tests/AiFts5UnavailableRuntimeTests.cs` (6 tests, all passing):

| Test | Proves |
|---|---|
| `EnsureAiObjects_NeverThrows_RegardlessOfFts5Outcome` | Startup survives, including a simulated restart |
| `FtsAvailable_ReflectsActualEnvironment_NotAssumed` | The flag is independently verified against a real, separate FTS5 probe — not just trusted |
| `Search_WorksCorrectly_WithoutFts5` | Name search returns the *correct* single match, not just "doesn't throw" |
| `Count_WorksCorrectly_WithoutFts5` | Counting works without FTS5 |
| `CaseReferenceResolver_WorksCorrectly_WithoutFts5` | Reminder case-linking's name resolution works without FTS5 |
| `FullOrchestrator_EndToEnd_WorksCorrectly_WithoutFts5` | The real public entry point (`AiOrchestrator.Handle`) works end to end without FTS5 |

These did not need to *simulate* an FTS5-less environment artificially — this specific SQLite build genuinely never registers FTS5, so every test in this suite (and every one of the 479 tests in the full project suite) already runs against that exact condition on every execution. That is itself informative: this isn't an edge case that might occur somewhere — it is the actual, constant runtime condition on this environment, and the whole AI module (75 AI-specific tests, all passing) has been validated against it repeatedly across this entire engagement.

Full AI-suite regression check after adding these tests: **75/75 passed** (69 pre-existing + 6 new).

---

## Conclusion

| # | Question | Answer |
|---|---|---|
| 1 | Does `AiInitializer` correctly fall back? | Yes — verified independently, not assumed |
| 2 | Is the exception fully swallowed, does startup continue? | Yes — verified by calling the real entry point unwrapped |
| 3 | Does any search path still depend on FTS5? | No — exhaustively grepped, zero unguarded references |
| 4–6 | Fix / make optional / force LIKE path | Nothing to fix — already correct |
| 7 | Tests added | 6 new, all passing, 75/75 AI tests green overall |

**What the user saw in the debugger was real** — the exception genuinely fires, every single time the app starts, because this machine's SQLite build never registers FTS5. **It was never a startup failure** — it's an already-handled, already-tested, deterministic first-chance exception that Visual Studio surfaces because of its own break-on-throw setting (addressed separately). This audit's job was to stop assuming that and go verify it directly; it did, with new tests that fail loudly if this ever regresses.
