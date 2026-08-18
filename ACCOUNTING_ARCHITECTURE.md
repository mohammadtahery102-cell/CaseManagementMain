# ACCOUNTING_ARCHITECTURE.md

Technical reference for the **Accounting module** (`حسابداری داخلی ایتام`) only.
Scope: `Accounting/` plus its direct dependencies. Nothing outside the module is described here.

---

## 1. Module architecture

Three layers, matching the project standard (no SQL in forms, no UI in the repository).

```
Presentation      FrmAccounting.cs          12 tabs: transactions, stipend, salary,
                                            expenses, reports, periods, funds, parties,
                                            categories ×2, settings, backup, integrity
                  FrmAccountingRepair.cs    historical-data repair tool (separate dialog)
                        │
Business / Data   AccountingRepo.cs         all SQL for the module; centre + reversal filters
                  AccReports.cs             8 reports, print + Excel, with reconciliation rows
                  AccIntegrity.cs           read-only integrity engine (13 checks)
                  AccRepair.cs              read-only detection + confirmed single-item repair
                  Money.cs                  rounding / comparison / conversion helpers
                  AccAudit.cs               financial audit trail writer
                  AccountingRuleException.cs  rule violations (+ AccountingDuplicateException)
                        │
DAL               DAL/DatabaseHelper.cs     connection factory, query/scalar/nonquery,
                                            ExecuteInTransaction, ExecuteInsertReturningId
                        │
                  SQLite (CaseDB.sqlite)
```

Support classes outside `Accounting/`:

| File | Role |
|---|---|
| `Helpers/AccountingInitializer.cs` | Creates/migrates all `Acc*` tables. Runs at startup from `Program.cs`. |
| `Helpers/AccountingBackupHelper.cs` | Independent backup/restore of `Acc*` tables only (full replace, in one transaction). |
| `Helpers/SecurityContext.cs` | Supplies `Username`, `CenterFilterId`, `IsSuperAdmin()`. |

**Entry points:** `FrmDashboard` → `FrmAccounting`; integrity tab → `FrmAccountingRepair` (SuperAdmin only).

---

## 2. Database changes

All migrations are **additive**. `AccountingInitializer.EnsureColumn` adds columns only when absent, so re-running is safe and existing rows are never rewritten. Verified against a copy of production: row counts and column sums identical before/after migration.

### Existing tables (unchanged structure)

`AccPeriod`, `AccFund`, `AccParty`, `AccIncomeCategory`, `AccExpenseCategory`,
`AccTransaction`, `AccStipend`, `AccSalary`, `AccExpenseItem`, `AccSettings`, `AccAudit`

### New columns

**Reversal support** — added to `AccTransaction`, `AccStipend`, `AccSalary`, `AccExpenseItem`:

| Column | Type | Purpose |
|---|---|---|
| `IsReversed` | `INTEGER NOT NULL DEFAULT 0` | 1 = voided; excluded from every balance and report |
| `VoidReason` | `TEXT NULL` | Mandatory reason captured at void time |
| `VoidedBy` | `TEXT NULL` | Username |
| `VoidedAt` | `TEXT NULL` | `datetime('now')` |

The `DEFAULT 0` is what makes the migration non-destructive: every pre-existing row stays valid and every balance is unchanged.

**Revision link** — added to `AccTransaction`:

| Column | Type | Purpose |
|---|---|---|
| `RevisesTxnID` | `INTEGER NULL` | The voided document this one replaces. `NULL` = ordinary document |

`NULL` for every pre-existing row, so nothing changes for historical data.

**Audit detail** — added to `AccAudit`:

| Column | Type | Purpose |
|---|---|---|
| `OldValue` | `TEXT NULL` | Value before the change |
| `NewValue` | `TEXT NULL` | Value after the change |
| `Reason` | `TEXT NULL` | Why the change was made |

**Pre-existing from earlier work:** `FundID` on `AccStipend` / `AccSalary` / `AccExpenseItem`, and `MonthTo` on `AccPeriod`.

### New indexes

`IX_AccTxn_DocNo (PeriodID, DocNo)` · `IX_AccTxn_Reversed (IsReversed)` ·
`IX_AccStipend_Fund` · `IX_AccSalary_Fund` · `IX_AccExpItem_Fund`

No `UNIQUE` constraint was added — see §8.

---

## 3. Integrity engine (`AccIntegrity.cs`)

Read-only. Never writes. Returns `List<Issue>` with `Severity` (`بحرانی` / `هشدار` / `اطلاع`), `Category`, `Description`, `Entity`, `EntityId`, `Amount`.

`RunAllChecks()` — 13 checks:

1. **Balance equation** — `Opening + Income − Expense = Closing`, per period
2. Records with no `PeriodID`
3. Records with no `CenterID`
4. Dangling FK references (`PeriodID`/`FundID`/`PartyID` pointing at deleted rows)
5. Stipend totals — `TotalPaid == FamilyCount × AmountPerFamily`
6. Currency conversion — `Amount == round(DollarAmount × DollarRate)`; also flags dollar-without-rate
7. Zero/negative amounts
8. Duplicate transactions (same date + direction + fund + amount + period)
9. Duplicate `DocNo` within a period
10. Duplicate stipend rows
11. Fund balances — negative balances; transactions with no fund
12. **Possible double entry** — stipend/salary recorded *both* in its own tab *and* as an expense transaction
13. Period date validity, and transaction dates outside their period's range

**Check 1 deliberately avoids tautology.** Calling `GetPeriodClosing()` and comparing it to the same formula would prove nothing. The check recomputes the components with *independent* SQL and compares against `GetPeriodClosing()`, so a missing filter in either path shows up as a mismatch.

Reports carry their own reconciliation. `AccReports.AddReconciliationRows` asserts the balance equation on the printed figures; `AddLedgerTieOut` compares a report total against the ledger's own aggregate. Both print `✔` or a `⚠` with the exact difference, so a discrepancy cannot leave the building unnoticed.

---

## 3b. Correcting a posted document (`ReviseTransactionAtomic`)

A posted document is never edited in place. "ویرایش" on the transactions tab means **void the original and issue a replacement**, both inside one database transaction.

Why not `UPDATE`: a posted document records an event that actually happened. Changing its amount in place makes yesterday's printed, signed report disagree with the database, with no way to tell which is right.

Order of operations inside the transaction:

1. `EnsureMutable` — the **original's own period** must be open (not merely the one selected in the combo).
2. Void the original — `WHERE ... AND COALESCE(IsReversed,0) = 0`. If another user voided it in the meantime, `affected == 0` and the whole transaction is rolled back.
3. Duplicate detection for the replacement. The original is already voided at this point, so revising *without* changing the amount is not falsely flagged.
4. Allocate a **fresh** `DocNo`. The old number stays with the voided document; reusing it would put two documents on one number.
5. Insert the replacement with `RevisesTxnID` pointing at the original.

Two audit rows are written (`ابطال بابت اصلاح` on the original, `صدور سند اصلاحی` on the replacement) so the correction path is visible from either end. A reason is mandatory — enforced in the repository, not just the form.

**Atomicity is the point.** If the replacement violates a rule, the void is rolled back with it; the money never disappears from the ledger. `TransactionRevisionTests.Revise_WhenReplacementIsInvalid_RollsBackTheVoid` covers exactly this.

---

## 4. Repair tool (`AccRepair.cs` + `FrmAccountingRepair.cs`)

Repairs **historical data only**. Contains no accounting calculation and no business logic.

**`Detect()`** — read-only, returns `List<RepairItem>`:

| Detector | Suggestion derived from |
|---|---|
| Orphaned records (no `PeriodID`) | Period whose date range contains the record's date → else the centre's only period → else **no suggestion** |
| Missing `CenterID` | Centre of the record's period → else of its fund → else none |
| Malformed dates | Same date normalised to `yyyy/MM/dd` |
| Duplicates | Keep lowest ID, void the rest |

**`Apply(item, reason)`** — one item, explicit call only. There is **no "repair all"**. Guards, in order:

1. Reason must be non-blank
2. `SecurityContext.IsSuperAdmin()`
3. Target must exist (e.g. destination period)
4. New date must match `yyyy/MM/dd`
5. **Optimistic guard** — the `WHERE` clause repeats the old value (`AND PeriodID IS NULL`, `AND DateCol = @old`). If another user changed the row since the scan, zero rows update and the repair aborts rather than overwriting.
6. Writes `AccAudit` with old value, new value and reason

Voiding routes through `AccountingRepo.Void*`, so closed-period and centre rules still apply — the repair tool has no shortcut around normal rules.

UI: grid of issues → detail panel showing problem, current value, suggested correction and **basis for the suggestion** → editable control (period combo / centre combo / date box / read-only note) → mandatory reason → confirmation dialog naming the exact before→after → apply → auto-rescan. Excel export lists every issue including the suggestion basis, with manual-decision rows highlighted.

**Suggestions are never guesses.** Where there is no sound basis the tool says so and leaves the choice to the accountant. On current production data this means 26 of 33 items are marked manual.

**Ordering note:** repair malformed dates *first*. Fixing period start/end dates lets the date-range matcher work, which converts several orphan items from manual to auto-suggested.

---

## 5. Validation rules

Enforced in `AccountingRepo` (data layer), not only in forms — so any future caller inherits them.

| Rule | Where |
|---|---|
| Amount > 0, finite, ≤ `Money.MaxAmount` (1e9) | `AddTransactionAtomic`, `AddSalary`, `AddExpenseItem` |
| `FamilyCount > 0`, `AmountPerFamily > 0`, `OrphanCount >= 0` | `AddStipend`, `UpdateStipend` |
| Employee name / expense description non-empty | `AddSalary`, `AddExpenseItem` |
| `Amount == round(Dollar × Rate)` (±1 rounding tolerance) | `Money.IsConversionConsistent` |
| Record's own period must be open | `EnsureMutable` |
| Record must belong to the active centre | `EnsureMutable` + `AND (@cid = 0 OR CenterID = @cid)` on every write |
| Already-voided records are immutable | `EnsureMutable` |
| Duplicate transaction requires explicit confirmation | `AddTransactionAtomic(confirmedDuplicate)` |

`EnsureMutable` checks the **record's** period, not the combo box's — selecting a closed-period row and picking an open period in the UI no longer bypasses the lock.

---

## 6. Audit flow

Every financial operation writes one `AccAudit` row via `AccAudit.LogChange`:

```
Operation │ EntityName │ EntityID │ OldValue │ NewValue │ Reason
Username  │ MachineName │ IPAddress │ CenterID │ CreatedAt
```

Covered: transaction insert/void · stipend, salary, expense insert/update/void · period create/update/open/close · fund and party update · data repairs.

Audit failure never aborts the financial operation (recording the document matters more than recording the log), but it is no longer silent: on failure the entry is appended to `AccAudit_Fallback.log` beside the executable, so a broken audit trail is discoverable.

---

## 7. Important design decisions

**Money stays `REAL`.** Integer minor units are more correct, but migrating every money column on a live database was judged higher risk than the drift it prevents. Mitigation: all money passes through `Money.Round` (2 dp, `AwayFromZero`) and comparisons use `Money.AreEqual` with `Epsilon = 0.005`. **Never compare amounts with `==`.**

**Delete became void.** Hard `DELETE` removed the amount from every balance leaving only an ID in the audit log. Records are now flagged `IsReversed` and retained with reason, actor and timestamp. `Delete*` method names are kept as wrappers over `Void*` so existing callers still compile.

**Duplicate prevention is application-level.** A `UNIQUE` index would fail to create against existing production duplicates. Instead, detection + allocation + insert run inside one `BeginImmediate` transaction, and existing duplicates are surfaced by the integrity report for human decision.

**Repairs are never automatic.** Automatic correction of financial data turns one mistake into an irreversible one. Detection is read-only; every repair is one item, one confirmation, one mandatory reason.

**`busy_timeout` is scoped to the write path**, set inside `ExecuteInTransaction` rather than `GetConnection()`, so no other form's behaviour changes.

**`last_insert_rowid()` must share the connection.** `Pooling` is off, so the previous pattern (`ExecuteNonQuery` then a separate `ExecuteScalar`) always returned 0 — 39 of 43 audit rows had `EntityID = 0`. `ExecuteInsertReturningId` runs both statements on one connection inside one transaction.

---

## 8. Future maintenance notes

**When adding any query that sums or lists financial records, add `AND COALESCE(IsReversed,0) = 0`.** Omitting it silently reintroduces voided amounts into balances. This is the single easiest way to break the module.

**When adding a write, add `AND (@cid = 0 OR CenterID = @cid)`** and call `EnsureMutable` first.

**Known open items:**

- **≈6.97M AFN reconciliation gap.** Sum of fund balances (14,320,839) ≠ sum of period closings (7,349,339). Every report is internally correct; the gap is records with no `PeriodID`, which fund balances count and period reports cannot see. Closes only when the orphans are assigned via the repair tool — requires accountant judgment.
- `AccSettings` has no `CenterID`, so letterhead, logo and signatures are shared across all centres. Pre-existing.
- Money remains `REAL` — mitigated, not eliminated.
- Duplicate prevention is app-level; direct database writes bypass it.
- Two production transactions carry `DollarAmount` with no `DollarRate` (implied 66.10); the conversion record is incomplete.

**Tests:** `CaseManagement.Tests` (net472, MSTest), 60 tests, all passing. Each test builds a real temporary SQLite database via `AccountingInitializer` — deliberately not mocked, because most of the module's logic lives in SQL (centre filters, reversal filters, aggregation, atomicity, `last_insert_rowid`), which is exactly what a mock would hide. `RepairTests` covers the repair engine; the rest cover money, balances, reversal, validation, duplicates and concurrency.

**Not covered by tests:** the WinForms layer. `FrmAccountingRepair` was smoke-tested manually against a production copy (a one-selection-behind bug in `Selected()` was found and fixed there); reinstating that harness requires adding `System.Windows.Forms` / `System.Drawing` references to the test project.

**Before deploying any schema change:** back up `CaseDB.sqlite`, run the migration against a copy, and compare row counts and column sums before/after.
