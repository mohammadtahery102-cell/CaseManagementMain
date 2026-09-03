# SYSTEM_HARDENING_AUDIT.md

**Audit only. No code modified.**

Date: 30 August 2026
Scope: Security · Sync & disaster recovery · ID card · UI/UX (FrmCase, FrmFamily, "FrmGuardian", Dashboard)
Method: direct source and schema inspection; live database sampled at `bin/Debug/CaseDB.sqlite`

---

## 0. Executive summary

The system is considerably more hardened than a first glance suggests. Account lockout, session timeout, a permission matrix, record locking, security-event auditing, DPI awareness and encrypted backups are all genuinely implemented — several of them well. Two prior assumptions I had carried were **wrong and are corrected below**.

The serious findings cluster in three places:

1. **The database file itself is unencrypted.** Every app-level control — lockout, permissions, center isolation — is advisory to anyone who can copy `CaseDB.sqlite`.
2. **There is no full-resync capability anywhere in the codebase.** The procedural mitigation for post-disaster sync divergence cannot actually be performed.
3. **Editing a case and closing the window discards the work silently.** No unsaved-changes guard exists on either `FrmCase` or `FrmFamily`.

### Findings by severity

| Severity | Count | Items |
|---|---|---|
| 🔴 **Critical** | 2 | Unencrypted database · No full-resync path after disaster recovery |
| 🟠 **High** | 5 | No unsaved-changes guard · Batch-print 20s timeout can print partial output · No recovery runbook · Dashboard fully synchronous · WebView2 Runtime not installed by installer |
| 🟡 **Medium** | 6 | PBKDF2-SHA1 at 100k iterations · Lockout counter never time-decays · Lockout as denial-of-service vector · Refresh token stored in plaintext · Oversized forms · Dashboard refresh on every navigation return |
| 🔵 **Low** | 4 | Login timing side-channel · Clock-dependent lockout · No batch-size cap · `TblAuditLogs` dead table |

### Two corrections to previously recorded assumptions

| Previously believed | Actual |
|---|---|
| "No account lockout or session timeout defined" (carried since the early project analysis) | **Both are fully implemented.** Lockout with configurable threshold/duration at `FrmLogin.cs:580–732`; session timeout via a global `IMessageFilter` at `Helpers/SessionTimeoutMonitor.cs` |
| "Permission enforcement is UI-only" | **Center isolation is enforced in SQL**, not just the UI — 184 occurrences of the `(@cid = 0 OR CenterID = @cid)` pattern, plus `CenterGuard.EnsureCaseAccess` on record-level entry points |

---

## 1. Security

### 1.1 🔴 CRITICAL — The database is not encrypted at rest

`App.config`:
```xml
<add name="CaseDb" connectionString="Data Source=|DataDirectory|\CaseDB.sqlite;Version=3;" />
```

No `Password=`, no `PRAGMA key`, no SQLCipher. The file is plaintext SQLite.

Anyone with read access to the file obtains, with no credentials at all: every case record, national ID (تذکره) numbers, phone numbers, addresses, family composition, disability and health status, the full financial ledger, and the `TblUsers` password hashes for offline cracking.

**This subordinates every other security control in this document.** Lockout, session timeout, the permission matrix and center isolation are all enforced *by the application*; none of them are enforced by the data. On a shared or stolen provincial-office machine they are bypassed by copying one file.

Consistency note: backups **are** AES-encrypted (`BackupEncryption.cs`), and the automatic backup refuses to run without a password. So the project already treats this data as sensitive in transit and at rest *in backups* — the live database is the gap in an otherwise deliberate posture.

### 1.2 ✅ Account lockout — implemented, with two caveats

`FrmLogin.cs:580–732`. Genuinely good:

- Configurable `MaxFailedAttempts` (default 5) and `LockoutMinutes` (default 15)
- Counter resets on successful login
- Both failures and lock events written to `EntSecurityEvent`, the lock itself at **Critical** severity
- Identical message for "unknown user" and "wrong password" — no username enumeration via response text
- `LockoutUntil` parsed with `InvariantCulture`, with a comment recording a previously-fixed bug where Persian-calendar parsing produced a ~621-year lockout

**🟡 MEDIUM — the failure counter never decays with time.** It is cleared *only* on successful login (`:724–732`). After one lockout expires, `FailedLoginCount` remains at 5, so the very next wrong password satisfies `newFailedCount >= MaxFailedAttempts` and re-locks immediately. A legitimate user who has forgotten their password gets one attempt per 15 minutes, indefinitely. Whether this is intended hardening or an oversight is a product decision — but it is not what "5 attempts" implies to an administrator reading the setting.

**🟡 MEDIUM — lockout is a denial-of-service vector.** Knowing a username is enough to keep that account permanently locked by failing five times every fifteen minutes. Standard mitigation (per-IP throttling) does not apply cleanly to a LAN desktop app; the practical mitigation is administrator visibility, which exists via `FrmSecurityAudit`.

**🔵 LOW — timing side-channel.** An unknown username returns immediately, while a known username runs 100,000 PBKDF2 iterations first. The measurable difference allows username enumeration despite the identical message.

**🔵 LOW — lockout is wall-clock dependent.** `LockoutUntil` is compared against local `DateTime.Now`; a user able to change the system clock bypasses it. Low impact, since that user typically also has file access (see 1.1).

### 1.3 🟡 MEDIUM — Password hashing is below current guidance

`Helpers/PasswordHelper.cs`: PBKDF2 via `Rfc2898DeriveBytes`, 32-byte random salt from a CSPRNG, 32-byte output, **100,000 iterations**, with per-user iteration counts stored so legacy 10,000-iteration hashes remain verifiable. The design is sound and the migration path is well handled.

The gap: on .NET Framework 4.7.2 the `Rfc2898DeriveBytes(string, byte[], int)` constructor uses **HMAC-SHA1**. OWASP's current guidance is PBKDF2-HMAC-SHA256 at 600,000 iterations, or PBKDF2-HMAC-SHA1 at 1,300,000. At 100,000 SHA-1 iterations the work factor is roughly an order of magnitude below recommendation.

This matters mainly *because of 1.1* — the hashes are readable offline. Raising the iteration count or moving to the SHA-256 overload is straightforward given the existing per-user `PasswordIterations` column, which was clearly designed for exactly this.

### 1.4 ✅ Session timeout — implemented, and better than typical

`Helpers/SessionTimeoutMonitor.cs`, started at `Program.cs:105`. Uses a global `Application.AddMessageFilter` observing mouse and keyboard messages, so activity is detected **inside modal dialogs** too — a common failure mode this implementation explicitly avoids. Checks every 30 seconds.

On timeout it warns and closes the application entirely rather than attempting to unwind open dialogs. The code documents this as a deliberate safety choice. It is defensible, but note the interaction with 1.6: **an idle timeout that closes the app will discard unsaved case edits without prompting.**

### 1.5 ✅ Permission enforcement — layered, with the expected structural limit

- 64 `HasPermission` / `Require` call sites across 20 files
- Role permissions overlaid by per-user exceptions (`PermissionService.GetCache`)
- `Require` writes a `PermissionDenied` security event on refusal
- Unknown permission keys fall through to `LegacyFallback`, which infers from the key suffix and — worth noting — **defaults unknown non-suffixed keys to "any logged-in user allowed"** (`PermissionService.cs:143`). Fail-open rather than fail-closed. Acceptable given it exists for backwards compatibility, but a new feature that forgets to register its key silently becomes public to all users.
- Center isolation is applied **in SQL** (184 sites) and via `CenterGuard` on record entry points — this satisfies the "database level, not just UI" requirement in the original specification.

Structural limit: SQLite has no row-level security, so all of the above is application-tier. See 1.1.

### 1.6 🟡 MEDIUM — Refresh token stored in plaintext

`SyncState.RefreshToken` is written unencrypted (`HttpSyncTransport.cs:209`). Combined with 1.1, a stolen database file yields a usable sync credential. Recent backup work correctly excludes this key from backups, so the exposure is limited to the live file — but the underlying storage is still plaintext.

---

## 2. Sync, disaster recovery and runbook

### 2.1 🔴 CRITICAL — No full-resync capability exists

A codebase-wide search for `resync`, `full sync`, `push all`, `rebuild outbox`, `reset baseline` and equivalents returns **nothing**. There is no way — in the UI, in DevCenter, or programmatically — to force a complete re-push of local data to the server.

Why this is critical rather than merely missing:

- Upload reads **only** from `SyncOutbox` (`SyncOutboxService.cs:285`)
- `SyncOutbox` is deliberately excluded from backups (correctly — replaying another install's queue is unsafe)
- Therefore, after a disaster recovery, any operations that had not yet been sent are gone from the queue, and **nothing can regenerate them**

The office's data is intact locally, and head office's data is intact centrally, but the two are silently divergent with **no supported path to reconcile them**. The nearest existing tool, `DevCenterRepair`'s "retry failed rows", only re-attempts rows *already present* in the outbox — it cannot rebuild a lost one.

I previously recommended documenting a post-recovery full re-sync as a procedural mitigation. That recommendation was not implementable: the procedure has no product capability behind it.

### 2.2 🟠 HIGH — No disaster recovery runbook

No operational document describes: restoring a backup, verifying the restore, what is intentionally *not* restored (device identity, sync queue, record locks), or what an administrator must do afterwards.

`Sync/FrmSyncHelp.cs` is 627 lines of in-app help and contains **zero** references to recovery, disaster, or full resynchronisation.

This matters most for the people least equipped to improvise: a provincial office with intermittent connectivity and no on-site IT support. The technical recovery path works and is well tested; the human procedure around it is undocumented.

### 2.3 ✅ What is solid

- Restore runs in a single transaction with rollback on any error
- A destructive legacy-format restore is gated to SuperAdmin only
- Backups are AES-encrypted; automatic backup refuses to run without a password
- A real disaster-recovery drill exists with negative-control coverage
- Sync conflict detection, baseline tracking and device-scoped state are properly separated
- Absence of `SyncBaseline` is already handled as "first sync" rather than failing

---

## 3. ID card module

Substantial and well-built: 6,348 lines across 11 files, with template versioning (`GetVersions` / `RestoreVersion`), duplication, activation state, per-field toggles, field ordering and text overrides.

### 3.1 🟠 HIGH — Batch print can silently produce partial output

`FrmGuardianCardBatchPrint.cs:538`:
```csharp
await Task.WhenAny(renderDone.Task, Task.Delay(TimeSpan.FromSeconds(20)));
if (!renderDone.Task.IsCompleted)
    _webView.CoreWebView2.WebMessageReceived -= onRenderDone;

ShowRenderStage();
string statusText = items.Count + " کارت آماده شد";
```

If rendering exceeds the fixed 20-second budget, the code **detaches the completion handler and proceeds regardless**, then reports `"{items.Count} cards ready"` and enables Print and Save-as-PDF.

The count reported is the number of cards *requested*, not the number actually rendered. For a large batch on a slow provincial machine, the operator is told the job succeeded and prints an incomplete set — with no error and no indication which cards are missing. Printed ID cards are physically distributed to beneficiaries, so a silent partial run is discovered late and is expensive to correct.

### 3.2 🟠 HIGH — WebView2 Runtime is not installed by the installer

Card rendering requires the Microsoft Edge WebView2 **Runtime**, a separate machine-wide component. `Installer/Guardian.iss` references WebView2 only in a comment about x64 native libraries; there is no Evergreen bootstrapper step. `Installer/README.md` refers to bundled "SQLite/WebView2 natives", which are the .NET wrapper assemblies, not the Runtime.

The code degrades politely (`"برای نمایش کارت‌ها به Microsoft Edge WebView2 Runtime نیاز است"`), so this is a deployment gap rather than a crash — but on an offline machine the operator cannot resolve it without a download.

### 3.3 🔵 LOW — No batch size cap

Selection is unbounded; `rows.Count` flows straight into rendering. A filter matching thousands of cases produces a very large WebView2 render with only the 20-second timeout as a backstop — which is precisely the condition that triggers 3.1.

### 3.4 ✅ Strengths worth preserving

- **QR generation uses the QRCoder library**, not hand-rolled Reed–Solomon. The code comments explicitly record rejecting a manual implementation as unverifiable without a real scanner — exactly the right call.
- Template changes are versioned and restorable
- Center access is enforced through `CenterGuard.EnsureCaseAccess` in `CaseCardRepository` (three separate entry points)
- A confirmation dialog precedes batch printing, and per-case failures are counted and surfaced (`failedCount`)
- Rendering is `async`, so the UI is not blocked

---

## 4. UI / UX

### 4.0 Scope note — `FrmGuardian` does not exist

There is no `FrmGuardian` form in the codebase. The guardian (سرپرست) is not a separate entity: guardian data lives as `Head*` columns on `TblCase` (`HeadFullName`, `HeadFatherName`, `HeadTazkiraNo`, `HeadSadat`, `HeadBirthDate`, `HeadEducationLevel`, `HeadIdCardType`, `HeadOriginalResidence`, `HeadCurrentResidence`) and is edited within `FrmCase`. The only guardian-named forms are `FrmGuardianCardPreview` and `FrmGuardianCardBatchPrint`, covered in §3.

I have audited the guardian surface as it actually exists rather than assume a form that isn't there.

### 4.1 🟠 HIGH — No unsaved-changes guard (silent data loss)

`FrmCase.OnFormClosing` (`:2871–2882`) releases the record lock and stops the heartbeat, then closes. It performs **no dirty check and shows no prompt**. `FrmFamily` has no `FormClosing` override at all.

A user who edits a case — potentially many fields — and closes the window, presses Escape, or is closed by the idle session timeout (§1.4) loses the work with no warning. In a data-entry-heavy application where forms carry 35+ fields, this is the most likely everyday cause of lost work.

The building blocks are already present: the form tracks a record lock and knows its own lifecycle. Only the dirty-state check is missing.

### 4.2 🟠 HIGH — Dashboard is fully synchronous

`FrmDashboard.cs` is 3,225 lines with **zero** occurrences of `async`, `await`, `Task.Run` or `BackgroundWorker`. All eight query sites execute on the UI thread.

At the current 1,661 cases / 3,802 members this is likely tolerable. The specification targets 100,000+ cases and 500,000+ members, at which point aggregate queries and the trend charts will freeze the window with no progress indication.

### 4.3 🟡 MEDIUM — Dashboard refreshes on every navigation return

`RefreshAll()` is invoked after **every** child form closes — at minimum lines 127, 129, 131, 134, 146 and 207. Returning from any module re-runs the complete dashboard refresh synchronously. Combined with 4.2, the cost is paid on every navigation, not just at startup.

### 4.4 🟡 MEDIUM — Very large forms

| File | Lines |
|---|---|
| `FrmCase.cs` | 4,221 |
| `FrmDashboard.cs` | 3,225 |
| `FrmCardTemplateManager.cs` | 2,129 |
| `FrmFamily.cs` | 1,845 |

These are maintainability and regression-risk concerns rather than defects. `CLAUDE.md` correctly forbids speculative refactoring; noting them so the risk is visible when changes land in these files.

### 4.5 ✅ Strengths

- **RTL is applied centrally**, not per-form: `UiTheme` sets `RightToLeft` from `Lang.IsRightToLeft` and `UiTheme.ApplySweep(this)` propagates it. `FrmFamily` having no direct `RightToLeft` reference is correct inheritance, not a gap. The code carries detailed comments about label-alignment mirroring — this has clearly been worked through carefully.
- **DPI handling is correct**: `app.manifest` declares `PerMonitorV2, PerMonitor` with a documented fallback, and `ResponsiveLayout` deliberately uses `AutoScaleMode.Dpi` rather than `Font`, with the reasoning recorded (font changes must not rescale layout).
- **Record locking** in `FrmCase` with a heartbeat prevents concurrent-edit corruption, and locks are released on close.
- Validation is present and substantial — 116 validation-related sites in `FrmCase`, 93 in `FrmFamily`.
- Persian date handling is centralised (`PersianDateHelper`, `UiTheme.ApplyPersianDateColumns`).

---

## 5. Ranked findings

### 🔴 Critical

| # | Finding | Area | Why |
|---|---|---|---|
| C-1 | Database file unencrypted at rest | Security | One copied file exposes all beneficiary PII, financial records and password hashes; subordinates every other control |
| C-2 | No full-resync capability exists | Sync | After disaster recovery, office and head office diverge with **no supported reconciliation path** |

### 🟠 High

| # | Finding | Area |
|---|---|---|
| H-1 | No unsaved-changes guard on `FrmCase` / `FrmFamily` | UI/UX |
| H-2 | Batch print proceeds after 20s timeout and reports success | ID card |
| H-3 | No disaster recovery runbook | Sync |
| H-4 | Dashboard fully synchronous — will not scale to spec targets | UI/UX |
| H-5 | WebView2 Runtime not installed by installer | ID card / deployment |

### 🟡 Medium

| # | Finding | Area |
|---|---|---|
| M-1 | PBKDF2-HMAC-SHA1 at 100k iterations, below OWASP guidance | Security |
| M-2 | Lockout counter never decays — effectively 1 attempt per window after first lock | Security |
| M-3 | Lockout usable as targeted denial of service | Security |
| M-4 | `RefreshToken` stored in plaintext in `SyncState` | Security |
| M-5 | Dashboard `RefreshAll()` on every navigation return | UI/UX |
| M-6 | Oversized forms (4.2k / 3.2k / 2.1k / 1.8k lines) | Maintainability |

### 🔵 Low

| # | Finding | Area |
|---|---|---|
| L-1 | Login timing side-channel enables username enumeration | Security |
| L-2 | Lockout bypassable by changing the system clock | Security |
| L-3 | No batch-size cap on card printing | ID card |
| L-4 | `TblAuditLogs` is dead — read by DevCenter, written by nothing | Data hygiene |

---

## 6. Suggested sequencing (not implemented)

Ordered by value against effort and risk. Each is a separate decision for you.

| Order | Item | Rationale | Risk of the change |
|---|---|---|---|
| 1 | **H-1** unsaved-changes guard | Highest everyday harm, smallest well-understood change, no schema impact | 🟢 Low |
| 2 | **H-2** batch print timeout | Silent wrong output in a physically distributed artefact; localised fix | 🟢 Low |
| 3 | **H-3** recovery runbook | Documentation only, no code risk — but see C-2, which limits what the runbook can honestly promise | 🟢 None |
| 4 | **C-2** full-resync capability | Removes the ceiling on H-3; genuine feature work touching the sync engine | 🟠 Medium–High |
| 5 | **M-1** raise hash work factor | `PasswordIterations` column already exists for exactly this migration | 🟢 Low |
| 6 | **H-5** installer bootstrapper | Deployment packaging only | 🟢 Low |
| 7 | **C-1** database encryption | Highest security value, but the largest change: SQLCipher or equivalent affects the connection layer, backup/restore, sync and the test suite. Needs its own design pass | 🔴 High |

**A note on C-1 versus C-2 ordering.** C-1 is the more severe finding, but C-2 is the more *actionable* one: encrypting the database is a cross-cutting architectural change with real regression risk across everything the last four tiers stabilised, while the resync gap is contained within the sync module. I would not begin C-1 without a dedicated design and a rollback plan.

**A note on H-3.** Writing the runbook before C-2 exists means documenting a recovery procedure whose final step cannot be performed. The honest interim runbook must state that post-recovery divergence requires manual reconciliation — which is itself worth telling operators explicitly rather than leaving them to discover it.
