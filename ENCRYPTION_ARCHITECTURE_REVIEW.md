# ENCRYPTION & SECURITY ARCHITECTURE REVIEW
## CaseManagement — Data-at-Rest Protection Strategy

| | |
|---|---|
| **Document Type** | Architectural analysis and recommendation only — **no code changed** |
| **Date** | 2026-08-23 |
| **Builds on** | `SYSTEM_AUDIT_REPORT.md` §10.4 (CM-01/SEC-003) · `RELEASE_READINESS_AUDIT.md` §6 |
| **Goal** | Determine the safest, lowest-risk encryption strategy for the current WinForms + SQLite architecture — not necessarily the most complete one |

---

# 1. Current Architecture — Data-at-Rest Surface

## 1.1 SQLite Database Location

`App.config:23` — `Data Source=|DataDirectory|\CaseDB.sqlite;Version=3;`. `DAL/DatabaseHelper.cs` resolves `|DataDirectory|` to `AppDomain.CurrentDomain.BaseDirectory` (the installed application folder) unless something else sets `AppDomain.SetData("DataDirectory", ...)` first — nothing else in the shipped app does. **One SQLite file, at the install location, typically `Program Files\CaseManagement\CaseDB.sqlite`.** `GetConnection()` is a single chokepoint — every connection in the entire app goes through this one method (this matters a great deal for how cheap encryption integration would be, see §2).

Package in use: `System.Data.SQLite.Core` **1.0.115.5** (`packages.config`), the standard free/open build. **No encryption call exists anywhere in the codebase** — confirmed by a full search for `SetPassword`, `Aes`, `SQLCipher`, `ProtectedData` (zero matches outside password hashing and license-token signing, neither of which encrypts data at rest).

## 1.2 Backup Mechanism

`Helpers/BackupHelper.cs` (54 KB): `ExportBackup` serializes ~17 tables into a single `System.Data.DataSet`, then calls `dataSet.WriteXml(path, XmlWriteMode.WriteSchema)` — **a plain, human-readable XML file** (`CaseManagementBackup.xml`), plus a `File.Copy` of the entire attached-documents/photos folder into the backup directory. `Helpers/AccountingBackupHelper.cs` does the identical thing for the 11 `Acc*` tables, as a **separate, parallel backup**. `Helpers/AutoBackupService.cs` runs this same export automatically (daily/weekly/monthly, configurable) to a predictable path. **None of these three paths compress, checksum, or encrypt anything.** A backup is a folder anyone with file access can open in a text editor.

## 1.3 Document/Photo Storage

`Helpers/FileHelper.cs`: default root is `Application.StartupPath\CaseFiles` (inside the install folder), but an Admin/SuperAdmin can redirect it to any folder via `SetBaseRootFolder` — the chosen path is remembered in a small pointer file at `%AppData%\CaseManagement\BaseRootFolder.txt`. Four sections (`HeadPhoto`, `FamilyPhoto`, `MemberPhotos`, `Docs`), plain files on disk, no encryption, no content-type/malware scanning (a separate, already-tracked gap — `RELEASE_READINESS_AUDIT.md` §5 territory, not this document's concern).

## 1.4 Sync Architecture

`Sync/` (15K+ LOC, 33 files) moves **structured data**, not raw files: an outbox pattern (`SyncOutboxService`) captures each mutation, and `HttpSyncTransport` (online, user-supplied URL — the in-app example uses `https://...trycloudflare.com`, but plain `http://` is not blocked by validation) or `HttpFileSyncTransport`/`SyncFileService` (offline, USB/courier package exchange) carries it to/from a remote server. **Crucially: nothing in Sync ever touches the raw `.sqlite` file or the raw backup archive.** It reads/writes through the same `DatabaseHelper` connection chokepoint as everything else. This is the single most important fact for the options analysis below — it means live-database encryption is largely *transparent* to Sync, and backup encryption has *zero* interaction with Sync at all.

## 1.5 Multi-Center Implications

This is **not** one shared database — it is a **distributed architecture**: each branch office runs its own standalone installation with its own local `CaseDB.sqlite`, scoped by `CenterGuard`/`SecurityContext.CenterFilterId` to (mostly) that branch's own data, and Sync reconciles branches against a head-office/central server. A SuperAdmin can select "All Centers" to see aggregated data on one install (typically head office). **Consequence for encryption: there is no single database to protect — there are potentially dozens of independent SQLite files on dozens of independent machines, each of which would need its own key if the live database were encrypted.** Key management is therefore inherently a **per-site, decentralized problem**, not a single central-server problem. This is the architectural fact that most constrains which options are realistic.

## 1.6 Existing Login/Security Model

`Helpers/PasswordHelper.cs`: PBKDF2 (`Rfc2898DeriveBytes`), 100,000 iterations, 32-byte CSPRNG salt, constant-time comparison — genuinely strong, and already establishes a **proven cryptographic pattern in this exact codebase** that any new encryption work should reuse rather than reinvent. `SecurityContext` holds session state **in memory only** (no persisted token, no "remember me"); a session ends when the process exits. There is no master password, no key-escrow mechanism, and no existing concept of "a secret the app needs to unlock data" anywhere in the current design — this is new territory, not an extension of something that exists.

---

# 2. Options Evaluated

## Option A — SQLCipher (full-database transparent encryption)

Page-level AES-256 encryption of the entire `.sqlite` file, transparent to SQL once a key is supplied via `PRAGMA key` at connection open.

| Dimension | Assessment |
|---|---|
| **Security level** | **Very high.** Industry-standard, protects the entire live database including every column, not just specific fields. |
| **Development effort** | **High.** `System.Data.SQLite.Core` 1.0.115.5 (the package this project uses) does **not** include SQLCipher support in its free build — that requires swapping to a SQLCipher-enabled native provider (a different, less-common NuGet package or a self-compiled native `SQLite.Interop.dll`). This is a **native binary replacement**, not a NuGet version bump. |
| **Risk of breaking existing code** | **Medium.** SQL syntax and the ADO.NET surface stay identical if a drop-in-compatible provider is used, and `DatabaseHelper.GetConnection()` being the sole chokepoint helps enormously — but the native-binary swap is exactly the failure mode this project has **already been burned by**: `Installer/README.md` documents a known antivirus-quarantine issue with the *current, unsigned, non-encrypted* `SQLite.Interop.dll`. A SQLCipher-enabled native binary is an even less common, less-trusted binary in the eyes of antivirus heuristics — this risk would very likely get worse, not stay the same. |
| **Impact on Sync** | **Low.** Sync reads/writes through the same connection chokepoint; encryption is transparent to it (§1.4). |
| **Impact on Backup/Restore** | **None, and that's a hidden gap.** `BackupHelper.ExportBackup` queries data through the connection and writes a fresh plaintext XML — SQLCipher encrypts the *live file*, not the *exported backup*. **SQLCipher alone does not close the backup-exposure gap identified in `RELEASE_READINESS_AUDIT.md` §5** — it would need to be paired with Option D anyway for full coverage. |
| **Performance impact** | Low-moderate (~5-15% typical overhead for page encrypt/decrypt) — not a practical concern at this app's data volume. |
| **Deployment complexity** | **High.** New native binaries per architecture (x86/x64), a **key-management/master-password UX that does not exist today** (§1.6), and — critically — a **migration tool to re-encrypt every already-deployed plaintext database in the field**, across an unknown number of independently-run branch installations (§1.5), each of which would need the new binaries, a key, and a supervised one-time conversion. |

## Option B — SQLite file-encryption wrappers

Covers two sub-approaches: (a) `System.Data.SQLite`'s legacy built-in password/encryption feature, and (b) a generic "encrypt the file when closed, decrypt when opened" wrapper.

| Dimension | Assessment |
|---|---|
| **Security level** | **Not practically available as described.** (a) The built-in encryption feature was removed from the free `System.Data.SQLite` build years ago (it now requires a commercially-licensed SEE build) — confirmed by the complete absence of `SetPassword`/`Password=` anywhere in this codebase despite the app being well past that transition. (b) Wrapping a *live, continuously-open* database file with whole-file encryption is fundamentally unsound: SQLite holds the file open with active locks throughout a running session, so there is no safe moment to encrypt/decrypt it without either blocking all access or leaving a plaintext temp copy on disk — which just relocates the exposure rather than closing it. |
| **Development effort** | N/A for the live DB. (Sub-approach (b) *is* viable for backup files, which is exactly Option D — see below.) |
| **Everything else** | Not evaluated separately; this option collapses into Option A (if a licensed SEE build were purchased) or Option D (if scoped to backups only). Listed here to document why it was considered and ruled out as a distinct path. |

## Option C — Windows DPAPI

`System.Security.Cryptography.ProtectedData`, keyed to either the current Windows user or the local machine.

| Dimension | Assessment |
|---|---|
| **Security level** | **Medium, and scope-dependent.** `LocalMachine` scope protects against a stolen *file* being read on a *different* machine, but any process/user on the *same* machine can still decrypt it — weak against a compromised or malicious local user. `CurrentUser` scope is stronger locally but **fails outright** if the app is used by multiple staff sharing one Windows login, or if a machine is reimaged/replaced — a real risk given this is exactly the kind of low-IT-maturity field deployment this system targets. |
| **Development effort** | **Low-medium.** Built into .NET Framework, no native binaries, small well-documented API. |
| **Risk of breaking existing code** | **Low**, if scoped narrowly (e.g., wrapping only a stored backup passphrase or the backup file itself) rather than applied at the database-column level. |
| **Impact on Sync** | **None**, if scoped to backups. **High**, if ever extended to field-level column encryption — `SyncComparer`/`SyncApplier`/`SyncConflictAnalyzer` all currently compare raw field values for change-detection and conflict resolution; encrypted fields would break that comparison logic entirely and require a substantial redesign. **Field-level DPAPI encryption is explicitly not recommended for this reason.** |
| **Impact on Backup/Restore** | Low, if used only to protect a *stored* backup passphrase rather than the archive content itself (DPAPI-encrypted backup archives cannot be restored on a different/replaced machine under `CurrentUser`/often not under `LocalMachine` either after a reimage — a poor fit for a *disaster recovery* artifact, whose entire purpose is portability off the original machine). |
| **Performance impact** | Negligible at the scale this would be used. |
| **Deployment complexity** | **Low.** No extra native binaries, no extra install steps — genuinely the lowest-friction option mechanically. Its weakness is entirely about *what it protects against*, not deployment cost. |

**Verdict on DPAPI:** best used as a small supporting piece (e.g., protecting a locally-stored backup passphrase so it isn't sitting in plaintext in `TblSettings`) rather than as the primary encryption mechanism for anything that needs to survive leaving the original machine.

## Option D — Encrypted Backups Only

Leave the live database and document/photo files exactly as they are; encrypt the *output* of `BackupHelper.ExportBackup`, `AccountingBackupHelper.ExportBackup`, and `AutoBackupService`'s scheduled archives, using AES with a key derived (PBKDF2) from an admin-supplied passphrase — reusing the exact cryptographic pattern already proven in `PasswordHelper.cs`.

| Dimension | Assessment |
|---|---|
| **Security level** | **Medium, explicitly bounded.** Protects the single artifact the original audit called "the highest-value single-file exfiltration target" (a backup is portable, predictably located, and exactly what an office thief or departing employee would take). **Does not protect** the live machine if it is stolen or compromised while running — the live `.sqlite` file and `CaseFiles` folder remain fully plaintext after this change. This gap must be stated plainly to whoever approves this recommendation, not glossed over. |
| **Development effort** | **Low.** Touches 3-4 existing files plus one new small helper; no architectural change. |
| **Risk of breaking existing code** | **Low.** The backup format is entirely self-contained — nothing outside `BackupHelper.cs`/`AccountingBackupHelper.cs` reads `CaseManagementBackup.xml` directly. A format-version marker lets `ImportBackup` transparently detect and still read old, pre-encryption backups (§6). |
| **Impact on Sync** | **None.** Sync never touches backup files (§1.4). |
| **Impact on Backup/Restore** | This *is* the change — direct and contained. |
| **Performance impact** | **Negligible.** Encryption only runs during the already-slow, infrequent export/import operation, never on a hot query path. |
| **Deployment complexity** | **Low.** Pure managed .NET (`System.Security.Cryptography.Aes`), already part of .NET Framework — **zero new native binaries**, which specifically avoids re-triggering the antivirus/native-DLL quarantine risk this project has already documented (Option A's biggest deployment liability). |

## Option E — Hybrid Approaches

The only hybrid worth naming explicitly: **Option D now, as a deliberate, scoped first phase of a longer-term plan toward Option A (SQLCipher) later**, once (a) the native-binary/code-signing groundwork already flagged as an open TODO in `Installer/README.md` is resolved, and (b) a proper key-management/master-password UX — a real product decision requiring stakeholder input on recovery procedures and staff-turnover handling — has been designed, not improvised under a code-change deadline. A secondary, low-priority addition: use DPAPI (`LocalMachine` scope) to protect the *stored auto-backup passphrase* itself (§1.6/Option C), so unattended scheduled backups can encrypt without a human typing a passphrase every night, without leaving that passphrase sitting in plaintext in the settings table.

---

# 3. Recommendation for Version 1.0

## **Option D — Encrypted Backups Only, explicitly scoped as Phase 1 of a longer-term roadmap toward Option A.**

This is the answer to the question actually asked — *safest and lowest-risk*, not *most complete*. Reasoning, weighed directly against the other options:

- It is the only option with **low** development effort, **low** risk of breaking existing code, **low** deployment complexity, and **zero** impact on Sync, simultaneously.
- It specifically **avoids re-triggering a risk this exact project has already documented in production** (the native-DLL antivirus-quarantine issue in `Installer/README.md`) — Option A would very plausibly make that problem worse, not just risk a new one.
- It closes the **single highest-value exfiltration target** named in the original system audit, immediately.
- It **reuses a cryptographic pattern already proven in this codebase** (`PasswordHelper.cs`'s PBKDF2 approach) rather than introducing new crypto primitives.
- It does **not** require solving the hardest, least-code-related problem first — key management across dozens of independent branch installations (§1.5) — before shipping *anything*. Option A cannot ship without solving that; Option D can ship without solving the *harder* version of it (a per-machine live-database key), only the *easier* version (an admin-chosen backup passphrase).

**What this recommendation deliberately does not claim:** it does not make this "an encrypted system." The live database and attached documents/photos remain exactly as exposed as they are today. This must be communicated as a bounded, honest first step — not sold as closing CM-01. A stolen or compromised *running* machine is still a full data breach after this change ships. If leadership needs the live database itself protected before Version 1.0 can go to real charity centers, that is a legitimate position — it just means accepting Option A's much higher cost, longer timeline, and its own new deployment risk, and that decision should be made explicitly, with this document's cost comparison in hand, not by default.

---

# 4. Exact Files That Would Need Modification (Option D)

| File | Change |
|---|---|
| `Helpers/BackupEncryption.cs` *(new)* | AES-256 encrypt/decrypt stream helpers + PBKDF2 key derivation from a passphrase, mirroring `PasswordHelper.cs`'s existing iteration/salt pattern |
| `Helpers/BackupHelper.cs` | `ExportBackup`: zip the backup folder (`System.IO.Compression.ZipFile`, already in .NET Framework 4.5+, no new dependency) then encrypt the zip. `ImportBackup`: detect format-version marker — encrypted-zip vs. legacy plaintext folder — prompt for passphrase only when needed, decrypt+unzip before the existing import logic runs unchanged |
| `Helpers/AccountingBackupHelper.cs` | Identical treatment for the `Acc*` backup path |
| `Helpers/AutoBackupService.cs` | Scheduled backups also encrypted; needs a **stored** passphrase since no one is present to type one — this is the one real design decision in this whole option (see §6) |
| `FrmSettings.cs` | Passphrase prompt on manual backup-now/restore; a Settings field to configure the auto-backup passphrase (stored via DPAPI so it isn't plaintext in `TblSettings` — the one place Option C earns its keep here) |
| `Accounting/FrmAccounting.cs` | Same UI addition for `ExportAccountingBackup`/`ImportAccountingBackup` |
| `CaseManagement.Tests/BackupEncryptionTests.cs` *(new)* | Round-trip encrypt/decrypt, wrong-passphrase rejection, and — importantly — confirms a **pre-upgrade plaintext backup is still importable** after this ships |

## Estimated Development Days

| Task | Days |
|---|---|
| Core encryption helper (AES + PBKDF2 + zip step) | 1.0 |
| `BackupHelper.cs` integration (export + import + format detection) | 1.5 |
| `AccountingBackupHelper.cs` integration | 0.5 |
| `AutoBackupService.cs` integration + stored-passphrase design | 1.0 |
| UI (`FrmSettings.cs` + `FrmAccounting.cs`) | 1.0 |
| Tests | 1.0 |
| Manual backup/restore drill on realistic data (closes part of the gap `RELEASE_READINESS_AUDIT.md` §5 already flagged) | 1.0 |
| Buffer / documentation | 0.5 |
| **Total** | **≈ 7.5 developer days** |

*For comparison: Option A (SQLCipher) is realistically 15-20+ development days, **not counting** the separate, longer key-management design and stakeholder-decision cycle that would have to happen before implementation could even start.*

---

# 5. Migration Strategy for Existing Databases

- **The live database and document/photo files need no migration at all** — Option D never touches them. This is itself part of why this option is low-risk.
- **Backups taken before this change ships remain, and stay, unencrypted** — they are historical artifacts, out of scope for retroactive encryption. Recommend a documented *operational* (not code) step: once the new version is deployed, an admin takes one fresh encrypted backup and securely deletes old plaintext backup folders.
- **Forward compatibility is the real migration mechanism**: `ImportBackup` must detect the new format by a version/magic marker and, if absent, fall back to the existing plaintext-XML-folder parsing path completely unchanged. This guarantees a backup taken by a branch office that hasn't yet updated to this version — or taken before this feature existed — is still restorable after the upgrade.

---

# 6. Rollback Strategy

- **Code rollback is simple and safe**: Option D is purely additive (a new encryption wrapper around export, with backward-compatible detection on import) — no schema change, no data migration to undo. Reverting the commit fully reverts the feature.
- **The one real risk this introduces is a lost passphrase** — this is inherent to *any* encryption scheme, not specific to how it's implemented, but it must be solved as a **product/operational decision before this ships**, not left implicit: without a documented key-recovery or escrow procedure (e.g., a SuperAdmin-held written copy, or a generated recovery passphrase stored separately), a lost passphrase makes that backup permanently unrecoverable — a strictly worse outcome for that specific backup than today's zero-encryption state. This is the one item in this entire document that is a **decision**, not a coding task, and it should be resolved explicitly before implementation begins.
- **Operational escape hatch recommended during rollout**: ship a settings flag that lets an admin temporarily fall back to plaintext export while any post-deploy issue is investigated, consistent with `CLAUDE.md`'s stability-first priority — encryption should never become the reason a center *can't* get a working backup out during an actual emergency.

---

*Analysis and recommendation only. No code was written or modified.*
