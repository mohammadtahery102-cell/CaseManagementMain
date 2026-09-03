# SYSTEM AUDIT REPORT
## CaseManagement — Orphan & Vulnerable Family Case Management System

| | |
|---|---|
| **Report Version** | 1.0 |
| **Audit Date** | 2026-08-21 |
| **Auditor Roles** | Software Architect · Security Auditor · QA Lead · Product Owner · Technical Writer · Business Analyst |
| **Codebase Location** | `C:\Projects\CaseManagement` |
| **Test Project** | `C:\Projects\CaseManagement.Tests` |
| **Audience** | Executives · Developers · Auditors · Testers · Future Maintainers |

---

## ⚠️ CRITICAL PREFACE — PLATFORM MISMATCH IN AUDIT REQUEST

The audit was requested as an **"Android project"** review covering Kotlin, Jetpack Compose, Material3, Room, Coroutines, Flow, Gradle, AndroidManifest, and Android device compatibility.

**None of these exist in this codebase.**

This system is a **Microsoft Windows desktop application**:

| Requested (Android) | Actual (This System) |
|---|---|
| Kotlin | C# 7.3 |
| Jetpack Compose / Material3 | Windows Forms (WinForms) |
| Room ORM | Raw ADO.NET over SQLite |
| Coroutines / Flow | `System.Threading.Tasks` / `BackgroundWorker` / `Timer` |
| Gradle | MSBuild (`.csproj` + `packages.config`) |
| AndroidManifest.xml | `App.config` + `app.manifest` |
| Android SDK 26–35 | .NET Framework 4.7.2 |
| APK / Play Store | `WinExe` + Inno Setup installer |

This report has been **fully re-mapped** to the real technology stack. Every numbered section you requested is delivered, with Android-specific sections translated to their genuine equivalents (Section 3 = .NET dependency audit; Section 7 = SQLite schema audit; Section 12 = Windows/.NET compatibility). No section has been dropped.

---

## 📋 AUDIT METHODOLOGY & LIMITATIONS

**What was performed:**
- Full file-tree enumeration and line-count analysis (184 non-designer C# files)
- Static reading of all initialization code, DAL, security services, sync services, backup engine, export engine, and report engine
- Complete database schema enumeration (all `CREATE TABLE` statements across 4 initializers)
- Cross-reference analysis (call-graph tracing) to identify orphaned/unenforced code
- Dependency inventory from `packages.config` and `.csproj`
- Full test suite execution: **346 tests, 344 passed, 2 skipped, 0 failed**
- Clean compile verification (MSBuild, 0 errors, 17 pre-existing warnings)

**What was NOT performed — findings in these areas are marked `Not Verified`:**
- Runtime execution on physical target hardware or multiple Windows builds
- Performance profiling, memory analysis, or load testing with production-scale datasets
- Line-by-line reading of all 49 forms (large forms audited by targeted inspection of save/delete/validate paths)
- Penetration testing or dynamic security testing
- Installer execution and upgrade-path validation
- WebView2 rendering behaviour and `Templates/*.js` harness correctness
- Network behaviour of the HTTP sync transport against a live server

**Confidence legend used throughout:**

| Symbol | Meaning |
|---|---|
| ✓ | **Fully Implemented** — verified present, wired, and reachable in code |
| ⚠ | **Partially Implemented** — exists but incomplete, unenforced, or unreachable |
| ✗ | **Missing** — not present in code |
| ❓ | **Not Verified** — could not be confirmed within audit scope |

---

# 1. Executive Summary

## 1.1 Project Identification

| Attribute | Value |
|---|---|
| **Project Name** | CaseManagement (`CaseManagement.exe`) |
| **Assembly GUID** | `{DC7974AB-0C21-4532-9A07-48C4468C74F5}` |
| **Domain** | Humanitarian / NGO case management |
| **Geography** | Afghanistan (Dari/Persian primary, Pashto/Arabic/Urdu/English supported) |
| **Deployment Model** | On-premise Windows desktop, single-file SQLite database, optional multi-branch sync |

## 1.2 Application Purpose

The system manages **case files for orphans and vulnerable families** served by a charitable organization. Each *case* represents a household headed by a guardian, containing family members (typically orphaned children), supporting documents, financial assistance records, and a full service-status lifecycle (pending → active → temporarily suspended → discontinued).

The application additionally provides an **internal accounting ledger** for the organization's own funds (income, expenses, stipends, salaries), **guardian ID card production**, **assistance receipt printing**, and a **multi-branch synchronization** capability for organizations operating across multiple provinces with unreliable connectivity.

## 1.3 Target Users

| User Type | Role in System | Primary Screens |
|---|---|---|
| **Field Surveyor / Data Entry Operator** | Registers cases, family members, documents | Case, Family, Documents |
| **Branch Manager (Admin)** | Approves, edits, deletes, runs reports | Dashboard, Reports, Archive, Duplicates |
| **Accountant** | Manages funds, transactions, stipends, salaries | Accounting, Finance |
| **Head Office Administrator (SuperAdmin)** | Cross-branch oversight, user management, permissions, sync | All modules + Dev Center |
| **Auditor / Inspector** | Reviews audit trail, change history, security events | Audit Report, Versions, Security Audit |

## 1.4 Technology Stack (Summary)

- **Language:** C# (7.3), .NET Framework **4.7.2**
- **UI:** Windows Forms, custom-themed, RTL-first, DPI-aware (PerMonitorV2)
- **Database:** SQLite via `System.Data.SQLite` 1.0.115.5 — **64 tables**, no ORM, hand-written parameterized SQL
- **Reporting:** Microsoft ReportViewer 15.0 (RDLC), ClosedXML 0.105 (Excel), DocumentFormat.OpenXml 3.1.1 (Word)
- **Other:** QRCoder, WebView2, SixLabors.Fonts, Microsoft.SqlServer.Types
- **Build:** MSBuild, Visual Studio 18, `packages.config` (legacy NuGet)
- **Installer:** Inno Setup (`.iss`)

## 1.5 Scale Metrics

| Metric | Value |
|---|---|
| C# source files (excl. designers) | **184** |
| Total lines of code (excl. designers) | **≈ 78,500** |
| Windows Forms | **49** |
| Database tables | **64** |
| Automated tests | **346** (344 pass / 2 skip / **0 fail**) |
| Supported UI languages | **5** (fa, ps, ar, ur, en) |
| Largest single file | `FrmCase.cs` — 3,789 lines |

## 1.6 Maturity Level

**Level: Late Beta / Early Production — feature-rich but with unenforced security controls.**

This is **not** a prototype. It is a substantial, mature codebase with genuine engineering discipline: a 346-test automated suite, extensive inline design rationale (in Persian), defensive error handling, incremental non-destructive database migrations, and evidence of numerous real bugs found and fixed with documented explanations.

However, it exhibits a **consistent and repeating structural weakness**: *sophisticated subsystems are built to completion but never wired into the application*. Three independent instances of this pattern were confirmed:

1. `VersionService` (full record-versioning engine) — was never called from any save path *(fixed during this engagement)*
2. `PermissionService` (fine-grained RBAC with role + per-user overrides) — **still never called** to gate any CRUD operation
3. `LicenseManager` (HMAC-signed licensing with hardware binding) — **still never enforced** anywhere

## 1.7 Completion & Readiness Estimates

> These are **reasoned estimates**, weighted by module size and criticality. They are not measured metrics.

| Dimension | Estimate | Basis |
|---|---|---|
| **Feature Completion** | **82%** | Core case/family/docs/finance/reporting complete; accounting complete; sync complete but risky; RBAC and licensing incomplete |
| **Production Readiness** | **61%** | Blocked primarily by unencrypted data at rest, unenforced permissions, and unvalidated sync |
| **Security Score** | **48 / 100** | See Section 10 |
| **Code Quality** | **68 / 100** | See Section 11 |
| **Architecture** | **58 / 100** | See Section 11 |
| **Test Coverage (est.)** | **~35%** | See Section 14 |

## 1.8 Overall Risk Assessment

### 🔴 HIGH — Overall Risk Rating

The single dominant risk is **confidentiality of highly sensitive humanitarian data**. This system stores, in **plaintext on disk**:

- Names, national ID (Tazkira) numbers, addresses, and phone numbers of orphans and widows
- Photographs of minors
- Disability status, marital status, economic vulnerability ratings
- Financial assistance amounts per household

The SQLite database, all attached document files, all photographs, and all backup archives are **entirely unencrypted**. Any person with file-system access — an office thief, a repair technician, a departing employee with a USB drive, or malware — obtains the complete dataset instantly. In the operating context (Afghanistan, vulnerable minority populations including *Sadat* and *Ahl-e Sunnat* communities explicitly tracked as data fields), this is not merely a compliance issue; it is a **physical safety risk to data subjects**.

**Secondary risks:**
- Fine-grained permissions are configurable by administrators but **enforce nothing**, creating a dangerous false sense of access control
- The synchronization subsystem is the largest and most complex module (15,179 LOC across 3 parallel transport paths) and carries correspondingly high defect probability
- Two orphaned database tables and one duplicated audit table indicate schema drift

---

# 2. System Overview

## 2.1 Business Purpose

A charitable organization operating across multiple Afghan provinces provides ongoing financial and material support to orphans and vulnerable families. The organization must:

1. **Register and vet** applicant households through field surveys
2. **Track eligibility** over time as circumstances change (a child ages out, a widow remarries, a family relocates)
3. **Disburse and record** monthly stipends and one-off assistance
4. **Prove accountability** to donors through auditable records and reports
5. **Operate offline** in provinces with unreliable internet, then reconcile with head office
6. **Produce physical artifacts** — guardian ID cards, assistance receipts, case dossiers

## 2.2 Core Workflow

```
   APPLICANT INTAKE                CASE LIFECYCLE                  SERVICE DELIVERY
   ────────────────                ──────────────                  ────────────────

   ┌──────────────┐
   │ TblApplicant │  Prospective household registered
   │  "در انتظار" │  (name, phone, province, request type)
   └──────┬───────┘
          │ Field survey conducted
          │ Converted (ConvertedCasID)
          ▼
   ┌──────────────────────────────────────────┐
   │              TblCase                     │
   │  Guardian identity, address, economic     │
   │  priority, disability, request type       │
   │  ServiceStatus: در انتظار تایید           │
   └──────┬───────────────────────────────────┘
          │
          ├──► TblFamily     (household members, roles, ages)
          ├──► TblDocs       (Tazkira scans, death certificates, photos)
          └──► TblCaseRelation (links to related households)
          │
          │ Approval / activation
          ▼
   ServiceStatus: فعال ──────────────────────┐
          │                                   │
          │                                   ▼
          │                          ┌────────────────┐
          │                          │ TblAssistance  │  Per-case aid
          │                          │  + Receipt     │  printed receipt
          │                          └────────────────┘
          │                                   │
          │                          ┌────────────────┐
          │                          │ GuardianCard   │  ID card issued
          │                          └────────────────┘
          │
          ├──► قطع موقت  (temporarily suspended, with reason + timestamp + user)
          ├──► قطع        (discontinued, with reason)
          └──► IsArchived=1 (archived, restorable)

   Every transition writes: TblCaseStatusHistory + TblAuditLog + EntRecordVersion
```

## 2.3 User Journey

**First launch (fresh installation):**
1. `Program.Main` sets Persian culture and RTL caption hooks
2. Four initializers run in sequence, creating/migrating all 64 tables idempotently
3. `EnsureDefaultAdmin` creates a default administrator account
4. Auto-backup check runs; organization theme applied
5. Login form appears

**Daily operator journey:**
1. Login (username + password; account lockout after N failed attempts)
2. **Center selection** — user picks their branch; SuperAdmin may select "All Centers"
3. Dashboard loads with KPI cards, charts, and a module sidebar filtered by `ModuleService`
4. Operator opens *Cases* → creates or searches a case → switches to *Members* / *Documents* tabs
5. Exports a Word/PDF dossier or prints a guardian card
6. Session times out after configured inactivity (application closes with warning)

## 2.4 Major Modules

| # | Module | Folder | LOC | Purpose |
|---|---|---|---|---|
| 1 | **Core Case Management** | `(root)` | 20,660 | Cases, family, documents, applicants, archive, search, duplicates |
| 2 | **Helpers / Infrastructure** | `Helpers/` | 17,468 | Theming, RTL, Persian dates, file handling, backup, exports, i18n |
| 3 | **Synchronization** | `Sync/` | 15,179 | Offline queue, HTTP transport, file transport, conflict resolution, media sync |
| 4 | **Enterprise Core** | `Enterprise/` | 8,210 | Workflow, approvals, tasks, rules, locks, versions, permissions, modules, errors |
| 5 | **Accounting** | `Accounting/` | 6,646 | Funds, periods, transactions, stipends, salaries, expenses, integrity, repair |
| 6 | **Dev Control Center** | `DevCenter/` | 4,773 | Hidden diagnostics, health report, repair tooling |
| 7 | **Guardian Card** | `GuardianCardIntegration/` | 3,227 | ID card templates, rendering, barcode/QR, batch print |
| 8 | **Assistance Receipts** | `AssistanceReceiptIntegration/` | 1,890 | Aid package definitions, receipt rendering, batch print |
| 9 | **Data Access** | `DAL/` | 207 | Single `DatabaseHelper` — connection, transaction, query primitives |
| 10 | **Models** | `Models/` | 158 | 5 thin POCOs (largely vestigial — see §11) |

## 2.5 Data Flow

```
  ┌────────────────────────────────────────────────────────────────┐
  │                      WinForms UI Layer                          │
  │   FrmCase · FrmFamily · FrmDocs · FrmFinance · FrmAccounting    │
  └───────────────────────────┬────────────────────────────────────┘
                              │  Direct SQL construction in
                              │  event handlers (no ViewModel,
                              │  no Repository for core entities)
                              ▼
  ┌────────────────────────────────────────────────────────────────┐
  │                    DAL.DatabaseHelper                           │
  │   GetConnection() · ExecuteInTransaction() · Query()            │
  │   ExecuteInsertReturningId()   [ForeignKeys=ON, busy_timeout]   │
  └───────────────────────────┬────────────────────────────────────┘
                              ▼
  ┌────────────────────────────────────────────────────────────────┐
  │              SQLite file — CaseDB.sqlite (PLAINTEXT)            │
  │              |DataDirectory|\CaseDB.sqlite                      │
  └───────────────────────────┬────────────────────────────────────┘
                              │
      ┌───────────────────────┼───────────────────────┐
      ▼                       ▼                       ▼
  Side-effect writers    Export engines          Sync engines
  ─────────────────      ──────────────          ────────────
  AuditLogger            OpenXmlCaseExporter     SyncOutboxService
  VersionService         ExcelReportExporter     SyncService
  SyncOutboxService      RdlcExportHelper        SyncFileService
  ErrorLogger            GridReportExporter      MediaSyncEngine
```

**Architectural note:** There is **no repository layer for the core domain**. `FrmCase.cs`, `FrmFamily.cs`, and `FrmDocs.cs` build and execute SQL directly inside button-click handlers. Repository classes exist *only* in the two integration modules (`CaseCardRepository`, `AssistanceReceiptRepository`, `CardTemplateRepository`, `AssistancePackageRepository`). This is the single largest architectural inconsistency in the system.

## 2.6 Offline Workflow

The system is **offline-first by default** — it is a local desktop application with a local database and requires no network to function.

Optional multi-branch synchronization operates through an **outbox pattern**:

1. Every mutating operation calls `SyncOutboxService.Capture(entity, id, operation)`, writing a row to `SyncOutbox` with state `Pending`
2. `BackgroundSyncManager` periodically (configurable interval) attempts to drain the queue
3. `SyncService.PushChanges` reads pending rows via `GetPending(batchSize)` and hands them to an `ISyncTransport`
4. Each row is marked `Sent`, `Failed`, or `Conflict` based on server response
5. Pull direction uses a cursor (`KeyPullCursor`) to fetch remote changes
6. Conflicts are analyzed by `SyncConflictAnalyzer` (using `EntRecordVersion` snapshots as merge base) and resolved via `FrmSyncConflicts`

**Three parallel transport implementations exist:**

| Transport | Class | Use Case | Status |
|---|---|---|---|
| HTTP (online) | `HttpSyncTransport` | Direct server connection | ⚠ Blocked by network (port 7844) |
| File-based (offline) | `HttpFileSyncTransport` + `SyncFileService` | USB/courier package exchange | ✓ Implemented, 26+18 tests |
| HTML (legacy) | `HtmlSyncProvider` | Superseded | ⚠ Legacy, should be retired |

**Critical gap identified and fixed during this engagement:** `FrmCase` never called `SyncOutboxService.Capture`. Because `SyncService` reads *exclusively* from the outbox with no scan-based fallback, cases created or edited in the main case form **were never transmitted to the server**. Cases only synced by accident if someone happened to register financial assistance against them (the sole `TblCase` outbox writer was in `FrmFinance`). This has been corrected for insert, update, and delete paths.

## 2.7 Reporting Workflow

Four distinct reporting mechanisms coexist:

| Mechanism | Engine | Output | Entry Point |
|---|---|---|---|
| **Full case dossier** | `OpenXmlCaseExporter` | .docx (+ PDF conversion) | Case form → "ورد" / "پی دی اف" |
| **Legacy case report** | ReportViewer RDLC (`RptFullCase.rdlc`) | Preview / PDF / Word | `FrmCaseReport` |
| **Dynamic report builder** | `ReportCatalog` + `ReportRunner` | Grid → Excel | `FrmReportBuilder` |
| **Grid/statistical exports** | `ExcelReportExporter`, `GridReportExporter` | .xlsx | Various forms |

The dynamic report builder now exposes **6 sources** (Cases, Family, Documents, Assistance, Case Status History, Family Status History), each with a declared column catalog supporting display, filtering, and grouping. Center and archive filters are applied automatically at the SQL level for every source.

## 2.8 Backup Workflow

```
  Automatic (Program.Main → AutoBackupService.RunDailyBackupIfDue)
     │
     ├─ Reads schedule (Daily / Weekly / Monthly) from settings
     ├─ Skips if interval not elapsed since AutoBackupLastDate
     ├─ Writes to <BackupPath>\AutoBackups\  (or default root)
     ├─ Prunes to BackupRetentionCount (default 14)
     └─ Logs to TblAuditLog

  Manual (FrmSettings → Backup tab)
     │
     └─ BackupHelper.ExportBackup(folder)
            │
            ├─ Serializes 17 tables to a DataSet
            ├─ Copies physical files folder
            └─ Writes archive to disk  ⚠ UNENCRYPTED

  Restore — two modes:
     ├─ MERGE  : GlobalID-based dedup, ID remapping, child-table remap
     └─ CLASSIC: DELETE + direct insert, original IDs preserved (disaster recovery)

  Accounting has a SEPARATE backup path:
     └─ AccountingBackupHelper (11 Acc* tables) — NOT included in main backup
```

**Corrected during this engagement:** the main backup previously omitted `TblFamilyStatusHistory`, `TblFamilyRoleHistory`, `TblApplicant`, `TblApplicantStatusHistory`, and `EntRecordVersion` entirely, and silently dropped four columns (`ChangeType`, `Reason`, `Notes`, `UserID`) when restoring case status history. Both defects are fixed.

## 2.9 Architecture Explanation

The system follows a **layered-but-leaky desktop architecture**:

- **Presentation:** WinForms with heavy custom theming (`UiTheme.cs` alone is 61 KB). RTL is applied globally via a message-filter hook (`RtlCaptions.Install()`), and multi-language substitution is applied to every opened window via `LanguageSweep.Install()`.
- **Business logic:** Distributed. Some lives in dedicated services (`WorkflowService`, `RuleEngine`, `AccountingRepo`, `DuplicateDetector`); much lives inline in form event handlers.
- **Persistence:** No ORM. Hand-written parameterized SQL against a single `DatabaseHelper`. Schema is created and migrated *imperatively at every application start* by four idempotent initializers using `CREATE TABLE IF NOT EXISTS` + `EnsureColumn` (an `ALTER TABLE ADD COLUMN` wrapper guarded by a `PRAGMA table_info` check).
- **Cross-cutting:** Static service classes (`SecurityContext`, `AuditLogger`, `SettingsHelper`, `Lang`, `ErrorLogger`) provide ambient state and behaviour.

**Key architectural strength:** The migration strategy is genuinely robust. Every schema change is additive and idempotent, meaning any older database upgrades cleanly on next launch without a version table or migration scripts.

**Key architectural weakness:** Static mutable global state (`SecurityContext.UserId`, `UiTheme.SizeScale`, `WorkflowService.PermissionGate`) makes the system difficult to unit test in isolation and impossible to run multi-tenant in a single process. The test suite works around this by manipulating `AppDomain` data directories and calling `SecurityContext.SignIn` directly.

---

# 3. Technology Stack Audit

> **Note:** Section re-mapped from the requested Android stack. See Critical Preface.

## 3.1 Platform & Language

| Component | Version | Assessment |
|---|---|---|
| **Language** | C# 7.3 (implied by net472 + VS default) | ⚠ Dated. No nullable reference types, no pattern-matching enhancements, no records |
| **Runtime** | .NET Framework **4.7.2** | ⚠ **Windows-only, in extended support.** Not .NET Core/5+. No cross-platform path. Microsoft supports 4.7.2 as an OS component but it receives no feature work |
| **UI Framework** | Windows Forms | ⚠ Mature and stable, but legacy. Steep talent-acquisition risk long-term |
| **Output Type** | `WinExe` | ✓ Correct |
| **Platform Target** | `AnyCPU` (Debug), `x64` (Release configs present) | ⚠ Mixed — see §13 for `System.Data.SQLite` native interop risk |
| **DPI Awareness** | `PerMonitorV2` via App.config | ✓ **Excellent.** Correctly configured with `EnableWindowsFormsHighDpiAutoResizing` |

**Critical observation:** .NET Framework 4.7.2 is bundled with Windows 10 1803+ and all Windows 11 builds, so deployment does not require a runtime installer on modern systems. On Windows 8.1 or early Windows 10, a redistributable is required.

## 3.2 Dependency Inventory

18 NuGet packages are declared in `packages.config`. Full audit:

### 3.2.1 Data Access

| Package | Version | Why It Exists | Used? | Removable? |
|---|---|---|---|---|
| `System.Data.SQLite.Core` | 1.0.115.5 | Primary database engine | ✓ Yes — everywhere | ✗ **No** — core dependency |
| `Stub.System.Data.SQLite.Core.NetFramework` | 1.0.115.5 | Deploys native `SQLite.Interop.dll` for x86/x64 | ✓ Yes — required at runtime | ✗ **No** |

> ⚠ **Version note:** 1.0.115.5 corresponds to SQLite ~3.38 (2022). Newer SQLite releases contain security and correctness fixes. **Recommend upgrade evaluation.**

### 3.2.2 Document Generation

| Package | Version | Why It Exists | Used? | Removable? |
|---|---|---|---|---|
| `DocumentFormat.OpenXml` | 3.1.1 | Word (.docx) generation for case dossiers | ✓ Yes — `OpenXmlCaseExporter` (45 KB) | ✗ No |
| `DocumentFormat.OpenXml.Framework` | 3.1.1 | Transitive dependency of above | ✓ Indirect | ✗ No |
| `ClosedXML` | 0.105.0 | Excel (.xlsx) export | ✓ Yes — `ExcelReportExporter`, `GridReportExporter` | ✗ No |
| `ClosedXML.Parser` | 2.0.0 | Transitive — formula parsing | ✓ Indirect | ✗ No |
| `ExcelNumberFormat` | 1.1.0 | Transitive of ClosedXML — number format strings | ✓ Indirect | ✗ No |
| `RBush.Signed` | 4.0.0 | Transitive of ClosedXML — R-tree spatial index for cell ranges | ✓ Indirect | ✗ No |
| `SixLabors.Fonts` | 1.0.0 | Transitive of ClosedXML — font metrics for column auto-width | ✓ Indirect | ✗ No |

> ⚠ **Known issue (documented in project memory):** ClosedXML fails inside the *test harness* with a `System.Memory` assembly binding conflict. This is a test-project configuration problem, **not an application defect** — `ExportFullReport` is consequently untested and one test works around it by executing raw SQL instead.

### 3.2.3 Reporting

| Package | Version | Why It Exists | Used? | Removable? |
|---|---|---|---|---|
| `Microsoft.ReportingServices.ReportViewerControl.Winforms` | 150.1652.0 | RDLC rendering for the legacy full-case report | ⚠ **Marginally** — only `RptFullCase.rdlc` via `FrmCaseReport` (92 LOC) | ⚠ **Candidate for removal** |
| `Microsoft.SqlServer.Types` | 14.0.314.76 | Required *only* to satisfy ReportViewer's native dependency | ⚠ Yes, but only for the above | ⚠ Removable with ReportViewer |

> 🔍 **Finding:** The RDLC reporting path is largely superseded by `OpenXmlCaseExporter`. `FrmCaseReport.cs` is the smallest form in the system (92 lines). `Program.Main` contains a defensive `SqlServerTypes.Utilities.LoadNativeAssemblies()` call added specifically to fix a `FileNotFoundException` in this path.
> **Recommendation:** Evaluate retiring RDLC. Removing it would eliminate 2 packages, ~5 assembly references, a native-assembly loader, an entire `SqlServerTypes/` folder, and the `.xsd`/`.rdlc` artifacts — a meaningful reduction in deployment size and startup risk. **Requires product-owner confirmation that no user depends on the RDLC layout.**

### 3.2.4 Presentation & Utility

| Package | Version | Why It Exists | Used? | Removable? |
|---|---|---|---|---|
| `QRCoder` | 1.8.0 | QR codes on guardian ID cards | ✓ Yes — `QrCodeHelper` | ✗ No |
| `Microsoft.Web.WebView2` | 1.0.4022.49 | HTML-based template rendering/preview | ❓ **Not Verified** — referenced; `Templates/*.js` harness files exist (`doc-page.js`, `image-slot.js`, `support.js` — 167 KB total) plus a separate `AssistanceReceiptWebViewHarness` project | ❓ Assess |
| `Microsoft.Bcl.HashCode` | 1.1.1 | Transitive — `HashCode.Combine` polyfill for net472 | ✓ Indirect | ✗ No |

> ⚠ **WebView2 deployment risk:** WebView2 requires the **Evergreen Runtime** installed on the target machine. Windows 11 ships it; Windows 10 may not. If any user-facing feature depends on WebView2, the installer must detect and bootstrap the runtime. **Not Verified whether the Inno Setup script does this.**

### 3.2.5 BCL Polyfills

| Package | Version | Why It Exists | Used? | Removable? |
|---|---|---|---|---|
| `System.Memory` | 4.5.5 | `Span<T>` for net472 (ClosedXML/OpenXml) | ✓ Indirect | ✗ No |
| `System.Buffers` | 4.5.1 | Transitive | ✓ Indirect | ✗ No |
| `System.Numerics.Vectors` | 4.5.0 | Transitive | ✓ Indirect | ✗ No |
| `System.Runtime.CompilerServices.Unsafe` | 4.7.0 | Transitive | ✓ Indirect | ✗ No |

All four have explicit `bindingRedirect` entries in `App.config` — ✓ correctly configured.

## 3.3 Dependency Health Summary

| Assessment | Count |
|---|---|
| ✓ Essential and actively used | 12 |
| ⚠ Used but candidate for removal | 2 (`ReportViewer`, `SqlServer.Types`) |
| ❓ Usage not verified | 1 (`WebView2`) |
| ✗ Confirmed unused/dead | **0** |

**Verdict:** The dependency graph is **lean and disciplined**. There is no dependency bloat. The project notably avoids heavyweight frameworks (no DI container, no ORM, no MVVM framework, no logging framework) — a deliberate choice that reduces supply-chain surface but increases hand-written code volume.

**Supply-chain risk:** ⚠ Medium. `packages.config` (legacy format) does not support lock files or transitive-dependency pinning. No `PackageReference` migration, no SBOM, no automated vulnerability scanning detected.

## 3.4 Build Configuration

| Item | Value | Assessment |
|---|---|---|
| Build system | MSBuild (VS 18 / `Current`) | ✓ Standard |
| Package format | `packages.config` | ⚠ Legacy — migrate to `PackageReference` |
| Test framework | MSTest (`vstest.console.exe`) | ✓ Adequate |
| Test project format | SDK-style (`Microsoft.NET.Sdk`), `net472` | ✓ Modern (inconsistent with main project's legacy format) |
| Full test run duration | **~6.2 minutes** (even with `/Parallel`) | ⚠ Slow — see §15 |
| CI/CD pipeline | **✗ None detected** | 🔴 See §11 |
| Code signing | `CaseManagement_TemporaryKey.pfx` **committed to git** | 🔴 See §10 |

---

# 4. Folder Structure Review

## 4.1 Complete Project Tree

```
C:\Projects\CaseManagement\
│
├── CaseManagement.csproj              Legacy-format MSBuild project
├── packages.config                    18 NuGet packages
├── App.config                         Connection string, DPI awareness, binding redirects
├── app.manifest                       Windows application manifest
├── Program.cs                         Entry point — culture, RTL, i18n, 4 initializers, login, dashboard
├── CLAUDE.md                          AI-assistant working rules (production-safety constraints)
├── ACCOUNTING_ARCHITECTURE.md         Accounting module design document
├── CaseManagement_TemporaryKey.pfx    🔴 Signing certificate (SHOULD NOT BE IN VCS)
├── .gitignore                         Correctly excludes *.sqlite, *.db, bin/, obj/, packages/
│
├── ─── ROOT FORMS (22 files, 20,660 LOC) ───────────────────────────────
│   ├── FrmDashboard.cs      (3,214)   Main shell: KPI cards, charts, sidebar nav, audit grid
│   ├── FrmCase.cs           (3,789)   ⭐ Largest file — case CRUD, tabs, exports
│   ├── FrmSettings.cs       (3,684)   Multi-tab admin: org, appearance, security, backup, centers, license
│   ├── FrmFamily.cs         (1,735)   Family member CRUD + status/role history
│   ├── FrmDocs.cs           (1,015)   Document attachment CRUD + archive
│   ├── FrmAdvancedSearch.cs   (955)   Cross-entity search
│   ├── FrmLogin.cs            (867)   Auth + center selection + lockout
│   ├── FrmFinance.cs          (804)   Per-case assistance registration
│   ├── FrmApplicant.cs        (618)   Pre-case applicant intake
│   ├── FrmArchive.cs          (587)   Archived case/doc restore + permanent delete
│   ├── FrmReportBuilder.cs    (512)   Dynamic report designer
│   ├── FrmUsers.cs            (476)   User account management
│   ├── FrmDuplicates.cs       (422)   Duplicate detection review
│   ├── FrmAssignMemberRole.cs (408)   Bulk member-role assignment
│   ├── FrmCaseRelations.cs    (315)   Inter-case relationship linking
│   ├── FrmDataQualityReport.cs(264)   Data quality issues
│   ├── FrmChangePassword.cs   (241)   Password change
│   ├── FrmBarcode.cs          (240)   Barcode lookup
│   ├── FrmAbout.cs            (236)   Version + license activation
│   ├── FrmCaseReport.cs        (92)   ⚠ Legacy RDLC viewer (smallest form)
│   ├── DsFullCaseReport.xsd.*         RDLC typed dataset
│   └── RptFullCase.rdlc               RDLC report definition
│
├── DAL/                     (1 file, 207 LOC)
│   └── DatabaseHelper.cs              Connection factory, transactions, query helpers
│
├── Models/                  (5 files, 158 LOC)
│   ├── CaseModel.cs · FamilyModel.cs · DocumentModel.cs
│   ├── AssistanceModel.cs · UserModel.cs
│   └── ⚠ Thin POCOs — largely vestigial (see §11.7)
│
├── Helpers/                 (62 files, 17,468 LOC)  — Infrastructure layer
│   ├── ── Database & Migration ──
│   │   ├── DatabaseInitializer.cs    (120 KB) ⭐ Creates/migrates 30+ Tbl* tables
│   │   └── AccountingInitializer.cs   (20 KB)  Creates 11 Acc* tables
│   ├── ── UI Framework ──
│   │   ├── UiTheme.cs                 (61 KB) ⭐ Central theming engine
│   │   ├── ResponsiveLayout.cs · SidebarNav.cs · ModernTitleBar.cs
│   │   ├── DashboardCard.cs · StatCard.cs · SectionCard.cs · Sparkline.cs
│   │   ├── PillTabStrip.cs · ToggleSwitch.cs · ColorSwatchButton.cs
│   │   ├── FieldBox.cs · GridPager.cs · WindowChrome.cs · LoginHeroPanel.cs
│   │   └── SettingsCardPanel.cs
│   ├── ── Localization & RTL ──
│   │   ├── LangData.cs               (142 KB) ⭐ Embedded translation dictionary
│   │   ├── Lang.cs · LanguageSweep.cs · RtlCaptions.cs
│   │   ├── PersianDateHelper.cs · PersianDatePicker.cs
│   │   └── AfghanGeoData.cs                   Province/district reference data
│   ├── ── Security ──
│   │   ├── PasswordHelper.cs                  PBKDF2 hashing
│   │   ├── SecurityContext.cs                 Ambient session state
│   │   ├── SessionTimeoutMonitor.cs           Idle timeout
│   │   ├── AuditLogger.cs                     Audit + status history writers
│   │   ├── CenterGuard.cs                     Multi-tenant access check
│   │   ├── LicenseManager.cs · LicenseInfo.cs ⚠ Not enforced
│   │   └── SettingsHelper.cs
│   ├── ── Backup & Files ──
│   │   ├── BackupHelper.cs            (42 KB) Export/import, merge + classic restore
│   │   ├── AccountingBackupHelper.cs          Separate Acc* backup
│   │   ├── AutoBackupService.cs · FileCleanupHelper.cs
│   │   └── FileHelper.cs              (35 KB) Path/storage management
│   ├── ── Export & Reporting ──
│   │   ├── OpenXmlCaseExporter.cs     (45 KB) ⭐ Word dossier generator
│   │   ├── ExcelReportExporter.cs · GridReportExporter.cs
│   │   ├── ReportDefinitions.cs               Report catalog + runner
│   │   ├── ReportTemplateHelper.cs · ReportFilterCriteria.cs
│   │   ├── FrmReportFilter.cs · FrmTemplatePicker.cs
│   │   ├── PrintHelper.cs · PdfConversionHelper.cs · RdlcExportHelper.cs
│   │   └── IdCardHelper.cs
│   ├── ── Domain Logic ──
│   │   ├── CaseDomain.cs                      Service-status vocabulary (centralized)
│   │   ├── DuplicateDetector.cs      (24 KB)  Fuzzy duplicate matching
│   │   ├── DataQualityChecker.cs
│   │   ├── ExcelCaseImporter.cs               Bulk import
│   │   ├── CaseGridColumns.cs · LookupHelper.cs · SqlHelpers.cs
│   │   └── HadithProvider.cs                  Daily inspirational quote
│   └── ── Misc ──
│       ├── ImageOrientationHelper.cs · LogoHelper.cs
│       ├── FormShortcuts.cs · Msg.cs
│
├── Sync/                    (33 files, 15,179 LOC) — Largest subsystem
│   ├── ── Orchestration ──
│   │   ├── BackgroundSyncManager.cs   (39 KB) Timer-driven auto-sync
│   │   ├── SyncService.cs             (30 KB) Push/pull orchestration
│   │   └── SyncEngine.cs · OfflineSyncInitializer.cs (31 KB)
│   ├── ── Transport ──
│   │   ├── ISyncTransport.cs · SyncTransportModels.cs
│   │   ├── HttpSyncTransport.cs       (26 KB) Online HTTP
│   │   ├── HttpFileSyncTransport.cs   (30 KB) File-package exchange
│   │   ├── OfflineSyncTransport.cs
│   │   └── HtmlSyncProvider.cs        (33 KB) ⚠ Legacy path
│   ├── ── Queue & State ──
│   │   ├── SyncOutboxService.cs       (29 KB) Outbox pattern
│   │   ├── SyncBaselineStore.cs · SyncModels.cs · SyncCodeNormalizer.cs
│   ├── ── Conflict Resolution ──
│   │   ├── SyncComparer.cs · SyncConflictAnalyzer.cs
│   │   ├── SyncConflictResolver.cs · SyncConflictStore.cs · SyncApplier.cs
│   ├── ── Files & Media ──
│   │   ├── SyncFileService.cs         (77 KB) ⭐ Largest sync file
│   │   ├── MediaScanner.cs            (43 KB) · MediaSyncEngine.cs · MediaModels.cs
│   ├── ── Validation ──
│   │   ├── PackageValidator.cs        (32 KB) · ValidationModels.cs
│   ├── ── UI ──
│   │   ├── FrmSyncWizard.cs          (105 KB) ⭐ Largest single file in project
│   │   ├── FrmSyncHelp.cs · FrmSyncSimple.cs · FrmServerConnection.cs
│   │   ├── FrmSyncConflicts.cs · FrmValidationReport.cs
│   └── └── CaseNumbering.cs                   Cross-branch code allocation
│
├── Enterprise/              (25 files, 8,210 LOC) — "Enterprise Core" phase 1
│   ├── EnterpriseInitializer.cs       (50 KB) Creates 22 Ent* tables
│   ├── EntDb.cs                               Micro data-access for Ent* tables
│   ├── ── Workflow & Approval ──
│   │   ├── WorkflowService.cs · FrmWorkflowAdmin.cs · FrmWorkflowAction.cs
│   │   ├── ApprovalService.cs · FrmApprovals.cs
│   ├── ── Governance ──
│   │   ├── PermissionService.cs      ⚠ Built but NOT enforced on CRUD
│   │   ├── FrmPermissionMatrix.cs · ModuleService.cs · FrmModules.cs
│   │   ├── RuleEngine.cs · FrmRules.cs
│   │   ├── LockService.cs · FrmLocks.cs
│   ├── ── Audit & History ──
│   │   ├── VersionService.cs · FrmVersions.cs   ✓ Now wired (this engagement)
│   │   ├── SecurityAudit.cs · FrmSecurityAudit.cs
│   │   ├── ErrorLogger.cs · FrmErrorLog.cs
│   ├── ├── TaskService.cs · FrmTasks.cs
│   └── └── EnterpriseModels.cs · EntPrompt.cs
│
├── Accounting/              (9 files, 6,646 LOC)
│   ├── FrmAccounting.cs              (144 KB) ⭐ Largest form file on disk
│   ├── AccountingRepo.cs              (81 KB) Data access + business rules
│   ├── AccReports.cs                  (70 KB) Financial reports
│   ├── AccIntegrity.cs                (34 KB) Balance/consistency validation
│   ├── AccRepair.cs                   (34 KB) Corruption repair
│   ├── FrmAccountingRepair.cs · AccAudit.cs
│   ├── Money.cs                               Monetary arithmetic
│   └── AccountingRuleException.cs
│
├── DevCenter/               (5 files, 4,773 LOC) — Hidden diagnostics
│   ├── DevCenterService.cs           (107 KB) ⭐ Diagnostics queries
│   ├── FrmDevCenter.cs                (69 KB) Hidden UI (keyboard-activated)
│   ├── DevCenterRepair.cs             (49 KB) Automated repair routines
│   ├── DevCenterHealthReport.cs       (31 KB)
│   ├── DevCenterAccess.cs                     Keyboard hook, SuperAdmin-only
│   └── DEVELOPER_CONTROL_CENTER.md    (32 KB) Documentation
│
├── GuardianCardIntegration/ (11 files, 3,227 LOC)
│   ├── GuardianCardRenderer.cs · CardService.cs
│   ├── CardTemplateRepository.cs · CaseCardRepository.cs   ✓ Repository pattern
│   ├── FrmCardTemplateManager.cs · FrmGuardianCardPreview.cs
│   ├── FrmGuardianCardBatchPrint.cs · FrmCardNoticesEdit.cs
│   └── Code128Barcode.cs · QrCodeHelper.cs · GuardianCardData.cs
│
├── AssistanceReceiptIntegration/ (9 files, 1,890 LOC)
│   ├── AssistanceReceiptService.cs · AssistanceReceiptRenderer.cs
│   ├── AssistanceReceiptRepository.cs · AssistancePackageRepository.cs  ✓ Repository
│   ├── FrmAssistanceReceiptSinglePrint.cs · FrmAssistanceReceiptFilterPrint.cs
│   └── FrmAssistancePackageBatchPrint.cs · AssistancePackage.cs · AssistanceReceiptData.cs
│
├── Languages/               4 translation files × 40.3 KB
│   └── ar.txt · en.txt · ps.txt · ur.txt     (Persian = default, no file needed)
│
├── Templates/               Document & card templates
│   ├── FullCaseTemplate.docx · FullCaseTemplate_Sample.docx/.pdf
│   ├── الگوی_شماره_1.docx · الگوی_شماره_2.docx
│   ├── برگه دریافتی مساعدت.dc.html
│   ├── doc-page.js · image-slot.js · support.js   (167 KB — WebView2 harness)
│   └── Report_Template_Guide.md · github.md
│
├── Manual/                  TrainingManual.docx + .pdf (313 KB)
├── Installer/               CaseManagement.iss · Guardian.iss · README.md (Inno Setup)
├── SqlServerTypes/          Native assembly loader for ReportViewer
└── Properties/              AssemblyInfo.cs
```

## 4.2 Folder Purpose Reference

| Folder | Layer | Purpose | Cohesion |
|---|---|---|---|
| `(root)` | Presentation | Primary user-facing forms | ⚠ **Low** — 22 unrelated forms flat in root; should be `Forms/` subfolders by domain |
| `DAL/` | Persistence | Single connection/query abstraction | ✓ High |
| `Models/` | Domain | POCO definitions | ⚠ **Vestigial** — see §11.7 |
| `Helpers/` | Infrastructure | Catch-all utility layer | 🔴 **Very low** — 62 files spanning theming, i18n, security, backup, export, and domain logic. This is a "junk drawer" package |
| `Sync/` | Integration | Offline sync subsystem | ✓ High — well-organized |
| `Enterprise/` | Governance | Workflow, RBAC, audit, locking | ✓ High |
| `Accounting/` | Business | Financial ledger | ✓ High |
| `DevCenter/` | Ops | Hidden diagnostics/repair | ✓ High |
| `GuardianCardIntegration/` | Business | ID card production | ✓ High — proper repository pattern |
| `AssistanceReceiptIntegration/` | Business | Receipt production | ✓ High — proper repository pattern |
| `Languages/` `Templates/` `Manual/` `Installer/` | Assets | Non-code resources | ✓ Appropriate |

### 🔍 Structural Findings

1. **`Helpers/` is overloaded (17,468 LOC / 62 files).** It contains at minimum six distinct concerns that warrant separate packages: `UI/Theming`, `Localization`, `Security`, `Backup`, `Export`, `Domain`. This is the single highest-value refactor for long-term maintainability — and it is *low risk*, being purely a file-move + namespace operation.

2. **Root folder holds 22 forms flat.** `FrmCase`, `FrmSettings`, and `FrmDashboard` alone account for 10,687 lines in the root namespace.

3. **Inconsistent architecture between modules.** The two newest modules (`GuardianCardIntegration`, `AssistanceReceiptIntegration`) correctly use the repository pattern. The oldest and most critical code (case management) does not. This suggests the team's practices improved over time but legacy code was never retrofitted.

4. **A separate sibling project exists** — `C:\Projects\AssistanceReceiptWebViewHarness` — plus `CaseTemplate2Harness` and `SyncServer`. These are outside the audited solution and were **Not Verified**.

---

# 5. Module Inventory

## 5.1 Core Case Management

**Purpose:** Registration, maintenance, and lifecycle management of household case files.

**Features:**
- ✓ Case CRUD with ~40 fields (guardian identity, Tazkira, address, phone, job, skill, disability degree/type, migration card, marital status, education, economic priority, request type, survey data)
- ✓ Guardian photo + family group photo with orientation correction
- ✓ Service-status lifecycle with reason capture and suspension stamping (date + user)
- ✓ Display/edit mode locking (fields read-only until "Edit" pressed)
- ✓ Unique constraint on case code + form number
- ✓ Multi-center isolation via `CenterGuard.EnsureCaseAccess`
- ✓ Cascading child deletion (family/docs/assistance via `ON DELETE CASCADE`)
- ✓ Two delete modes: database-only, or database + physical files
- ✓ Archive/restore with permanent-delete option
- ✓ Inter-case relationships (`TblCaseRelation`)
- ✓ Excel bulk import (`ExcelCaseImporter`)
- ✓ Change history viewer *(added this engagement)*
- ⚠ Save operations are **not transactional** — the `INSERT`/`UPDATE` and the audit/version/outbox writes occur on separate connections

**Dependencies:** `DatabaseHelper`, `CenterGuard`, `AuditLogger`, `VersionService`, `SyncOutboxService`, `FileHelper`, `CaseDomain`, `PersianDateHelper`

**Data Sources:** `TblCase`, `TblFamily`, `TblDocs`, `TblAssistance`, `TblCaseRelation`, `TblCaseStatusHistory`, `TblArchiveHistory`

**UI Screens:** `FrmCase`, `FrmFamily`, `FrmDocs`, `FrmArchive`, `FrmCaseRelations`, `FrmAdvancedSearch`, `FrmDuplicates`, `FrmApplicant`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **92%** |
| **Production Ready** | ✅ Yes (with encryption caveat) |
| **Risk Level** | 🟡 Medium — non-transactional saves |

---

## 5.2 Family Member Management

**Purpose:** Manage individuals within a household, including orphan status, roles, and per-member service eligibility.

**Features:**
- ✓ Member CRUD with photo, Tazkira type/number, gender, birth date, education, physical status, disability
- ✓ **Per-member service status** independent of the case (`TblFamilyStatusHistory`)
- ✓ **Member role history** in a dedicated table (`TblFamilyRoleHistory`) — deliberately separated from status history
- ✓ Bulk role assignment (`FrmAssignMemberRole`)
- ✓ Sync outbox integration (insert/update/delete)
- ✓ Version history *(added this engagement)*
- ✓ Transactional delete (`BeginTransaction` with rollback on failure)
- ⚠ Audit-log text captures only 5 of ~20 fields

**Data Sources:** `TblFamily`, `TblFamilyStatusHistory`, `TblFamilyRoleHistory`

**UI Screens:** `FrmFamily`, `FrmAssignMemberRole`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **94%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟢 Low — best-implemented core module |

---

## 5.3 Document Management

**Purpose:** Attach, categorize, and archive supporting evidence (Tazkira scans, death certificates, medical records).

**Features:**
- ✓ File attachment with copy-to-managed-storage
- ✓ Document type, category, tags, description, reference number
- ✓ Original filename preservation
- ✓ Archive (soft delete) rather than hard delete
- ✓ Client-side search with escaped `RowFilter`
- ✓ Sync + version integration
- ❓ File type/size validation — **Not Verified**
- ✗ Virus scanning of uploaded files
- ✗ File integrity hashing

**Data Sources:** `TblDocs`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **88%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟡 Medium — unvalidated file ingestion |

---

## 5.4 Financial Assistance (Per-Case)

**Purpose:** Record aid disbursed to individual households.

**Features:**
- ✓ Assistance registration (date, type, amount, description)
- ✓ Automatic case request-type update
- ✓ Receipt printing (single, filtered batch, package batch)
- ✓ Assistance package definitions with line items
- ✓ Sync + version integration
- 🔴 **No linkage to the Accounting ledger** — see §5.5

**Data Sources:** `TblAssistance`, `TblAssistancePackage`, `TblAssistancePackageItem`

**UI Screens:** `FrmFinance`, `FrmAssistanceReceiptSinglePrint`, `FrmAssistanceReceiptFilterPrint`, `FrmAssistancePackageBatchPrint`

| Metric | Value |
|---|---|
| **Status** | ⚠ **Partially Complete** |
| **Completion** | **75%** |
| **Production Ready** | ⚠ Functionally yes, financially unreconciled |
| **Risk Level** | 🔴 **High** — aid recorded here never reconciles with the accounting ledger |

---

## 5.5 Accounting Ledger

**Purpose:** Organization-level financial management — donor income, operational expenses, orphan stipends, staff salaries.

**Features:**
- ✓ Fiscal periods with opening balances, multi-month spans, open/closed status
- ✓ Multiple funds (cash, bank, credit, central) with per-fund balances
- ✓ Counterparties (donors, offices, provinces, employees, vendors)
- ✓ Income/expense categorization
- ✓ Transactions with document numbers, USD amounts + exchange rate
- ✓ Orphan stipends by family size / *Sadat* type / region
- ✓ Staff salaries
- ✓ Expense line items
- ✓ **Reversal instead of deletion** (`IsReversed`) — ✓ correct accounting principle
- ✓ Integrity validation (`AccIntegrity`, 34 KB) and repair (`AccRepair`, 34 KB)
- ✓ Dedicated audit table (`AccAudit`)
- ✓ Dedicated backup (`AccountingBackupHelper`)
- ✓ Documented bug fix: `last_insert_rowid()` across separate connections had written 39 of 43 `AccAudit` rows with `EntityID = 0`
- 🔴 **`AccStipend` is aggregate** (by family size / region), with **no `CasID`** — impossible to answer "how much has household X received?"
- 🔴 Accounting tables are **excluded from the main backup**
- 🔴 Accounting tables are **excluded from synchronization entirely**
- ✗ Double-entry bookkeeping (single-entry cash book only)
- ✗ Multi-currency beyond manual USD→AFN rate

**Data Sources:** 11 `Acc*` tables

**UI Screens:** `FrmAccounting` (2,352 LOC), `FrmAccountingRepair`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete (as a standalone cash book)** |
| **Completion** | **85%** |
| **Production Ready** | ⚠ Yes standalone; ✗ as an integrated system |
| **Risk Level** | 🔴 **High** — data-loss risk (backup/sync gaps) + no case reconciliation |

---

## 5.6 Synchronization

**Purpose:** Reconcile data between branch offices and head office over unreliable/absent connectivity.

**Features:**
- ✓ Outbox pattern with durable queue (`SyncOutbox`)
- ✓ `GlobalID` GUID assignment via database triggers
- ✓ Background auto-sync (configurable interval, start/stop lifecycle)
- ✓ Three transports: HTTP, file-package, legacy HTML
- ✓ Conflict detection, analysis (using version snapshots as merge base), and resolution UI
- ✓ Media/file synchronization with download queue and states
- ✓ Package validation before import
- ✓ Cross-branch case-number allocation (`CaseNumbering`)
- ✓ Baseline store for three-way merge
- ✓ Comprehensive test coverage (**125+ tests** across 7 sync test files)
- ✓ `TblCase` outbox capture *(fixed this engagement — was entirely missing)*
- 🔴 **History tables are never synchronized** — two branches diverge permanently
- 🔴 **Accounting is never synchronized**
- ⚠ HTTP transport blocked in the user's environment (egress port 7844 closed → Cloudflare error 1033)
- ⚠ Three parallel transports = 3× defect surface

**Data Sources:** `SyncOutbox`, `SyncState`, `SyncBaseline`, `SyncConflict`, `SyncFile`, `SyncFileDownload`

**UI Screens:** `FrmSyncWizard` (1,899 LOC), `FrmSyncSimple`, `FrmSyncHelp`, `FrmServerConnection`, `FrmSyncConflicts`, `FrmValidationReport`

| Metric | Value |
|---|---|
| **Status** | ⚠ **Partially Complete** |
| **Completion** | **78%** |
| **Production Ready** | ⚠ **Pilot only** |
| **Risk Level** | 🔴 **High** — highest complexity, incomplete entity coverage |

---

## 5.7 Enterprise Core (Governance)

**Purpose:** Workflow, approvals, tasks, business rules, record locking, versioning, RBAC, module gating.

**Features:**
- ✓ Configurable workflows with states and transitions
- ✓ Multi-level approval chains
- ✓ Task assignment and status tracking
- ✓ Rule engine with logging
- ✓ Pessimistic record locking with force-release
- ✓ Full record versioning with field-level diff *(now wired — this engagement)*
- ✓ Module enable/disable at global, role, and user scope (`ModuleService`) — **and this IS enforced** in dashboard navigation
- ✓ Centralized error logging with severity, replacing crashes with Persian messages
- ✓ Security event log
- 🔴 **`PermissionService` is NOT enforced on any CRUD operation.** `HasPermission()` / `Require()` exist and are correct, but the *only* consumer is `WorkflowService.PermissionGate`. The permission matrix UI writes to `EntRolePermission` / `EntUserPermission`, which nothing reads for data operations
- ⚠ Version rollback deliberately not implemented (documented design decision — viewing/comparison only)

**Data Sources:** 22 `Ent*` tables

**UI Screens:** `FrmWorkflowAdmin`, `FrmApprovals`, `FrmTasks`, `FrmRules`, `FrmLocks`, `FrmVersions`, `FrmPermissionMatrix`, `FrmModules`, `FrmSecurityAudit`, `FrmErrorLog`, `FrmWorkflowAction`

| Metric | Value |
|---|---|
| **Status** | ⚠ **Partially Complete** |
| **Completion** | **70%** |
| **Production Ready** | 🔴 **No** — RBAC is decorative |
| **Risk Level** | 🔴 **Critical** — false sense of access control |

---

## 5.8 Reporting & Export

**Purpose:** Produce documents, spreadsheets, and analytical reports.

**Features:**
- ✓ Word case dossier with two custom templates + placeholder replacement + image embedding
- ✓ Page-break control (3 members/page, 4 documents/page for template 1)
- ✓ PDF conversion
- ✓ Excel exports (grid and full statistical)
- ✓ Dynamic report builder with **6 sources**, filters, grouping, saved templates
- ✓ Change-history and assistance sections in Word output *(added this engagement)*
- ✓ Batch export across multiple cases
- ⚠ RDLC legacy path retained but minimally used
- ✗ Scheduled/automated report delivery (`TblScheduledReport` exists but is **dead**)
- ✗ Chart/graph embedding in exports

**UI Screens:** `FrmReportBuilder`, `FrmCaseReport`, `FrmReportFilter`, `FrmTemplatePicker`, `FrmDataQualityReport`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **86%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟢 Low |

---

## 5.9 Guardian Card Production

**Purpose:** Design and print physical guardian identification cards.

**Features:**
- ✓ Template manager with configurable layout
- ✓ Card renderer with photo, barcode (Code128), QR code
- ✓ Live preview
- ✓ Batch printing
- ✓ Editable card notices/terms
- ✓ **Proper repository pattern** (`CardTemplateRepository`, `CaseCardRepository`)

**Data Sources:** `TblCardTemplate`, `TblCase`, `TblFamily`

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **90%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟢 Low |
| **Test Coverage** | ⚠ Minimal |

---

## 5.10 Developer Control Center

**Purpose:** Hidden diagnostic and repair console for SuperAdmin/support staff.

**Features:**
- ✓ Keyboard-activated, invisible to normal users, SuperAdmin-gated
- ✓ Health report (31 KB of checks)
- ✓ Automated repair routines (49 KB)
- ✓ System log viewer, sync status, database statistics
- ✓ VACUUM / REINDEX / ANALYZE maintenance
- ✓ **33 safety tests** — the most-tested single area
- ✓ 32 KB of dedicated documentation

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **95%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟡 Medium — powerful destructive capability; correctly gated but relies on role string |

---

## 5.11 Localization & Theming

**Purpose:** RTL-first, multi-language, themed user interface.

**Features:**
- ✓ 5 languages (Persian default + Pashto, Arabic, Urdu, English)
- ✓ Global RTL caption enforcement via message filter
- ✓ Automatic translation sweep on every window open
- ✓ Persian (Jalali) calendar throughout, custom `PersianDatePicker`
- ✓ Afghan province/district reference data
- ✓ Configurable theme colors, font family, font scale
- ✓ Per-monitor DPI awareness
- ⚠ Theme changes require application restart (documented limitation)

| Metric | Value |
|---|---|
| **Status** | ✓ **Complete** |
| **Completion** | **93%** |
| **Production Ready** | ✅ Yes |
| **Risk Level** | 🟢 Low |

---

## 5.12 Module Summary Matrix

| # | Module | LOC | Status | Complete | Prod-Ready | Risk |
|---|---|---|---|---|---|---|
| 1 | Core Case Management | 20,660 | ✓ Complete | 92% | ✅ | 🟡 |
| 2 | Family Members | *(in above)* | ✓ Complete | 94% | ✅ | 🟢 |
| 3 | Documents | *(in above)* | ✓ Complete | 88% | ✅ | 🟡 |
| 4 | Financial Assistance | *(in above)* | ⚠ Partial | 75% | ⚠ | 🔴 |
| 5 | Accounting Ledger | 6,646 | ✓ Complete* | 85% | ⚠ | 🔴 |
| 6 | Synchronization | 15,179 | ⚠ Partial | 78% | ⚠ Pilot | 🔴 |
| 7 | Enterprise Core | 8,210 | ⚠ Partial | 70% | 🔴 No | 🔴 |
| 8 | Reporting & Export | *(Helpers)* | ✓ Complete | 86% | ✅ | 🟢 |
| 9 | Guardian Card | 3,227 | ✓ Complete | 90% | ✅ | 🟢 |
| 10 | Assistance Receipts | 1,890 | ✓ Complete | 87% | ✅ | 🟢 |
| 11 | Dev Control Center | 4,773 | ✓ Complete | 95% | ✅ | 🟡 |
| 12 | Localization/Theming | *(Helpers)* | ✓ Complete | 93% | ✅ | 🟢 |

\* Complete as a standalone cash book; incomplete as an integrated subsystem.

---

# 6. Screen-by-Screen Audit

> 49 forms exist. The 20 most significant are audited in detail; the remainder are summarized in §6.21.

## 6.1 FrmLogin (867 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Username/password auth, PBKDF2 verification with per-user iteration count, **account lockout after N failed attempts** (configurable, default 5) with timed unlock (default 15 min), center selection, "All Centers" for SuperAdmin, last-center recall, audit logging of login events |
| **Missing** | ✗ Multi-factor authentication · ✗ Password-expiry enforcement (`ForcePasswordChangeDays` setting exists — enforcement **Not Verified**) · ✗ CAPTCHA/progressive delay · ✗ Concurrent-session control |
| **Validation** | ✓ Good |
| **UX** | ✓ Custom hero panel, themed, RTL |
| **Error Handling** | ✓ Persian messages, no stack-trace leakage |
| **Offline** | ✓ Fully offline |
| **Production Ready** | ✅ **Yes** |
| **Completion** | **88%** |

---

## 6.2 FrmDashboard (3,214 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | KPI stat cards, sparklines, 12-month trend charts (activated/discontinued from status history), province/district/status filters, module-gated sidebar navigation (26 entries), audit-event grid, reminders, daily Hadith |
| **Missing** | ✗ Dashboard customization/widget arrangement · ✗ Export of dashboard view · ✗ Real-time refresh (manual `RefreshAll` only) |
| **Validation** | N/A (read-only) |
| **UX** | ✓ Strong — modern card-based design |
| **Error Handling** | ✓ Guarded queries |
| **Offline** | ✓ Fully offline |
| **Performance** | ⚠ Multiple aggregate queries at load; **Not Verified** at scale |
| **Production Ready** | ✅ **Yes** |
| **Completion** | **90%** |

---

## 6.3 FrmCase (3,789 LOC) — Primary Screen

| Aspect | Assessment |
|---|---|
| **Implemented** | ~40-field CRUD, tabbed layout (case / members / documents), display-vs-edit locking, dual photos, duplicate code check, center guard, two-mode delete, exports (Word/PDF/Excel/print/batch), barcode, **change-history button** *(new)*, dashboard-filter pass-through |
| **Missing** | ✗ Field-level permission control · ✗ Concurrent-edit protection (`LockService` exists but is **not used here**) · ✗ Draft/autosave · ✗ Undo |
| **Validation** | ✓ `ValidateForm()` + unique constraints + FK enforcement |
| **UX** | ✓ Good; ⚠ 3,789 LOC in one form indicates maintenance difficulty |
| **Error Handling** | ⚠ `Msg.Show(ex.Message)` only — **does not route to `ErrorLogger`** |
| **Offline** | ✓ Fully offline |
| **Concurrency** | 🔴 **Last-write-wins.** Two users editing the same case silently overwrite each other |
| **Transactionality** | 🔴 Save is **not** wrapped in a transaction |
| **Production Ready** | ⚠ **Yes with caveats** |
| **Completion** | **89%** |

---

## 6.4 FrmFamily (1,735 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Member CRUD, photo with orientation fix, per-member service status + reason, member-role history, transactional delete with photo cleanup, sync outbox, version history, **history button** *(new)* |
| **Missing** | ✗ Age auto-calculation display ❓ · ✗ Bulk member import |
| **Validation** | ✓ Good — `COALESCE` fix for NULL photo paths documented |
| **Error Handling** | ✓ Compensating cleanup of copied photos on failure |
| **Production Ready** | ✅ **Yes** |
| **Completion** | **91%** |

---

## 6.5 FrmDocs (1,015 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Attach/edit/archive, type/category/tags, managed file copy, escaped client-side search, sync + version |
| **Missing** | ✗ File-type whitelist ❓ · ✗ Size limits ❓ · ✗ Malware scanning · ✗ Checksums · ✗ Thumbnail preview grid ❓ |
| **Production Ready** | ⚠ **Yes with file-validation caveat** |
| **Completion** | **85%** |

---

## 6.6 FrmSettings (3,684 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Organization identity, appearance (colors/font/scale), security policy (min password length, max attempts, lockout, session timeout, audit toggle), backup config + manual backup/restore, center management (CRUD), SuperAdmin account, license activation, storage paths, training manual path, bulk case deletion |
| **Missing** | ✗ Settings export/import · ✗ Change confirmation for destructive settings ❓ |
| **Risk** | 🔴 Contains **bulk case deletion** and **restore** — the two most destructive operations, in a settings screen |
| **Production Ready** | ⚠ **Yes** |
| **Completion** | **88%** |

---

## 6.7 FrmFinance (804 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Assistance registration (date/type/amount/description), case request-type update, assistance listing, reports, sync + version |
| **Missing** | 🔴 **No accounting-ledger posting** · ✗ Approval before disbursement · ✗ Budget/ceiling checks · ✗ Duplicate-payment detection |
| **Production Ready** | ⚠ **Functionally yes; financially uncontrolled** |
| **Completion** | **72%** |

---

## 6.8 FrmAccounting (2,352 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Full multi-tab cash book: periods, funds, parties, categories, transactions, stipends, salaries, expenses; reversal workflow with mandatory reason; balance reporting; integrity checks |
| **Missing** | ✗ Double-entry · ✗ Case linkage · ✗ Sync · ✗ Inclusion in main backup |
| **Validation** | ✓ Strong — `AccountingRuleException`, reversal rules, duplicate detection |
| **Production Ready** | ⚠ **Standalone yes** |
| **Completion** | **85%** |

---

## 6.9 FrmSyncWizard (1,899 LOC) — Largest File

| Aspect | Assessment |
|---|---|
| **Implemented** | Guided multi-step sync (export package, import package, validate, resolve conflicts, apply) |
| **Risk** | 🔴 Highest-complexity UI in the system |
| **Production Ready** | ⚠ **Pilot only** |
| **Completion** | **78%** |

---

## 6.10 FrmPermissionMatrix (326 LOC)

| Aspect | Assessment |
|---|---|
| **Implemented** | Role × permission grid, per-user overrides (explicit grant/deny), persistence to `EntRolePermission` / `EntUserPermission` |
| **🔴 CRITICAL** | **The permissions configured here are never enforced on data operations.** The only consumer of `PermissionService.HasPermission` is `WorkflowService.PermissionGate`. An administrator can revoke "delete case" from a role and that role **will still be able to delete cases**, because `FrmCase` checks `SecurityContext.CanDelete()` (a role-string comparison), not the permission matrix |
| **Production Ready** | 🔴 **No — actively misleading** |
| **Completion** | **45%** (UI complete, enforcement absent) |

---

## 6.11–6.20 Remaining Major Screens

| Screen | LOC | Completion | Prod-Ready | Key Notes |
|---|---|---|---|---|
| `FrmAdvancedSearch` | 955 | 85% | ✅ | Cross-entity search |
| `FrmApplicant` | 618 | 82% | ✅ | Intake + conversion to case; ⚠ excluded from backup until this engagement |
| `FrmArchive` | 587 | 88% | ✅ | Restore + permanent delete; ✓ sync + version wired |
| `FrmUsers` | 476 | 84% | ✅ | User CRUD, status toggle, delete; ⚠ role assignment is free-text role string |
| `FrmReportBuilder` | 512 | 86% | ✅ | 6 sources, filters, grouping, saved templates |
| `FrmDuplicates` | 422 | 87% | ✅ | Fuzzy matching; ✓ auto-merge deliberately **not** implemented (documented safety decision) |
| `FrmAssignMemberRole` | 408 | 90% | ✅ | Bulk role change with history |
| `FrmWorkflowAdmin` | 661 | 72% | ⚠ | Workflow designer; ❓ real-world usage unverified |
| `FrmApprovals` | 601 | 74% | ⚠ | Approval queue; gated by unenforced permissions |
| `FrmDevCenter` | 1,296 | 95% | ✅ | Hidden; 33 safety tests |
| `FrmCaseReport` | 92 | 60% | ⚠ | Legacy RDLC; candidate for retirement |
| `FrmDataQualityReport` | 264 | 80% | ✅ | Quality issue listing |
| `FrmCaseRelations` | 315 | 85% | ✅ | Relationship linking |
| `FrmChangePassword` | 241 | 86% | ✅ | ❓ Complexity policy enforcement unverified |
| `FrmBarcode` | 240 | 85% | ✅ | Barcode scan lookup |
| `FrmAbout` | 236 | 90% | ✅ | Version + license activation (unenforced) |
| `FrmVersions` | 185 | 88% | ✅ | ✓ Now populated with real data |
| `FrmSecurityAudit` | 206 | 78% | ✅ | Security event viewer |
| `FrmErrorLog` | 272 | 85% | ✅ | Centralized error viewer |
| `FrmLocks` | 146 | 70% | ⚠ | Lock viewer; ⚠ locking not applied to case editing |

## 6.21 Screen Audit Summary

| Category | Count |
|---|---|
| ✓ Production-ready | **34** |
| ⚠ Ready with caveats | **12** |
| 🔴 Not ready | **3** (`FrmPermissionMatrix`, `FrmSyncWizard`, `FrmCaseReport`) |
| **Average completion** | **≈ 83%** |

---

# 7. Database Audit

> **Note:** Section re-mapped from the requested "Room database" audit. This system uses **raw SQLite via ADO.NET** with no ORM. Concepts are translated: Room `@Entity` → `CREATE TABLE`; Room migrations → imperative `EnsureColumn` calls; Room DAO → hand-written SQL in forms.

## 7.1 Database Overview

| Attribute | Value |
|---|---|
| **Engine** | SQLite 3 (via `System.Data.SQLite` 1.0.115.5) |
| **File** | `\|DataDirectory\|\CaseDB.sqlite` |
| **Encryption** | 🔴 **NONE** |
| **Tables** | **64** |
| **Indexes** | **73** |
| **Foreign Keys** | ✓ Enabled per-connection (`builder.ForeignKeys = true`) |
| **Journal Mode** | ❓ Not explicitly set — SQLite default (`DELETE`), **not WAL** |
| **Busy Timeout** | 8,000 ms — ⚠ only on `ExecuteInTransaction`, not on `GetConnection` |
| **Connection Pooling** | Disabled (not specified in connection string) |
| **Migration Strategy** | Imperative, idempotent, run at every application start |

## 7.2 Schema Families

| Prefix | Count | Owner | Purpose |
|---|---|---|---|
| `Tbl*` | 25 | `DatabaseInitializer.cs` (120 KB) | Core domain: cases, family, docs, users, centers, lookups, history |
| `Acc*` | 11 | `AccountingInitializer.cs` | Financial ledger |
| `Ent*` | 22 | `EnterpriseInitializer.cs` | Workflow, RBAC, versions, locks, errors |
| `Sync*` | 6 | `OfflineSyncInitializer.cs` | Sync queue, state, conflicts, files |

## 7.3 Core Entity Detail

### 7.3.1 `TblCase` — Central Entity

**Purpose:** One household case file.

| Aspect | Detail |
|---|---|
| **PK** | `CasID INTEGER PRIMARY KEY AUTOINCREMENT` |
| **Base columns** | 34 declared in `CREATE TABLE` |
| **Retrofitted columns** | `CenterID`, `IsArchived`, `GlobalID`, `RowVersion`, `SuspensionReason`, `SuspensionDate`, `SuspendedByUserId`, `SuspendedByUsername`, `HeadIdCardType`, `PhysicalStatusNotes`, `CoveredByOrgNames`, and others — added via `EnsureColumn` |
| **Constraints** | `UNIQUE(Code)`, `UNIQUE(FormNo)`, `CHECK(ServiceStatus IN (...))` generated from `CaseDomain` |
| **Indexes** | 6 — `CaseDate`, `Code`, `FormNo`, `Province`, `(Province,District)`, `ServiceStatus` |
| **Children** | `TblFamily`, `TblDocs`, `TblAssistance`, `TblCaseRelation` (×2 FK) — all `ON DELETE CASCADE` |

**🔍 Problems identified:**

1. ⚠ **`UNIQUE(Code)` does not prevent duplicate blanks.** `Code` is `TEXT NULL`, and SQLite permits unlimited `NULL`s in a UNIQUE index. Imported or synced cases lacking a code will not collide. The code compensates with an explicit `IsCodeExists()` check, but the database does not enforce it.
2. ⚠ **All columns are `TEXT`**, including dates. `CaseDate`, `SurveyDate` store Jalali or Gregorian strings depending on the write path. Date range filtering relies on lexical string comparison, which is correct **only** for zero-padded `yyyy/MM/dd` or `yyyy-MM-dd`. Mixed calendars in one column would silently produce wrong report results. **Not Verified** whether any path writes a non-padded or mixed format.
3. 🔴 **Multi-tenancy is retrofitted.** `CenterID` is nullable and was added by migration. Legacy rows may have `NULL`, which `(@CID = 0 OR CenterID = @CID)` filters **exclude** — meaning pre-migration cases can become invisible to non-SuperAdmin users.
4. ⚠ **No `UpdatedBy` column.** `UpdatedAt` exists but the identity of the last editor is only recoverable from the audit/version tables.
5. ✓ **Good:** `CHECK` constraint on `ServiceStatus` centralized through `CaseDomain.ServiceStatusCheckSql` — a genuinely strong design that prevents vocabulary drift.

---

### 7.3.2 `TblFamily` — Household Members

| Aspect | Detail |
|---|---|
| **PK** | `FamID` |
| **FK** | `CasID → TblCase.CasID ON DELETE CASCADE` |
| **Base columns** | 23 |
| **Retrofitted** | `ServiceStatus`, `MemberRole`, `MemberIdCardType`, `GlobalID`, `RowVersion`, suspension fields |
| **Indexes** | 1 — `IX_TblFamily_CasID` |
| **Children** | `TblFamilyStatusHistory`, `TblFamilyRoleHistory` (both CASCADE) |

**🔍 Problems:**
1. ⚠ **Only one index.** No index on `MemberName`, `MemberTazkiraNo`, or `ServiceStatus`. Duplicate detection and member search perform full scans.
2. ⚠ **No `CenterID`** — center isolation is inherited only via `JOIN TblCase`. Every family query must join, or it leaks cross-center data. **This is a latent security trap** for any future developer writing a direct `TblFamily` query.
3. ⚠ `BirthDate` is `TEXT` with no format constraint; age calculation correctness is **Not Verified**.
4. ✗ **No `IsArchived`** — members of an archived case remain "active" at the row level.

---

### 7.3.3 `TblDocs` — Attachments

| Aspect | Detail |
|---|---|
| **Base columns** | Only **7** |
| **Retrofitted** | `DocNo`, `DocCategory`, `DocTags`, `IsArchived`, `GlobalID`, `RowVersion`, `CreatedAt` |
| **Indexes** | 1 — `IX_TblDocs_CasID` |

**🔍 Problems:**
1. 🔴 **No file integrity guarantee.** `DocFilePath` is a plain path. No checksum, no size, no MIME type. If the file is moved, renamed, or deleted outside the application, the row becomes a dangling pointer with no detection mechanism.
2. ⚠ No index on `DocType` / `DocCategory` despite these driving the search filter.

---

### 7.3.4 `TblUsers` — Accounts

```sql
UserID             INTEGER PRIMARY KEY AUTOINCREMENT
Username           TEXT NOT NULL UNIQUE
PasswordHash       BLOB NOT NULL      -- PBKDF2-SHA1, 32 bytes
PasswordSalt       BLOB NOT NULL      -- 32 bytes CSPRNG
Role               TEXT NOT NULL      -- ⚠ free-text, no FK
IsActive           INTEGER NOT NULL DEFAULT 1
MustChangePassword INTEGER NOT NULL DEFAULT 1
CreatedAt          TEXT NOT NULL DEFAULT (datetime('now'))
-- Retrofitted: PasswordIterations, LastCenterID, FailedAttempts, LockoutUntil
```

**🔍 Problems:**
1. 🔴 **`Role` is an unconstrained free-text column.** There is no `TblRole` table and no `CHECK` constraint. Authorization throughout the application performs `string.Equals(Role, "Admin", OrdinalIgnoreCase)`. A typo (`"admin "` with trailing space, `"Adminstrator"`) silently produces a user with **no privileges** — or, in the `IsSuperAdmin` path, a failure to match means silent privilege *denial* rather than escalation (fail-closed, which is the safer direction). Still, this is fragile and untestable.
2. ✗ **No `Email`, no `FullName`, no `LastLoginAt`, no `PasswordChangedAt`.** Password-expiry policy (`ForcePasswordChangeDays`) cannot be enforced without `PasswordChangedAt`. **This strongly suggests the setting is non-functional** — marked ❓ Not Verified.
3. ✓ **Good:** `PasswordIterations` per-user column enabling gradual PBKDF2 strengthening without invalidating existing passwords. This is a genuinely sophisticated design.

---

### 7.3.5 History & Audit Tables

| Table | Purpose | Status |
|---|---|---|
| `TblCaseStatusHistory` | Case service-status transitions with reason/notes/user | ✓ Written on every change |
| `TblFamilyStatusHistory` | Member status transitions | ✓ Written |
| `TblFamilyRoleHistory` | Member role changes (deliberately separate table) | ✓ Written |
| `TblApplicantStatusHistory` | Applicant status transitions | ✓ Written |
| `TblArchiveHistory` | Archive/restore events | ✓ Written |
| `TblAuditLog` | General operation trail (user, op, entity, old→new) | ✓ Written; ⚠ **can be disabled** via `AuditEnabled=0` |
| `TblAuditLogs` | 🔴 **Duplicate/parallel audit table** | 🔴 See below |
| `EntRecordVersion` | Full row snapshots + field-level diff | ✓ **Now wired** (this engagement) |
| `EntSecurityEvent` | Security events | ✓ Written by `SecurityAudit` |
| `EntErrorLog` | Unhandled exceptions | ✓ Written by `ErrorLogger` |
| `AccAudit` | Accounting-specific trail | ✓ Written |

**🔴 CRITICAL SCHEMA DEFECT — `TblAuditLog` vs `TblAuditLogs`:**

Two near-identically-named audit tables exist with **different schemas**:

| `TblAuditLog` | `TblAuditLogs` |
|---|---|
| `LogID, UserID, Username, Operation, EntityName, EntityID, OldValue, NewValue, CenterID, CreatedAt` | `LogID, TableName, ActionType, RecordID, UserID, ActionDate` |
| Written by `AuditLogger.Log()` — **actively used everywhere** | 🔴 **Never written by any code** |
| Read by `FrmDashboard`, backup, exports | Read **only** by `DevCenterService.GetSystemLog()` |

`DevCenterService.GetSystemLog()` queries `TblAuditLogs` and its inline comment claims *"the application already writes record-level change tracking here"* — **this is false**. No `INSERT` into `TblAuditLogs` exists anywhere in the codebase. The Dev Center "System Log" screen will **always be empty**, and its comment actively misleads maintainers.

**Recommendation:** Either point `GetSystemLog()` at `TblAuditLog`, or drop `TblAuditLogs`. Do not leave both.

---

### 7.3.6 Financial Tables

**🔴 MONETARY PRECISION FINDING:**

All amount columns across the system are `REAL` (IEEE-754 double):
- `TblAssistance.Amount REAL NOT NULL`
- `AccTransaction.Amount`, `DollarAmount`, `DollarRate`
- `AccStipend.AmountPerFamily`, `TotalPaid`
- `AccSalary.Amount`, `AccExpenseItem.Price`
- `AccPeriod.OpeningBalance`, `AccFund.OpeningBalance`

The **Accounting module correctly mitigates this** with `Money.cs` — a well-documented helper providing `Round()` (AwayFromZero), `AreEqual()` with a 0.005 epsilon, and explicit guidance never to use `==` on doubles. The inline documentation explains the decision to keep `REAL` columns rather than risk a migration on a live production database. **This is defensible, mature engineering.**

However — 🔴 **`Money` is used in only 4 files, all inside `Accounting/`.** `TblAssistance.Amount` in `FrmFinance` uses raw `double`/`decimal.TryParse` arithmetic with no rounding discipline. Per-case assistance totals are therefore subject to the exact floating-point accumulation error that `Money` was written to prevent.

**Recommendation:** Either extend `Money` usage to `FrmFinance` and assistance reporting, or promote `Money` out of `Accounting/` into `Helpers/` and apply it at every monetary boundary.

**Additional finding:** `AccStipend` has **no `CasID`** column. Stipends are recorded in aggregate (province / district / *Sadat* type / family size / count). It is therefore **structurally impossible** to answer "how much has household X received in stipends?" from the accounting ledger.

---

### 7.3.7 Orphaned / Dead Tables

| Table | Created By | Read By | Written By | Verdict |
|---|---|---|---|---|
| `TblScheduledReport` | `DatabaseInitializer` | ✗ Nothing | ✗ Nothing | 🔴 **DEAD** |
| `TblCaseTransferHistory` | `DatabaseInitializer` | ✗ Nothing | ✗ Nothing | 🔴 **DEAD** |
| `TblAuditLogs` | `DatabaseInitializer` | `DevCenterService` only | ✗ Nothing | 🔴 **DEAD (write side)** |

These represent **planned features that were never built** (scheduled reporting; inter-center case transfer). They cost nothing at runtime but mislead maintainers into believing the capability exists.

---

## 7.4 Entity Relationship Explanation (ERD)

```
                          ┌──────────────┐
                          │  TblCenter   │  (multi-tenant root)
                          └──────┬───────┘
                                 │ CenterID (nullable, retrofitted — ⚠ soft link, NO FK)
                                 │
   ┌──────────────┐        ┌─────▼────────┐        ┌──────────────┐
   │ TblApplicant │───────▶│   TblCase    │◀──────▶│TblCaseRelation│ (self-ref ×2)
   └──────┬───────┘ Convert└──┬───┬───┬───┘        └──────────────┘
          │ CASCADE            │   │   │  CASCADE (all children)
          ▼                    │   │   │
  ┌─────────────────────┐      │   │   │
  │TblApplicantStatus   │      │   │   └────────────▶ ┌──────────────┐
  │      History        │      │   │                  │TblAssistance │
  └─────────────────────┘      │   │                  └──────┬───────┘
                               │   │                         │ (no FK)
          ┌────────────────────┘   └──────────┐              ▼
          ▼                                   ▼      ┌─────────────────┐
   ┌──────────────┐                    ┌──────────┐  │AssistanceReceipt│
   │  TblFamily   │                    │ TblDocs  │  └─────────────────┘
   └──┬────────┬──┘                    └──────────┘
      │CASCADE │CASCADE
      ▼        ▼
 ┌─────────┐ ┌──────────┐        ┌────────────────────┐
 │FamilyStatus│FamilyRole│        │TblCaseStatusHistory│ ⚠ NO FK to TblCase
 │  History  ││ History  │        └────────────────────┘
 └─────────┘ └──────────┘

  ╔═══════════════════════════════════════════════════════════════╗
  ║  🔴 COMPLETELY DISCONNECTED ISLANDS — no FK to any of above:   ║
  ║                                                                ║
  ║   Acc* (11 tables)   — financial ledger, aggregate only        ║
  ║   Ent* (22 tables)   — polymorphic (EntityName + EntityID)     ║
  ║   Sync* (6 tables)   — polymorphic (EntityName + GlobalID)     ║
  ╚═══════════════════════════════════════════════════════════════╝
```

**ERD observations:**

1. ✓ The core case cluster (`TblCase` + 4 children) has **proper FK constraints with CASCADE**, and foreign keys are enabled per connection. Deleting a case correctly removes members, documents, assistance, and relations.
2. ⚠ **`TblCaseStatusHistory` has no FK** to `TblCase`. Deleting a case orphans its status history rows permanently. (`TblFamilyStatusHistory` and `TblFamilyRoleHistory` *do* have FKs.)
3. ⚠ **`TblCenter` has no FK** from any table. `CenterID` is a soft reference. Deleting a center does not cascade or restrict — orphaning every record in that branch.
4. ⚠ **`Ent*` and `Sync*` use polymorphic references** (`EntityName TEXT` + `EntityID INTEGER`). This is unavoidable for a generic versioning/queue design, but it means the database cannot enforce referential integrity, and orphaned version rows accumulate after deletion (intentionally — deletion snapshots must survive).
5. 🔴 **The accounting subsystem is a fully disconnected island.** There is no path in the schema from a case to its financial support in the ledger.

## 7.5 Migration Readiness

| Aspect | Assessment |
|---|---|
| **Strategy** | Imperative + idempotent, executed at every startup |
| **Mechanism** | `CREATE TABLE IF NOT EXISTS` + `EnsureColumn()` (guarded `ALTER TABLE ADD COLUMN` via `PRAGMA table_info`) |
| **Version tracking** | ✗ **None** — no `schema_version` table |
| **Downgrade path** | ✗ None |
| **Destructive rebuilds** | ⚠ Present — `MigrateServiceStatusRebuild` performs table rename + recreate + copy |
| **Data migrations** | ✓ Present — e.g. legacy status-value remapping, `MemberRole` extraction from `TblFamilyStatusHistory` into `TblFamilyRoleHistory` |
| **Ordering hazards** | ⚠ Documented — one migration must run *after* table creation or it fails with "no such table"; discovered by `ServiceStatusFilterTests` |

**Verdict: ✓ Strong for forward migration.** Any old database upgrades cleanly on launch. This is genuinely well-executed and is one of the system's best engineering qualities.

**Risks:**
- 🔴 **No schema version means no way to detect a database newer than the application.** If a branch is downgraded (older EXE opens a newer DB), behaviour is undefined — silent misbehaviour rather than a clean refusal.
- ⚠ The rename-and-rebuild migration is the highest-risk operation in the codebase; a crash mid-migration on a non-WAL database could leave `TblCase_old` orphaned. A documented bug already occurred here (SQLite auto-rewrote FK references during rename).

## 7.6 Missing Fields Summary

| Table | Missing | Impact |
|---|---|---|
| `TblUsers` | `PasswordChangedAt` | Password expiry cannot work |
| `TblUsers` | `Email`, `FullName`, `LastLoginAt` | No account recovery, poor audit readability |
| `TblCase` | `UpdatedBy` | Last editor not directly queryable |
| `TblDocs` | `FileHash`, `FileSize`, `MimeType` | No integrity or type validation |
| `TblFamily` | `IsArchived`, `CenterID` | Archive inconsistency; join-dependent isolation |
| `AccStipend` | `CasID` | Cannot reconcile aid to household |
| `TblAssistance` | `ApprovedBy`, `ApprovedAt` | No disbursement approval trail |
| *(all)* | `schema_version` | No migration safety net |

## 7.7 Future Improvements

1. 🔴 **Enable WAL journal mode** — improves concurrent read/write substantially for a multi-user shared-file deployment. One-line change: `PRAGMA journal_mode=WAL`.
2. 🔴 **Encrypt the database** (SQLCipher or `SQLiteConnection.SetPassword`).
3. 🟠 Add `schema_version` table with forward-only guard.
4. 🟠 Add `TblRole` with FK from `TblUsers.Role`.
5. 🟠 Add FK from `TblCaseStatusHistory.CasID`.
6. 🟡 Index `TblFamily.MemberName`, `MemberTazkiraNo`; `TblDocs.DocType`.
7. 🟡 Drop `TblScheduledReport`, `TblCaseTransferHistory`; resolve `TblAuditLogs`.
8. 🟡 Apply `Money` discipline to `TblAssistance`.

---

# 8. Business Features Audit

| # | Capability | Status | Evidence | Risk | Priority |
|---|---|---|---|---|---|
| 1 | **Applicant intake** | ✓ Implemented | `FrmApplicant`, `TblApplicant`, conversion to case | 🟢 Low | — |
| 2 | **Case registration** | ✓ Implemented | `FrmCase`, 40+ fields, validation, uniqueness | 🟡 Med | — |
| 3 | **Family member management** | ✓ Implemented | `FrmFamily`, per-member status + role history | 🟢 Low | — |
| 4 | **Document attachment** | ⚠ Partial | Works; ✗ no type/size/malware validation, no integrity hash | 🟡 Med | 🟠 High |
| 5 | **Service-status lifecycle** | ✓ Implemented | `CaseDomain` vocabulary + `CHECK` constraint + history + reason capture | 🟢 Low | — |
| 6 | **Per-case assistance registration** | ⚠ Partial | Recorded; ✗ no approval, ✗ no budget check, ✗ no ledger posting | 🔴 High | 🔴 Critical |
| 7 | **Income registration (org)** | ✓ Implemented | `AccTransaction` direction=دریافت, funds, parties, categories | 🟡 Med | — |
| 8 | **Expense registration (org)** | ✓ Implemented | `AccTransaction` + `AccExpenseItem` | 🟡 Med | — |
| 9 | **Stipend management** | ⚠ Partial | Aggregate only — ✗ **no household linkage** | 🔴 High | 🔴 Critical |
| 10 | **Salary management** | ✓ Implemented | `AccSalary` | 🟢 Low | — |
| 11 | **Fund/cash-book balances** | ✓ Implemented | `AccFund` + `GetFundBalance`; documented fix linking stipend/salary/expense to funds | 🟡 Med | — |
| 12 | **Fiscal period management** | ✓ Implemented | `AccPeriod` with open/closed, opening balance, multi-month | 🟢 Low | — |
| 13 | **Document reversal (not deletion)** | ✓ Implemented | `IsReversed` + mandatory reason + 8 dedicated tests | 🟢 Low | — |
| 14 | **Currency conversion** | ⚠ Partial | Manual `DollarAmount` + `DollarRate` per transaction; ✗ no rate table, ✗ no history, ✗ no auto-conversion | 🟡 Med | 🟡 Medium |
| 15 | **Customer/counterparty accounts** | ⚠ Partial | `AccParty` exists; ❓ per-party statement/balance **Not Verified** | 🟡 Med | 🟡 Medium |
| 16 | **Dynamic reporting** | ✓ Implemented | 6 sources, filters, grouping, saved templates | 🟢 Low | — |
| 17 | **Word/PDF/Excel export** | ✓ Implemented | OpenXml + ClosedXML + PDF conversion; history & assistance sections added | 🟢 Low | — |
| 18 | **Scheduled reports** | ✗ **Missing** | `TblScheduledReport` created but **dead** | 🟢 Low | 🟡 Medium |
| 19 | **Guardian ID cards** | ✓ Implemented | Templates, renderer, barcode, QR, batch print | 🟢 Low | — |
| 20 | **Assistance receipts** | ✓ Implemented | Single, filtered batch, package batch | 🟢 Low | — |
| 21 | **Duplicate detection** | ✓ Implemented | `DuplicateDetector` (24 KB) fuzzy matching; auto-merge deliberately omitted | 🟢 Low | — |
| 22 | **Data quality checks** | ✓ Implemented | `DataQualityChecker` + `FrmDataQualityReport` | 🟢 Low | — |
| 23 | **Bulk Excel import** | ✓ Implemented | `ExcelCaseImporter` with insert/skip counts + audit | 🟡 Med | — |
| 24 | **Archive / restore** | ✓ Implemented | Soft archive + restore + permanent delete | 🟢 Low | — |
| 25 | **Multi-center (multi-tenant)** | ⚠ Partial | Enforced in queries via `CenterFilterId`; ⚠ `CenterID` nullable, no FK, legacy NULLs invisible | 🟠 High | 🟠 High |
| 26 | **Role management** | ⚠ Partial | 4 hard-coded roles as free-text strings; ✗ no role table, ✗ no custom roles | 🟠 High | 🟠 High |
| 27 | **Fine-grained permissions** | 🔴 **Not enforced** | Full RBAC engine + admin UI exist; **zero CRUD enforcement** | 🔴 **Critical** | 🔴 **Critical** |
| 28 | **Module enable/disable** | ✓ Implemented | `ModuleService` — global/role/user scope, **genuinely enforced** in navigation | 🟢 Low | — |
| 29 | **Approval workflow** | ⚠ Partial | Engine + chains + UI exist; ⚠ gated by unenforced permissions; ❓ not applied to case/assistance | 🟠 High | 🟠 High |
| 30 | **Business rule engine** | ⚠ Partial | `RuleEngine` + `FrmRules` + logging; ❓ real-world rule coverage **Not Verified** | 🟡 Med | 🟡 Medium |
| 31 | **Record locking** | ⚠ Partial | `LockService` complete; 🔴 **not applied to case/family editing** — last-write-wins persists | 🟠 High | 🟠 High |
| 32 | **Audit trail** | ✓ Implemented | `TblAuditLog` + status histories + `EntRecordVersion`; ⚠ can be disabled by setting | 🟡 Med | — |
| 33 | **Full change history (versioning)** | ✓ **Implemented** | `EntRecordVersion` — **wired this engagement** across insert/update/delete for 4 entities + viewer | 🟢 Low | — |
| 34 | **Backup (automatic)** | ✓ Implemented | Daily/weekly/monthly, retention pruning, audit-logged | 🟠 High | 🟠 High |
| 35 | **Backup (manual)** | ✓ Implemented | `BackupHelper.ExportBackup` — 22 tables after this engagement's fixes | 🟠 High | — |
| 36 | **Restore** | ✓ Implemented | Two modes: GlobalID merge, and classic full replace | 🔴 High | 🟠 High |
| 37 | **Backup encryption** | ✗ **Missing** | Plaintext DataSet + copied files | 🔴 **Critical** | 🔴 **Critical** |
| 38 | **Accounting in main backup** | ✗ **Missing** | Separate `AccountingBackupHelper` only — easily forgotten | 🔴 High | 🔴 Critical |
| 39 | **Offline operation** | ✓ Implemented | Fully local by default | 🟢 Low | — |
| 40 | **Multi-branch sync** | ⚠ Partial | Outbox + 3 transports + conflict resolution; ✗ history & accounting never sync | 🔴 High | 🔴 Critical |
| 41 | **Media/file sync** | ✓ Implemented | `MediaScanner` + `MediaSyncEngine` + download queue | 🟠 High | — |
| 42 | **Sync conflict resolution** | ✓ Implemented | Analyzer + resolver + UI + 14 tests | 🟠 High | — |
| 43 | **Licensing** | 🔴 **Not enforced** | HMAC-signed tokens + hardware ID + expiry — **never gates anything** | 🟡 Med | 🟡 Medium |
| 44 | **Session timeout** | ✓ Implemented | Global message filter; closes app on timeout | 🟢 Low | — |
| 45 | **Account lockout** | ✓ Implemented | Configurable attempts + duration | 🟢 Low | — |
| 46 | **Password policy** | ⚠ Partial | `MinPasswordLength` setting exists; ❓ complexity & expiry enforcement **Not Verified** (no `PasswordChangedAt` column ⇒ expiry likely non-functional) | 🟠 High | 🟠 High |
| 47 | **Error logging** | ✓ Implemented | `ErrorLogger` global handler + severity + Persian messages + viewer | 🟢 Low | — |
| 48 | **Diagnostics & repair** | ✓ Implemented | Dev Center — health report, repair routines, 33 safety tests | 🟡 Med | — |
| 49 | **Multi-language UI** | ✓ Implemented | 5 languages, auto-sweep on window open | 🟢 Low | — |
| 50 | **Jalali calendar** | ✓ Implemented | `PersianDateHelper`, `PersianDatePicker`, app-wide culture | 🟢 Low | — |

## 8.1 Business Feature Scorecard

| Status | Count | % |
|---|---|---|
| ✓ **Fully Implemented** | **31** | 62% |
| ⚠ **Partially Implemented** | **14** | 28% |
| 🔴 **Built but Not Enforced** | **2** | 4% |
| ✗ **Missing** | **3** | 6% |

**The "built but not enforced" category is the most dangerous.** Permissions and licensing both present a complete, convincing administrative interface that has no effect on system behaviour. An administrator who revokes a permission and believes the system is now secure has been actively misled by the software.

---

# 9. Afghan Localization Audit

## 9.1 Architecture

| Component | Purpose | Assessment |
|---|---|---|
| `Lang.cs` (18 KB) | Runtime translation lookup, language selection/persistence | ✓ Solid |
| `LangData.cs` (142 KB) | Embedded 5-column translation table (fa \| ps \| ar \| ur \| en) | ✓ Well-structured |
| `LanguageSweep.cs` | Auto-applies translation to **every** window on open | ✓ Clever — no per-form work needed |
| `Languages/*.txt` | External override channel for native translators (no recompile) | 🔴 **0% populated** |
| `RtlCaptions.cs` | Global RTL title-bar/caption enforcement via message filter | ✓ Excellent |
| `PersianDateHelper.cs` / `PersianDatePicker.cs` | Jalali calendar + custom date control | ✓ Complete |
| `AfghanGeoData.cs` (10 KB) | Afghan province/district reference data | ✓ Present |

## 9.2 RTL Support — ✓ Excellent

**Verdict: This is the strongest localization dimension in the system.**

- ✓ `RtlCaptions.Install()` is called in `Program.Main` **before the login form**, guaranteeing every window — including modal dialogs — receives RTL captions
- ✓ Forms set `RightToLeft = Yes` and `RightToLeftLayout = true`
- ✓ Persian culture (`PersianDateHelper.GetPersianCulture()`) applied to `CurrentCulture` and `CurrentUICulture` at startup
- ✓ DPI awareness (`PerMonitorV2`) prevents the layout distortion that historically pushed buttons off-screen
- ✓ Dedicated RTL regression tests exist (`AccountingRtlTests`)
- ✓ Project memory records that user feedback *"it's on the left"* consistently means *"make it RTL"* — indicating RTL issues were actively hunted and fixed

## 9.3 Translation Coverage — ⚠ Partial

| Language | Code | Strings in `LangData.cs` | Fill Rate |
|---|---|---|---|
| Persian / Dari | `fa` | 522 (source keys) | ✓ 100% (native) |
| Pashto | `ps` | 522 | ✓ **100%** |
| Arabic | `ar` | 522 | ✓ **100%** |
| Urdu | `ur` | 522 | ✓ **100%** |
| English | `en` | 522 | ✓ **100%** |

**However** — each `Languages/*.txt` file contains **927 additional on-screen strings**, auto-generated under the header:

> *"بخش ۱ — اولویت بالا: این متن‌ها روی صفحه دیده می‌شوند و هنوز ترجمه ندارند"*
> *("Section 1 — high priority: these texts are visible on screen and are not yet translated")*

**All 927 entries in all 4 files are empty (0% translated).**

### Realistic coverage calculation

| | Count |
|---|---|
| Strings translated (`LangData.cs`) | 522 |
| Strings identified as visible but untranslated (`*.txt`) | 927 |
| **Estimated total display strings** | **≈ 1,449** |
| **Actual translation coverage** | **≈ 36%** |

> ⚠ **The `LangData.cs` header comment claims the project has "580 unique display strings."** The generated `.txt` files contradict this, listing 927 *additional* untranslated strings. Either the comment is outdated or the sweep tool counts differently. **Marked ❓ Not Verified** — but under either reading, coverage is materially below 100%.

**Behaviour on miss:** ✓ **Correct and safe.** Untranslated strings fall back to Persian — never to a blank string or a raw key. A non-Persian user therefore sees a *mixed* interface rather than a broken one.

**Practical impact:** The 522 translated strings cover the highest-frequency UI chrome (buttons: New/Save/Edit/Delete, form titles, field labels, common messages). A Pashto-speaking user will find navigation usable but will encounter Persian for most domain-specific content, validation messages, and report headers.

## 9.4 Afghan Terminology — ⚠ **Inconsistent**

This is the most significant localization defect. The system mixes **Afghan Dari** and **Iranian Persian** administrative vocabulary.

| Concept | ✓ Afghan (Correct) | Uses | 🔴 Iranian (Incorrect) | Uses |
|---|---|---|---|---|
| Province | **ولایت** | 88 | **استان** | **51** |
| District | **ولسوالی** | 79 | **شهرستان** | 2 |
| ID document | **تذکره** | 132 | کد ملی | 0 ✓ |
| Currency | **افغانی** | 59 | **ریال** | **7** |
| Currency | — | — | تومان | 0 ✓ |

### 🔴 Finding 9.4.1 — `استان` used 51 times

The Iranian term for province appears **51 times** against 88 correct uses of `ولایت` — a **37% contamination rate** on the single most important geographic term in an Afghan system.

**Affected files (top offenders):**

| File | Occurrences | Severity |
|---|---|---|
| `Helpers/DatabaseInitializer.cs` | 6 | 🔴 **Critical** — may be seeded into `TblLookup` reference data |
| `Accounting/AccRepair.cs` | 5 | 🟠 High — user-visible repair messages |
| `FrmCase.cs` | 3 | 🔴 **Critical** — primary data-entry screen |
| `DevCenter/DevCenterService.cs` | 2 | 🟡 Medium — admin-only |
| `Helpers/UiTheme.cs` | 2 | 🟡 Medium |
| `FrmAssistancePackageBatchPrint.cs` | 2 | 🟠 High — **printed output** |
| `Helpers/FieldBox.cs` | 2 | 🟠 High — shared control |
| `Helpers/Msg.cs` | 2 | 🟠 High — shared messages |

> ⚠ **`DatabaseInitializer.cs` is the most serious.** If `استان` appears in seeded `TblLookup` values or column labels, the wrong term is **persisted into production databases** and will propagate through exports, reports, and sync packages. Correcting it later requires a data migration, not just a code change.

### 🔴 Finding 9.4.2 — `ریال` (Iranian Rial) used 7 times

The Iranian currency appears in **user-facing and printed** contexts:

| File | Line | Context |
|---|---|---|
| `AssistanceReceiptService.cs` | 64 | 🔴 **Printed receipt** — given to beneficiaries |
| `GuardianCardData.cs` | 9 | 🔴 **Printed ID card** |
| `Helpers/LangData.cs` | 188, 190, 191 | 🔴 **Translation table** — propagates to all 4 languages |
| `Helpers/ReportDefinitions.cs` | 53, 64 | 🟠 Report column labels |

**Impact:** The official currency of Afghanistan is the **Afghani (؋ / AFN)**. Printing "ریال" on a receipt handed to an Afghan beneficiary, or on a guardian ID card, is a **credibility and correctness failure visible to the organization's donors and the people it serves**. Because three occurrences are inside `LangData.cs`, the error is replicated into Pashto, Arabic, Urdu, and English output.

## 9.5 Jalali Calendar — ✓ Good

- ✓ Application-wide Persian culture with `PersianCalendar`
- ✓ `PersianDateHelper.ToPersianDateString()` used consistently in exports
- ✓ Custom `PersianDatePicker` control (17 KB)
- ✓ Dedicated tests (`Dates_AreJalaliDayMonthYear`, `NonDateValues_AreLeftAlone`)
- ⚠ **Storage format risk:** dates are stored as `TEXT` with no constraint. `TblReminder.RemindAt` is deliberately stored as **Gregorian ISO** (documented, so `datetime('now')` comparison works), while `TblCase.CaseDate` stores Jalali strings. **Two different calendars in one database with no column-level indication of which is which.** Any future developer writing a date query must know this per-column.
- ❓ **Not Verified:** whether every write path zero-pads (`1404/02/05` vs `1404/2/5`). Non-padded dates break lexical range filtering silently.

## 9.6 Currency Formatting — ⚠ Partial

- ✓ `N0` format used for amounts (thousands separator, no decimals) — appropriate for Afghani
- ✓ `Money.Decimals = 2` retained for USD conversion fidelity
- ⚠ No centralized currency formatter — `ToString("N0")` is repeated inline throughout
- 🔴 Currency **symbol** is inconsistent (`ریال` contamination above)
- ✗ No Persian/Eastern-Arabic digit rendering option (۱۲۳ vs 123) — **Not Verified** whether required

## 9.7 Localization Scorecard

| Dimension | Score | Verdict |
|---|---|---|
| RTL support | **95 / 100** | ✓ Excellent |
| i18n architecture | **90 / 100** | ✓ Excellent design |
| Jalali calendar | **85 / 100** | ✓ Good; storage-format ambiguity |
| Translation coverage | **36 / 100** | ⚠ Infrastructure ready, content incomplete |
| **Afghan terminology** | **55 / 100** | 🔴 **Significant contamination** |
| Currency correctness | **60 / 100** | 🔴 Wrong currency on printed artifacts |
| **Overall Localization** | **70 / 100** | ⚠ Strong foundation, incorrect content |

## 9.8 Recommended Fixes (Prioritized)

| # | Fix | Effort | Priority |
|---|---|---|---|
| 1 | Replace all 7 `ریال` → `افغانی`, prioritizing `AssistanceReceiptService`, `GuardianCardData`, `LangData` | **1 hour** | 🔴 **Critical** |
| 2 | Audit all 51 `استان` → `ولایت`; **check `DatabaseInitializer` for seeded lookup data and write a data migration if found** | 3–4 hours | 🔴 **Critical** |
| 3 | Replace 2 `شهرستان` → `ولسوالی` | 15 min | 🟠 High |
| 4 | Add a **build-time guard test** asserting zero occurrences of `استان`/`شهرستان`/`ریال`/`تومان` in source — prevents regression permanently | 1 hour | 🟠 **High** |
| 5 | Commission native Dari/Pashto translator to fill `Languages/ps.txt` (927 strings) | 2–3 days | 🟡 Medium |
| 6 | Document Jalali-vs-Gregorian storage convention per column; add a naming convention (e.g. `*_G` suffix for Gregorian) | 2 hours | 🟡 Medium |
| 7 | Centralize currency formatting in one `Money.Format()` helper | 2 hours | 🟡 Medium |

> 💡 **Recommendation #4 is the highest-leverage item.** A single unit test scanning the source tree for forbidden terms converts a recurring manual review burden into an automated, permanent guarantee — and this project already has the test infrastructure to host it.

---

# 10. Security Audit

## 10.1 Authentication — ⚠ Good with gaps

| Control | Status | Detail |
|---|---|---|
| Password hashing | ✓ **Strong** | PBKDF2 (`Rfc2898DeriveBytes`), **100,000 iterations**, SHA-1 PRF |
| Salt | ✓ **Strong** | 32 bytes from `RandomNumberGenerator` (CSPRNG), unique per user |
| Hash comparison | ✓ **Strong** | Constant-time (`FixedTimeEquals` — manual XOR accumulation) |
| Iteration migration | ✓ **Excellent** | Per-user `PasswordIterations` column allows strengthening without invalidating existing passwords. Legacy users verify at 10,000; new/changed passwords use 100,000 |
| Account lockout | ✓ Implemented | Configurable threshold (default 5) + duration (default 15 min) |
| Session timeout | ✓ Implemented | Global `IMessageFilter` catching mouse/keyboard across all windows including modals |
| Forced password change | ✓ Present | `MustChangePassword` column, default 1 |
| Password expiry | 🔴 **Non-functional** | `ForcePasswordChangeDays` setting exists but **`TblUsers` has no `PasswordChangedAt` column** — expiry is uncomputable |
| Password complexity | ❓ Not Verified | `MinPasswordLength` setting exists; enforcement of complexity not confirmed |
| Multi-factor auth | ✗ Missing | — |
| Credential recovery | ✗ Missing | No email/security questions; lockout requires admin DB intervention |
| Concurrent sessions | ✗ Not controlled | Same account may log in on multiple machines |

> ⚠ **PBKDF2-SHA1 note:** `Rfc2898DeriveBytes` on .NET Framework 4.7.2 defaults to **HMAC-SHA1**. While 100,000 iterations of PBKDF2-SHA1 remains acceptable by current OWASP guidance (which recommends 1.3M for SHA-1, 600k for SHA-256), SHA-256 would be preferable. **However**, changing the PRF would invalidate all existing passwords unless a second migration column is added — the same pattern already used successfully for iterations. **Low priority given the far larger encryption gap.**

**Authentication verdict: 7.5 / 10** — genuinely well-engineered core, undermined by a non-functional expiry policy.

---

## 10.2 Authorization — 🔴 **Critical Weakness**

### Two parallel, disconnected authorization systems exist:

**System A — `SecurityContext` (crude, but actually enforced):**
```csharp
IsSuperAdmin()  → Role == "SuperAdmin"
IsAdmin()       → IsSuperAdmin() || Role == "Admin"
CanEdit()       → IsAdmin() || Role == "Operator"
CanDelete()     → IsAdmin()
```
- ✓ Actually called throughout forms (`FrmCase.btnDelete_Click`, `btnEdit_Click`, etc.)
- 🔴 Based on **free-text role string comparison** with no `TblRole` table or `CHECK` constraint
- 🔴 Only **4 hard-coded roles**; no custom roles possible
- 🔴 **Coarse-grained** — no distinction between "edit case demographics" and "change service status"

**System B — `PermissionService` (sophisticated, but enforces nothing):**
- Full RBAC: `EntPermission`, `EntRolePermission`, `EntUserPermission`
- Per-user explicit grant **and** explicit deny overriding role
- Cached with `InvalidateCache()`
- `HasPermission(key)` and `Require(key, entity, id)` — the latter logging a security event on denial
- Complete administrative UI (`FrmPermissionMatrix`)

### 🔴 CRITICAL FINDING — SEC-001

**`PermissionService` governs nothing except workflow transitions.**

Call-graph analysis of every `PermissionService.*` reference in the codebase:

| Call Site | Purpose |
|---|---|
| `PermissionService.Install()` | Setup — assigns `WorkflowService.PermissionGate = HasPermission` |
| `FrmPermissionMatrix` (×6 calls) | Administrative UI only |
| `FrmModules` (×2 calls) | Reads role list for module UI |
| `DevCenterService` (×1) | `InvalidateCache()` |

**`HasPermission()` / `Require()` are called from exactly ONE runtime path:** `WorkflowService.PermissionGate`, consumed by `WorkflowService.CanTransition()` and `ApprovalService`.

**Consequence:** An administrator opens the Permission Matrix, revokes `case.delete` from the *Operator* role, and saves. The UI confirms success. **The Operator can still delete cases** — because `FrmCase.btnDelete_Click` checks `SecurityContext.CanDelete()`, which is a role-string comparison that never consults `EntRolePermission`.

**This is worse than having no permission system**, because it manufactures false confidence in a control that does not exist.

**Severity: 🔴 CRITICAL** · **CVSS-equivalent: High** · **OWASP: M1 Improper Credential Usage / M3 Insecure Authentication-Authorization**

**Remediation options:**
| Option | Effort | Recommendation |
|---|---|---|
| **A.** Wire `PermissionService.Require()` into every CRUD entry point, keeping `SecurityContext` as a fallback | 3–5 days | ✅ **Recommended** |
| **B.** Remove `FrmPermissionMatrix` from navigation until enforcement lands | 1 hour | ✅ **Immediate stopgap** |
| **C.** Leave as-is | — | 🔴 Unacceptable |

> **Recommend doing B today and A in the next sprint.** Option B costs one line and immediately eliminates the false-confidence risk.

**Authorization verdict: 3 / 10**

---

## 10.3 Multi-Tenant Isolation — ⚠ Partial

| Control | Status |
|---|---|
| `CenterGuard.EnsureCaseAccess()` on case read/write/export | ✓ Present |
| `CenterFilterId` applied to queries (`@CID = 0 OR CenterID = @CID`) | ✓ Widely used |
| SuperAdmin "All Centers" bypass | ✓ Intentional |
| `TblCase.CenterID` nullable, no FK | 🔴 Weak |
| `TblFamily` / `TblDocs` have **no** `CenterID` | 🔴 Isolation depends entirely on the developer remembering to `JOIN TblCase` |
| Legacy `NULL` CenterID rows | 🔴 Invisible to non-SuperAdmin — silent data loss from the user's perspective |

**Finding SEC-002 (🟠 High):** Center isolation is enforced *by convention in query text*, not by the schema. Any future direct query against `TblFamily` or `TblDocs` that omits the join **silently leaks cross-branch data**. There is no defence-in-depth (no row-level security, no view layer, no repository chokepoint).

---

## 10.4 Data Storage — 🔴 **Critical**

### 🔴 CRITICAL FINDING — SEC-003: No Encryption At Rest

**Verified by exhaustive search:** the only cryptography in the entire codebase is `HMACSHA256` in `LicenseManager` (license token signing) and `Rfc2898DeriveBytes` in `PasswordHelper`. There is **no** `Aes`, no `ProtectedData` (DPAPI), no `SQLiteConnection.SetPassword`, no SQLCipher.

**Connection string:**
```xml
<add name="CaseDb"
     connectionString="Data Source=|DataDirectory|\CaseDB.sqlite;Version=3;"
     providerName="System.Data.SQLite" />
```
No `Password=` parameter. **The database is a plaintext file.**

**What is exposed in plaintext:**

| Asset | Location | Sensitivity |
|---|---|---|
| Orphan & widow names, father names | `CaseDB.sqlite` | 🔴 PII |
| **Tazkira (national ID) numbers** | `TblCase.HeadTazkiraNo`, `TblFamily.MemberTazkiraNo` | 🔴 **Government ID** |
| Home addresses, current & original residence | `TblCase` | 🔴 **Physical location of vulnerable minors** |
| Phone numbers (own + relative) | `TblCase` | 🔴 PII |
| **Photographs of minors** | File system (`PhotoPath`, `MemberPhotoPath`) | 🔴 **Biometric/image of children** |
| Disability status & degree | `TblCase`, `TblFamily` | 🔴 Special-category health data |
| Marital status, economic priority | `TblCase` | 🔴 Sensitive |
| **Religious/ethnic markers** (`HeadSadat`, `Religion`, `SadatType`) | `TblCase`, `AccStipend` | 🔴 **Special-category — minority identification** |
| Financial assistance amounts per household | `TblAssistance` | 🟠 Financial |
| Scanned documents (Tazkira, death certificates) | File system | 🔴 **Identity documents** |
| **All backups** | `AutoBackups/` folder | 🔴 **Complete dataset, unencrypted** |

**Threat model — realistic attack paths requiring only file access:**
1. Laptop/desktop theft from a branch office → complete dataset
2. Departing employee copies `CaseDB.sqlite` to USB → complete dataset, no trace
3. Repair technician servicing a machine → complete dataset
4. Commodity malware/ransomware exfiltrating `*.sqlite` → complete dataset
5. Backup folder on a network share with loose ACLs → complete dataset
6. Sync package intercepted in transit (courier/USB) → **Not Verified** whether packages are encrypted

**Contextual severity:** In Afghanistan, a file identifying vulnerable minority households (the schema explicitly tracks *Sadat*, *Ahl-e Sunnat*, and religion), their exact addresses, and photographs of their children is not merely a privacy concern — it is a **document that could enable physical targeting**. The organization's own beneficiaries bear the risk.

**Severity: 🔴 CRITICAL** · **OWASP: M9 Insecure Data Storage** · **Also implicates M2 Inadequate Supply Chain / M5 Insecure Communication (sync)**

**Remediation:**
| Option | Effort | Trade-off |
|---|---|---|
| **SQLCipher** (via `System.Data.SQLite` with `Password=`) | 3–5 days | ✅ Strongest. Requires key management + migration of all existing DBs |
| **Windows EFS / BitLocker** at OS level | 1 day | ⚠ Partial — protects at rest only, not against a logged-in user or a copied file |
| **DPAPI-encrypt backups only** | 1–2 days | ⚠ Partial — closes the highest-volume leak path first |

> ⚠ **This change breaks every existing installation** and requires a migration plan, key custody policy, and a documented recovery procedure for a lost key. It **must not** be attempted without the product owner's explicit sign-off on those three items. This is why it was deliberately **not** implemented during the previous remediation engagement.

---

## 10.5 Backup Security — 🔴 Critical

| Control | Status |
|---|---|
| Backup encryption | 🔴 **None** — plaintext `DataSet` serialization + raw file copy |
| Backup integrity/checksum | ✗ None |
| Backup access control | ✗ Filesystem ACLs only |
| Backup location | ⚠ User-configurable — could be a network share or synced cloud folder |
| Retention pruning | ✓ Implemented (default 14) |
| Restore authorization | ⚠ Reachable from Settings; gated by `IsAdmin()` role string only |
| Restore audit trail | ⚠ `LastRestoreDate` setting; ❓ full audit entry **Not Verified** |

**Finding SEC-004 (🔴 Critical):** Backups are the **highest-value, lowest-effort exfiltration target** — a single self-contained file containing the entire dataset, written automatically on a schedule to a predictable location.

---

## 10.6 Input Validation — ✓ Good

| Vector | Status |
|---|---|
| **SQL injection** | ✓ **Not found.** Exhaustive review found parameterized queries throughout. Dynamic SQL fragments are built exclusively from **code-controlled constants** (`CaseDomain.SqlValueList`, `IdCardHelper`, `SyncFileService` state constants, report catalog `Expression` fields) — never from user input |
| `DataView.RowFilter` injection | ✓ **Mitigated.** `FrmDocs` and `FrmArchive` use `EscapeDataViewLike()`; `FrmDuplicates` and `FrmValidationReport` escape quotes. ⚠ `FrmDevCenter.cs:840` builds a filter from raw `text` — admin-only, and impact is a filter exception rather than data disclosure. **Severity: Low** |
| Path traversal | ⚠ `IsStoredPhotoPathAllowed()` guard exists before deletion — ✓ good practice. ❓ Full coverage of all file operations **Not Verified** |
| File upload validation | 🔴 **None found** — no extension whitelist, size cap, or content-type check |
| Numeric/date parsing | ✓ `TryParse` used consistently |
| Business-rule validation | ✓ `ValidateForm()`, `AccountingRuleException`, `CHECK` constraints |

**Finding SEC-005 (🟡 Medium):** Users may attach arbitrary files (including `.exe`, `.js`, `.hta`) as case documents. The application copies them into managed storage. If any workflow later opens a document with the shell default handler, this becomes a **malware delivery and execution path**. **Not Verified** whether documents are opened via `Process.Start`.

---

## 10.7 Sensitive Data Exposure

| Vector | Status |
|---|---|
| Passwords in logs | ✓ Not found |
| Connection string secrets | ✓ None (no password in connection string — because there is no encryption) |
| Stack traces to users | ✓ Suppressed — `ErrorLogger` presents Persian messages |
| Exception messages to users | ⚠ `Msg.Show("خطا در ذخیره: " + ex.Message)` in `FrmCase` — raw SQLite messages may reach the user (schema disclosure; low impact for a desktop app) |
| **Signing certificate in VCS** | 🔴 **`CaseManagement_TemporaryKey.pfx` is tracked in git** |
| Audit log disable | ⚠ `AuditEnabled = 0` silently disables the general audit trail — an admin can erase their own accountability |
| Debug output | ⚠ `Debug.WriteLine` used widely — stripped in Release ✓ |
| `audit_errors.log` | ⚠ Plaintext file in application directory containing entity IDs and operation names |

**Finding SEC-006 (🟠 High):** The `.pfx` signing certificate is committed to version control. Although named "TemporaryKey" (Visual Studio's ClickOnce default), any private key in a repository must be treated as compromised. **Remediation:** `git rm --cached`, add to `.gitignore`, rotate the certificate, and — if the repository was ever shared — assume the key is public.

---

## 10.8 Findings Register

### 🔴 CRITICAL

| ID | Finding | Impact | OWASP |
|---|---|---|---|
| **SEC-001** | `PermissionService` configured but **never enforced** on CRUD | False sense of access control; unauthorized operations succeed | M3 |
| **SEC-003** | **No encryption at rest** — DB, photos, documents all plaintext | Total dataset compromise on any file access; physical risk to beneficiaries | M9 |
| **SEC-004** | **Backups unencrypted** and auto-generated to a predictable path | Single-file total exfiltration | M9 |

### 🟠 HIGH

| ID | Finding | Impact |
|---|---|---|
| **SEC-002** | Center isolation enforced by query convention, not schema | Cross-branch data leak from any query missing the join |
| **SEC-006** | Signing certificate `.pfx` committed to git | Key compromise; ability to sign malicious builds |
| **SEC-007** | Password expiry non-functional (no `PasswordChangedAt` column) | Stale credentials persist indefinitely |
| **SEC-008** | Roles are unconstrained free-text; no role table | Typo → silent privilege misassignment; no custom roles |
| **SEC-009** | `LicenseManager` never enforced | No deployment control (business risk rather than security) |
| **SEC-010** | No record locking on case/family edit despite `LockService` existing | Silent last-write-wins data loss |

### 🟡 MEDIUM

| ID | Finding | Impact |
|---|---|---|
| **SEC-005** | No file-upload validation (type/size/content) | Malware storage; potential execution path |
| **SEC-011** | Audit trail can be disabled via a setting | Admin can erase accountability |
| **SEC-012** | Raw exception text shown to users in `FrmCase` | Minor schema disclosure |
| **SEC-013** | Sync package encryption **Not Verified** | Possible plaintext data in transit via USB/courier |
| **SEC-014** | No `schema_version` guard | Older EXE on newer DB → undefined behaviour |

### 🟢 LOW

| ID | Finding | Impact |
|---|---|---|
| **SEC-015** | `FrmDevCenter` RowFilter from unescaped input | Filter exception (admin-only) |
| **SEC-016** | PBKDF2 uses SHA-1 PRF | Below ideal; mitigated by 100k iterations |
| **SEC-017** | `audit_errors.log` plaintext in app directory | Minor metadata disclosure |
| **SEC-018** | No concurrent-session control | Shared-credential use undetected |

## 10.9 OWASP Mobile Top 10 (2024) Alignment

> Mapped to desktop equivalents, since the requested Android framing does not apply.

| # | Risk | Status | Notes |
|---|---|---|---|
| **M1** | Improper Credential Usage | ⚠ **Partial** | Strong hashing ✓; but authorization bypass via SEC-001 |
| **M2** | Inadequate Supply Chain Security | ⚠ **Partial** | Lean deps ✓; but no lock file, no SBOM, no scanning, `.pfx` in VCS |
| **M3** | Insecure Authentication/Authorization | 🔴 **FAIL** | SEC-001 — permission system enforces nothing |
| **M4** | Insufficient Input/Output Validation | ⚠ **Partial** | SQL injection absent ✓; file upload unvalidated ✗ |
| **M5** | Insecure Communication | ❓ **Not Verified** | HTTPS usage and sync-package encryption unconfirmed |
| **M6** | Inadequate Privacy Controls | 🔴 **FAIL** | No encryption, no data minimization, no retention policy, no subject-rights mechanism |
| **M7** | Insufficient Binary Protection | ⚠ **Partial** | .NET IL trivially decompilable; no obfuscation; HMAC license key embedded in binary |
| **M8** | Security Misconfiguration | ⚠ **Partial** | Audit disableable; default admin account created automatically |
| **M9** | **Insecure Data Storage** | 🔴 **FAIL** | SEC-003 / SEC-004 — the dominant finding |
| **M10** | Insufficient Cryptography | 🔴 **FAIL** | Cryptography essentially absent outside password hashing |

**Result: 4 FAIL · 5 PARTIAL · 1 NOT VERIFIED · 0 PASS**

## 10.10 Security Score

| Domain | Weight | Score | Weighted |
|---|---|---|---|
| Authentication | 15% | 75 | 11.3 |
| **Authorization** | 20% | **30** | 6.0 |
| **Data at rest** | 25% | **10** | 2.5 |
| Data in transit | 10% | 50 ❓ | 5.0 |
| Input validation | 10% | 75 | 7.5 |
| Audit & logging | 10% | 80 | 8.0 |
| Configuration & secrets | 10% | 55 | 5.5 |
| | | **TOTAL** | **45.8** |

# 🔴 SECURITY SCORE: **46 / 100**

**Interpretation:** The system demonstrates **genuinely competent security engineering in the areas it addresses** — the password subsystem in particular (PBKDF2 with per-user iteration migration and constant-time comparison) exceeds what is typically found in comparable NGO software. Input validation is disciplined; no SQL injection was found across ~78,500 lines.

The score is driven down by two structural omissions rather than sloppy coding: **the complete absence of encryption at rest**, and **an authorization system that was built but never connected**. Both are architectural decisions, not bugs — and both are fixable with focused, well-scoped work.

---

# 11. Code Quality Audit

## 11.1 Architecture Quality — **55 / 100**

| Aspect | Score | Assessment |
|---|---|---|
| Layering | 50 | DAL exists; business logic is split between services and form event handlers |
| Separation of concerns | 45 | 🔴 `FrmCase.cs` (3,789 LOC) performs UI, validation, SQL, file I/O, and export orchestration |
| Consistency | 40 | 🔴 Newest modules use repositories; oldest/most critical do not |
| Modularity | 70 | ✓ `Sync`, `Enterprise`, `Accounting` are cleanly bounded |
| Dependency direction | 60 | Mostly downward; static globals create hidden coupling |
| Extensibility | 65 | ✓ Module/permission/workflow engines are genuinely extensible designs |

**Strengths:** The four-initializer bootstrap is clean and idempotent. `Sync`, `Enterprise`, and `Accounting` are well-bounded subsystems with clear internal structure. `CaseDomain` centralizing the service-status vocabulary — and generating the SQL `CHECK` constraint from it — is a genuinely elegant pattern that prevents an entire class of drift bugs.

**Weaknesses:** No repository layer for the core domain. Static mutable global state (`SecurityContext`, `UiTheme`, `WorkflowService.PermissionGate`) is pervasive. `Helpers/` is a 62-file junk drawer.

## 11.2 MVVM / Pattern Implementation — **N/A → 40 / 100**

> The requested MVVM audit does not apply — WinForms with no MVP/MVVM framework. Scored against **appropriate desktop patterns** instead.

- ✗ No presenter/view-model separation — forms are god-objects
- ⚠ Some service extraction (`WorkflowService`, `RuleEngine`, `AccountingRepo`, `DuplicateDetector`) ✓
- 🔴 Business logic embedded in `btnSave_Click` handlers is the dominant pattern in core forms
- **Consequence:** Core case logic is **not unit-testable without instantiating a Form**, which is exactly why the test suite must use STA threads, `SuppressDialogs`, and `frm.Show()` workarounds

## 11.3 Repository Pattern — **35 / 100**

| Module | Repository? |
|---|---|
| `GuardianCardIntegration` | ✓ `CardTemplateRepository`, `CaseCardRepository` |
| `AssistanceReceiptIntegration` | ✓ `AssistanceReceiptRepository`, `AssistancePackageRepository` |
| `Accounting` | ⚠ `AccountingRepo` (81 KB — a repository, but monolithic) |
| **Core (Case/Family/Docs)** | 🔴 **None** — raw SQL in forms |

**This inconsistency is the single clearest indicator of the project's evolution:** newer code is better structured than older code, but the older code carries the highest business criticality.

## 11.4 UI Best Practices — **65 / 100**

- ✓ Consistent theming via `UiTheme`
- ✓ Global RTL and i18n hooks — zero per-form work required
- ✓ DPI awareness correctly configured
- ✓ Responsive layout helper
- ✓ Custom controls (`FieldBox`, `StatCard`, `PillTabStrip`, `GridPager`) promote consistency
- ⚠ Designer files hand-edited (`StyleBtn`/`SetBtn` helper patterns) — deviates from the WinForms designer round-trip but is more maintainable in practice
- 🔴 Very large form classes
- ⚠ Theme changes require restart (documented)

## 11.5 Naming Conventions — **80 / 100**

- ✓ Consistent `Frm*` prefix for forms
- ✓ Consistent `Tbl*` / `Acc*` / `Ent*` / `Sync*` table prefixes
- ✓ Consistent control prefixes (`txt`, `btn`, `cmb`, `dgv`, `lbl`)
- ✓ PascalCase methods, camelCase fields — conventional
- ⚠ `TblAuditLog` vs `TblAuditLogs` — a genuinely dangerous near-collision
- ⚠ Mixed Persian/English identifiers in SQL aliases (`AS 'شناسه'`) — pragmatic for direct grid binding but couples data access to presentation language

## 11.6 Code Duplication — **60 / 100**

Observed duplication:
- Photo save/cleanup logic repeated across `FrmCase`, `FrmFamily`, `FrmDocs`
- `SELECT last_insert_rowid()` pattern repeated (partly consolidated into `ExecuteInsertReturningId` ✓)
- Center-filter SQL fragment `(@CID = 0 OR CenterID = @CID)` repeated in dozens of queries
- Audit-text builders (`BuildCurrentCaseAuditText`, `BuildFamilyAuditText`, `BuildDocAuditText`) — parallel implementations
- Grid setup/theming repeated across forms

> 💡 The center-filter duplication is the most consequential: it is a **security control** implemented by copy-paste. A single omission creates a data leak (SEC-002).

## 11.7 Dead Code & Unused Assets — **65 / 100**

| Item | Verdict |
|---|---|
| `TblScheduledReport` | 🔴 Dead table — feature never built |
| `TblCaseTransferHistory` | 🔴 Dead table — feature never built |
| `TblAuditLogs` | 🔴 Dead on the write side; misleading comment claims otherwise |
| `Models/*.cs` (5 POCOs, 158 LOC) | ⚠ **Largely vestigial** — forms read `DataTable`/`DataRow` directly; models appear barely used |
| `HtmlSyncProvider` (33 KB) | ⚠ Legacy transport superseded by HTTP/file paths |
| `FrmCaseReport` + RDLC + `SqlServerTypes` | ⚠ Legacy reporting path, minimally used |
| Unused NuGet packages | ✓ **None** |
| `LicenseManager` (14 KB) | ⚠ Complete but unreachable in effect |
| `PermissionService` enforcement path | 🔴 Unreachable — see SEC-001 |

## 11.8 Maintainability — **60 / 100**

**Positive — genuinely unusual strengths:**
- ✓ **Exceptional inline documentation.** Comments explain *why*, not *what* — including the specific bug that motivated each fix, the measured impact, and the alternatives rejected. Examples: the `last_insert_rowid()` cross-connection bug (with the "39 of 43 rows had EntityID=0" measurement), the `COALESCE` NULL-photo fix, the modal-dialog test hang. **This is better than most commercial codebases.**
- ✓ `CLAUDE.md` codifies production-safety rules
- ✓ `ACCOUNTING_ARCHITECTURE.md` and `DEVELOPER_CONTROL_CENTER.md` (32 KB) provide real design docs
- ✓ Idempotent migrations mean any old DB self-upgrades

**Negative:**
- 🔴 File sizes: `FrmSyncWizard.cs` 105 KB, `FrmAccounting.cs` 144 KB, `DevCenterService.cs` 107 KB, `AccountingRepo.cs` 81 KB, `SyncFileService.cs` 77 KB
- 🔴 `Helpers/` has no internal structure
- ⚠ Comments are **Persian-only** — a barrier for any future non-Persian-speaking maintainer
- ✗ No CI/CD pipeline detected
- ✗ No static analysis / linting configuration
- ⚠ 17 compiler warnings tolerated at baseline

## 11.9 Testability — **55 / 100**

- ✓ 346 tests exist and pass — substantial investment
- ✓ Tests run against a **real temporary SQLite database**, not mocks — high-fidelity integration testing
- ✓ `TestEnvironment` with `[AssemblyInitialize]` solving the modal-dialog hang is a thoughtful fix (documented as having previously aborted ~220 of 334 tests)
- 🔴 Core form logic requires STA threads and `frm.Show()` to test — a direct consequence of §11.2
- 🔴 Static global state requires careful setup/teardown (`SecurityContext.SignIn` / `SignOut`, `AppDomain` DataDirectory juggling)
- ⚠ 6.2-minute suite runtime discourages frequent execution
- ⚠ ClosedXML binding conflict makes `ExportFullReport` untestable in-harness

## 11.10 Code Quality Scorecard

| Category | Score |
|---|---|
| Architecture quality | 55 |
| Design patterns (desktop-appropriate) | 40 |
| Repository pattern | 35 |
| UI best practices | 65 |
| Naming conventions | 80 |
| Code duplication | 60 |
| Dead code | 65 |
| **Documentation** | **90** |
| Maintainability | 60 |
| Testability | 55 |
| Test coverage | 35 |
| **OVERALL CODE QUALITY** | **🟡 60 / 100** |

> **Reader's note:** A 60/100 here should not be read as "poor." This codebase is **well above average for NGO/internal-line-of-business software**. The documentation quality (90) is exceptional. The score is held down by structural debt in the oldest, largest files — debt that is clearly visible, well-understood by the team (as the comments demonstrate), and addressable incrementally.

---

# 12. Platform Compatibility Audit

> **Note:** Section re-mapped. The requested audit covered Samsung/Xiaomi/Redmi/Poco/Oppo/Vivo/Huawei/Pixel and Android 8–15. **None apply** — this is a Windows desktop application. Audited against Windows versions and hardware/display configurations instead.

## 12.1 Operating System Support

Declared in `app.manifest`:

| Windows Version | Declared | .NET 4.7.2 Available | Verdict |
|---|---|---|---|
| **Windows 11** (all builds) | ✓ | ✓ In-box | ✅ **Fully supported** |
| **Windows 10** 1803+ | ✓ | ✓ In-box | ✅ **Fully supported** |
| **Windows 10** RTM–1709 | ✓ | ⚠ Requires redistributable | ⚠ Supported with installer prerequisite |
| **Windows 8.1** | ✓ | ⚠ Requires redistributable | ⚠ Supported; ⚠ **OS is EOL (Jan 2023)** |
| **Windows 8** | ✓ | ⚠ Requires redistributable | 🔴 OS EOL; not recommended |
| **Windows 7** | ✓ | ⚠ Requires redistributable + SHA-2 update | 🔴 **OS EOL (Jan 2020)** — declared but should be dropped |
| **Windows Server 2016/2019/2022** | (inherits) | ✓ | ✅ Expected to work — ❓ Not Verified |
| **macOS / Linux** | ✗ | ✗ | ✗ **Impossible** — WinForms + .NET Framework |

> ⚠ **Finding COMPAT-001:** The manifest declares Windows 7 and Windows 8 support. Both are end-of-life and receive no security updates. Given the sensitivity of the data (§10.4), running this system on an unpatched OS compounds the encryption gap materially. **Recommend removing the Win7/Win8 `supportedOS` entries and setting a Windows 10 1809 minimum**, documented in the installer.

## 12.2 DPI & Display Compatibility — ✓ **Excellent**

This is a standout area. Configuration in `app.manifest` + `App.config`:

```xml
<dpiAwareness>PerMonitorV2, PerMonitor</dpiAwareness>   <!-- Win10 1703+ -->
<dpiAware>true/pm</dpiAware>                            <!-- Older fallback -->
<gdiScaling>true</gdiScaling>                           <!-- Smooth GDI scaling -->
```
Plus `EnableWindowsFormsHighDpiAutoResizing = true`.

| Scenario | Support |
|---|---|
| 100% scaling (96 DPI) | ✅ |
| 125% / 150% / 175% / 200% scaling | ✅ **Correctly handled** |
| Multi-monitor, mixed DPI | ✅ PerMonitorV2 |
| Dragging window between different-DPI monitors | ✅ Re-layouts correctly |
| 4K displays | ✅ |
| Legacy OS without DPI awareness API | ✅ Graceful fallback chain |

The inline comments document that this configuration was added specifically to fix a real bug where case-form buttons were pushed off-screen at 125%+ scaling. ✓ Combined with `ResponsiveLayout.cs` (16 KB), display handling is **production-grade**.

## 12.3 CPU Architecture

| Config | PlatformTarget | Native interop deployed |
|---|---|---|
| Debug\|AnyCPU, Release\|AnyCPU | AnyCPU | ✓ Both `x86\` and `x64\SQLite.Interop.dll` present |
| Debug\|x64, Release\|x64 | x64 | ✓ |

- ✓ **Correct.** `System.Data.SQLite` requires the matching native `SQLite.Interop.dll`; both architectures are deployed, so AnyCPU resolves at runtime.
- ⚠ **Finding COMPAT-002 (Low):** the `x64` configurations also set `<Prefer32Bit>true</Prefer32Bit>`. This property only has meaning for `AnyCPU` and is **contradictory/ignored** under an explicit `x64` target. Harmless but indicates copy-paste config drift.
- ✗ **ARM64 Windows** (Surface Pro X, Snapdragon laptops): **not supported** — no ARM64 SQLite interop deployed. Would run under x64 emulation on Windows 11 ARM. ❓ Not Verified.

## 12.4 Runtime Prerequisites

| Prerequisite | Required | Installer handles it? |
|---|---|---|
| .NET Framework 4.7.2 | ✓ Always | ❓ **Not Verified** — Inno Setup scripts not audited |
| Visual C++ Redistributable | ✓ For SQLite interop | ❓ **Not Verified** |
| **WebView2 Evergreen Runtime** | ⚠ If WebView2 features used | ❓ **Not Verified** — 🔴 **highest deployment risk** |
| ReportViewer runtime | ✓ Bundled as assemblies | ✓ |
| Windows fonts (Persian/Arabic) | ✓ Segoe UI + Arabic script | ✓ In-box on Win10+ |

> 🔴 **Finding COMPAT-003 (High):** WebView2 is **not** installed by default on Windows 10. If any user-visible feature depends on it, that feature will throw on a clean Windows 10 machine. The installer must detect and bootstrap the Evergreen Runtime. **This must be verified before wider deployment.**

## 12.5 Localization/Regional Compatibility

| Scenario | Status |
|---|---|
| Windows set to Persian locale | ✅ Native |
| Windows set to English locale | ✅ App forces `CurrentCulture` at startup |
| Windows set to Arabic/Pashto locale | ✅ |
| Non-RTL Windows | ✅ App applies RTL itself |
| Persian font availability | ✅ Segoe UI covers Arabic script on Win10+ |
| ⚠ Regional decimal separator | ⚠ App forces Persian culture — `decimal.TryParse` on user input uses that culture. ❓ Behaviour with a comma decimal separator **Not Verified** |
| ⚠ `DateTime.Parse` in `AutoBackupService` | ⚠ Uses culture-dependent `DateTime.TryParse` on a stored `yyyy-MM-dd` string under a **Persian** culture — a latent parsing risk. Backup dates are written with `InvariantCulture` in `FrmLogin` but read without it here. **Marked ❓ Not Verified**; worth a targeted test |

## 12.6 Compatibility Estimate

| Target Environment | Compatibility |
|---|---|
| Windows 11 x64 | **98%** |
| Windows 10 1809+ x64 | **97%** |
| Windows 10 older / 8.1 | **80%** (prerequisites) |
| Windows 7/8 | **60%** (EOL, unsupported by us) |
| Windows on ARM64 | **50%** ❓ (emulation only) |
| Windows Server | **90%** ❓ |
| macOS / Linux | **0%** |

### **Overall compatibility for the realistic target estate (Win10 1809+ / Win11 x64): ≈ 97%**

---

# 13. Crash Risk Assessment

> Mapped from the requested Android crash classes to their .NET/WinForms equivalents.

## 13.1 Global Crash Protection — ✓ Present

`ErrorLogger.Install()` is called during startup (after table creation, since it logs to `EntErrorLog`). It registers global unhandled-exception handlers so the application shows a Persian message and logs, rather than terminating abruptly. **This substantially reduces hard-crash risk across the board.**

## 13.2 Risk Register

### 🔴 HIGH RISK

| ID | Class | Scenario | Evidence | Mitigation Status |
|---|---|---|---|---|
| **CR-01** | `FileNotFoundException` / `TypeInitializationException` (≈ Android `ClassNotFoundException`) | Missing `SQLite.Interop.dll` for the running architecture | Both x86/x64 deployed ✓ | ✓ Mitigated — **but installer must copy both folders**. ❓ Not Verified |
| **CR-02** | `FileNotFoundException` on `Microsoft.SqlServer.Types` | RDLC report rendering | ✓ **Known, fixed** — `SqlServerTypes.Utilities.LoadNativeAssemblies()` in `Program.Main`, wrapped in try/catch, with an explanatory comment | ✓ Mitigated |
| **CR-03** | `SQLiteException: database is locked` | Two users writing concurrently to a shared DB file | ⚠ `busy_timeout=8000` set **only** in `ExecuteInTransaction`; `GetConnection()` has none. Journal mode is **not WAL** | 🔴 **Partially mitigated** — highest realistic crash source in multi-user deployment |
| **CR-04** | Data loss (silent, not a crash) | Two users edit the same case; last write wins | `LockService` exists but is **not applied** to case/family editing | 🔴 **Not mitigated** |
| **CR-05** | `WebView2` runtime missing | Any WebView2-dependent screen on clean Win10 | ❓ Not Verified | ❓ **Unknown** |

### 🟠 MEDIUM RISK

| ID | Class | Scenario | Mitigation |
|---|---|---|---|
| **CR-06** | `NullReferenceException` | `DBNull` vs `null` handling on nullable columns | ⚠ Mixed — `GetDbString`, `GetVal`, `EntDb.ToText` helpers exist ✓, but raw `row["Col"].ToString()` also appears |
| **CR-07** | Migration failure | Crash during `MigrateServiceStatusRebuild` (rename + recreate + copy) leaves `TblCase_old` orphaned | ⚠ Non-WAL journal; a documented bug already occurred here (SQLite auto-rewrote FK refs during rename) |
| **CR-08** | `UnauthorizedAccessException` (≈ Android permission crash) | Writing to `Program Files`, a read-only network share, or a locked backup folder | ✓ Manifest requests `asInvoker` (no elevation) and data goes to user folders ✓. ⚠ Backup path is user-configurable → could point somewhere unwritable |
| **CR-09** | Cross-thread `InvalidOperationException` | `BackgroundSyncManager` timer touching UI controls | ❓ Not Verified — needs review of `BackgroundSyncManager` UI callbacks |
| **CR-10** | `OutOfMemoryException` | Loading many full-resolution photos; large Excel export | ⚠ `GridPager` exists ✓; image handling in batch card printing ❓ Not Verified |
| **CR-11** | Dangling file reference | `TblDocs.DocFilePath` points to a moved/deleted file | 🔴 No integrity check; behaviour on open ❓ Not Verified |

### 🟡 LOW RISK

| ID | Class | Scenario | Mitigation |
|---|---|---|---|
| **CR-12** | `FormatException` on date parse | Non-padded or mixed-calendar date strings | ⚠ `TryParse` used in most paths ✓ |
| **CR-13** | `DataView.RowFilter` `SyntaxErrorException` | Special characters in search box | ✓ Escaped in `FrmDocs`/`FrmArchive`; ⚠ `FrmDevCenter` unescaped |
| **CR-14** | Navigation/modal issues (≈ Android navigation crash) | Modal dialog stack, session timeout during an open modal | ✓ Session timeout **deliberately closes the whole app** rather than unwinding modals — a documented, safe design choice |
| **CR-15** | Theme/font application failure | Invalid hex color or missing font in settings | ✓ Guarded with try/catch + fallback |
| **CR-16** | `Control.Visible` false-negative in tests | Testing visibility before `Show()` | ✓ Documented; requires `frm.Show()` with `Minimized`+`ShowInTaskbar=false` |

## 13.3 Crash Risk Summary

| Severity | Count | Dominant Theme |
|---|---|---|
| 🔴 High | **5** | **Concurrency** (CR-03, CR-04) + deployment prerequisites (CR-01, CR-05) |
| 🟠 Medium | **6** | Data-shape handling and migration robustness |
| 🟡 Low | **5** | Input edge cases — mostly mitigated |

### 🔴 Top Recommendation — CR-03 / CR-04

**Enable WAL journal mode and apply `busy_timeout` to all connections.**

```csharp
// In DatabaseHelper.GetConnection() — after opening:
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=8000;
```

**Impact:** WAL allows concurrent readers alongside a writer, dramatically reducing "database is locked" errors in the shared-file multi-user scenario this system is deployed into. This is a **small, high-value, low-risk change** — but it must be tested against the backup/restore and migration paths, since WAL introduces `-wal` and `-shm` sidecar files that any file-copy backup **must** account for.

> ⚠ **Important caveat:** `BackupHelper` copies the database via `DataSet` serialization (not file copy), so WAL sidecars are not an issue there. But any external/manual file-copy backup procedure would need updating. **Document this before enabling WAL.**

---

# 14. Testing Coverage Audit

## 14.1 Test Suite Overview

| Metric | Value |
|---|---|
| Test project | `CaseManagement.Tests` (SDK-style, net472) |
| Framework | MSTest v2 |
| Test files | **40** |
| **Total test methods** | **346** |
| **Passed** | **344** |
| **Skipped** | **2** (pre-existing — ClosedXML binding conflict) |
| **Failed** | **0** ✅ |
| Execution time | **6.2 minutes** (with `/Parallel`) |
| Test style | **Integration** — real temporary SQLite DB, not mocks |

## 14.2 Coverage by Area

| Area | Tests | Coverage | Assessment |
|---|---|---|---|
| **Sync (all)** | **125** | ✓ **Strong** | `SyncFileTests` 26, `SyncEngineFoundation` 23, `OfflineSyncFoundation` 19, `FileDownloadSync` 18, `HttpSyncTransport` 15, `SyncConflictResolver` 14, `HttpFileSyncTransport` 10 |
| **Dev Center safety** | **33** | ✓ **Strong** | Highest single-file count — appropriate for destructive tooling |
| **Accounting** | **≈ 51** | ✓ Good | `Repair` 15, `Integrity` 12, `ReversalAndValidation` 11, `Balance` 9, `TransactionRevision` 8, `Money` 7 |
| **Reporting / Export** | **≈ 40** | ✓ Good | `CaseReportTemplate` 12, `WordExportFormatting` 7, `ExportRegression` 7, `Template1PageBreak` 6, template discovery/choice 8 |
| **Case domain / status** | **≈ 28** | ✓ Good | `ServiceStatusFilter` 14, `CaseDomainMigration` 6, `CaseGridColumns` 8 |
| **Record history** | **7** | ✓ **New** (this engagement) | `RecordHistoryWiringTests` |
| **Package/receipt** | **14** | ⚠ Moderate | `PackageEndToEnd` 9, `AssistanceReceiptModule` 4, batch print 1 |
| **Forms/UI** | **≈ 20** | ⚠ Light | Mostly construction smoke tests |
| **Duplicates/concurrency** | **6** | ⚠ Light | |
| **Applicant / member photo** | **10** | ⚠ Light | |
| **🔴 Security** | **0** | 🔴 **NONE** | No tests for `PasswordHelper`, `SecurityContext`, `CenterGuard`, `PermissionService`, lockout, or session timeout |
| **🔴 Backup/Restore** | **0 direct** | 🔴 **NONE** | 🔴 **The most dangerous gap** |
| **🔴 Localization** | **0** | 🔴 **NONE** | Would have caught the `ریال`/`استان` contamination |
| **🔴 Guardian Card** | **0** | 🔴 **NONE** | Entire 3,227-LOC module untested |

## 14.3 Critical Testing Gaps

### 🔴 GAP-01 — No Backup/Restore Tests
`BackupHelper.cs` is 42 KB implementing two distinct restore strategies with ID remapping, GlobalID deduplication, and child-table reassignment. **It has zero direct test coverage.**

This is the single highest-risk gap in the suite: a restore defect **destroys production data irrecoverably**, and restore is exercised precisely when the organization is already in a disaster scenario. The recent fixes to this file (missing tables, dropped columns, `famIdMap` remapping) were verified only by compilation and full-suite regression — **not by a test that actually round-trips a backup**.

**Recommended minimum:** seed DB → export → wipe → import (both modes) → assert full fidelity including history tables and all columns.

### 🔴 GAP-02 — No Security Tests
Zero tests for password hashing/verification, iteration migration, account lockout, session timeout, center isolation, or permission evaluation. `CenterGuard` — a **security boundary** — is entirely untested.

### 🔴 GAP-03 — No Localization Guard
A 10-line test asserting zero occurrences of `استان`, `شهرستان`, `ریال`, `تومان` in the source tree would have prevented every finding in §9.4 and would prevent all future regressions permanently.

### ⚠ GAP-04 — Core Form Logic Under-tested
`FrmCase` (3,789 LOC) has approximately 3 tests, all construction/grid smoke tests. The save/update/delete paths — including the newly wired audit, version, and outbox calls — are **not** covered end-to-end. Root cause: business logic lives in event handlers (§11.2).

## 14.4 Coverage Estimate

> No coverage instrumentation is configured. This is a **reasoned estimate** based on test-to-code ratios per module.

| Module | LOC | Est. Coverage |
|---|---|---|
| Sync | 15,179 | **~55%** |
| Accounting | 6,646 | **~45%** |
| Dev Center | 4,773 | **~40%** |
| Helpers (export/report subset) | ~5,000 | **~35%** |
| Helpers (backup/security subset) | ~7,000 | **~5%** 🔴 |
| Enterprise | 8,210 | **~20%** |
| Core forms (root) | 20,660 | **~10%** 🔴 |
| Guardian Card | 3,227 | **0%** 🔴 |
| Assistance Receipt | 1,890 | **~25%** |

### **Estimated overall line coverage: ~35%**

**Quality note:** The tests that *do* exist are **high quality** — real database integration, meaningful assertions, and explanatory comments. The suite has demonstrable value: it caught a migration-ordering bug (`ServiceStatusFilterTests`) and a modal-dialog hang that was silently aborting ~220 of 334 tests. This is a team that uses its tests rather than writing them for ceremony.

## 14.5 Testing Recommendations

| # | Action | Effort | Priority |
|---|---|---|---|
| 1 | **Backup/restore round-trip tests** (both modes, all tables, all columns) | 2 days | 🔴 **Critical** |
| 2 | **Localization guard test** (forbidden-term scan) | 1 hour | 🔴 **Critical (best ROI)** |
| 3 | **Security tests** — hashing, iteration migration, lockout, `CenterGuard` isolation | 2 days | 🔴 Critical |
| 4 | Enable coverage instrumentation (Coverlet / VS Code Coverage) | 4 hours | 🟠 High |
| 5 | Extract case save/delete logic into a testable service, then test it | 5 days | 🟠 High |
| 6 | Add CI pipeline running build + tests on every commit | 1 day | 🟠 High |
| 7 | Resolve ClosedXML binding conflict to unblock `ExportFullReport` testing | 4 hours | 🟡 Medium |
| 8 | Guardian Card render tests | 2 days | 🟡 Medium |

---

# 15. Performance Audit

> ⚠ **No profiling was performed.** All findings are **static analysis inferences** and are marked accordingly. Actual measurement is required before optimization.

## 15.1 Startup Performance

`Program.Main` executes synchronously before the login form appears:

| Step | Cost | Notes |
|---|---|---|
| Persian culture setup | Negligible | |
| `SqlServerTypes.LoadNativeAssemblies` | ~10–50 ms | Native DLL load |
| `RtlCaptions.Install()` + `Lang.Initialize()` + `LanguageSweep.Install()` | ~10–30 ms | `LangData` builds a 522-entry dictionary |
| **`DatabaseInitializer.EnsureDatabaseObjects()`** | ⚠ **Significant** | 120 KB of DDL: ~25 `CREATE TABLE IF NOT EXISTS`, ~40 `EnsureColumn` (each a `PRAGMA table_info`), ~50 `CREATE INDEX IF NOT EXISTS`, plus data migrations |
| `AccountingInitializer` | ~50 ms | 11 tables |
| `EnterpriseInitializer` | ⚠ ~100 ms | 22 tables + seed data |
| `OfflineSyncInitializer` | ~50 ms | 6 tables + triggers |
| `AutoBackupService.RunDailyBackupIfDue()` | 🔴 **Potentially seconds** | On backup day, performs a **full DataSet export + file copy synchronously on the UI thread before login** |

### 🔴 Finding PERF-01 — Full schema verification on every launch

All four initializers run **every single time** the application starts, re-verifying 64 tables, ~73 indexes, and ~40 columns. On a fresh install this is necessary; on the 500th launch it is pure overhead.

**Estimated cost:** 200–800 ms typical; **worse on network-mounted or slow disks**. ❓ Not measured.

**Recommendation:** Introduce a `schema_version` row. If the stored version matches the application's expected version, skip the entire DDL pass. This **also** delivers the migration-safety guard recommended in §7.5 — one change, two benefits.

### 🔴 Finding PERF-02 — Synchronous backup blocks startup

On the day a scheduled backup is due, `RunDailyBackupIfDue()` performs a complete database export and file-tree copy **on the UI thread, before the login window appears**. With a large database and many attached documents, the user sees **no window at all** for the duration — indistinguishable from a hung application.

**Recommendation:** Move to a background task after login, with a status indicator. Alternatively show a splash/progress window.

## 15.2 Database Performance

| Aspect | Assessment |
|---|---|
| Indexes | ✓ **73 indexes** — good coverage on `TblCase` (6), FKs, and history tables |
| Missing indexes | ⚠ `TblFamily.MemberName` / `MemberTazkiraNo` (duplicate detection, member search); `TblDocs.DocType` / `DocCategory` (search filter) |
| **Journal mode** | 🔴 **Not WAL** — writers block readers |
| **Connection pooling** | 🔴 **Disabled** — every operation opens a new connection; documented in `DatabaseHelper` comments as the cause of the `last_insert_rowid()` bug |
| Query patterns | ⚠ `SELECT *` used in backup and export paths; acceptable there |
| N+1 risk | ⚠ `MergeChildTable` and `MergeFamilyHistory` execute one `INSERT` (plus one `last_insert_rowid()`) **per row** inside a transaction — correct, but O(n) round-trips on large restores |
| Aggregates | ⚠ Dashboard issues multiple `GROUP BY` aggregates at load; ✓ indexed on `(NewStatus, ChangedAt)` |

### ⚠ Finding PERF-03 — Connection pooling disabled
Every `GetConnection()` creates a fresh `SQLiteConnection`. For a desktop app this is *usually* acceptable, but the code comments confirm it caused a real correctness bug. Enabling pooling would improve throughput — **but must not be done without re-verifying the `last_insert_rowid()` fix**, which now correctly uses a single connection within a transaction.

## 15.3 UI Performance

| Aspect | Assessment |
|---|---|
| Grid paging | ✓ `GridPager` (13 KB) exists — ✓ good |
| Large dataset handling | ⚠ Some grids bind full `DataTable`s; `DataView.RowFilter` filters **client-side in memory** |
| ❓ Virtual mode | ❓ Not Verified whether `DataGridView.VirtualMode` is used anywhere |
| Image handling | ⚠ `ImageOrientationHelper` (11 KB) processes photos; ❓ thumbnail caching Not Verified |
| Theming | ⚠ `UiTheme` (61 KB) applied per-form on open; ❓ cost Not Verified |
| Language sweep | ⚠ `LanguageSweep` walks the **entire control tree of every window on open** — O(controls). For `FrmCase` with hundreds of controls this runs on every open |
| Startup of large forms | ⚠ `FrmCase` (3,789 LOC) + designer construction; ❓ Not measured |

### ⚠ Finding PERF-04 — Client-side filtering
`FrmDocs`, `FrmArchive`, `FrmDuplicates`, and `FrmValidationReport` load the full result set and filter in memory via `RowFilter`. This is fine for hundreds of rows and **degrades linearly** with dataset growth. For an organization with tens of thousands of cases, document search will become noticeably slow.

## 15.4 Export & Backup Performance

| Operation | Assessment |
|---|---|
| Word export (single case) | ✓ Acceptable — 3 queries + template copy + placeholder replace |
| Word export (batch) | ⚠ Loops per case; ❓ progress reporting Not Verified |
| Excel export | ⚠ ClosedXML is memory-hungry on large sheets; ❓ Not measured |
| **Backup export** | 🔴 Loads **22 full tables into an in-memory `DataSet`** — memory scales with total database size |
| **Backup restore (merge)** | ⚠ Row-by-row inserts with per-row `last_insert_rowid()` — O(n) round-trips |
| Photo/file copy | ⚠ `CopyDirectory` recursive, synchronous |

### 🔴 Finding PERF-05 — Backup loads entire database into memory
`ExportBackup` calls `LoadTable` for 22 tables with `SELECT *`, materializing everything into a `DataSet`. For a large deployment (100k+ cases with history and version snapshots — and `EntRecordVersion` now grows on **every edit** following this engagement's changes), this could approach or exceed available memory in a 32-bit process.

> ⚠ **Important interaction:** wiring `VersionService` (this engagement) means `EntRecordVersion` now accumulates a full row snapshot **per edit, per record**. Over years this may become the **largest table in the database**. Since it is now also included in backups, **backup size and memory cost will grow accordingly**.
>
> **Recommendation:** implement a version-retention policy (e.g. keep all versions for 12 months, then thin to one per month) **before** this becomes a production problem. This was not in scope of the remediation work and should be tracked.

## 15.5 Sync Performance

| Aspect | Assessment |
|---|---|
| Batch upload | ✓ `UploadBatchSize` configurable |
| Cursor-based pull | ✓ Efficient incremental design |
| Background execution | ✓ Timer-driven, cancellable, graceful stop |
| Media sync | ⚠ `MediaScanner` (43 KB) scans the file tree; ❓ incremental vs full scan Not Verified |
| Conflict analysis | ⚠ Loads version snapshots per conflict |

## 15.6 Performance Recommendations (Prioritized)

| # | Recommendation | Expected Gain | Effort | Risk |
|---|---|---|---|---|
| 1 | **`schema_version` guard to skip DDL** on unchanged schema | **200–800 ms** every launch | 1 day | 🟢 Low |
| 2 | **Move auto-backup off the startup path** | Removes multi-second startup stall on backup days | 4 hours | 🟢 Low |
| 3 | **Enable WAL journal mode** | Major concurrency improvement | 1 day | 🟡 Med — verify backup interaction |
| 4 | **Version retention policy** for `EntRecordVersion` | Prevents unbounded growth | 2 days | 🟢 Low |
| 5 | Add missing indexes (`TblFamily.MemberName`, `MemberTazkiraNo`, `TblDocs.DocType`) | Faster search/duplicate detection | 2 hours | 🟢 Low |
| 6 | Server-side (SQL) filtering instead of `RowFilter` in search screens | Scales with data growth | 3 days | 🟡 Med |
| 7 | Stream backup export table-by-table instead of one `DataSet` | Bounded memory | 2 days | 🟡 Med |
| 8 | **Profile before further optimization** | — | 1 day | 🟢 Low |

> 💡 **Start with #8.** Every finding in this section is a static inference. One profiling session would convert this list from hypotheses into a measured, correctly-ordered backlog.

---

# 16. Production Readiness Matrix

| Subsystem | Completion | Production Ready? | Risk | Blocking Issue |
|---|---|---|---|---|
| **Case registration & CRUD** | 92% | ✅ Yes | 🟡 Med | Non-transactional saves; no record locking |
| **Family member management** | 94% | ✅ Yes | 🟢 Low | — |
| **Document attachment** | 88% | ⚠ Conditional | 🟡 Med | No file validation (SEC-005) |
| **Applicant intake** | 82% | ✅ Yes | 🟢 Low | — |
| **Service-status lifecycle** | 95% | ✅ Yes | 🟢 Low | — |
| **Archive / restore (case)** | 88% | ✅ Yes | 🟢 Low | — |
| **Change history / versioning** | 90% | ✅ Yes | 🟢 Low | Retention policy needed (PERF) |
| **Audit trail** | 85% | ⚠ Conditional | 🟡 Med | Can be disabled by admin (SEC-011) |
| **Duplicate detection** | 87% | ✅ Yes | 🟢 Low | — |
| **Data quality checks** | 80% | ✅ Yes | 🟢 Low | — |
| **Excel bulk import** | 85% | ✅ Yes | 🟡 Med | — |
| **Advanced search** | 85% | ✅ Yes | 🟡 Med | Client-side filtering scales poorly |
| **Per-case assistance** | 75% | ⚠ Conditional | 🔴 High | No approval; no ledger reconciliation |
| **Accounting ledger** | 85% | ⚠ Conditional | 🔴 High | Excluded from main backup **and** sync |
| **Currency conversion** | 60% | ⚠ Conditional | 🟡 Med | Manual rate entry only |
| **Reporting (dynamic builder)** | 86% | ✅ Yes | 🟢 Low | — |
| **Word / PDF export** | 90% | ✅ Yes | 🟢 Low | — |
| **Excel export** | 82% | ✅ Yes | 🟡 Med | Untestable in harness |
| **RDLC legacy report** | 60% | ⚠ Deprecate | 🟡 Med | Candidate for removal |
| **Scheduled reports** | 0% | ✗ Missing | 🟢 Low | Dead table only |
| **Guardian ID cards** | 90% | ✅ Yes | 🟡 Med | Zero test coverage |
| **Assistance receipts** | 87% | ✅ Yes | 🟡 Med | Wrong currency printed (§9.4.2) |
| **Multi-center isolation** | 70% | ⚠ Conditional | 🟠 High | Convention-based, not schema-enforced |
| **Authentication** | 88% | ✅ Yes | 🟡 Med | Password expiry non-functional |
| **Authorization (role-based)** | 55% | ⚠ Conditional | 🟠 High | 4 hard-coded free-text roles |
| **Authorization (permissions)** | 45% | 🔴 **NO** | 🔴 **Critical** | **SEC-001 — enforces nothing** |
| **Module enable/disable** | 90% | ✅ Yes | 🟢 Low | — |
| **Workflow engine** | 72% | ⚠ Pilot | 🟠 High | Gated by unenforced permissions |
| **Approval chains** | 74% | ⚠ Pilot | 🟠 High | Not applied to case/assistance |
| **Business rules engine** | 70% | ⚠ Pilot | 🟡 Med | Real-world coverage unverified |
| **Record locking** | 70% | 🔴 **NO** | 🟠 High | Built but **not applied** to editing |
| **Licensing** | 60% | 🔴 **NO** | 🟡 Med | Built but **never enforced** |
| **Backup (automatic)** | 85% | ⚠ Conditional | 🔴 **Critical** | **Unencrypted (SEC-004)**; blocks startup |
| **Backup (manual)** | 88% | ⚠ Conditional | 🔴 **Critical** | **Unencrypted**; zero test coverage |
| **Restore** | 85% | ⚠ Conditional | 🔴 **Critical** | **Zero test coverage** (GAP-01) |
| **Data encryption at rest** | 0% | 🔴 **NO** | 🔴 **Critical** | **SEC-003 — none exists** |
| **Sync — outbox & queue** | 88% | ⚠ Pilot | 🟠 High | — |
| **Sync — HTTP transport** | 75% | ⚠ Pilot | 🔴 High | Network-blocked in user's environment |
| **Sync — file transport** | 85% | ⚠ Pilot | 🟠 High | Package encryption unverified |
| **Sync — HTML (legacy)** | 60% | ⚠ Deprecate | 🟡 Med | Superseded |
| **Sync — conflict resolution** | 82% | ⚠ Pilot | 🟠 High | — |
| **Sync — media/files** | 80% | ⚠ Pilot | 🟠 High | — |
| **Sync — history tables** | 0% | ✗ Missing | 🔴 **Critical** | **Branches diverge permanently** |
| **Sync — accounting** | 0% | ✗ Missing | 🔴 **Critical** | Not synchronized at all |
| **Error logging** | 90% | ✅ Yes | 🟢 Low | — |
| **Dev Control Center** | 95% | ✅ Yes | 🟡 Med | Powerful; role-string gated |
| **RTL support** | 95% | ✅ Yes | 🟢 Low | — |
| **Jalali calendar** | 85% | ✅ Yes | 🟡 Med | Mixed calendar storage undocumented |
| **Multi-language** | 36% | ⚠ Conditional | 🟡 Med | 927 strings untranslated |
| **Afghan terminology** | 55% | 🔴 **NO** | 🟠 High | **Iranian terms + wrong currency on printed output** |
| **DPI / display handling** | 95% | ✅ Yes | 🟢 Low | — |
| **Installer** | ❓ | ❓ Not Verified | ❓ | Prerequisites unconfirmed |
| **CI/CD** | 0% | ✗ Missing | 🟠 High | No automated build/test gate |

## 16.1 Readiness Rollup

| Verdict | Subsystems | % |
|---|---|---|
| ✅ **Production ready** | **24** | 44% |
| ⚠ **Conditional / pilot only** | **20** | 36% |
| 🔴 **Not ready** | **7** | 13% |
| ✗ **Missing** | **3** | 5% |
| ❓ **Not verified** | **1** | 2% |

### **Weighted Production Readiness: 61%**

---

# 17. Missing Features Analysis

## 17.1 🔴 Critical Missing Features

*Absence of these creates unacceptable risk to data, beneficiaries, or the organization's accountability.*

### CM-01 — Encryption at Rest
**What's missing:** Any encryption of the database, attached documents, photographs, or backups.
**Why it matters:** The complete dataset — including Tazkira numbers, home addresses, photographs of minors, and religious/ethnic markers — is readable by anyone who obtains the file. In the operating context this is a physical-safety risk to beneficiaries, not merely a privacy issue.
**Dependency:** Requires key-management policy, migration plan for existing installations, and a documented key-recovery procedure before implementation.

### CM-02 — Permission Enforcement
**What's missing:** Any call to `PermissionService.Require()` from CRUD operations.
**Why it matters:** Administrators configure permissions that have no effect. This is worse than having no permission UI at all, because it creates documented-but-false assurance. An auditor reviewing the system would reasonably conclude access controls exist.

### CM-03 — Backup Encryption & Verification
**What's missing:** Encrypted backup archives; integrity checksums; automated restore verification.
**Why it matters:** Backups are the highest-value single-file exfiltration target, written automatically to a predictable path. Additionally, an untested restore path (§14 GAP-01) means backup *existence* is not the same as backup *usability*.

### CM-04 — History & Accounting Synchronization
**What's missing:** `TblCaseStatusHistory`, `TblFamilyStatusHistory`, `TblFamilyRoleHistory`, `EntRecordVersion`, and all `Acc*` tables are excluded from sync.
**Why it matters:** Two branches running the same system will hold permanently divergent audit trails and completely independent financial ledgers. Head office cannot produce a consolidated, auditable financial position — which is precisely what donors require.

### CM-05 — Concurrent Edit Protection
**What's missing:** Application of the existing `LockService` (or optimistic concurrency via `RowVersion`) to case and family editing.
**Why it matters:** Two operators editing the same case silently overwrite one another. There is no warning, no conflict, no recovery. Data loss is invisible.

### CM-06 — Assistance ↔ Ledger Reconciliation
**What's missing:** Any structural link between `TblAssistance` (per-household aid) and the `Acc*` ledger. `AccStipend` has no `CasID`.
**Why it matters:** The organization cannot answer "how much has this household received?" from its accounting system, nor reconcile disbursements against the cash book. This is a core accountability failure for a grant-funded charity.

## 17.2 🟠 Important Missing Features

### IM-01 — Correct Afghan Terminology
51 uses of Iranian `استان`, 2 of `شهرستان`, and **7 of `ریال`** — including on **printed receipts and guardian ID cards**. Highest-embarrassment, lowest-effort fix in the entire report.

### IM-02 — Role Management
No `TblRole` table; roles are free-text strings compared with `string.Equals`. Only 4 hard-coded roles; no custom roles; a typo produces silent misassignment.

### IM-03 — Functional Password Policy
`ForcePasswordChangeDays` cannot work — `TblUsers` has no `PasswordChangedAt` column. Complexity enforcement unverified.

### IM-04 — Backup & Restore Test Coverage
42 KB of restore logic with ID remapping and two distinct strategies, exercised only in a real disaster. Zero direct tests.

### IM-05 — Assistance Approval Workflow
Aid can be recorded with no approval step, no budget ceiling, and no duplicate-payment detection. The `ApprovalService` engine exists but is not applied here.

### IM-06 — CI/CD Pipeline
No automated build/test gate. The 346-test suite runs only when a developer chooses to run it (and takes 6.2 minutes, discouraging frequency).

### IM-07 — Schema Version Guard
No `schema_version` table. An older executable opening a newer database produces undefined behaviour instead of a clean refusal. Also blocks the startup optimization in §15.6.

### IM-08 — Multi-Center Schema Enforcement
`CenterID` is nullable with no FK; `TblFamily`/`TblDocs` lack it entirely. Isolation depends on every developer remembering to join correctly.

### IM-09 — File Upload Validation
No extension whitelist, size cap, content-type check, or integrity hash on attached documents.

### IM-10 — Translation Completion
927 identified on-screen strings untranslated across all 4 target languages.

## 17.3 🟡 Future Enhancements

| # | Enhancement | Value |
|---|---|---|
| FE-01 | **Scheduled report delivery** — `TblScheduledReport` exists as a dead table; implement or drop | Donor reporting automation |
| FE-02 | **Double-entry bookkeeping** — current ledger is single-entry cash book | Audit-grade financials |
| FE-03 | **Multi-currency with rate history** — replace per-transaction manual rates | Accurate historical reporting |
| FE-04 | **Inter-center case transfer** — `TblCaseTransferHistory` is a dead table | Beneficiary relocation handling |
| FE-05 | **Dashboard customization** | User productivity |
| FE-06 | **Data retention & purge policy** | GDPR-equivalent compliance; controls `EntRecordVersion` growth |
| FE-07 | **Beneficiary self-service portal / SMS notification** | Reduce office visits |
| FE-08 | **Biometric or photo-based duplicate detection** | Fraud prevention |
| FE-09 | **Migrate to .NET 8 + modern UI** | Long-term platform viability |
| FE-10 | **Structured logging + telemetry** | Operational visibility |
| FE-11 | **Retire `HtmlSyncProvider` and RDLC path** | Reduce maintenance surface ~40 KB |
| FE-12 | **Split `Helpers/` into cohesive packages** | Maintainability |

---

# 18. Roadmap

> Estimates assume **1–2 developers** familiar with the codebase. They include implementation, testing, and documentation — not user acceptance testing or rollout.

## Phase 1 — Stop the Bleeding (4–6 weeks) 🔴

*Goal: eliminate false-assurance controls and the highest-embarrassment defects. Nothing here requires architectural change.*

| # | Task | Effort | Owner |
|---|---|---|---|
| 1.1 | Fix all `ریال` → `افغانی` (receipts, cards, `LangData`) | 1 day | Dev |
| 1.2 | Fix all `استان`→`ولایت`, `شهرستان`→`ولسوالی`; **audit `DatabaseInitializer` seed data and write a data migration if terms were persisted** | 3 days | Dev |
| 1.3 | Add **localization guard test** (forbidden-term scan) | 0.5 day | Dev |
| 1.4 | **Hide `FrmPermissionMatrix` from navigation** until enforcement lands (stopgap for SEC-001) | 0.5 day | Dev |
| 1.5 | Remove `.pfx` from version control; rotate certificate | 0.5 day | DevOps |
| 1.6 | **Backup/restore round-trip tests** (both modes, all tables/columns) | 5 days | QA |
| 1.7 | Security test suite (hashing, lockout, `CenterGuard`) | 5 days | QA |
| 1.8 | Add `PasswordChangedAt` column; make password expiry functional | 2 days | Dev |
| 1.9 | Move auto-backup off the startup path | 1 day | Dev |
| 1.10 | Resolve `TblAuditLogs` duplication; drop dead tables | 1 day | Dev |
| 1.11 | Set up CI pipeline (build + test on commit) | 2 days | DevOps |
| 1.12 | Remove Win7/Win8 from manifest; verify installer prerequisites (incl. **WebView2**) | 2 days | DevOps |

**Exit criteria:** No Iranian terminology in source or printed output. Permission UI no longer misleads. Backup/restore proven by test. CI green on every commit.

## Phase 2 — Close the Security Gap (8–12 weeks) 🔴

*Goal: make the system genuinely safe to hold this data.*

| # | Task | Effort | Notes |
|---|---|---|---|
| 2.1 | **Design encryption strategy** — SQLCipher vs DPAPI vs OS-level; **key custody & recovery policy** | 1 week | ⚠ **Requires product-owner sign-off** |
| 2.2 | Implement database encryption + migration tool for existing installations | 3 weeks | Highest-risk change in the roadmap |
| 2.3 | Encrypt backups + add integrity checksums | 1 week | |
| 2.4 | Encrypt attached documents & photographs at rest | 2 weeks | |
| 2.5 | **Wire `PermissionService.Require()` into all CRUD entry points** | 2 weeks | Keep `SecurityContext` as fallback |
| 2.6 | Add `TblRole` table + FK; migrate free-text roles | 1 week | |
| 2.7 | Apply `LockService` (or `RowVersion` optimistic concurrency) to case/family editing | 1.5 weeks | Fixes CM-05 |
| 2.8 | File upload validation (whitelist, size, hash) | 1 week | |
| 2.9 | Verify/implement sync package encryption | 1 week | |

**Exit criteria:** Data unreadable without a key. Permissions actually enforced. Concurrent edits detected. Security score target: **≥ 80/100**.

## Phase 3 — Integrity & Integration (10–14 weeks) 🟠

*Goal: make the data trustworthy across branches and reconcilable financially.*

| # | Task | Effort |
|---|---|---|
| 3.1 | Add history tables to sync (all 4 history tables + `EntRecordVersion`) | 3 weeks |
| 3.2 | Add accounting to sync **and to the main backup** | 3 weeks |
| 3.3 | Link assistance to ledger — add `CasID` to stipends; build reconciliation view | 3 weeks |
| 3.4 | Assistance approval workflow (apply existing `ApprovalService`) | 2 weeks |
| 3.5 | `schema_version` guard + startup DDL skip (also delivers PERF-01) | 1 week |
| 3.6 | Enable WAL + universal `busy_timeout`; verify backup interaction | 1 week |
| 3.7 | `EntRecordVersion` retention policy | 1 week |
| 3.8 | Multi-center schema hardening (`CenterID` NOT NULL + FK; add to child tables) | 2 weeks |

**Exit criteria:** Branches converge on identical history. Consolidated financial position producible. Concurrency errors materially reduced.

## Phase 4 — Sustainability (12–20 weeks) 🟡

*Goal: reduce long-term maintenance cost and platform risk.*

| # | Task | Effort |
|---|---|---|
| 4.1 | Extract case/family/docs logic into testable services + repositories | 6 weeks |
| 4.2 | Split `Helpers/` into cohesive packages | 2 weeks |
| 4.3 | Retire `HtmlSyncProvider` and the RDLC reporting path | 2 weeks |
| 4.4 | Complete translations (927 strings × 4 languages) | 3 weeks (translator) |
| 4.5 | Raise test coverage to ≥ 60% | 6 weeks |
| 4.6 | Performance profiling + targeted optimization | 2 weeks |
| 4.7 | Evaluate .NET 8 migration feasibility | 3 weeks (spike) |
| 4.8 | Structured logging + operational telemetry | 2 weeks |

**Exit criteria:** Code quality ≥ 80. Coverage ≥ 60%. Documented platform strategy.

## 18.1 Timeline Summary

| Phase | Duration | Cumulative | Focus |
|---|---|---|---|
| **Phase 1** | 4–6 weeks | ~1.5 months | Truthfulness & safety nets |
| **Phase 2** | 8–12 weeks | ~4.5 months | Confidentiality & access control |
| **Phase 3** | 10–14 weeks | ~8 months | Integrity & integration |
| **Phase 4** | 12–20 weeks | ~13 months | Sustainability |

> ⚠ **Deployment guidance:** The system may continue in **limited production** during Phase 1 **only** if compensating controls are applied immediately — full-disk encryption (BitLocker) on every machine, restricted physical access, and locked-down backup folder permissions. **Wider rollout should wait for Phase 2 completion.**

---

# 19. User Manual

> **Audience:** Data-entry operators, field surveyors, branch staff.
> ⚠ **Verification note:** Steps below are derived from code inspection, not from operating the running application. Screen labels are given in Persian as they appear in source. Marked ❓ where the exact interaction could not be confirmed.

## 19.1 Before You Start — Security Basics 🎓

*The system holds information that could put families at risk. Three rules matter more than anything else in this manual:*

1. **Never copy the database or a backup file to a USB drive or personal device.** The file is **not encrypted** — anyone who finds that drive can read every family's name, address, ID number, and children's photographs.
2. **Lock your screen whenever you step away** (`Windows + L`). The application will close itself after a period of inactivity, but that gap is enough for someone to read the screen.
3. **Never share your account.** Every action is recorded against your username. If a colleague uses your login, the audit trail blames you.

## 19.2 First Launch

1. Start `CaseManagement.exe`.
2. On first run the system builds its database automatically — this may take a few seconds. No action is required.
3. The **login window** (ورود به سیستم) appears.
4. Enter the username and password provided by your administrator.
5. You will likely be required to **change your password immediately** (new accounts are created with "must change password" set).
6. Select your **center** (مرکز) from the list.
   - Most users see only their own branch.
   - Head-office administrators may see an "All Centers" (همه مراکز) option.
7. The **Dashboard** (داشبورد) opens.

> ⚠ **If you are locked out:** after 5 failed attempts (default) the account locks for 15 minutes. Wait, or contact your administrator. There is **no** self-service password reset.

## 19.3 Understanding the Dashboard

The dashboard shows:
- **Statistic cards** — case counts by status
- **Charts** — 12-month trends of activations and discontinuations
- **Filters** — province (ولایت), district (ولسوالی), service status
- **Sidebar** — navigation to every module you have access to
- **Event log grid** — recent activity

> 💡 If a module is missing from your sidebar, your administrator has disabled it for your role — this is normal, not a fault.

## 19.4 Creating a New Case (پرونده)

1. Click **پرونده‌ها** (Cases) in the sidebar.
2. Click **جدید** (New).
3. Complete the guardian details:
   - **کد اختصاصی** (Unique code) — must be unique across the system
   - **شماره فرم** (Form number) — must be unique
   - **نام سرپرست** / **نام پدر سرپرست** (Guardian name / father's name)
   - **شماره تذکره** (Tazkira number)
   - **ولایت** / **ولسوالی** (Province / district)
   - **آدرس** (Address), **شماره تماس** (Phone)
   - **نوع درخواست** (Request type), **اولویت‌بندی اقتصادی** (Economic priority)
   - **وضعیت خدمات** (Service status)
4. Attach photos: guardian photo and family group photo.
5. Click **ذخیره** (Save).

> ⚠ **Duplicate codes are rejected.** If you see "کد اختصاصی تکراری است", another case already uses that code.

> ⚠ **Two people must not edit the same case at the same time.** The system does **not** currently warn you — the last person to save silently overwrites the other. Coordinate verbally before editing an existing case.

## 19.5 Editing a Case

1. Select the case from the grid.
2. Fields are **read-only** by default — this is intentional, to prevent accidental changes.
3. Click **ویرایش** (Edit) to unlock the fields.
4. Make changes, then click **ذخیره** (Save).

Every change is recorded. To see the history, click **تاریخچه** (History) — this shows every version of the record, who changed it, when, and which fields changed.

## 19.6 Adding Family Members

1. With a case open, switch to the **members** tab.
2. Click **جدید** (New).
3. Enter member details: name, father's name, Tazkira, gender, birth date, education, physical status, disability.
4. Set the member's **role** (نقش عضو) and **service status** — these are tracked independently of the case.
5. Click **ذخیره** (Save).

> 💡 Member role changes and service-status changes are each recorded in their own history. Click **تاریخچه** (History) on the member form to review them.

## 19.7 Attaching Documents

1. Switch to the **documents** tab.
2. Click **جدید** (New), then browse for the file.
3. Set document type, category, tags, and description.
4. Save. The file is **copied** into the system's managed storage — the original may be moved or deleted afterwards.

> ⚠ **Security:** only attach documents you have obtained legitimately. The system does **not** currently scan attachments for viruses, and it does not restrict file types. Never attach an executable (`.exe`, `.bat`, `.js`) file.

## 19.8 Registering Financial Assistance

1. Open **مالی** (Finance) from the sidebar.
2. Select the case.
3. Enter **تاریخ** (date), **نوع کمک** (assistance type), **مبلغ** (amount in **افغانی**), and description.
4. Click **ثبت کمک** (Register assistance).
5. Print a receipt from the receipt screens.

> ⚠ **Important limitation:** assistance recorded here does **not** post to the accounting ledger automatically. Your accountant must record the corresponding payment separately in the **حسابداری** (Accounting) module.

## 19.9 Generating Reports

**Case dossier (Word/PDF):**
1. Open the case.
2. Click **ورد** (Word) or **پی دی اف** (PDF).
3. Choose a template if prompted.
4. The document includes case details, family members, documents, financial assistance, and full change history.

**Custom report:**
1. Open **گزارش‌ساز پویا** (Dynamic report builder).
2. Choose a source: Cases, Family members, Documents, Assistance, **Case status history**, or **Family status history**.
3. Select columns, add filters, optionally group.
4. Run, then export to Excel.

> 💡 You can save a report layout as a template and reuse it.

## 19.10 Searching

- **Quick search:** the search box on each screen filters the visible list.
- **جستجوی پیشرفته** (Advanced search): searches across cases, members, and documents.
- **بارکد و جستجو** (Barcode): scan a guardian card barcode to jump directly to a case.

## 19.11 Archiving vs Deleting

| Action | Effect | Reversible? |
|---|---|---|
| **بایگانی** (Archive) | Hides the case from normal lists | ✅ Yes — restore from Archive |
| **حذف (فقط نرم‌افزار)** (Delete, database only) | Removes the record; files remain on disk | ❌ **No** |
| **حذف کامل** (Full delete) | Removes the record **and** its files | ❌ **No** |

> 🔴 **Prefer Archive.** Deletion also removes all family members, documents, and assistance records for that case, permanently. Only administrators can delete.

## 19.12 Backup (What You Should Know)

- The system takes an **automatic backup** on a schedule set by your administrator (daily by default).
- You do not need to do anything for this.
- ⚠ **Backups are not encrypted.** Never copy the backup folder to a personal drive or cloud folder.
- If you are asked to run a manual backup, it is in **تنظیمات** (Settings) → Backup tab — but this is normally an administrator task.

---

# 20. Administrator Manual

> **Audience:** Branch managers, head-office administrators, IT support.

## 20.1 🎓 Security Briefing — Read This First

**You are responsible for data that can endanger people.** This section explains the three things about this system that are most commonly misunderstood.

### ⚠ Misunderstanding 1: "The permission matrix protects my data."

**It does not.** The Permission Matrix screen (ماتریس مجوزها) lets you grant and revoke fine-grained permissions, and it saves them successfully. **However, those permissions are not currently checked when users create, edit, or delete records.**

Access is actually controlled by the **role** assigned in User Management:

| Role | Can view | Can edit | Can delete | Admin functions |
|---|---|---|---|---|
| `SuperAdmin` | ✓ All centers | ✓ | ✓ | ✓ |
| `Admin` | ✓ Own center | ✓ | ✓ | ✓ |
| `Operator` | ✓ Own center | ✓ | ✗ | ✗ |
| *(anything else)* | ✓ Own center | ✗ | ✗ | ✗ |

**Practical guidance:** control access **only** through the Role field in User Management. Treat the Permission Matrix as non-functional until your development team confirms otherwise.

> ⚠ The role name must be spelled **exactly** — `Admin`, not `admin ` or `Administrator`. A misspelled role silently results in view-only access.

### ⚠ Misunderstanding 2: "Our data is safe because there's a password."

The login password protects the *application*. It does **not** protect the *data file*.

**The database is stored unencrypted.** Anyone who can copy `CaseDB.sqlite` — or any backup — can open it with freely available tools and read everything: names, Tazkira numbers, addresses, photographs of children, disability status, and religious/ethnic markers.

**Compensating controls you must apply today:**
1. ✅ Enable **BitLocker** full-disk encryption on every machine running the system.
2. ✅ Restrict the backup folder with NTFS permissions to administrators only.
3. ✅ Never place the backup folder inside OneDrive, Google Drive, Dropbox, or any auto-syncing folder.
4. ✅ Physically secure machines; enforce screen-lock policy.
5. ✅ Wipe drives securely before disposal or repair.

### ⚠ Misunderstanding 3: "A backup means we're safe."

A backup you have never restored is a **hypothesis**, not a safety net. The restore logic in this system currently has **no automated test coverage**.

**You must test restores yourself, quarterly**, on a spare machine — never on production.

## 20.2 User Management

**Location:** Sidebar → **کاربران و دسترسی** (Users & Access)

**Creating a user:**
1. Click add user.
2. Enter username (must be unique).
3. Set a strong initial password.
4. Assign the **Role** — exactly `SuperAdmin`, `Admin`, or `Operator`.
5. Save. The user will be forced to change the password at first login.

**Deactivating vs deleting:** Prefer **deactivating** (`IsActive = 0`). Deleting a user removes the account but audit records retain the username string.

**Unlocking a locked account:** Accounts lock automatically after the configured number of failed attempts. Wait for the lockout period to elapse, or ❓ *(the exact admin unlock path was not verified — confirm with your development team)*.

> 🔴 **There is no password reset feature.** If a user forgets their password, an administrator must set a new one through User Management.

## 20.3 Security Settings

**Location:** Settings → Security tab

| Setting | Recommended | Notes |
|---|---|---|
| `MinPasswordLength` | **12** | ❓ Complexity enforcement unverified |
| `MaxFailedAttempts` | **5** | |
| `LockoutMinutes` | **15** | |
| `SessionTimeoutMinutes` | **10** | App closes on timeout |
| `ForcePasswordChangeDays` | — | 🔴 **Non-functional** — the database lacks the column needed to compute expiry. Do not rely on it |
| `AuditEnabled` | **Always 1** | 🔴 **Never set to 0.** This disables the audit trail, destroying your own accountability record |

## 20.4 Center (Branch) Management

**Location:** Settings → Centers tab

- Each center has a code, name, province, address, phone, manager, logo, and color.
- Users are assigned to a center at login.
- SuperAdmin may select "All Centers" to work across branches.

> ⚠ **Do not delete a center that has cases.** There is no foreign key protecting this — deleting a center **orphans every record in it**, potentially making those cases invisible to all non-SuperAdmin users.

## 20.5 Module Management

**Location:** Sidebar → **مدیریت ماژول‌ها** (Module Management)

Unlike the Permission Matrix, **module enable/disable genuinely works.** You can switch modules off globally, per role, or per user, and they will disappear from the sidebar.

> 💡 **This is currently your most effective access-control tool beyond roles.** Use it to hide Accounting, Dev Center, or Sync from operators who should not see them.

## 20.6 Backup Strategy

### Recommended configuration

| Setting | Recommendation |
|---|---|
| Schedule | **Daily** |
| Retention count | **30** (default is 14) |
| Backup path | A dedicated folder on a **separate physical drive**, never a cloud-synced folder |

### The 3-2-1 rule 🎓

- **3** copies of the data
- **2** different storage media
- **1** copy stored **off-site**

For the off-site copy: because backups are unencrypted, place them in an **encrypted container** (VeraCrypt, or a BitLocker-encrypted USB drive) before moving them off-site. Never email a backup.

### ⚠ Accounting is backed up separately

The **main backup does not include accounting data**. The Accounting module has its own backup function. **You must run both**, or you will lose your entire financial ledger in a disaster.

> 🔴 This is the single most likely cause of catastrophic, unrecoverable data loss in current operations. Put it in your written procedure.

### Restore modes

| Mode | Behaviour | Use when |
|---|---|---|
| **Merge** | Matches records by GlobalID, remaps IDs, inserts only new records | Combining data; partial recovery |
| **Classic** | Deletes existing data and restores with original IDs | Disaster recovery onto a clean installation |

> 🔴 **Classic restore is destructive.** Always take a fresh backup immediately before restoring.

## 20.7 Disaster Recovery Procedure

**Prepare in advance:**
1. Document where backups are stored and who holds access.
2. Keep the installer and the correct application version available offline.
3. Record the .NET Framework and any runtime prerequisites.
4. **Test this procedure quarterly on a spare machine.**

**In a disaster:**
1. Install Windows and the application on the replacement machine.
2. Launch once so the database structure is created.
3. Copy the most recent backup to the machine.
4. Settings → Backup → Restore → **Classic mode**.
5. Restore the **accounting backup separately**.
6. Verify: case count, family count, recent assistance records, and **run a report** to confirm data reads correctly.
7. Verify attached documents and photographs opened correctly.
8. Record the recovery in your incident log.

**Recovery objectives:**
- **RPO** (max data loss): up to **24 hours** with daily backups
- **RTO** (time to restore): ❓ Not measured — establish this during your quarterly test

## 20.8 Multi-Branch Synchronization

> ⚠ **Sync is pilot-quality. Do not rely on it as the sole path for critical data.**

**Known limitations you must plan around:**
1. 🔴 **Change history does not synchronize.** Each branch keeps its own audit trail; they will never match.
2. 🔴 **Accounting does not synchronize at all.** Financial data must be consolidated manually.
3. ⚠ The online HTTP path is currently blocked in your network (outbound port 7844 closed — Cloudflare error 1033). Use the **file-based** (USB/courier) path.
4. ⚠ ❓ Sync packages may not be encrypted — treat a sync USB drive with the same care as a backup.

**Monitoring:** The Sync screen and Dev Center show pending and failed item counts. Investigate any persistent failed count.

## 20.9 Maintenance Schedule

| Frequency | Task |
|---|---|
| **Daily** | Confirm automatic backup ran; check error log |
| **Weekly** | Review audit log for unusual activity; check sync pending/failed counts |
| **Monthly** | Run Data Quality report; run Duplicates report; verify backup folder size and free disk space |
| **Quarterly** | 🔴 **Test a full restore on a spare machine**; review user accounts and deactivate leavers; run Dev Center health report; VACUUM/REINDEX |
| **Annually** | Review roles and access; rotate administrator passwords; review retention of old cases |

## 20.10 Dev Control Center ⚠

A hidden diagnostics console exists, activated by a keyboard sequence and restricted to SuperAdmin.

> 🔴 **It contains destructive repair operations.** Use it only when instructed by your development team, and **always take a backup first**. Its "System Log" screen will appear empty — this is a known defect (`TblAuditLogs` is never written), not a sign of missing activity.

---

# 21. Final Verdict

## 21.1 Scorecard

| Dimension | Score | Grade |
|---|---|---|
| **Feature Completion** | **82 / 100** | B |
| **Production Readiness** | **61 / 100** | D+ |
| **Security** | **46 / 100** | 🔴 F |
| **Code Quality** | **60 / 100** | C- |
| **Architecture** | **58 / 100** | D+ |
| **Scalability** | **55 / 100** | D+ |
| **Maintainability** | **60 / 100** | C- |
| **Documentation** | **90 / 100** | 🟢 A |
| **Test Coverage** | **35 / 100** | 🔴 F |
| **Localization** | **70 / 100** | C |
| **Platform Compatibility** | **97 / 100** | 🟢 A |

### **Overall Weighted Score: 62 / 100**

## 21.2 Recommendation

# ⚠ REQUIRES ADDITIONAL DEVELOPMENT
### *(Suitable for continued limited production under compensating controls; not suitable for expanded rollout)*

**This is not a redesign case.** The architecture, while imperfect, is sound enough to build on. The system delivers real, substantial value today and is in active production use. A rewrite would discard an enormous amount of correct, well-documented domain logic and would be the wrong decision.

**Nor is it production-ready in the full sense.** Three findings block that classification:

1. 🔴 **No encryption at rest** — the dataset is a plaintext file describing vulnerable minors and their exact locations.
2. 🔴 **Permissions that enforce nothing** — a control that exists in the UI, is configurable, appears to work, and does nothing.
3. 🔴 **An untested restore path** — the disaster-recovery mechanism has never been verified to work.

## 21.3 What This System Does Well

It would be a disservice to end on the negatives. This codebase demonstrates:

- ✅ **Exceptional inline documentation.** Comments explain *why* a decision was made, what bug motivated it, what the measured impact was, and what alternatives were rejected. Better than most commercial software.
- ✅ **Genuinely sophisticated password handling** — per-user PBKDF2 iteration migration is a technique many senior teams never implement.
- ✅ **Robust, idempotent migrations** — any old database self-upgrades cleanly with no version scripts.
- ✅ **A real, valuable 346-test suite** that has demonstrably caught real bugs.
- ✅ **Best-in-class RTL and DPI handling** for a Persian-language desktop application.
- ✅ **Correct accounting principles** — reversal instead of deletion, with a documented, well-reasoned mitigation for floating-point currency.
- ✅ **Deliberate, documented restraint** — auto-merge of duplicates and version rollback were *consciously not built* because they could destroy correct data. That judgement is a sign of engineering maturity.

## 21.4 The Central Pattern

One structural theme explains most of this report's critical findings:

> **Sophisticated subsystems are built to completion, then never connected.**

Three confirmed instances:

| Subsystem | Built | Wired |
|---|---|---|
| `VersionService` — full record versioning | ✓ Complete | ✗ → ✅ **Fixed** in the prior engagement |
| `PermissionService` — full RBAC | ✓ Complete | 🔴 **Still not wired** |
| `LicenseManager` — HMAC licensing | ✓ Complete | 🔴 **Still not wired** |

Plus two adjacent cases: `LockService` (built, not applied to editing) and `TblScheduledReport` / `TblCaseTransferHistory` (tables created, features never built).

**This is the most important insight in this report.** The team's *building* capability is strong; the *integration and completion* discipline is where the gap lies. The remediation is not "write more code" — much of the needed code already exists. It is to **finish connecting what has already been built**, and to add a definition-of-done that requires a feature to be reachable, enforced, and tested before it is considered complete.

## 21.5 Immediate Actions (This Week)

| # | Action | Effort | Why now |
|---|---|---|---|
| 1 | **Enable BitLocker on every machine** running the system | 1 day | Only immediate mitigation for SEC-003 |
| 2 | **Lock down the backup folder** (NTFS admin-only; not cloud-synced) | 2 hours | Closes highest-volume leak path |
| 3 | **Hide the Permission Matrix** from navigation | 30 min | Stops the false-assurance risk today |
| 4 | **Fix `ریال` → `افغانی`** (7 occurrences) | 1 hour | Wrong currency is on printed receipts and ID cards **right now** |
| 5 | **Write the accounting backup into your written procedure** | 1 hour | Most likely cause of catastrophic loss |
| 6 | **Schedule a restore test** on a spare machine | 4 hours | Your backups are currently unverified |
| 7 | **Remove `.pfx` from git; rotate certificate** | 2 hours | Key must be assumed compromised |

## 21.6 Conditions for "Production Ready"

The system may be reclassified as **Production Ready** when:

- [ ] Database, documents, photographs, and backups are encrypted at rest
- [ ] `PermissionService` is enforced on all CRUD operations, **or** the Permission Matrix is removed
- [ ] Backup and restore have automated test coverage, and a restore has been verified manually
- [ ] Accounting is included in the main backup
- [ ] Concurrent-edit protection is active on case and family editing
- [ ] All Iranian terminology and currency errors are corrected, with a guard test preventing regression
- [ ] Security score ≥ 80 / 100
- [ ] Test coverage ≥ 60%
- [ ] CI pipeline runs build + tests on every commit

**Estimated time to meet these conditions: 4–5 months** (Phases 1 and 2 of §18) with 1–2 dedicated developers.

---

## Appendix A — Findings Index

| ID | Severity | Finding | Section |
|---|---|---|---|
| SEC-001 | 🔴 Critical | Permissions configured but never enforced | §10.2 |
| SEC-003 | 🔴 Critical | No encryption at rest | §10.4 |
| SEC-004 | 🔴 Critical | Backups unencrypted | §10.5 |
| SEC-002 | 🟠 High | Center isolation by convention, not schema | §10.3 |
| SEC-006 | 🟠 High | Signing certificate committed to git | §10.7 |
| SEC-007 | 🟠 High | Password expiry non-functional | §10.1 |
| SEC-008 | 🟠 High | Roles are unconstrained free text | §10.2 |
| SEC-009 | 🟠 High | Licensing never enforced | §10.7 |
| SEC-010 | 🟠 High | No record locking on edit | §10.7 |
| SEC-005 | 🟡 Medium | No file upload validation | §10.6 |
| SEC-011 | 🟡 Medium | Audit trail can be disabled | §10.7 |
| SEC-013 | 🟡 Medium | Sync package encryption unverified | §10.7 |
| SEC-014 | 🟡 Medium | No schema version guard | §10.7 |
| CR-03 | 🔴 High | "Database is locked" — non-WAL, partial busy_timeout | §13.2 |
| CR-04 | 🔴 High | Silent last-write-wins data loss | §13.2 |
| COMPAT-003 | 🟠 High | WebView2 runtime may be absent | §12.4 |
| GAP-01 | 🔴 Critical | No backup/restore tests | §14.3 |
| GAP-02 | 🔴 Critical | No security tests | §14.3 |
| GAP-03 | 🔴 Critical | No localization guard test | §14.3 |
| PERF-01 | 🟠 High | Full schema verification every launch | §15.1 |
| PERF-02 | 🟠 High | Synchronous backup blocks startup | §15.1 |
| PERF-05 | 🟠 High | Backup loads entire DB into memory | §15.4 |
| §9.4.1 | 🔴 Critical | 51 uses of Iranian `استان` | §9.4 |
| §9.4.2 | 🔴 Critical | `ریال` on printed receipts and ID cards | §9.4 |
| §7.3.5 | 🟠 High | `TblAuditLog` / `TblAuditLogs` duplication | §7.3.5 |
| §7.3.6 | 🟠 High | `Money` discipline not applied to `TblAssistance` | §7.3.6 |
| §7.3.7 | 🟡 Medium | Two dead tables | §7.3.7 |

## Appendix B — Audit Evidence

| Evidence | Result |
|---|---|
| Build (MSBuild, Debug) | ✅ 0 errors, 17 warnings (baseline) |
| Test suite (`vstest.console.exe /Parallel`) | ✅ 346 tests · 344 passed · 2 skipped · **0 failed** · 6.2 min |
| Source files analyzed | 184 C# files (excl. designers) |
| Lines of code analyzed | ≈ 78,500 |
| Database tables enumerated | 64 |
| Indexes enumerated | 73 |
| Forms enumerated | 49 |
| NuGet packages audited | 18 |
| Translation entries measured | 522 translated / 927 pending |

---

*End of report.*

**Prepared by:** Automated architectural, security, and quality audit
**Report date:** 2026-08-21
**Codebase state:** Working tree with uncommitted changes; last commit `6d11754`
**Next review recommended:** On completion of Phase 1 (§18)

