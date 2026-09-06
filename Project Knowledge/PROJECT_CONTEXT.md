# PROJECT_CONTEXT.md

> Permanent project memory + operational guide for AI coding agents.
> Authoritative: this file outranks assumptions and prior sessions.
> Keep updated. Keep short. Replace every `<...>` placeholder.
> Last updated: `2026-09-06` | Maintainer: `<name>`

## Authority
- This file has higher priority than assumptions.
- If code and this document conflict:
  1. Verify the code.
  2. Update this document.
  3. Explain the discrepancy.

---

# PROJECT OVERVIEW

- **Project Name:** CaseManagement
- **Purpose:** Case management for orphans / vulnerable children / disabled / migrant / elderly beneficiaries at a charity/NGO, with financial assistance tracking, accounting, ID card printing, and multi-center offline sync.
- **Business Domain:** Charity / NGO social case management (Afghanistan context; RTL Persian UI).
- **Target Users:** Case workers, center managers, accountants, system admins — multiple physical centers (`TblCenter`), offline-first with sync to head office.
- **Current Status:** Active development. Production data exists but has been declared **not valuable** for the ongoing "redesign" initiative (Phases 1–5) — no migration/backward-compat guarantee required during that initiative.
- **Version:** unversioned (no `AssemblyVersion` tracked here).
- **Repository:** `C:\Projects\CaseManagement` (local; no git — see [[casemanagement-partial-git-coverage]] memory).
- **UI / Docs Language:** Persian (Farsi), RTL. Code identifiers/comments are mixed English/Persian; comments explaining *why* are Persian.

---

# TECHNOLOGY STACK

- **Framework:** .NET Framework 4.7.2, WinForms, `OutputType=WinExe`.
- **Language:** C# 7.3 (`LangVersion` pinned in `.csproj`).
- **Database:** SQLite (`System.Data.SQLite.Core` 1.0.115.5), single file, `PRAGMA foreign_keys = ON`.
- **ORM / Data Access:** None — raw `SQLiteCommand`/parameterized SQL via `DAL/DatabaseHelper.cs`. No repository layer.
- **UI Framework:** WinForms, custom theme (`Helpers/UiTheme.cs`), custom `FieldBox`/`SectionCard` layout controls, RTL throughout.
- **Reporting Tools:** RDLC (`Microsoft.ReportingServices.ReportViewerControl.Winforms`) + typed dataset `DsFullCaseReport.xsd`; a separate metadata-driven report builder (`Helpers/ReportDefinitions.cs` + `FrmReportBuilder`).
- **Testing:** MSTest-style, `CaseManagement.Tests` (separate SDK-style project), 346+ tests against a real temp SQLite DB.
- **External Libraries:** ClosedXML 0.105 (Excel), DocumentFormat.OpenXml 3.1.1 (Word), QRCoder 1.8, Microsoft.Web.WebView2, RBush (spatial), SixLabors.Fonts.
- **Build Configuration:**
  - Build: `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Projects\CaseManagement\CaseManagement.csproj" /t:Build /p:Configuration=Debug /v:minimal /nologo`
  - Test build: same MSBuild, target `CaseManagement.Tests\CaseManagement.Tests.csproj`
  - Test run: `vstest.console.exe "C:\Projects\CaseManagement.Tests\bin\Debug\net472\CaseManagement.Tests.dll" /Parallel` (~6 min; kill any running Test Explorer host first or the DLL is locked)
  - Configs: Debug/Release, Platform AnyCPU (and x64 for some deps)
  - Target platform: AnyCPU
  - Package manager: NuGet, `packages.config` (old-style, non-SDK `.csproj` — **new `.cs` files must be added to `<Compile Include>` manually**, unlike the SDK-style Tests project)

---

# ARCHITECTURE

## Solution Structure
```
C:\Projects\
  CaseManagement/            # main WinForms app (old-style .csproj)
    Forms/                   # EMPTY — all forms actually live at project root
    Frm*.cs                  # 27+ forms at root (FrmCase, FrmDocs, FrmDashboard, ...)
    DAL/                     # DatabaseHelper.cs only — connection factory, not a repo layer
    Helpers/                 # ~45 files: schema init, backup, export, theme, lookups, i18n, domain
    Enterprise/               # workflow/approvals/permissions/rules/tasks/locks subsystem
    Sync/                     # offline outbox sync + legacy HTML import + media sync
    Accounting/               # double-entry accounting subsystem
    GuardianCardIntegration/  # ID card template designer/renderer/batch print
    AI/                       # Persian NLU assistant (search, reminders)
    AssistanceReceiptIntegration/ # cash/package receipt printing
    DevCenter/                # internal health-check/repair tooling
    Project Knowledge/        # THIS FILE
  CaseManagement.Tests/       # SDK-style test project (separate .csproj)
```

## Layers
- **Presentation:** `Frm*.cs` files at root — contain SQL directly (no separation).
- **Application / Services:** partial — `Enterprise/*Service.cs`, `Sync/Sync*Service.cs`, and the `Helpers/*Service.cs` family grown across Phases 3–5: `TimelineService`, `ReferenceDataService`, `RequiredDocumentService`, `CaseCompletionService`, `CaseModuleService`, `FieldVisitService`, `CaseFundingService`, `AssistanceRuleService`. **New cross-cutting logic belongs here, not in a form.**
- **Domain / Business:** `Helpers/CaseDomain.cs` (status/type constants), reference tables (`TblRequestType`, `TblServiceStatus`) as of Phase 3.
- **Data Access:** none dedicated — raw `SQLiteCommand` inline in forms/helpers.
- **Shared / Infrastructure:** `Helpers/` grab-bag (UiTheme, FileHelper, AuditLogger, LookupHelper, SecurityContext, LangData).

## Main Modules
- `FrmCase` — case (household) CRUD, the largest form (~4,600 lines).
- `FrmFamily` — family member CRUD.
- `FrmDocs` — document upload/classification per case.
- `FrmDashboard` — KPI dashboard, heavy `ServiceStatus`/`RequestType` consumer.
- `Enterprise/*` — generic workflow, approvals, permissions, record locking, rules engine (Warn/Block/Task/Audit), version snapshots.
- `Sync/*` — offline outbox (`SyncOutbox`) + HTTP/file transport + conflict tracking; separate legacy `HtmlSyncProvider` for HTML-based import with Persian header mapping.
- `Accounting/*` — funds, parties, transactions, periods, salary/stipend tracking; **not directly linked to `TblAssistance`** (no FK) — deliberate soft-link pattern (see Known Decisions).
- `GuardianCardIntegration/*` — ID card template designer + batch printing.

## Data Flow
- UI event (Frm*.cs) → inline `SQLiteCommand` → SQLite. No DTO/mapping layer.
- Cross-cutting: `AuditLogger` (writes `TblAuditLog`), `Helpers/TimelineService` (writes `TblCaseTimeline`, Phase 3+), `Enterprise.VersionService` (full-row snapshots in `EntRecordVersion`), `Sync.SyncOutboxService.Capture(...)` (queues outbound sync after every case-affecting write).

## Entry Points
- App start: `Program.cs` → `Lang.Initialize()` → `DatabaseInitializer.EnsureDatabaseObjects()` → `AccountingInitializer` → `AdminInitializer` → `EnterpriseInitializer` → `OfflineSyncInitializer` → `AiInitializer`.
- Main window: `FrmDashboard` (post-login).
- Background: `Helpers/AutoBackupService.cs`, `Sync/BackgroundSyncManager.cs`.

## Dependency Rules
- No enforced layering — this is historical debt, not a rule to imitate. New code should still avoid adding cross-form dependencies; prefer `Helpers/*Service.cs` static classes.
- Shared/reference-data reads go through cached static helpers (`LookupHelper`, `ReferenceDataService`) — never re-query `TblLookup`/reference tables ad hoc from a form.
- No circular project references (single-project solution; N/A across projects).

---

# DATABASE

## Main Tables (core spine; **94 tables** verified 2026-09-04 across 6 prefixes: Tbl/Ent/Acc/Sync/Adm/Ai — previously recorded as "~101", corrected by counting `CREATE TABLE IF NOT EXISTS` across all `*Initializer.cs`)
| Table | Purpose | Key Columns |
|---|---|---|
| `TblCase` | Case/household root **and** household-group root (Phase 6: `FamilyGroupID` soft self-references the root case's `CasID`; NULL = standalone; no separate family table by design) | PK `CasID`; FK `RequestTypeID`→`TblRequestType`, `ServiceStatusID`→`TblServiceStatus`, `CenterID`; unique `Code`, `FormNo`. **Phase 3 (revision):** +14 request-type-specific columns, flat on `TblCase` (not separate module tables) — Orphan: `MainResidenceProvince/District/Village`, `FatherDeathCause`; Disability: `DisabilityCause`, `DisabilityDescription`, `SpecialNeeds`, `DisabilityCardStatus`, `DisabilityCardNumber`; Migrant: `HasMigrationCard`, `MigrationCardNumber`, `DepartureDate`, `ArrivalDate`, `AssistanceDurationMonths`. **Pre-Phase-4:** +3 completion cache columns — `CompletionPercent` (INTEGER), `CompletionStatusCode` (`COMPLETE`/`IN_PROGRESS`/`INCOMPLETE`), `CompletionCalculatedAt`; indexed on `CompletionStatusCode` for future dashboard/report filtering |
| `TblFamily` | Family members | PK `FamID`; FK `CasID` CASCADE |
| `TblDocs` | Case documents | PK `DocID`; FK `CasID` CASCADE, `DocumentCategoryID`→`TblDocumentCategory` (Phase 3, nullable). **Phase 5.5-A:** `IsVerified`/`VerifiedBy`/`VerifiedDate`/`VerificationNotes` (foundation; not yet an activation condition) |
| `TblAssistance` | Cash/in-kind assistance given | PK `AssistanceID`; FK `CasID` CASCADE |
| `TblCaseRelation` | Related-case links | PK `RelationID`; FK `CasID`, `RelatedCasID` both CASCADE |
| `TblLookup` | Simple admin-editable pick-lists | PK `LookupID`; unique `(Category, Value)` |
| `TblCenter` | Multi-center | PK `CenterID` |
| `TblRequestType` | **Phase 3** — case classification reference (6 types) | PK `RequestTypeID`; unique `Code`; `ShowOrphanSection`/`ShowDisabilitySection`/`ShowMigrantSection` + **Phase 4** `ShowGuardianSection` flags (shared visibility rules — see Business Rules) |
| `TblServiceStatus` | **Phase 3** — service workflow reference (6 statuses) | PK `ServiceStatusID`; unique `Code` |
| `TblServiceStatusTransition` | **Phase 3** — allowed status transitions (data only, not yet enforced) | PK `TransitionID`; FK `FromStatusID`/`ToStatusID`→`TblServiceStatus` |
| `TblDocumentCategory` | **Phase 3** — document taxonomy (10 categories: 6 base + 4 disability-specific added in revision: `DISABILITY_AREA_PHOTO`, `DISABILITY_CARD_PHOTO`, `MEDICAL_DOCUMENTS`, `DISABILITY_VERIFICATION`) | PK `DocumentCategoryID`; unique `Code`; `FolderName` (ASCII) |
| `TblRequiredDocument` | **Phase 3** — mandatory-doc matrix per request type | PK `RequiredDocumentID`; FK `RequestTypeID`, `DocumentCategoryID`; unique pair |
| `TblRequiredField` | **Pre-Phase-4** — mandatory-*field*-name matrix per request type (mirrors `TblRequiredDocument`'s shape) | PK `RequiredFieldID`; FK `RequestTypeID` CASCADE; unique `(RequestTypeID, FieldName)`; `FieldName` is a literal column name in the table named by `SourceTable` (**Phase 4**, default `'TblCase'`), read by `CaseCompletionService` |
| `TblOrphan` | **Phase 4** — orphan + guardian data. Created for **three** child types (ORPHAN, UNSUPPORTED_CHILD, BADLY_SUPPORTED_CHILD) because the Guardian section spans all three; father/mother-death fields stay empty for the non-orphan two | PK `OrphanID`; FK `CasID` CASCADE; `UNIQUE(CasID)`; `GlobalID` |
| `TblDisability` | **Phase 4** — disability detail incl. card issuer/issue/expiry | PK `DisabilityID`; FK `CasID` CASCADE; `UNIQUE(CasID)`; `GlobalID`; indexed on `ExpiryDate` |
| `TblMigrant` | **Phase 4** — migration detail incl. origin/destination country | PK `MigrantID`; FK `CasID` CASCADE; `UNIQUE(CasID)`; `GlobalID` |
| `TblFieldVisit` | **Phase 5** — field visits, **many per case** | PK `VisitID`; FK `CasID` CASCADE; `GlobalID`; indexed `(CasID, VisitDate DESC)` |
| `TblFieldVisitPhoto` | **Phase 5** — photos per visit. The schema's **only grandchild** (parent is a visit, not a case); carries a denormalized `CasID` so the existing backup/sync child machinery works unchanged | PK `PhotoID`; FK `VisitID` CASCADE **and** `CasID` CASCADE |
| `TblFundingSource` | **Phase 5** — funding registry (config), seeded with 5 sources | PK `FundingSourceID`; unique `Code`; `IsActive` |
| `TblSponsor` | **Phase 5** — independent sponsor registry, reusable across cases | PK `SponsorID`; `IsActive`; `GlobalID` |
| `TblCaseFunding` | **Phase 5** — case ↔ funding source ↔ sponsor link, many per case | PK `CaseFundingID`; FK `CasID` CASCADE, `FundingSourceID`/`SponsorID` RESTRICT; `GlobalID` |
| `TblAssistanceRule` | **Phase 5** — recommended-amount rules (config) | PK `RuleID`; `Priority` (lower wins), `Amount`, `IsActive` |
| `TblAssistanceRuleCondition` | **Phase 5** — AND-ed conditions per rule | PK `ConditionID`; FK `RuleID` CASCADE |
| `TblVulnerabilityCriteria` | **Phase 5.5-B** — what is measured; `FactName` maps to a whitelisted `CaseFacts` name. Criteria whose fact does not exist yet are harmless placeholders | PK `CriteriaID`; unique `Code`; `IsActive` |
| `TblVulnerabilityRule` | **Phase 5.5-B** — Operator + Value + `ScoreValue` per criterion; all matches accumulate | PK `RuleID`; FK `CriteriaID` CASCADE |
| `TblVulnerabilityScore` | **Phase 5.5-B** — calculated score, band, reason, date; versioned via `IsCurrent` for audit. **No `GlobalID` — derived data, never synced** | PK `ScoreID`; FK `CasID` CASCADE |
| `TblVulnerabilityScoreDetail` | **Phase 5.5-B** — per-rule contribution (the score explanation). Grandchild of the case; carries denormalized `CasID` per the Decision #24 pattern | PK `DetailID`; FK `ScoreID` CASCADE, `CasID` CASCADE |
| `TblCaseRepresentative` | **Phase 7** — نمایندهٔ قانونی (وکیل/قیّم)، **چند ردیف به‌ازای هر پرونده** ولی هر جایگاه یکتا. Rep1 الزامی، Rep2 اختیاری — قاعده در `RepresentativeOrder` است نه در ستون‌های Rep1*/Rep2* | PK `RepresentativeID`; FK `CasID` CASCADE; `UNIQUE(CasID, RepresentativeOrder)`; `GlobalID`; indexed on `NationalID`/`Phone`/`FullName`/`RelationshipToBeneficiary` |
| `TblCaseTimeline` | **Phase 3** — append-only central case event log; schema is deliberately generic (`SourceTable`/`SourceID`/`Amount`/`FieldName`/`OldValue`/`NewValue`) so Payment/Assistance/FieldVisit/FundingSource modules can write to it later with **zero schema change** | PK `TimelineID`; FK `CasID` CASCADE; `SourceTable`/`SourceID` soft link |
| `TblCaseStatusHistory` | Legacy status-change history (still written, not replaced) | PK `StatusID`; `CasID` |
| `TblAuditLog` / `TblAuditLogs` | Two overlapping audit tables (known debt, not consolidated) | — |
| `Ent*` (20 tables) | Enterprise: workflow, approvals, permissions, rules, locks, versions | — |
| `Acc*` (12 tables) | Accounting: funds, parties, transactions, periods | — |
| `Sync*` (6 tables) | `SyncOutbox`, `SyncState`, `SyncConflict`, `SyncBaseline`, `SyncFile`, `SyncFileDownload` | — |

## Relationships
- `TblCase` 1—N `TblFamily`, `TblDocs`, `TblAssistance`, `TblCaseTimeline`, `TblCaseStatusHistory` (all CASCADE).
- `TblCase` 1—0..1 `TblOrphan`, `TblDisability`, `TblMigrant` (Phase 4; CASCADE, `UNIQUE(CasID)`).
- `TblCase` 1—N `TblFieldVisit` 1—N `TblFieldVisitPhoto` (Phase 5; CASCADE). The photo also holds a denormalized `CasID` — see Known Decision #24.
- `TblCase` 1—N `TblCaseFunding` N—1 `TblFundingSource` / N—0..1 `TblSponsor` (Phase 5; case link CASCADE, registries RESTRICT).
- `TblCase` 1—N `TblCaseRepresentative` (Phase 7; CASCADE). **والدِ نماینده عمداً `TblCase` است نه `TblDisability`** — با والدِ مستقیم، `BackupHelper.MergeChildTable` و `SyncApplier.ResolveParent` بدون تغییر کار می‌کنند (محدودیتِ «نوه» در تصمیم #۲۵ لمس نمی‌شود).
- `TblAssistanceRule` 1—N `TblAssistanceRuleCondition` (Phase 5; CASCADE).
- `TblVulnerabilityCriteria` 1—N `TblVulnerabilityRule` (Phase 5.5-B; CASCADE).
- `TblCase` 1—N `TblVulnerabilityScore` 1—N `TblVulnerabilityScoreDetail` (Phase 5.5-B; CASCADE; detail also holds a denormalized `CasID`).
- `TblRequestType` 1—N `TblCase` (RESTRICT, reference data) — **and** 1—N `TblRequiredDocument` (CASCADE).
- `TblServiceStatus` 1—N `TblCase` (RESTRICT) — **and** 1—N `TblServiceStatusTransition` (both From/To).
- `TblDocumentCategory` 1—N `TblDocs.DocumentCategoryID` (nullable FK) and 1—N `TblRequiredDocument`.
- `TblAssistance` ↔ `Acc*` (funds/parties): **no FK**, deliberate soft link (see Known Decisions).
- Cascade policy: case-owned data CASCADEs from `TblCase`; reference data (`TblRequestType`, `TblServiceStatus`, `TblDocumentCategory`) is never deleted, only deactivated (`IsActive`).

## Naming Conventions
- Tables: `Tbl`/`Ent`/`Acc`/`Sync`/`Adm`/`Ai` prefix + PascalCase.
- Columns: PascalCase. Reference tables: `Code` (stable ASCII business key, e.g. `ORPHAN`, `APPLICANT`) + `Name` (Persian display) — **Code is what application logic compares against; Name is display-only.**
- Primary keys: `<Entity>ID INTEGER PRIMARY KEY AUTOINCREMENT`.
- Foreign keys: `FK_<Child>_<Parent>`.
- Indexes: `IX_<Table>_<Col(s)>`; unique: `UQ_<Table>_<Col>` or inline `UNIQUE`.
- Dates: `TEXT`, `yyyy-MM-dd` (business date) or `yyyy-MM-dd HH:mm:ss`/`datetime('now')` (instant). Persian-calendar display handled at UI layer only (`PersianDateHelper`), never stored.
- Booleans: `INTEGER NOT NULL DEFAULT 0/1`.

## Migration Rules
- No formal migration framework. `Helpers/DatabaseInitializer.cs` (`EnsureDatabaseObjects()`, ~2,200 lines) is the de-facto migration system: idempotent `CREATE TABLE IF NOT EXISTS` + `EnsureColumn`/`EnsureColumns` (checks `PRAGMA table_info` before `ALTER TABLE ADD COLUMN`) + one-off data-fix routines, run on **every app startup**.
- **Adding a column:** always `EnsureColumn(con, "Table", "Col", "TYPE NULL")` or with `DEFAULT <value>` for `NOT NULL` — never a bare `ALTER TABLE` inline, never assume the column exists.
- **Never rename/drop `TblCase` via `ALTER TABLE RENAME`** — SQLite rewrites child `FOREIGN KEY` clauses to the old name, corrupting `TblFamily`/`TblDocs`/`TblAssistance` FKs (documented live bug; `RepairBrokenChildForeignKey` exists to fix affected installs). If a full rebuild is ever required, follow `RebuildCaseServiceStatusCheck`'s pattern: create new table → copy data → drop old → rename new (never rename the *original*).
- Every schema change ships inside `DatabaseInitializer.cs` (or a sibling `*Initializer.cs` for its subsystem) — there is no separate migration file/tool.
- Startup order matters: `DatabaseInitializer` → `AccountingInitializer` → `AdminInitializer` → `EnterpriseInitializer` → `OfflineSyncInitializer` → `AiInitializer`. New reference tables that other initializers depend on must be seeded in `DatabaseInitializer`.

## Versioning Rules
- No schema-version row/table. Idempotency (via `IF NOT EXISTS`/`ColumnExists` checks) is what makes repeated startup runs safe across versions.
- App does not check a schema version at startup — it just re-applies all `Ensure*` calls every launch.
- No formal backward-compatibility window; current project stance (2026 redesign) is explicitly **no migration/backward-compat guarantee required** for the redesign initiative.

---

# BUSINESS RULES

## Core Entities
- **Case (`TblCase`)** — a household/beneficiary record; the root entity. Identity = `Code`/`FormNo`.
- **Family Member (`TblFamily`)** — a person belonging to a case; has `MemberRole` (یتیم/پدر/مادر/فرزند/سایر).
- **Request Type (`TblRequestType`, Phase 3)** — primary case classification, exactly 6 values, no catch-all "Other": `ORPHAN` (ایتام), `UNSUPPORTED_CHILD` (بی‌سرپرست), `BADLY_SUPPORTED_CHILD` (بدسرپرست), `DISABLED` (معلول), `MIGRANT` (مهاجر), `ELDERLY` (کهن‌سال). **Mandatory** at manual entry (`FrmCase.ValidateForm`) and at DB level (`NOT NULL DEFAULT`, dynamic-column sync always carries it).
- **Service Status (`TblServiceStatus`, Phase 3)** — primary service workflow, 6 values: `APPLICANT` (متقاضی) → `UNDER_REVIEW` (در حال بررسی) → `PENDING_APPROVAL` (در انتظار تایید) → `ACTIVE` (فعال) → `TEMPORARILY_SUSPENDED` (قطع موقت, reversible) → `SUSPENDED` (قطع, terminal). Mandatory (pre-existing CHECK constraint + Phase 3 reference table).
- **Document Category (`TblDocumentCategory`, Phase 3)** — 10 categories: Identity, Migration, RequestForms, InvestigationForms, MedicalDisability, Educational + (revision) DisabilityAreaPhoto, DisabilityCardPhoto, MedicalDocuments, DisabilityVerification.

## Request-Type-Driven Section Visibility (Phase 4 matrix — shared rule, single source of truth)
`TblRequestType.ShowOrphanSection`/`ShowDisabilitySection`/`ShowMigrantSection`/`ShowGuardianSection`/`ShowRepresentativeSection` (Phase 7) are the **only** place this rule lives — read via `ReferenceDataService.GetRequestTypeSections(...)`/`GetRequestTypeSectionsByName(...)`. Any future module (assistance rules, vulnerability scoring, reports) must read these flags rather than hardcoding a `RequestTypeID`/`Code` check.

| Request Type | Orphan | Disability | Migrant | Guardian | Representative |
|---|---|---|---|---|---|
| `ORPHAN` | ✓ | ✓ | ✗ | ✓ | ✗ |
| `UNSUPPORTED_CHILD` | ✗ | ✓ | ✗ | ✓ | ✗ |
| `BADLY_SUPPORTED_CHILD` | ✗ | ✓ | ✗ | ✓ | ✗ |
| `DISABLED` | ✗ | ✓ | ✗ | ✗ | ✓ |
| `MIGRANT` | ✗ | ✗ | ✓ | ✗ | ✗ |
| `ELDERLY` | ✗ | ✗ | ✗ | ✗ | ✗ |

- **General Information and Family Information are always visible** — no flags for them (General applies to all 6 types; Family to 5 of 6). Deliberately not made toggleable: Family Information is the embedded `FrmFamily` Members tab, and gating it would mean touching tab/embedding plumbing for marginal gain.
- **Not touched:** the pre-existing `DisabilityType`/`DisabilityDegree`/`MigrationCardType` controls keep their original always-visible behavior (governed by `chkHeadHealthy`, not by request type) — only fields added in Phase 3/4 are gated, to avoid changing existing screen behavior for old data/tests.
- `FrmCase.UpdateRequestTypeSectionVisibility()` is the only place that reads these flags and toggles the four `FieldBox[]` groups (`orphanSectionFields`/`disabilitySectionFields`/`migrantSectionFields`/`guardianSectionFields`) **plus the Phase 7 Representative tab** via `SetRepresentativeTabVisible` (a `TabPage` has no working `Visible`, so it is removed from / inserted into `TabPages` at its fixed position before the Field-Visit tab).

## Specialized Case Modules (Phase 4 — `Helpers/CaseModuleService.cs`)
- **One row per case per module**, `UNIQUE(CasID)`; `CaseModuleService.Save(...)` UPSERTs (never blind INSERT).
- **Dual-write is mandatory** (Decision #19). Every field that has a same-meaning `TblCase` column is written to **both** the module table and `TblCase`. `CaseModuleService` owns both writes so they can never drift apart; **never write a mirrored field from form code**.
  - Mirrored: `MainResidence*`, `FatherDeathCause`, `DisabilityType/Degree/Cause/Description`, `SpecialNeeds`, `DisabilityCardStatus` (module: `HasDisabilityCard`), `DisabilityCardNumber`, `HasMigrationCard`, `MigrationCardNumber`, `Departure/ArrivalDate`, `AssistanceDurationMonths` (module: `AssistanceDuration`).
  - **Deliberately NOT mirrored:** `TblOrphan.EducationLevel` (the child's — `TblCase.EducationLevel` is the *head of household's* and is read with that meaning by RDLC/search) and `TblOrphan.SchoolName` (`TblFamily.SchoolName` is per-member; no `TblCase` column exists).
  - `TblMigrant.MaritalStatus` is **one-way** `TblCase → TblMigrant`: no second UI control was added, because the existing general-section `txtMaritalStatus` is the same fact and two controls would diverge.
- **Save/delete are gated by visibility:** `FrmCase.SaveCaseModules` writes only modules whose flag is on. A hidden section is never saved, never validated, and — since no row is created — contributes nothing to completion. This is how the "validation only applies to visible sections" rule is enforced.
- **Timeline:** `CaseModuleService` calls `TimelineService.LogModuleRecordCreated/Updated/Deleted` itself (`MODULE_RECORD_*` event types, `SourceTable` = module table name), so no caller can forget it.
- **Completion:** `CaseModuleService.Save`/`Delete` call `CaseCompletionService.RecalculateAndStore` themselves. Module-only fields participate in scoring via `TblRequiredField.SourceTable`; `CalculateFieldCompletion` loads each module row **once** per case (not per field) and resolves values from the right table.
- **Delete does not clear the `TblCase` mirror columns** — RDLC/search/exports read them, and blanking them would change those subsystems' behavior, which is outside this phase's mandate.

## Workflows
- Status transitions (data seeded in `TblServiceStatusTransition`, **not yet enforced in code** — Phase 3 infrastructure only, enforcement deferred to a later phase):
  `APPLICANT → UNDER_REVIEW | SUSPENDED`
  `UNDER_REVIEW → PENDING_APPROVAL | SUSPENDED`
  `PENDING_APPROVAL → ACTIVE | UNDER_REVIEW | SUSPENDED`
  `ACTIVE → TEMPORARILY_SUSPENDED | SUSPENDED` (both require a reason)
  `TEMPORARILY_SUSPENDED → ACTIVE | SUSPENDED`
  `SUSPENDED` is terminal (no outbound transitions seeded).
- Case save (create/edit) → `FrmCase.btnSave_Click`/`UpdateCurrentCase` → dual-write TEXT+ID columns → `AuditLogger` + `TblCaseStatusHistory` (unchanged, legacy) + `TimelineService` (Phase 3, new) → `SyncOutboxService.Capture`.
- Document add/verify/archive → `FrmDocs` → dual-write `DocCategory` TEXT + `DocumentCategoryID` → `TimelineService.LogDocumentAdded/Removed` → missing-document indicator auto-refreshes (`RequiredDocumentService`).
- Status change → `TimelineService.LogServiceStatusChanged` always writes `SERVICE_STATUS_CHANGED`, **plus** one derived event from `TblServiceStatus` flags (never hardcoded Persian text): `SERVICE_ACTIVATED` when the new status has `IsActiveService=1`, `SERVICE_TERMINATED` when `IsTerminal=1`, else `SERVICE_SUSPENDED` when not pre-service. `TimelineService` already defines every event-type constant the approved requirements list (`PAYMENT_CREATED/UPDATED`, `ASSISTANCE_GRANTED/MODIFIED`, `FIELD_VISIT_CREATED/UPDATED`, `FUNDING_SOURCE_ASSIGNED/CHANGED`) even though the modules that will call them don't exist yet — the schema and the constant catalog are ready; only the call sites are future work.

## Case Completion (pre-Phase-4 infrastructure — `Helpers/CaseCompletionService.cs`) — **Phase 3 is closed as of this section**
- **Single source of truth:** any future dashboard/report/filter reads either `CaseCompletionService.Calculate(casId)` (live) or the cached `TblCase.CompletionPercent`/`CompletionStatusCode` columns (fast, filterable) — never re-implements the percentage logic.
- **Inputs:** `TblRequiredField` (mandatory field names per request type — a data-quality signal, separate from `FrmCase.ValidateForm()`'s save-blocking checks) + `TblRequiredDocument` (existing mandatory-document matrix, reused as-is).
- **Type-relevance guarantee (two layers, not one):** (1) data layer — the `TblRequiredField`/`TblRequiredDocument` query is scoped by `WHERE RequestTypeID = @RequestTypeID`, so a field/document belonging to a different type is never even loaded into the candidate list, regardless of whether the case happens to hold leftover data in that column (e.g. a case whose type changed from Migrant to Orphan). (2) schema layer — `applicableFields` in `CalculateFieldCompletion` additionally drops any configured field name that has no matching `TblCase` column *from both numerator and denominator*, so a not-yet-migrated field name can never permanently cap the percentage below 100%.
- **Formula:** `OverallPercent = round(weightedSum / totalWeight)` where Field and Document each weigh 0.5. **Phase 5** adds two *optional* dimensions — Visit and Funding (0.25 each) — that enter the sum **only** when `TblRequestType.RequiresFieldVisit`/`RequiresFunding` is 1. Both default to 0, so with no configuration the formula reduces exactly to the pre-Phase-5 `round(Field×0.5 + Document×0.5)` — no existing percentage moves. Weights are named constants, not scattered literals. A dimension with zero configured required items defaults to 100%.
- **Status:** `100% → COMPLETE`, `0% → INCOMPLETE`, anything between → `IN_PROGRESS`.
- **Recalculation triggers:**
  - Per-case, writes the `TblCase` cache columns: after `FrmCase` insert/update (covers Request Type changes too — a type change only takes effect through a save, which already calls this), and inside `FrmDocs.LoadDocs()` (the same single hook already used for the missing-document indicator — covers document add/update/remove).
  - Bulk, for matrix-level changes: `CaseCompletionService.RecalculateForRequestType(requestTypeId)` and `RecalculateAll()` — ready-made hooks for when `TblRequiredField`/`TblRequiredDocument` definitions change (no admin UI edits them yet; these methods exist so a future admin screen has a trigger point to call, not the trigger itself).
- **Dashboard/report access:** `CaseCompletionService.GetStatusCounts(centerFilterId)` returns the tally from the indexed cache column. Now consumed by the dashboard (5.5-C/D).
- **Phase 5 triggers added:** `FieldVisitService.SaveVisit/DeleteVisit` and `CaseFundingService.AssignFunding/RemoveFunding` each call `RecalculateAndStore` themselves.

## Field Visits, Funding & Assistance Rules (Phase 5)
- **`FieldVisitService`** — the only writer of `TblFieldVisit`/`TblFieldVisitPhoto`. Owns timeline (`FIELD_VISIT_CREATED/UPDATED/DELETED`), sync capture, and completion recalc, so no caller can forget them. Visit photos live under the new `FileHelper.SectionVisitPhotos` section (`<Root>/<CaseCode>/<CaseCode>-VisitPhotos/`). Deleting a visit removes its DB rows (CASCADE) but **deliberately leaves files on disk** — same conservatism as `FrmDocs`.
- **`CaseFundingService`** — funding/sponsor registries + the case link. Removal defaults to `IsActive = 0` (soft), preserving funding history; `hardDelete: true` is opt-in. Emits `FUNDING_SOURCE_ASSIGNED/REMOVED` and `SPONSOR_ASSIGNED/REMOVED`.
- **`AssistanceRuleService`** — recommendation only; creates **no** payment and writes nothing to `TblAssistance`. A rule matches when **all** its conditions pass (AND); a rule with **zero** conditions never matches (guards against a half-configured row paying everyone). Winner = lowest `Priority`, ties broken by lowest `RuleID`. `Evaluate()` is deliberately side-effect free (safe for previews/reports); `EvaluateAndLog()` is the variant that writes `ASSISTANCE_RULE_MATCHED`.
  - **Facts** (closed list — an unknown `FieldName` never matches, so a config typo can't produce a wrong amount): `RequestType`, `ServiceStatus`, `DisabilityDegree`, `DisabilityType`, `MaritalStatus`, `SadatStatus`, `ChildrenCount`, `FamilyMemberCount`.
  - **`ChildrenCount` = family members whose `MemberRole` is `فرزند` or `یتیم`; `FamilyMemberCount` = all members.** Both exposed so rule authors pick the right one.
  - **Operators:** `=`, `<>`, `>`, `>=`, `<`, `<=`, `شامل`. Symbols reuse `Enterprise/RuleEngine`'s vocabulary so admins learn one syntax; `>=`/`<=` are new (the approved example needs them). Numeric comparison when both sides parse as numbers, else ordinal string.
- **Two rule engines coexist by design:** `EntRule`/`RuleEngine` = warn/block/task (no arithmetic); `TblAssistanceRule`/`AssistanceRuleService` = amount recommendation only. Different responsibilities, deliberately not merged.

## Strategic Business Rules (policy — decided 2026-09-03)
> Status: **policy recorded; FamilyGroup implemented (Phase 6).** Rule 1 already held structurally and is now documented as a guard. Rule 3 is implemented via `TblCase.FamilyGroupID`. Rule 4's incompatibility was found and fixed. Rule 2 is a data-entry policy for users, not code.

### 1. Primary Case Type Rule (already satisfied by the current architecture — must not regress)
- **A case is classified by the Request Type of its Head of Household, and by nothing else.** `TblCase.RequestTypeID` holds exactly one value per case; every standard count, report, filter, search result and service summary reads it per-case, so a household is counted once.
- Example: head = Migrant, child #1 = Disabled, child #2 = Orphan ⇒ **Migrants = 1**. The family is *not* also counted as Disabled or Orphan.
- **Family-member conditions** (`TblFamily.HasDisability`, `MemberDisabilityDegree`, …) may only drive Advanced/Analytical reports, eligibility analysis, vulnerability analysis and specialized filters — never standard classification counts.
- A member-level metric is compliant **only if it is explicitly labelled as a member statistic** (e.g. `FrmDashboard.cs` "total members / with disability"). Presenting a member condition as a *family or case* classification violates this rule.
- **Guard for future work:** Phase 4's `TblOrphan`/`TblDisability`/`TblMigrant` are 1:0..1 per case and are currently read by no dashboard or report. Any future feature that counts cases by joining these tables must still yield one classification per case, taken from the head's Request Type.

### 2. Independent Service Rule
- A family member who needs independent assistance, sponsorship, funding, payments, service history, vulnerability score, timeline or case management **must be given a separate Case**.
- Each independent case keeps its own timeline, assistance records, funding records, payments and reports. This is what prevents reporting conflicts and duplicated assistance.

### 3. Family-Centered Structure — **implemented**
- **Model: the case *is* the household root.** `TblCase.FamilyGroupID` holds the `CasID` of the household's root case:
  - `NULL` ⇒ standalone case (treated as its own root; `FamilyGroupService.GetFamilyGroupId` returns the `CasID`, so callers never handle NULL specially).
  - `FamilyGroupID == CasID` ⇒ this case is a household root.
  - `FamilyGroupID == other CasID` ⇒ this case is a member of that household.
- **There is no `TblFamilyGroup` table, deliberately.** Shared household data (address, phone, head) already lives on the root case; a separate table would duplicate it — the "second parallel family structure" the rules forbid. Structure is always two levels (root + members): linking to a case that is itself a member resolves to that member's root.
- **No FK and no backfill.** Soft self-reference (matching the `TblAssistance ↔ Acc*` soft-link precedent) avoids a self-referencing FK on `TblCase`, which would complicate the delete/restore paths and the historical rename hazard. Existing cases keep `FamilyGroupID = NULL` — zero migration.
- **Structural note (verified in code):** `TblFamily` is *misnamed* — it holds family **members** and is a child of `TblCase` (`FK_Family_Case`). It is on the opposite side of the relationship the family model needs, so it was **not** reused and is entirely unchanged.
- Syncs and backs up for free: `FamilyGroupID` is a `TblCase` column, and both paths use `SELECT *`.
- **Service:** `Helpers/FamilyGroupService.cs` — `GetFamilyGroupId`, `IsRoot`, `EnsureRoot` (called on new-case save), `LinkToFamily`, `UnlinkFromFamily`, `GetFamilyCases`, `GetMemberCount`, `SearchCasesForLink`. Emits `FAMILY_LINKED`/`FAMILY_UNLINKED` on **both** cases.
- **UI (minimal, by design):** a "خانواده" tab in `FrmCase` showing the family group ID, this case's role, the member count, the list of cases in the household, and Link/Unlink buttons with a lightweight in-code picker dialog. No separate family-management module, no household admin screens.

### 4. Duplicate Detection — compatibility only, never redesign
- `Helpers/DuplicateDetector.cs` is the **only** duplicate engine. Do not redesign, replace, or add a second one. It matches on National ID (tazkira), full name, father name, phone and address.
- **Known incompatibility with Rule 2/3 — found and fixed.** The engine buckets cases by exact phone/address/tazkira then scores name+father similarity. Once a family is split into independent cases, **siblings share father name, household phone and household address**, so every sibling pair scored as a probable duplicate (an *n*-case household produced *n*(*n*−1)/2 false positives).
- **The fix is one early-return in `DuplicateDetector.Merge`:** pairs whose `FamilyGroupId` is equal and non-zero are skipped. The engine is otherwise untouched — no redesign, no second engine. Unrelated cases that merely share a phone are still flagged exactly as before.
- Guarded by `CaseManagement.Tests/FamilyGroupDuplicateTests.cs`, which asserts **both** directions: same-family pairs are suppressed, unrelated same-contact pairs still flag, and unlinking restores detection.

### 5. Development Priorities
1. FamilyID and Family Management · 2. Duplicate Detection improvements · 3. Fraud detection and abuse prevention · 4. Face Recognition *evaluation only*.

### 6. Face Recognition — explicitly out of scope
- Do **not** create face tables, face encodings, biometric storage, or AI image matching in any current phase. Evaluation only, in a future phase.

## Service Suspension & Activation Gate (Phase 5.5-A)
### Suspension — what already existed vs. what was added
- **Pre-existing and verified, not rebuilt:** `TblCase.SuspensionReason`/`SuspensionDate`/`SuspendedByUserId`/`SuspendedByUsername`; `TblCaseStatusHistory` with `Reason`/`Notes`/`ChangeType`/`UserID` — this table *is* the full suspension **and reactivation** history, written by `AuditLogger.RecordStatusChange`; mandatory-reason validation in `FrmCase.ValidateForm`; `SERVICE_SUSPENDED`/`SERVICE_ACTIVATED`/`SERVICE_TERMINATED` timeline events.
- **Gap found and closed:** `RecordStatusChange` returns early when the status is unchanged, so **editing a suspension reason on an already-suspended case left no trace**. `FrmCase.UpdateCurrentCase` now compares the stored reason and emits `SUSPENSION_REASON_UPDATED` (timeline) plus an audit entry when it changes without a status change.
- No new suspension schema was needed.

### Staged activation gate — `Helpers/CaseActivationValidator.cs`
| Target status | Behaviour |
|---|---|
| Applicant, Under Review, Temporarily Suspended, Suspended | save freely |
| Pending Approval | save allowed, **warning** listing unmet items (user confirms) |
| **Active** | **strict — transition blocked** |

- Blocking conditions: **missing** mandatory document (category below `MinCount`) or **incomplete** mandatory document. **Verification status is deliberately ignored in this phase** — the fields exist but no verification workflow does, so requiring it would make every case unactivatable.
- **"Incomplete" has a precise definition:** a document row exists in a mandatory category but has **no file attached** (`DocFilePath` empty) — it satisfies the count while providing no actual evidence. Computed by `RequiredDocumentService.GetIncompleteRequiredCategories`.
- **Only the transition is blocked, never ordinary editing:** re-saving an already-Active case does not re-run the gate.
- The message lists **every** unmet item at once, split into missing vs. incomplete.
- Covered by `CaseManagement.Tests/ActivationGateTests.cs` (8 tests) including the ignore-verification and edit-already-active cases.

### Document verification — **foundation only**
- `TblDocs` gained `IsVerified` (INTEGER DEFAULT 0), `VerifiedBy`, `VerifiedDate`, `VerificationNotes`, indexed on `IsVerified`.
- `FrmDocs` has a verify checkbox + notes; **`VerifiedBy`/`VerifiedDate` are stamped automatically from `SecurityContext`, never typed** — a verification the user can attribute to someone else is worthless. Un-checking clears all three fields so no "unverified but has a verifier" row survives. Emits `DOCUMENT_VERIFIED`/`DOCUMENT_UNVERIFIED`.
- **Not yet wired into the activation gate** — that is a later phase, by explicit decision.

### Validation review (item 14) — result
- Section-visibility matrix verified consistent: `EnsureRequestTypeSectionFlags` (Phase 3, 3 flags) runs **before** `SetRequestTypeSections` (Phase 4, 4 flags incl. Guardian) in `EnsureFoundationLayerObjects`, so the approved Phase 4 matrix has the last word for all six request types. The two seeders are redundant but harmless; left in place rather than refactored.

## Vulnerability Score (Phase 5.5-B — `Helpers/VulnerabilityScoreService.cs`)
- **Configurable, never hardcoded.** Three config/result layers: `TblVulnerabilityCriteria` (what is measured → maps to one whitelisted `CaseFacts` name) · `TblVulnerabilityRule` (Operator + Value + ScoreValue per criterion) · `TblVulnerabilityScore` + `TblVulnerabilityScoreDetail` (result + per-rule breakdown).
- **Accumulating, not competing** — this is the key difference from `AssistanceRuleService`: assistance rules compete and one winner supplies the amount; vulnerability rules **all** contribute and their points are summed, then clamped to **0–100**.
- **Shared evaluation vocabulary.** Both engines use the same `CaseFacts` and the same `AssistanceRuleService.Compare` (made public in this phase). Two private comparators could interpret `>=` differently — a silent divergence that would be expensive to find later.
- **Bands:** `HIGH` / `MEDIUM` / `LOW`, thresholds from `TblAppSettings` (`VulnerabilityBandHigh` = 60, `VulnerabilityBandMedium` = 30 by default) — not compiled in.
- **Future-criteria placeholders.** `CaseFacts.Get()` returns `null` for an unknown fact name and a `null` fact never matches any operator, so a criterion pointing at data that does not exist yet scores zero harmlessly. `FEMALE_HEADED` and `POOR_HOUSING` are seeded with `IsActive = 0` on exactly this basis: **no new business-data field was added in this phase**, and they start working the day their backing fact exists — no schema change, no engine change. Guarded by `InactivePlaceholderCriteria_NeverContribute`.
- **Facts added this phase (all from existing data):** `HeadAge` (from `HeadBirthDate`), `HasBreadwinner` (from `Job`), `CompletionStatus`/`CompletionPercent`, `HasOrphanRecord`/`HasDisabilityRecord`/`HasMigrantRecord`, `FatherStatus`, `MotherStatus`, `IsStudent`, `MigrationCardType`.
- **Audit history preserved:** recalculation sets the previous row `IsCurrent = 0` and inserts a new one — scores are never overwritten. `CalculationReason` records *why* (case saved / module changed / rule changed / sync import / manual).
- **Cache columns are the only filter path** (`TblCase.VulnerabilityScore`, `VulnerabilityBand`, `VulnerabilityScoreDate`, both indexed) — same rule as `CompletionStatusCode` (Decision #14). `FrmAdvancedSearch` filters on `VulnerabilityBand`, never on a live calculation.
- **Recalculation triggers:** case save (`FrmCase`), module save (`CaseModuleService`), rule change (`RecalculateAll`), and **after a sync download batch completes** — collected in a `HashSet` and run once per affected case in a `finally`, never per imported row (see Decision #29).
- **Timeline:** `SCORE_CALCULATED` on first calculation, `SCORE_RECALCULATED` only when the value actually changed (an unchanged recalculation writes nothing, or every case save would spam the timeline), `SCORE_RULE_CHANGED` for config changes.
- **Not built (deliberate):** no criteria/rule admin UI — rules are seeded and editable only via SQL this phase; the admin screen is 5.5-C work. `FrmCase` shows the score **read-only** with its breakdown; there is no edit path from the case screen.

## Export & Report Verification (Phase 5.5-E) — audited end-to-end

**Verification is by real rendering, not by reading code.** The Phase 7 break proved a clean build says nothing about
whether a report renders. `CaseManagement.Tests/ExportEndToEndTests.cs` builds a fully-populated case (member,
2 documents with 1 verified, visit, funding + sponsor, assistance, representative) and then:
- renders a **real PDF** and asserts the `%PDF-` signature and size;
- renders **WORDOPENXML**, unzips it, strips tags, and asserts the output *actually contains*
  «وضعیت پرونده», «امتیاز آسیب‌پذیری», the computed «1 / 2» verified-docs value, and «وکیل آزمون»
  (the Phase 7 field whose absence used to break this exact path);
- renders a **minimal case with no child rows** to prove empty datasets don't throw;
- executes all 8 export sections and all 6 dashboard aggregates, asserting shape and counts.

> **Renderer note:** the ReportViewer package has **no CSV renderer**, and format `"WORD"` emits legacy MHTML, not
> OOXML. Use **`"WORDOPENXML"`** when you need machine-readable output. `RdlcExportHelper.RenderCase(caseId, format)`
> exists for exactly this.

**Audit results (all clean):** 45/45 `CaseData` fields supplied · 40 scoped + 5 unscoped references all resolve ·
0 undeclared refs · 0 duplicate datasets/textbox names · 0 unbalanced expressions · 7 valid visibility expressions.

**Known, accepted:**
- **`DocsData` is dead weight** — declared with 7 fields and supplied by all three code paths, but bound to no Tablix
  and referenced by no expression, so the RDLC never renders a document *table*. Documents are instead covered as the
  scalar «اسناد تأییدشده: verified / total». Adding a real documents Tablix needs layout reflow and is out of scope.
  The data source must keep being supplied — RDLC throws if a declared DataSet has no source at render time.
- **`{{LocationLink}}` and `{{MemberPhoto}}` have no dictionary entry** and that is correct: the first is handled by
  `ReplaceLocationLink`, the second by `ReplaceImageInElement` inside the family-block clone. Any genuinely unmatched
  placeholder is blanked by `RemoveUnusedPlaceholdersEverywhere`, so nothing ever renders literally.
- **21 dead Word mappings** (8 from 5.5-D, 13 pre-existing). Mapping a placeholder the template lacks is a no-op.
- **Excel cannot be covered automatically** — `ExcelCaseFileExport_...` *proves* the ClosedXML `System.Memory` binding
  failure rather than assuming it, and reports `Inconclusive`. The queries behind Excel are covered separately.
  **Root cause identified 2026-09-06:** the app ships `System.Memory` **4.0.1.2** (matching the `App.config` binding
  redirect) and Excel export works; `CaseManagement.Tests\bin\Debug\net472` ships **4.0.1.1**, which is the whole
  failure. It is a test-host packaging problem, not a product defect — align the test project's version to restore
  automatic Excel coverage. Verified by running `ExcelReportExporter`/`CaseFileExportService` against a copy of the
  live DB from a console host using the app's own `.config`: 1,429,807 bytes for 1,661 cases in 4.5 s, and a 13-sheet
  per-case workbook in 41 ms.

## Reports, Dashboard & Export Completion (Phase 5.5-D)

### ⚠ Single source of truth for the RDLC `CaseData` query — do not fork it
`RptFullCase.rdlc` is fed by **two** code paths: `FrmCaseReport.cs` (on-screen viewer) and `RdlcExportHelper.cs`
(PDF, Word, **and batch** export). Phase 7 added computed fields `Rep1Summary`/`Rep2Summary` to the RDLC and to the
*viewer* path only — the RDLC declared them as `CaseData` fields, so **PDF/Word/batch export requested columns that
did not exist**. The on-screen report looked correct, which is why it went unnoticed.

**Fixed in 5.5-D:** `RdlcExportHelper.CaseDataSql` is now the *only* definition, and `FrmCaseReport` consumes it.
`RepresentativeSummarySql` moved there too. **Any new report column must be added to `CaseDataSql` and nowhere else** —
adding it to one caller again would recreate the same silent break.

Guarded by `CaseManagement.Tests/CaseReportDataPathTests.cs`, which parses the RDLC and asserts every declared
`CaseData` field is actually produced by the query. A future divergence fails the test rather than shipping.

### RDLC additions (scalars only — no new Tablix)
Seven fields added to `CaseData`, plus a "وضعیت پرونده" block (header + 4 label/value pairs) at `Top` 7.90in–8.97in
inside the 9.1in body. **No existing element's geometry was touched.** Follows the `Lbl_X`/`Val_X` + `Visibility`
pattern Phase 7 established. Repeating-row history (timeline/visits/funding) stays **out of scope** — needs new
DataSets + Tablixes and layout reflow.

> The typed dataset `DsFullCaseReport.xsd` is **design-time only** — it is referenced in one comment and nowhere in
> executing code. `LocalReport` binds plain `DataTable`s, so **no Visual Studio regeneration is required** to add fields.

### Word (`OpenXmlCaseExporter`)
Query extended with the same computed summaries; eight placeholder mappings added. **These do nothing until the
placeholders are inserted into `FullCaseTemplate.docx`** (a hand-edited binary); mapping an absent placeholder is a
no-op. Add when the template is next edited: `{{CompletionPercent}}` `{{CompletionStatus}}` `{{VulnerabilityScore}}`
`{{VulnerabilityBand}}` `{{FundingSummary}}` `{{SponsorSummary}}` `{{VerifiedDocs}}` `{{FieldVisitCount}}`

### Excel (`ExcelReportExporter`, multi-case)
Eight columns added to the cases sheet: درصد تکمیل · وضعیت تکمیل · امتیاز آسیب‌پذیری · سطح آسیب‌پذیری ·
منابع تأمین مالی · خیّرین · مجموع مساعدت · اسناد تأییدشده. All read cache columns or scalar sub-selects.

### Dashboard (7 new cards, all in the existing "مدیریت و کیفیت" tab)
پرونده پرخطر · متوسط · کم‌خطر · میانگین امتیاز · سند تأییدنشده · نیازمند بازدید · بدون تأمین مالی.
All values come from the **single composite query** in `DashboardMetricsService.GetManagementMetrics` — the risk counts
hit `IX_TblCase_VulnBand` and the average reads the cached `VulnerabilityScore` column. **`VulnerabilityScoreService`
is never called from the dashboard path**; nothing is recalculated on load.

"نیازمند بازدید" and "بدون تأمین مالی" only count cases whose request type sets `RequiresFieldVisit` /
`RequiresFunding` — both default to 0, so these read 0 until an admin enables them (the Phase 5 contract).

### Drill-down
Risk cards and the missing-documents card are clickable and open **`FrmAdvancedSearch`** pre-filtered, via a new
`FrmAdvancedSearch(vulnerabilityBandDisplay, completionDisplay)` constructor that presets the combos and runs the
search. Chosen over a new `FrmCase` overload because those filters already exist in the search form (5.5-B/C) — so no
new public surface is added to `FrmCase`, and the user lands on filters they can adjust by hand.
`AttachCardClick` wires the handler recursively, since `StatCard` is a `Panel` whose child labels don't bubble clicks.

## Administration, Dashboard & Export Layer (Phase 5.5-C)

### Administration screens (new)
- **`Helpers/FrmFundingAdmin.cs`** — funding sources + sponsors, two tabs, search, show-inactive toggle, add/edit/activate/deactivate.
- **`Helpers/FrmAssistanceRuleAdmin.cs`** — rules grid + conditions grid + a read-only **"منطق محاسبه"** panel.
- Both are **Designer-less forms following `Enterprise/FrmRules`**, using `EntPrompt.Edit()` for data entry. Registered via `AddModuleNav`, so module permissions apply as for other admin screens.
- **No hard delete for funding sources or sponsors** — referenced by `TblCaseFunding` with RESTRICT; deactivation preserves funding history and warns with the affected case count. Assistance rules *can* be hard-deleted (conditions cascade; nothing holds a durable FK to `RuleID`).
- **Rule explanation is generated from the conditions** (`AssistanceRuleService.DescribeRule`), never stored — a stored description goes stale the moment conditions change.

### Dashboard (Phase 5.5-C)
- **`Helpers/DashboardMetricsService.cs` is the only place dashboard aggregation logic lives.** `FrmDashboard` binds values; it never embeds business logic.
- **Existing 13 `StatCard`s and their grid are untouched.** Total/Active/Pending/Suspended were already implemented and are *not* duplicated. New metrics live in the **"مدیریت و کیفیت"** tab (13 cards + 4 distribution grids after 5.5-D).
- `LoadManagementMetrics()` runs **last** in `RefreshAll()` — the missing-documents aggregate is the heaviest query and must not delay the primary cards.
- Management counters are fetched as **one composite query**, not seven round-trips.

### Export preparation layer (Phase 5.5-C)
- **`Helpers/CaseExportDataProvider.cs`** — 8 `DataTable` providers (timeline, field visits, funding, assistance history, vulnerability breakdown, document status, missing documents, case summary). `DataTable` is deliberate: it is what `ExcelReportExporter`, `PrintHelper.PrintDataTable` **and** `ReportDataSource` all consume, so the future RDLC phase is a one-line `DataSources.Add(...)` per section.
- **`Helpers/CaseFileExportService.cs`** — per-case Excel workbook + sequential print, both driven by that provider. Timeline is deliberately the **last** section, per the requirement that history appears at the end of outputs.
- The existing multi-case `ExcelReportExporter` is untouched — per-case and multi-case are different data shapes.
- ⚠ **ClosedXML cannot be covered by the automated suite** — the test project ships `System.Memory` 4.0.1.1 while the app ships 4.0.1.2 (the version its binding redirect names). The *app's* Excel path is verified working against the live DB; only the automated coverage is missing. Fix by aligning the test project's package version.

### Reporting (Phase 5.5-C)
- `ReportDefinitions.cs` gains 5 new sources — `MissingDocuments`, `Funding`, `FieldVisits`, `Timeline`, `VulnerabilityScore` — plus 5 new columns on the existing `Cases` source (completion %, completion status, vulnerability score, band, suspension reason).
- **Only additions; no existing `ColumnKey` was renamed**, so saved `TblReportTemplate` definitions keep working.

### Filtering (Phase 5.5-C)
- `FrmAdvancedSearch` gains **سطح آسیب‌پذیری** and **وضعیت تکمیل** filters. Both query the indexed cache columns (`VulnerabilityBand`, `CompletionStatusCode`) — never a live recomputation.

## FrmCase — Phase 5.5-C surfaces (**complete**)

> Deferred during Phase 5.5-C while the parallel Phase 7 work held `FrmCase`; applied once that reached a stable build.

**Summary tab — second stat row (`summaryStatusRow`).** Five read-only cards built with the existing `MkSummaryStat`
helper, sitting directly below the original six-card `statsRow`, which is untouched:
درصد تکمیل · وضعیت تکمیل · امتیاز آسیب‌پذیری · مبلغ پیشنهادی مساعدت · اسناد تأییدشده.
- Completion and vulnerability read the **cache columns** (`CompletionPercent`, `CompletionStatusCode`,
  `VulnerabilityScore`, `VulnerabilityBand`) — never a live recompute.
- Completion status and vulnerability band are **colour-coded** (Success / Warning / Danger) so state is readable at a glance.
- **اسناد تأییدشده** shows `verified / total` from `TblDocs.IsVerified` (the Phase 5.5-A columns) and turns Warning
  when any document is unverified — this is the document-verification surface on the case screen; per-document
  verification actions remain in `FrmDocs`, not duplicated here.

**Suggested assistance is display-only and side-effect free.** `RefreshSuggestedAssistance()` calls
`AssistanceRuleService.Evaluate()` — deliberately **not** `EvaluateAndLog()`, so merely opening a case never writes an
`ASSISTANCE_RULE_MATCHED` timeline event. The full calculation explanation is attached as a **tooltip** on the amount,
keeping the rule breakdown available without consuming screen space.

**`tabFunding`** — grid of the case's funding assignments plus تخصیص/حذف buttons. All writes go through
`CaseFundingService`, so timeline events and completion recalculation happen inside the service and cannot be
forgotten. Removal defaults to deactivation, preserving funding history. Assignment uses `EntPrompt.Edit` (no new form),
with "— بدون خیّر —" as the first sponsor option since funding without a named sponsor is the common case. If no active
funding source exists, the user is pointed at the admin screen rather than shown an empty dropdown.

**`btnExportCaseFile`** ("پرونده کامل") — distinct from the existing `btnExportExcel`, which is the *multi-case*
filtered report. This exports **one** case with every section via `CaseFileExportService`, offering Excel or Print.

**Refresh wiring:** `RefreshFundingTab()` + `RefreshCaseStatusStats()` are called from both the case-load and
clear-form paths, alongside the existing tab refreshes.

## Legal Representative (Phase 7 — `Helpers/CaseRepresentativeService.cs`)
- **Sole writer/reader of `TblCaseRepresentative`.** Owns validation, timeline, audit and sync capture so no caller can forget them — same rule as `CaseModuleService`/`FieldVisitService`.
- **Slots, not columns.** `RepresentativeOrder` 1 = required, 2 = optional; `UNIQUE(CasID, RepresentativeOrder)` enforces "one of each" in the database, and `Save` is an UPSERT on `(CasID, Order)`. A third representative needs one constant change (`MaxRepresentatives`), no schema change.
- **Saved with the case, not by its own button** — `FrmCase.SaveCaseRepresentatives` runs next to `SaveCaseModules`, and `ValidateRepresentatives()` is the last gate in `ValidateForm()`. A separate button would have let a user save the case without the representative that the case type requires.
- **Requirement is staged, mirroring `CaseActivationValidator` (Decision #42).** `Validate` takes an explicit `RepresentativeRequirement`:

| Context | Mode | Behaviour |
|---|---|---|
| New case (`currentCaseId == 0`) | `Required` | **blocked** — representative 1 must be entered before the case can be created |
| Existing case, ordinary edit | `Optional` | **saves freely**; a one-per-open informational `Msg.Show` if representative 1 is absent |
| → `PENDING_APPROVAL` | — | gate **warns**, user may continue |
| → `ACTIVE` | — | gate **blocks** until representative 1 exists |
| Already `ACTIVE`, ordinary edit | — | not gated (Phase 5.5-A rule preserved) |

  `Optional` never means "accept incomplete data": a *partially* filled representative is still rejected in both modes, and a representative 2 without a representative 1 is rejected. It only means the user is not forced to invent data a legacy record never had.
- **`CaseRepresentativeService.HasPrimary(casId)` is the single source** for both the activation gate and the legacy warning, so the two can never disagree. It tests for the existence of the row, not the completeness of its fields — a row that arrived by import or sync is existing data the gate must not re-judge.
- **`CaseActivationValidator.Evaluate` gained `requiresRepresentative` (defaults to `false`)**, so every pre-existing caller and all 8 Phase 5.5-A tests behave byte-identically. `FrmCase` passes the flag read from the *form's* currently selected request type, not from the database — the gate runs before the `UPDATE`, so the database still holds the old type when a user changes type and activates in one action.
- **An empty representative writes nothing.** `SaveCaseRepresentatives` skips the save when slot 1 is empty, so opening and re-saving a legacy case neither creates an empty row nor touches existing data. Deleting representative 1 happens only through the explicit delete button.
- **Delete is hard, not soft** (`IsActive = 0` would keep the slot occupied and block the replacement, since the UNIQUE is on the slot). The photo file on disk is deliberately left in place — same conservatism as `FrmDocs`/`FieldVisitService`.
- **Validation rules (all in `Validate`/`ValidateOne`, never in the form):** representative 1 required; name ≥ 3 chars; relationship must be a row of the `RepresentativeRelationship` lookup (closed vocabulary, admin-editable); national ID required and format-checked through the shared `IdCardHelper`; primary phone required, 7–15 digits after Persian/Arabic digit folding; secondary phone must differ from the primary; a half-filled representative 2 is rejected (complete it or leave it entirely empty).
- **Duplicate rules are asymmetric on purpose.** *Within* a case: same national ID, or same name + same phone, is blocked. *Across* cases: allowed and only warned about (`FindOtherCasesWithSameId`) — a licensed attorney or a family guardian legitimately represents several beneficiaries, so blocking it would make correct data unrecordable.
- **A person cannot represent themselves**: the representative's national ID is compared against `TblCase.HeadTazkiraNo`.
- ID and phone comparisons are normalized (dashes stripped, Persian/Arabic digits folded); names are normalized for `ي`/`ی`, `ك`/`ک` and repeated spaces — without this, the duplicate checks were defeated by typing style alone.
- **Photos** live in the new `FileHelper.SectionRepresentativePhotos` section (`<Root>/<CaseCode>/<CaseCode>-RepresentativePhotos/`), validated by the same image checks as the head photo.
- **Audit:** `TimelineService` `REPRESENTATIVE_CREATED/UPDATED/DELETED` (category `REPRESENTATIVE`) plus one field-level row per changed field carrying Old/New; `AuditLogger` records the same changes with distinct operation names for photo / address / phone changes so they can be filtered directly on `TblAuditLog`. An unchanged save writes nothing.
- **Security (finalized 2026-09-05):** four **dedicated** keys — `Representative.View`/`Edit`/`Delete`/`Print`. Representative data is a *third party's* identity (national ID, phone, address, photo), so its access level must be settable independently of the beneficiary's case. **Defaults are byte-identical to the `Case.*` equivalents**, so no role's behaviour changed on upgrade — guarded by `Representative_PermissionDefaults_MatchCaseEquivalents`. Enforced at three points: tab visibility (`View`), `SaveCaseRepresentatives` (`Edit` — skips only the representative block, never breaks the case save), and the clear-slot-2 button (`Delete`).
- **Search:** `FrmAdvancedSearch` filters by representative name / national ID / phone / relationship using `EXISTS` sub-queries (never `JOIN` — a join would duplicate a case that has two representatives and corrupt the pager's `COUNT`).
- **Output:** `OpenXmlCaseExporter` supplies `{{Rep1*}}`/`{{Rep2*}}` placeholders (Name, Relationship, IdCardType, NationalID, Phone, SecondaryPhone, Address, Notes) — always defined, so a template lacking them is unaffected; `RptFullCase.rdlc` shows a "نمایندهٔ قانونی" section whose rows are hidden by expression when empty, so non-disability reports print exactly as before. **Excel (added 2026-09-05):** 10 columns (5 per slot) on the "پرونده ها" sheet, built as **scalar sub-queries, never a `JOIN`** — a join would double the row of any case with two representatives and corrupt the summary sheet's case count. Same `IsActive = 1` + `RepresentativeOrder` rule as `RdlcExportHelper.RepresentativeSummarySql`, so Excel and the printed report always agree.
- **Not done, deliberately:** representative data is **not** on the ID card (see Decision #40), and the completion/vulnerability engines were not extended.

## Official Case Forms — print → sign → scan → attach (`Helpers/CaseOfficialForms.cs` + `CaseFormTokens.cs`)
- **Five institution forms live as Word templates in `Templates\Forms\`** — «فورم ۱ درخواست ایتام», «۲ درخواست نیازمندان», «۳ درخواست درمان», «۴ تحقیق و بررسی», «۷ پرونده بخش درمان» — rebuilt from the institution's own PDFs, A4, RTL, B Nazanin / B Titr, navy section bars. All are one page except **form 2**, which is deliberately two: its 8-row family table plus the disability *and* migrant blocks cannot fit one sheet, so `build_f2.py` places an explicit `page_break` before «شرح درخواست» — page 2 is then a complete «شرح + امضاء + مدارک» sheet instead of a stray signature block. They must stay in the `Forms` subfolder (`ReportTemplateHelper.DiscoverCaseTemplates` reads only the `Templates` root).
- **Three-layer split, none of the layers knows the others:** `CaseFormTokens` reads the database and returns the `{{Token}}` map; `CaseOfficialForms` knows only *which form, which document category, which fields are hand-filled*; `FrmDocxForm`/`DocxFormExport` own the dialog and the Word/PDF output.
- **Checkboxes are tokens too.** Every ☐ in a template is a token whose value is `☒`/`☐` (`CaseFormTokens.Chk`), so ticking «مرد/زن», «اهل تشیع/تسنن», priority, disability type etc. needs no engine change. **Every token in every template must have a key** — `DocxFormExport.AssertNoTokensLeft` rejects the output otherwise; `CaseFormTokens.BlankGroups` exists purely to give a key to the boxes/texts that have no database backing (they are filled by pen during the field visit).
- **No `TblDocs` row is written when the form is *generated*.** Phase 5.5 counts a document row without a file as "incomplete" and with a file as "complete"; registering the freshly printed, unsigned file would satisfy the mandatory-document matrix and neuter the activation gate. The row is created only when the **scanned signed copy** is attached, so the existing missing-document indicator correctly warns until then — no extra code.
- **`FrmDocxForm.AttachToCase` now writes the document category** (`DocCategory` + `DocumentCategoryID`), a `GlobalID`, and fires `SyncOutboxService` / `VersionService` / `TimelineService.LogDocumentAdded` — the same four hooks `FrmDocs` uses. Before this it wrote only `DocType`, so an attached signed form counted for nothing in the matrix, the completion percentage or the activation gate. It returns `bool` so the caller can refresh; `FrmDocs` passes `LoadDocs`.
- **Entry point: `FrmDocs` → «چاپ فورم رسمی»** (a `ContextMenuStrip` of the forms applicable to the case's request type). `ORPHAN` gets form 1, every other type gets form 2; forms 3 and 7 are offered to all cases because **`TblRequestType` has no "درمان" type** — the treatment module does not exist, so their medical fields are typed in the dialog or left blank for handwriting, and only the identity block auto-fills.
- **`INVESTIGATION_FORMS` is now a mandatory document for all six request types** (`MinCount = 1`). Business rule: the **first** field visit must have a review form; later visits may or may not. The per-visit expectation is *reported*, not enforced — `Cases` report source gained «تعداد بازدید میدانی» and «فورم بررسی آپلودشده» so the gap is visible without blocking anyone.
- **`FrmCase` warns on save** (`WarnMissingRequiredDocuments`) listing missing categories and categories whose row has no attached file. It warns, never blocks: the real block is the activation gate, and "print → sign → scan" is inherently multi-session, so blocking the save would make partial data unsavable.
- **Report coverage:** `Cases` gained «اسناد الزامی (تعداد)/موجود/کم», «اسناد کامل است؟», «تعداد کل اسناد آپلودشده»; `MissingDocuments` gained «وضعیت سند» (کامل/ناقص — so it can be filtered) and «بدون فایل پیوست». The two `REQUIRED_DOC_COUNT`/`SATISFIED_DOC_COUNT` sub-queries are `const` so the count columns and the yes/no column can never disagree.
- **Templates are generated, not hand-drawn.** The python generator lives in `Templates\Forms\_generator\` (see its README). Regenerate rather than editing the `.docx` in Word — Word can split a `{{Token}}` across runs and `AssertNoTokensLeft` will then reject every export of that form. `coverage.py` there is the guard: it fails if any template token lacks a key in `CaseFormTokens`.
- **Numeric values are wrapped in U+200E (`CaseFormTokens.Ltr`).** In an RTL paragraph Word treats Persian-shaped digits as *Arabic* numbers, so the neutral `-` / `/` between them stops joining the segments and `1401-2233-44551` prints as `44551-2233-1401`; dates and phone numbers break the same way. An LRM on each side fixes it and renders nothing. Do **not** use U+2066/U+2069 instead — B Nazanin draws them as empty boxes (both were measured). The wrap is applied only when the value has a digit and no Arabic-script letter, so Persian sentences are left alone.
- **Runtime guard: `CaseManagement.Tests/OfficialCaseFormTests.cs`.** `coverage.py` only compares text; this test actually seeds a case, runs `CaseFormTokens.Build` and renders all five templates through `DocxFormExport`, so a token broken across runs inside a `.docx` is caught too. It also pins the LRM rule and the checkbox mapping.

## Reference-Data Administration (Feature 1 — `Helpers/ReferenceDataAdminService.cs` + `FrmReferenceDataSettings`)
- **Sole writer of `TblRequestType`/`TblServiceStatus` from the UI.** The form holds no SQL.
- **`Code` is immutable and rows are never deleted.** Application logic compares `Code` (`"DISABLED"`, `"ACTIVE"`, …); only `Name` (display) and `IsActive` can change. "Delete" does not exist — deactivation removes a value from input lists while leaving existing cases untouched.
- **Renaming migrates the data with it.** `TblCase.RequestType`/`ServiceStatus` are TEXT mirrors holding the *name*, so `Rename` updates the reference row and every matching case in one transaction; otherwise existing cases would point at a value no longer in any list.
- **Built-ins are identified by a hardcoded default map** (the 6+6 seeded codes/names), which also powers "restore defaults" and the help text. `RestoreDefaults` resets built-in names and reactivates them but **never deletes user-added values**.
- Added values get a `CUSTOM_<hash>` code so they can never collide with a built-in code that logic compares against.
- Deactivation and restore-defaults both require **two-step confirmation** (confirm + type the exact name / the word «بازگردانی»). Permission: `Settings.Manage`.

## Validation Rules
- **Identity status is three-state and never blank (Feature 4).** Every record is `تذکره الکترونیکی` / `تذکره کاغذی` / `بدون تذکره`. `بدون تذکره` is now a **stored** value (was display-only), `IdCardHelper.FillCombo` no longer offers a blank option and defaults new records to it, `NormalizeType` funnels every write path (form, Excel import), and `IsValid` rejects a tazkira number on a `بدون تذکره` record. Startup migration `DatabaseInitializer.NormalizeIdCardStatus` converts blanks on `TblCase`/`TblFamily`, and both the dashboard count and the search filter match the stored value **or** legacy blanks.
- `RequestType`/`ServiceStatus`: must resolve to an active row in the reference table — enforced in `FrmCase.ValidateForm()`.
- `Code`/`FormNo`: unique, checked before insert/update.
- `HeadTazkiraNo` (national ID): format validated via `IdCardHelper`, conditional on `HeadIdCardType`.
- Suspension (`TEMPORARILY_SUSPENDED`/`SUSPENDED`): requires `SuspensionReason`.
- Document category: **not currently mandatory** (only length-checked) — deliberate, task scope did not require it.

## Permissions
| Role | Can | Cannot |
|---|---|---|
| SuperAdmin | everything, all centers | — |
| Admin | manage users/cases within assigned center(s) | see other centers unless granted |
| Operator | edit cases/docs (`Case.Edit`, `Docs.Edit`) per `PermissionService.Require(...)` | delete (`Case.Delete`, `Docs.Delete` — admin only) |
| Viewer | read-only | edit/delete |

- Permission check location: `Enterprise/PermissionService.cs`, called inline at the top of each form's save/edit/delete handlers (`PermissionService.Require("Case.Edit")` pattern).

## Special Cases
- A case's `RequestType`/`ServiceStatus` are **dual-written**: legacy TEXT columns (read by ~150 existing call sites: dashboard, search, reports, sync, exports, card printing) stay untouched; new `RequestTypeID`/`ServiceStatusID` FK columns are the authoritative identity for all new Phase 3+ features. Resolve by **Name** (Persian display text) via `ReferenceDataService.FindRequestTypeByName`/`FindServiceStatusByName` when bridging the two.
- Old free-text `RequestType` value "سایر" (Other) was **removed** from `CaseDomain.CaseTypes` in Phase 3 — any pre-existing case with that value keeps its TEXT column value untouched but its new `RequestTypeID` defaults to `ORPHAN` (id 1) via the one-time backfill (data not considered valuable; not a migration guarantee).

---

# CODING STANDARDS

## Naming Conventions
- Classes: PascalCase, `Frm` prefix for forms.
- Methods: PascalCase.
- Variables/fields: camelCase (locals), `_camelCase` sometimes for private fields (inconsistent — follow the surrounding file).
- Constants: PascalCase (`CaseDomain.StatusActive`) or `SCREAMING_CASE` for reference-table `Code` values (`"APPLICANT"`, `"ORPHAN"`).
- UI controls: `txt`/`cmb`/`btn`/`chk`/`dgv`/`lbl` + PascalCase name (note: several `ComboBox`es are historically named `txt*` — e.g. `txtServiceStatus`, `txtRequestType`, `txtDocCategory` — do not rename, matches existing convention).
- Files: one class per file, filename = class name.

## Folder Structure
- New form → project root (matches existing convention; `Forms/` folder is unused).
- New cross-cutting service → `Helpers/`, static class, `*Service.cs` or `*Helper.cs` suffix.
- New subsystem (rare) → its own top-level folder with a `*Initializer.cs` (see `Enterprise/`, `Sync/`, `Accounting/`).
- **Old-style `.csproj`: every new `.cs` file must be added to `CaseManagement.csproj` via `<Compile Include="..." />` manually** or the build fails with `CS0234`.

## UI Standards
- Layout: RTL (`RightToLeft = Yes`), Persian text throughout.
- Theme: `Helpers/UiTheme.cs` — semantic colors `Success`/`SuccessLight`, `Danger`/`DangerLight`, `Warning`/`WarningLight`; `UiTheme.FontBold(size)`.
- Fields: `Helpers/FieldBox.cs` (label-above-input) inside `Helpers/SectionCard.cs` (white rounded card) — the established pattern for all case/family/doc forms (`AddCaseField`/`AddField` helpers in each form's Designer.cs).
- Dropdowns: `ComboBoxStyle.DropDownList` for closed vocabularies; free `TextBox` for open text.
- Dialogs/messages: `Helpers/Msg.cs` (`Msg.Show(...)`), `UiTheme.ShowConfirm(...)`.
- Localization: `Helpers/Lang.cs`/`LangData.cs`.

## Database Standards
- Parameterized SQL only (`SQLiteParameter`/`AddWithValue`) — no string concatenation into SQL. Exception: `LIKE` search terms go through `EscapeDataViewLike` for `DataView.RowFilter`, not raw SQL.
- No repository/service layer enforced — forms talk to SQLite directly via `db.GetConnection()` (`DAL.DatabaseHelper`). Accept this for existing code; do not introduce a new abstraction layer without explicit instruction.
- Reference-data reads: always through cached helpers (`LookupHelper`, `ReferenceDataService`), never raw ad hoc queries in forms.

## Error Handling
- Catch specific exceptions where meaningful (`SQLiteException` for UNIQUE violations), else generic `catch (Exception ex)` + `Msg.Show(...)`.
- Cross-cutting writers (`AuditLogger`, `TimelineService`, `VersionService`) **swallow their own exceptions** (log to Debug/error file) — a logging failure must never block the primary save.
- User-facing messages in Persian via `Msg.Show`; never raw exception text unless already Persian-safe.

## Logging
- `AuditLogger` → `TblAuditLog`/`TblAuditLogs` (business audit trail).
- `Helpers/TimelineService` (Phase 3+) → `TblCaseTimeline` (append-only, case-centric event log) — parallel to, not a replacement for, `AuditLogger`/`TblCaseStatusHistory`.
- `Enterprise.ErrorLogger`/`EntErrorLog` → technical error log.
- Never log secrets/passwords/PII beyond what's already business data (Persian names/IDs are inherent to this domain).

## Performance Rules
- Index every FK and every column used in dashboard/report `WHERE`/`GROUP BY`.
- No loops issuing per-row queries against `TblCase`/`TblFamily` at scale — batch or join.
- Reference data (`TblLookup`, `TblRequestType`, `TblServiceStatus`, `TblDocumentCategory`) is cached in-process (`LookupHelper`/`ReferenceDataService`); call `ClearCache()` after admin edits.

---

# DEVELOPMENT RULES

## Allowed Changes
- Additive schema changes via `EnsureColumn`/new `CREATE TABLE IF NOT EXISTS` in the appropriate `*Initializer.cs`.
- New forms/services following existing patterns.
- Bug fixes within existing structure.

## Restricted Areas
- `TblCase` structural rebuilds — approval required, must follow the create→copy→drop→rename pattern (never plain `RENAME`).
- `Helpers/DatabaseInitializer.cs`, `Helpers/BackupHelper.cs` — high blast radius, documented incident history (see [[casemanagement-backup-restore-bugs]]); change with care, verify against real backup/restore round-trip when touched.
- `Acc*` tables/`Accounting/*` — financial integrity; do not add FKs from case tables into accounting tables without going through the existing soft-link pattern.

## Refactoring Rules
- Only when explicitly requested.
- No rename-only churn on existing controls/columns (many are read by 50–150 call sites — see `RequestType`/`ServiceStatus` dual-write precedent).
- Preserve public method signatures unless approved.

## Testing Requirements
- Test project: `C:\Projects\CaseManagement.Tests`.
- Build: MSBuild on `CaseManagement.Tests.csproj` (SDK-style, auto-includes new `.cs` files — unlike the main app).
- Run: `vstest.console.exe <dll> /Parallel` — **~3 hours 12 minutes** for the full suite (664 tests, measured end-to-end 2026-09-06). The long-standing "~6 minutes" figure was wrong by ~30× and is what made a healthy run look hung. Kill any running Test Explorer host first (`taskkill /IM vstest.console.exe /F`) or the DLL is locked.
- 2 tests are always Skipped (pre-existing, unrelated — ClosedXML/System.Memory binding issue). 1 test (`Diag_BatchDesignOverride_AppliesToAllCards`) always fails under `vstest.console` due to `Application.StartupPath` differing from the app's real folder — pre-existing, not a regression signal.
- ⚠ **The suite is not hung — it is just very long (corrected 2026-09-06).** An earlier run was killed at ~50 min and recorded here as a deadlock because `vstest.console`'s own CPU sat frozen at 146.5 s; that is the *orchestrator* process, which idles while the test host works, so its flat CPU proves nothing. A completed run took **3 h 12 min** and finished 660/664. Never diagnose a hang from elapsed time or from `vstest.console` CPU. Practical rule: pipe stdout line-by-line (`| ForEach-Object { $_ | Out-File -Append }`) so progress is visible — a plain redirect buffers and the run looks silent — and confirm progress by counting `Passed`/`Failed` lines rather than waiting on a total.
- Full-run baseline (2026-09-06): **664 total · 660 passed · 1 failed · 3 skipped.** The one failure is the documented environmental `Diag_BatchDesignOverride_AppliesToAllCards`; the three skips are 2 WebView2-dependent + 1 ClosedXML binding in the test host. Treat any other failure as a real regression.
- **The 3 h is caused by `/Parallel`, not by slow tests (measured 2026-09-06).** Summed test durations in the parallel run are ~78 min against 192 min wall clock. The decisive evidence is one test: `SaveAssistance_NonCash_PersistsPackageIdZeroAmount_AndUpdatesCaseRequestType` reports **21 m 17 s** under `/Parallel` but **1 s** in a filtered non-parallel run — the same test, same machine. It drives the real `FrmFinance` form, so under `/Parallel` it contends with other tests for the UI thread / DB fixtures instead of doing 21 minutes of work. Conclusion: **do not assume the suite is inherently 3 h**; measure a non-parallel full run before optimising any individual test, and treat UI-driving tests as the parallelism hazard. Next heaviest (genuinely IO-bound) are the backup / disaster-recovery / AI-performance classes at 16–40 s each.
- **Fast lane vs full lane.** Do not run the full suite on every edit. Fast lane (~minutes, covers the domain/service logic):
  `vstest.console.exe <dll> /TestCaseFilter:"FullyQualifiedName!~Backup&FullyQualifiedName!~Restore&FullyQualifiedName!~DisasterRecovery&FullyQualifiedName!~Encrypt&FullyQualifiedName!~Performance&FullyQualifiedName!~Screenshot&FullyQualifiedName!~HealthReport&FullyQualifiedName!~SaveAssistance_NonCash"`
  Full lane (the 3 h run) belongs to pre-release verification only, and to any change touching backup/restore, sync, or the export pipeline.
- New logic should get a test where the existing suite has a natural home for it; do not weaken/delete existing tests to make a run pass.

## Deployment Rules
- Build output: `C:\Projects\CaseManagement\bin\Debug\CaseManagement.exe` (Debug) — no CI/CD pipeline documented in-repo.
- No formal environments (dev/staging/prod) — single deployable WinForms app per install/center.
- Pre-deploy: run full test suite; verify `DatabaseInitializer.EnsureDatabaseObjects()` runs clean against both a fresh DB and an existing production-shaped DB (schema is additive-and-idempotent by design).

---

# KNOWN DECISIONS

| # | Decision | Reason | Date | Status |
|---|---|---|---|---|
| 1 | Dual-write `RequestType`/`ServiceStatus`: keep legacy TEXT columns, add `RequestTypeID`/`ServiceStatusID` FK columns as new authoritative identity | ~150 existing call sites read the TEXT columns (dashboard, search, reports, sync, exports, card printing); rewriting all of them was out of scope and high-risk. Avoids the `TblCase` CHECK-rebuild hazard entirely. | 2026-09-03 | active |
| 2 | No `TblCaseDocument`/dynamic-forms tables (`TblFormDefinition`/`TblFormField`/`TblFormFieldValue`) | Explicit user decision: fixed set of service types, strongly-typed domain tables only, no generic form builder. | 2026-09-03 | active |
| 3 | Request Type = exactly 6 values, no "Other" catch-all | Explicit user decision, overriding the pre-existing 7th "سایر" value. | 2026-09-03 | active |
| 4 | `TblAssistance` has no FK into `Acc*` (accounting) tables | Pre-existing architectural boundary between case-management money-tracking and double-entry accounting; preserved as-is in Phase 3 (Funding Source design deferred). | pre-existing | active |
| 5 | `TblCaseTimeline` is additive, not a replacement for `TblCaseStatusHistory`/`AuditLogger` | "Do not rewrite working modules" — existing history writers stay untouched; timeline is a new parallel, richer log for future Word/PDF/Excel and payment/visit integration. | 2026-09-03 | active |
| 6 | `TblCaseTimeline` is backed up (`BackupHelper` `LoadTable`) but **not yet merged/restored** on backup restore | `BackupHelper`/restore logic is high-risk (documented incident history); hand-rolling a 19-column merge method for a brand-new table was judged out of scope for "Foundation only". Known, disclosed gap — not a silent bug. | 2026-09-03 | active — revisit when timeline becomes load-bearing (e.g. payment/visit integration) |
| 7 | Document category folder-based file storage (`FileHelper.GetDocumentCategoryFolder`) exists but is **not yet wired into the live save path** — new documents still land in the flat per-case `Docs` folder, only with an English standardized filename | Wiring category subfolders into `FrmDocs`'s existing edit/replace/delete-old-file logic was judged too risky for "Foundation only"; the helper is ready for a later phase to adopt. | 2026-09-03 | active — revisit in the phase that builds document verification/expiry workflow |
| 8 | `TblServiceStatusTransition` seeded but not enforced | Transition-matrix data exists (foundation for a future workflow gate) but `FrmCase` does not yet block invalid status changes — avoids adding new save-blocking logic in a "Foundation only" phase. | 2026-09-03 | active — enforcement is a later-phase task |
| 9 | ~~Orphan/Disability/Migrant fields added as flat `TblCase` columns, not separate module tables~~ | Matches the existing precedent (`DisabilityDegree`/`MigrationCardType` are already flat `TblCase` columns); avoids new FK/cascade relationships and a heavier per-type detail-table UI in a phase explicitly scoped to stay additive and low-risk. | 2026-09-03 | **superseded by #19 (Phase 4)** |
| 10 | "Guardian Information"/"Family Information" for orphans not modeled as new fields | Already covered by existing `TblCase` head-of-household fields and `TblFamily` members; adding parallel fields would duplicate existing data capture. | 2026-09-03 | active |
| 11 | Disability "Area Photo"/"Card Photo" implemented as `TblDocumentCategory` rows (via `FrmDocs`), not new `PictureBox` controls on `FrmCase` | The approved requirements group them under "Disability Documents", and the mandatory-document matrix (`TblRequiredDocument`) already exists to enforce them — reusing it avoids duplicating `FrmCase`'s separate photo-upload plumbing (`PhotoPath`/`FamilyPhotoPath`). | 2026-09-03 | active |
| 12 | Section visibility (`ShowOrphanSection`/`ShowDisabilitySection`/`ShowMigrantSection`) only gates the *new* Phase 3 fields, not the pre-existing `DisabilityType`/`DisabilityDegree`/`MigrationCardType` controls | Those three fields are read by existing logic (`chkHeadHealthy`, `UpdateHeadPhysicalState`) for every request type today; changing their visibility rule would be a behavior change to existing screens outside this phase's mandate. | 2026-09-03 | active |
| 13 | `TimelineService` defines all ~17 approved event-type constants now, but only wires the ones with an existing caller (Case/Status/Document + derived Service Activated/Suspended/Terminated) | Requirement: "future modules must write to the same timeline table without schema redesign." The generic `TblCaseTimeline` shape already supports Payment/Assistance/FieldVisit/FundingSource; defining their constants now means those future modules just call `TimelineService.Log(...)`, no new schema or constant work. | 2026-09-03 | active |
| 14 | Case Completion cached as columns on `TblCase` (`CompletionPercent`/`CompletionStatusCode`), not a separate `TblCaseCompletion` snapshot table | Requirement explicitly names "filtering" as a use case — a flat, indexed column supports `WHERE CompletionStatusCode = ...` directly; a snapshot table would need a join on every dashboard/report/filter query. | 2026-09-03 | active |
| 15 | Required-field matrix (`TblRequiredField`) tracks "data quality," separate from `FrmCase.ValidateForm()`'s save-blocking mandatory checks | A case can be saved today with these fields empty (only `Code`/`HeadFullName`/`RequestType`/`ServiceStatus` are hard-validated); completion percentage is a quality metric, not a new save gate — adding a new blocking validation was out of scope for this task. | 2026-09-03 | active |
| 16 | No UI surface added for completion (no badge/label on any form, no dashboard/report/filter wired) | Task explicitly framed this as infrastructure "used later in dashboards, reports and filtering" — Phase 4 hasn't started; adding UI now would be scope creep ahead of that phase's own review. | 2026-09-03 | active — UI wiring is Phase 4 work |
| 17 | Bulk recalculation (`RecalculateForRequestType`/`RecalculateAll`) added as callable methods with no caller yet | No admin screen edits `TblRequiredField`/`TblRequiredDocument` today, so nothing currently needs to trigger a bulk recompute — the methods exist so that whenever such a screen is built, the trigger point is already there rather than being retrofitted. | 2026-09-03 | active — first caller arrives with the matrix-editing admin screen |
| 18 | Field-relevance filtering happens at the SQL `WHERE RequestTypeID = @RequestTypeID` layer, not by post-filtering an all-types field list in C# | Guarantees a field belonging to a different request type can never influence a case's percentage even if the column happens to hold leftover data from a prior type change — the exclusion is structural, not incidental. | 2026-09-03 | active |

| 19 | Phase 4 module tables (`TblOrphan`/`TblDisability`/`TblMigrant`) **supplement** the Phase 3 flat `TblCase` columns via dual-write — they do not replace them. **This supersedes Decision #9** | `DisabilityType`/`DisabilityDegree`/`MigrationCardType`/`MaritalStatus` on `TblCase` are read by the RDLC typed dataset (`DsFullCaseReport`), `OpenXmlCaseExporter`, `ExcelCaseImporter`, `FrmAdvancedSearch` and `FrmDashboard` — all explicitly out of scope for Phase 4 ("no report/export/dashboard redesign"). Moving the data would break four subsystems this phase may not touch. Dual-write also keeps `CaseCompletionService` working for mirrored fields with no change. | 2026-09-03 | active |
| 20 | `TblOrphan` rows are created for three request types, not just `ORPHAN` | Guardian fields (`GuardianName`/`GuardianRelationship`) live in `TblOrphan`, but the approved matrix shows the Guardian section for `ORPHAN`, `UNSUPPORTED_CHILD` and `BADLY_SUPPORTED_CHILD`. Confirmed with the user rather than assumed. Orphan-specific death fields simply stay empty for the two non-orphan types. | 2026-09-03 | active |
| 21 | `TblRequiredField` gained a `SourceTable` column (default `'TblCase'`) | Module-only fields (`FatherStatus`, `GuardianName`, `OriginCountry`, …) have no `TblCase` column, so the existing `applicableFields` guard would have silently dropped them from scoring and inflated percentages toward 100%. `SourceTable` closes that hole while leaving every pre-existing row's behavior byte-identical. | 2026-09-03 | active |
| 22 | `TblMigrant.MaritalStatus` is mirrored one-way from the existing general-section control; no second UI control added | Two controls for one fact guarantees divergence. `TblCase.MaritalStatus` is already displayed for every request type and read by RDLC/search/`FrmFamily`. | 2026-09-03 | active |
| 23 | Module delete leaves the `TblCase` mirror columns populated | Blanking them would change what RDLC/search/exports display — a behavior change to subsystems outside this phase's mandate. | 2026-09-03 | active — revisit if a later phase makes module tables the sole source for reports |

**Phase 3 status: closed.** Request Type/Service Status foundation, document classification + mandatory-document matrix, case timeline, and case completion infrastructure are all in place, tested, and documented above.

| 24 | `TblFieldVisitPhoto` carries a denormalized `CasID` alongside its real `VisitID` FK | It is the schema's first grandchild. `BackupHelper.MergeChildTable` and `SyncApplier.ResolveParent` both assume a direct `CasID`; the extra column lets both work unchanged instead of modifying two high-risk subsystems. | 2026-09-03 | active |
| 25 | `TblFieldVisitPhoto` is **excluded** from `SyncedTables` — visit photos do not sync between branches | `SyncApplier.ResolveParent` can only translate `ParentGlobalID` → local `CasID`; there is **no** path to translate a remote `VisitID` to the local one. Registering it would attach photos to whichever local visit happened to share that integer ID — silent data corruption. Backup/restore *does* cover photos (via `visitIdMap`). Grandchild support in `SyncApplier` is a prerequisite for enabling this. | 2026-09-03 | active — blocks visit-photo sync until `SyncApplier` supports grandchildren |
| 26 | Funding sources and assistance rules/conditions are **not** in `SyncedTables`; sponsors are | Per the approved sync model, config is one-way head-office data — branches creating divergent `Code` values would make the rule matrix inconsistent across nodes. Sponsors are two-way because branches legitimately register local donors. | 2026-09-03 | active |
| 27 | Completion's optional Visit/Funding dimensions are weight-normalized, not additive | `OverallPercent = weightedSum / totalWeight` keeps the total at 100% whichever dimensions are active, and reduces *exactly* to the old 50/50 formula when both flags are 0 — so enabling the feature later cannot retroactively change untouched cases. | 2026-09-03 | active |
| 28 | A rule with zero conditions never matches | A half-configured rule row would otherwise match every case and recommend its amount to everyone. Fails closed. | 2026-09-03 | active |

| 29 | Post-sync score recalculation runs **once per affected case after the download batch**, in a `finally`, not per imported row | Per-row would push work into `SyncApplier` — the highest-risk file, and the one whose grandchild limitation already forced Decision #25 — and would recalculate repeatedly for a case that received several changes. The `finally` matters because `Download` has five exit paths (cancel, offline, last page, stalled cursor, exception); without it most paths would leave stale scores. | 2026-09-03 | active |
| 30 | Vulnerability scores are **never synced**; only case data and configuration are authoritative | Score is a derived value. Recomputing locally means no score conflicts, no stale scores, no branch/head-office mismatch, and smaller payloads. It also sidesteps the grandchild-sync limitation for `TblVulnerabilityScoreDetail` entirely. | 2026-09-03 | active |
| 31 | `AssistanceRuleService.Compare` changed from `private` to `public` and is shared by both rule engines | Two independent comparators could interpret `>=` or numeric-vs-string coercion differently — a silent divergence between "suggested amount" and "risk score" that would be very hard to trace. One definition, one meaning. | 2026-09-03 | active |
| 32 | No new business-data fields for Vulnerability Score; `FEMALE_HEADED` and `POOR_HOUSING` seeded inactive | Explicit user decision: the phase uses existing data only. The engine's unknown-fact-never-matches rule makes these placeholders inert until their backing data exists, so no schema or engine change is needed to activate them later. | 2026-09-03 | active — activate when head-gender / housing data is introduced |
| 33 | Score recalculation writes a timeline event only when the value actually changed | Every case save triggers a recalculation; logging unconditionally would bury real risk changes under identical repeated rows. First calculation logs `SCORE_CALCULATED`; subsequent *changes* log `SCORE_RECALCULATED`. | 2026-09-03 | active |

**Phase 4 status: complete.** Three specialized module tables + `CaseModuleService`, request-type-driven section visibility (4 flags incl. Guardian), visibility-gated validation/save, timeline integration (`MODULE_RECORD_*`), and completion integration (`SourceTable`-aware scoring). Sync (`SyncedTables` + `SyncApplier.NeverWrite`) and backup/restore (load, merge, whole-replace, `DeleteCurrentData`) all cover the three new tables.

| 34 | Phase 5.5 was split into **5.5-A** (Suspension + Document Validation), **5.5-B** (Vulnerability Score + its UI/filters), **5.5-C** (Assistance & Funding Management UI), **5.5-D** (Dashboard + Output Layer) | Business logic must be stable before reports are rewritten, so the export layer is touched **once** at the end instead of repeatedly. Nothing was dropped — management UIs, completion visibility and dashboard widgets are distributed across B/C/D. | 2026-09-03 | active |
| 35 | The activation gate ignores `IsVerified` in 5.5-A even though the columns exist | No verification workflow or UI existed when the gate was written; requiring verification would have made **every** case unactivatable. Extend the gate when the verification workflow ships. | 2026-09-03 | active — revisit with the verification-workflow phase |
| 36 | 5.5-C's backend already exists from Phase 5 | `TblAssistanceRule`, `TblAssistanceRuleCondition` and `AssistanceRuleService` (evaluation, priority, suggested amount, explanation) were built in Phase 5. 5.5-C is therefore *verify + add Funding Source integration + build the UI*, **not** a rebuild. | 2026-09-03 | active |

| 37 | Legal representatives live in a child table `TblCaseRepresentative` keyed by `(CasID, RepresentativeOrder)`, **not** as `Rep1*`/`Rep2*` column pairs on `TblDisability` | The approved requirements demand "avoid duplicated structures", "support future expansion" and "maintain referential integrity" at once. Two eight-column blocks on one row satisfy none of them: they duplicate the structure, a third representative would need eight more columns, and a flat column can carry no FK. The child table gives CASCADE integrity, and a third slot is a row, not a migration. | 2026-09-04 | active |
| 38 | `TblCaseRepresentative`'s parent is `TblCase`, not `TblDisability` | A representative belongs to the beneficiary, not to the disability record. Had it hung off `TblDisability` it would be the schema's third *grandchild*, and both `BackupHelper.MergeChildTable` and `SyncApplier.ResolveParent` — which can only translate `ParentGlobalID` → `CasID` — would have needed the same special-casing that forced Decision #25. With a direct parent, backup, restore and two-way sync all work unchanged. | 2026-09-04 | active |
| 39 | Representatives are gated by a fifth section flag (`ShowRepresentativeSection`, seeded on for `DISABLED` only) rather than a hardcoded request-type check | Keeps the single-source-of-truth rule the Phase 4 matrix established. Extending the feature to another request type is an `UPDATE`, not a recompile — and the guard test asserts exactly that. | 2026-09-04 | active |
| 40 | Representative data is **not** placed on the ID card; recommendation only | Requested explicitly ("do not automatically place … evaluate space and business need first"). The front face already carries 11 data fields plus two photos in a
A5-landscape footprint (210 × 148.5 mm trim + 3 mm bleed — **not** a credit-card/ID-1 size; corrected 2026-09-06 by measuring the rendered bundle), and the card's audience is the field worker verifying the beneficiary — not the office processing a legal proxy. See the assessment in the Phase 7 delivery notes: if it is ever added, the recommendation is representative **name + phone only**, on the **back** face, as an optional catalog field defaulting to off. | 2026-09-04 | active |
| 41 | Representative fields were **not** added to the three Word case templates, only to the exporter's placeholder map | None of `FullCaseTemplate.docx` / `الگوی_شماره_1.docx` / `الگوی_شماره_2.docx` is disability-specific — all six request types print through them, so a representative block would leave empty rows on the other five. The RDLC detailed report, which *can* hide rows by expression, carries the visible output instead. The placeholders exist, so adding them to a template is a Word edit, not a code change. | 2026-09-04 | **superseded by #80** — قالب تکمیل شد |

| 42 | Representative 1 is **not** an unconditional save gate: `Required` for new cases and for the transition to Active, `Optional` for ordinary edits of existing cases | The first implementation enforced it unconditionally, which made every pre-existing disability case unsaveable — an operator could not correct an unrelated phone number, and the suspension/termination workflow was blocked with it. That is a release blocker, and it also contradicted the precedent Phase 5.5-A set with `CaseActivationValidator` (Decision #30), which deliberately blocks only the *transition* and never ordinary editing. The staged rule enforces the requirement for everything created from now on while leaving the legacy population editable. Audited before the change; see the Phase 7 impact report. | 2026-09-04 | active |

| 86 | **Feature 4 migration converts a blank identity type to «بدون تذکره» only when the tazkira number is *also* blank.** Rows with a number but no type are left untouched and keep surfacing under the legacy `UnknownDisplay` bucket in statistics | Stamping "has no tazkira" onto a row that stores a tazkira number is self-contradictory data, and Decision #43 records an explicit rule against inferring type from number. Auto-resolving them either way would destroy information in a migration that runs on every startup. Leaving them visible lets the user decide. Locked by `F4_BlankTypeButHasNumber_IsLeftUntouched`. | 2026-09-06 | active — revisit when the user resolves the residual rows |
| 87 | The dashboard's «بدون تذکره» count and `FrmAdvancedSearch`'s filter match **the stored value OR a legacy blank**, not blank alone | Both were written when `بدون تذکره` was display-only and tested `type='' AND number=''`. After the Feature 4 migration that predicate matches nothing, so the KPI card would have silently read 0 and the filter returned no rows — a regression invisible until someone noticed the number was wrong. | 2026-09-06 | active |
| 88 | Field-visit "pending / completed" is **derived** (`VisitResult` empty ⇒ در انتظار) as a report column, not a new `TblFieldVisit` status column | The seven required visit reports are all column/filter/group-by combinations over the existing `FieldVisits` report source, which already inherits Excel/PDF/print from the report builder. A status column would have been a schema change plus a UI to maintain it, for a fact the data already expresses. | 2026-09-06 | active |
| 89 | Guardian photo is `TblOrphan.GuardianPhotoPath` + `FileHelper.SectionGuardianPhotos`, added via `EnsureColumn` | It was the only photo with no manual-entry path (head, family and both representatives already had one). A separate `FileHelper` section — not `SectionHeadPhoto` — because the child's guardian is not the household head and must be replaced/removed independently. Audit came free: `CaseModuleService` already logs per-field old→new, so photo changes appear in the case History tab with no extra code. | 2026-09-06 | active |
| 43 | HTML import **never** re-formats a tazkira number. A bare 13-digit value is stored verbatim and classified `Paper`; only a dashed 4-4-5 / 5-4-4 pattern is classified `Electronic` | Verified empirically before deciding: `InferTypeFromNumber` requires dashes, and `IsValid(Paper, "1401140214033")` returns **true** — so the unformatted value passes form validation and duplicate detection (which normalizes both sides) is unaffected. The only cost is that such a number counts as Paper in `CategorySql` statistics. Inserting dashes would mean classifying it Electronic, and a genuine 13-digit *paper* tazkira would then be silently rewritten — data damage in an import path, to fix a cosmetic reporting skew. The stale test `Members_ElectronicTazkiraWithoutDashes_IsFormatted` (written 2026-08-30, five days before the "never re-format" rule was committed in `814d3d2`) asserted the opposite and justified it with the false claim that the form rejects the value; it was replaced by `Members_UndashedTazkira_IsStoredVerbatim_AndClassifiedPaper` + `Members_DashedTazkira_IsStoredVerbatim_AndClassifiedElectronic`, which pin the real contract from both sides. | 2026-09-05 | active |

**Phase 7 status: complete.** `TblCaseRepresentative` + `CaseRepresentativeService`, the RTL two-card Representative tab in `FrmCase`, validation (required/format/duplicate/self/relationship), photo storage, timeline + audit with old/new values, four search filters, Word placeholders and an expression-hidden RDLC section. Sync (`SyncedTables`) and backup/restore (load, merge, whole-replace, `DeleteCurrentData`) cover the new table. 23 dedicated tests.

**Phase 5.5-A status: complete.** Suspension verified end-to-end with the reason-edit gap closed; staged activation gate live; document-verification foundation in place; validation review passed.

| 44 | **Optional dates are stored NULL, not "today"** — `dtpDisabilityIssueDate`/`dtpDisabilityExpiryDate`/`dtpFatherDeathDate` set `ShowCheckBox=true`; `FrmCase.DateOrNull(picker)` writes NULL when unchecked (the date twin of `TextOrNull`); `SetDatePickerValue` maps NULL→unchecked | `PersianDatePicker` always holds a value (default today) and the save path wrote `.Value.Date` unconditionally, so **every** disability record claimed a card issued and expiring the day it was saved, and every orphan record a father's death date of "today". `IX_TblDisability_Expiry` indexed fabricated data. The picker already supported the null state — it was never wired up. Any new optional date control must follow this pattern. | 2026-09-04 | active |
| 45 | **Conditional mandatory documents** — «عکس کارت معلولیت» is required only when `DisabilityCardStatus = 'دارد'`. The rule lives **only** in `RequiredDocumentService` (`ConditionalFilterJoined`/`ConditionalFilterByCaseId` + `AddConditionalParameters`) and is applied by all three consumers of the matrix, including `CaseCompletionService.CalculateDocumentCompletion` | The category was seeded `MinCount=1` for `DISABLED` while `DisabilityCardStatus` explicitly allows «ندارد»/«در حال اقدام» — so a disabled beneficiary with no government card could never pass the activation gate and never receive service. Applying it to only some consumers would make the gate and the completion percentage disagree about the same case. Comparison is **exact equality, never LIKE**: «ندارد» contains «دارد». | 2026-09-04 | active |
| 46 | `CaseModuleService.Save` wraps the module write and the `TblCase` mirror write in **one transaction**; `Delete` uses `SyncOutboxService.PrepareDelete`/`CommitDelete` | The two writes were separate statements, so a mid-way failure produced exactly the dual-write divergence the class exists to prevent (screen and reports disagreeing). `Delete` captured nothing to the outbox although the module tables are in `SyncedTables`, so a branch's deletion never reached head office and could be resurrected on the next sync. Helper signatures unchanged — SQLite transactions are connection-scoped. | 2026-09-04 | active |
| 47 | Module edits log **per-field** history via `TimelineService.LogModuleFieldChanged` (old/new/user/time), alongside the existing `MODULE_RECORD_UPDATED` header event. Only genuinely changed fields are logged | `LogModuleRecordUpdated` passed `null,null,null` into the `FieldName`/`OldValue`/`NewValue` columns that `TblCaseTimeline` always had, so "the degree changed from اول to سوم" was unanswerable. Unchanged saves must not log, or the timeline fills with empty rows. | 2026-09-04 | active |
| 48 | `TblCaseTimeline` is now **readable**: `TimelineService.GetCaseTimeline(casId, limit=500)` + the read-only «تاریخچه» tab (last tab of `FrmCase`) | The table had six writers and zero readers — the richest history the system kept was invisible. The query rides the existing `IX_TblCaseTimeline_Case (CasID, EventAt DESC)` index and is capped for low-end laptops. The tab is deliberately last and read-only: history is reviewed, never typed. | 2026-09-04 | active |
| 49 | Disability data reaches the card JSON (`GuardianCardData.DisabilityType`/`DisabilityDegree`) but is deliberately **not** added to `CardFieldCatalog` | `CaseCardRepository` already read both into `CaseModel` and then dropped them, so no template could bind them. Catalog entries without a matching HTML template element would offer the designer a field whose settings are silently ignored — the exact failure the catalog's own comment warns against. Add the catalog entry **together with** the template change, never before. | 2026-09-04 | active |

| 50 | **Phase 7 — نمایندهٔ قانونی** (`TblCaseRepresentative`): جدولِ فرزندِ چندتاییِ پرونده با `UNIQUE(CasID, RepresentativeOrder)` (دقیقاً دو اسلات)، `CaseRepresentativeService` تنها نویسنده/خواننده، تب اختصاصی در `FrmCase`. `ShowRepresentativeSection` **فقط برای `DISABLED`** برابر ۱ است | نماینده «ماژولِ تخصصیِ نوعِ درخواست» نیست (پس `CategoryModule` نگرفت و در `TblOrphan/TblDisability/TblMigrant` جا نمی‌شد) بلکه موجودیتی چندتایی است — برای همین جدولِ جدا با ordering. جستجو با `EXISTS` نوشته می‌شود نه `JOIN`: جوین روی جدولِ ۱:N ردیفِ پرونده را تکثیر و شمارشِ نتایج و خروجی‌ها را خراب می‌کرد. | 2026-09-04 | active |
| 51 | **A1 — پروندهٔ `DISABLED` نمی‌تواند سرپرستِ «سالم» داشته باشد.** `FrmCase.ApplyDisabledHeadRule` (از `UpdateRequestTypeSectionVisibility` صدا زده می‌شود) تیکِ `chkHeadHealthy` را برمی‌دارد و `txtDisabilityType`/`txtDisabilityDegree` را فعال می‌کند — فقط وقتی `Code == "DISABLED"` | `ClearForm` تیک را پیش‌فرض «سالم» می‌گذاشت و `UpdateHeadPhysicalState` این دو فیلد را غیرفعال **و خالی** می‌کرد. طبقِ تصمیم #۱۲ این دو کنترل تابعِ نمایشِ بخشِ معلولیت نیستند، پس کاربر هیچ نشانه‌ای نمی‌دید و پرونده با هر دو فیلد خالی ذخیره می‌شد — همان دو فیلدی که `TblRequiredField` برای DISABLED الزامی کرده و تنها دو فیلدِ معلولیتی که به RDLC/کارت/جستجو/داشبورد می‌رسند. مقایسه با `Code` طبقِ قاعدهٔ پروژه («Code چیزی است که منطق با آن مقایسه می‌کند»). `Enabled`ِ خودِ چک‌باکس دست‌نخورده ماند تا حالتِ فقط‌خواندنی تغییر نکند. | 2026-09-05 | active |
| 52 | **H4 — `FrmFinance` نوعِ درخواست را فقط در صورتِ تغییرِ آگاهانه می‌نویسد، و آن‌وقت هر دو ستون را.** سه شرط: (۱) مقدارِ ذخیره‌شده واقعاً در کمبو نشسته باشد (`selectedCaseRequestTypeSelectable`)، (۲) مقدار عوض شده باشد، (۳) نام به ردیفِ مرجع نگاشت شود — وگرنه هیچ نوشتنی انجام نمی‌شود و کمک ثبت می‌گردد | `cmbRequestType` از نوعِ `DropDownList` است: انتساب `.Text` با مقداری خارج از فهرست بی‌صدا نادیده گرفته می‌شود و کمبو نوعِ *پروندهٔ قبلی* را نگه می‌دارد؛ UPDATE بی‌قید همان را روی این پرونده مهر می‌کرد (بازطبقه‌بندیِ خاموش). ضمناً `RequestTypeID` هرگز نوشته نمی‌شد، پس ستونِ متنی و کلیدِ خارجی برای همیشه واگرا می‌شدند — گزارش‌ها یک نوع و دروازهٔ فعال‌سازی/اسنادِ الزامی نوعِ دیگری. حالا `REQUEST_TYPE_CHANGED` در تایم‌لاین و یک ردیفِ حسابرسی هم ثبت می‌شود. | 2026-09-05 | active |

**Disability release audit (2026-09-04): complete.** Full inventory, 15 findings, 8 fixed (2 Critical, 4 High, 4 Medium), 10 regression tests, suite 549/553. See `DISABILITY_RELEASE_REPORT.md`, `H4_IMPACT_ASSESSMENT.md`, `DISABILITY_MODIFIED_FILES.md`.
- **Known-failing test: one.** `Diag_BatchDesignOverride_AppliesToAllCards` (environmental — `Application.StartupPath` under `vstest.console`). The second one previously listed here was the stale `Members_ElectronicTazkiraWithoutDashes_IsFormatted`, retired by Decision #43.
- **H4 open (High):** `FrmFinance` writes `TblCase.RequestType` on every assistance save, updates only the TEXT column (never `RequestTypeID`), and silently keeps the previous value when a case's type is absent from `TblLookup` — silent reclassification plus permanent TEXT/FK divergence. Investigated, deliberately not fixed. Full analysis in `H4_IMPACT_ASSESSMENT.md`.
- **Open data risk:** disability/orphan rows written before Decision #44 still hold fabricated dates. Detection query in `DISABILITY_RELEASE_REPORT.md` §5; cleanup needs explicit approval.

| # | Decision | Reason | Date | Status |
|---|---|---|---|---|
| 53 | **Conditional mandatory documents:** `DISABILITY_CARD_PHOTO` is mandatory for `DISABLED` **only when `DisabilityCardStatus = 'دارد'`**. Rule lives in exactly two `const` SQL fragments + `AddConditionalParameters` on `RequiredDocumentService`, consumed by all three readers of the matrix (`GetMissingRequiredCategories`, `GetIncompleteRequiredCategories`, `CaseCompletionService.CalculateDocumentCompletion`) | The category was seeded `MinCount=1` and the activation gate hard-blocks ACTIVE on any missing mandatory doc, while `DisabilityCardStatus` explicitly allows «ندارد» — so a disabled beneficiary with no government card could never be activated. Comparison is exact, never `LIKE`: «ندارد» contains «دارد». Applied to all three readers so the gate and the completion percentage can never disagree. | 2026-09-04 | active |
| 54 | Optional dates use `PersianDatePicker.ShowCheckBox` + `FrmCase.DateOrNull(...)`; `SetDatePickerValue` maps NULL → unchecked | The picker always holds a `DateTime`, so writing `.Value` unconditionally made "no date recorded" and "recorded today" identical in the DB. Every disability record claimed a card issued **and expiring** today, and every orphan case a father's death date of the save date. Applies to `dtpDisabilityIssueDate`, `dtpDisabilityExpiryDate`, `dtpFatherDeathDate`. | 2026-09-04 | active |
| 55 | `CaseModuleService.Save` wraps the module write + `TblCase` mirror in one transaction; helper signatures unchanged | SQLite transactions are connection-scoped, so existing private helpers participate without signature churn. Without it, a failed `MirrorToCase` produced exactly the dual-write divergence the class exists to prevent — and RDLC/exports/search/dashboard read the mirror. | 2026-09-04 | active |
| 56 | `TimelineService` is now the sole writer **and** the reader of `TblCaseTimeline` (`GetCaseTimeline`), surfaced as a read-only "تاریخچه" tab (last tab) in `FrmCase` | The table had six writers and zero readers — the richest history the system kept was invisible. Read-only by design: history is never hand-edited. | 2026-09-04 | active |
| 57 | Disability fields reach Word/Excel exports; `GuardianCardData` gains `DisabilityType`/`DisabilityDegree` but they are **not** added to `CardFieldCatalog` | 9 of 12 disability fields were write-only. Card templates are HTML rows in the DB; listing a field in the designer that no template renders would let users configure something silently ignored — the catalog's own documented rule. The properties make the data *available* to a future template; the catalog entry waits for that template. | 2026-09-04 | active |

**Phase 5 status: complete.** Field visits (+photos) with a FrmCase tab, funding-source/sponsor registries and the case-funding link, and the assistance-rule recommendation engine. Timeline covers all 9 requested events; completion gained two optional dimensions that are inert until enabled. Sync and backup/restore cover every new table except visit photos (Decision #25).

| 58 | Admin screens reuse `Enterprise/FrmRules` + `EntPrompt.Edit` instead of new Designer forms | Four admin screens with near-identical shape (grid + search + add/edit/toggle) already had a proven pattern in the codebase. New Designer files would have added ~1,500 lines of generated layout and a second UI idiom to maintain. | 2026-09-04 | active |
| 59 | Funding sources and sponsors are deactivated, never deleted | Both are referenced by `TblCaseFunding` with RESTRICT. Deletion would either fail or orphan case funding history; `IsActive = 0` keeps pick-lists clean while preserving the past. | 2026-09-04 | active |
| 60 | Dashboard gets a new tab rather than more cards in the existing grid | The 13 existing `StatCard`s sit in a fixed grid keyed to `SummaryCardCount`; changing its geometry has no test coverage and previously caused a blank-cards bug. The user's own guidance allowed a tabbed section. Total/Active/Pending/Suspended were found already implemented and deliberately **not** duplicated. | 2026-09-04 | active |
| 61 | Export layer returns `DataTable`, not DTOs/ViewModels | All three consumers — `ExcelReportExporter`, `PrintHelper.PrintDataTable`, and RDLC's `ReportDataSource` — take `DataTable`. DTOs would force every consumer to convert back, and would make the future RDLC wiring harder rather than easier. | 2026-09-04 | active |
| 62 | Per-case export lives in a new `CaseFileExportService`, not inside `ExcelReportExporter` | That class produces a *multi-case* workbook; per-case output is a different data shape. Merging them would complicate a working exporter for no gain. | 2026-09-04 | active |
| 63 | `ReportBuilderForm_ConstructsWithoutError` assertion changed from "exactly 6 sources" to per-key presence checks | The count assertion broke on every legitimate source addition while guaranteeing nothing — 6 *wrong* sources would still pass. Presence checks are strictly stronger: they also fail if a source is removed or renamed, which is what actually breaks saved user report templates. | 2026-09-04 | active |

| 64 | Case-screen status shown as a second `MkSummaryStat` row, not a redesigned header | Reuses the summary tab's existing stat-card idiom and leaves the original six-card row and its geometry untouched — the same reasoning that kept the dashboard's 13-card grid intact (#60). | 2026-09-04 | active |
| 65 | `FrmCase` calls `AssistanceRuleService.Evaluate()`, never `EvaluateAndLog()` | Opening a case is a read. Logging a rule match on mere display would fill the timeline with events the user never caused and make `ASSISTANCE_RULE_MATCHED` meaningless as an audit signal. | 2026-09-04 | active |
| 66 | Rule calculation detail lives in a tooltip, not a visible panel | The requirement is that the explanation be *available* read-only; a permanent panel would cost summary-tab space for information needed only occasionally. | 2026-09-04 | active |
| 67 | Document verification on the case screen is a `verified / total` indicator, not a second verification UI | `FrmDocs` (embedded in the docs tab) already owns per-document verification from Phase 5.5-A. A second set of verify controls would be two write paths to the same columns. | 2026-09-04 | active |
| 68 | "پرونده کامل" is a new button rather than changing `btnExportExcel` | `btnExportExcel` produces the multi-case filtered report; repurposing it would silently remove a working feature. Per-case and multi-case export are different deliverables. | 2026-09-04 | active |
| 69 | `txtMigrationCardType` moved into `migrantSectionFields` — the last Phase-3 always-visible carve-out | The Phase-3 comment at `FrmCase.cs:550` deliberately left `DisabilityType`/`DisabilityDegree`/`MigrationCardType` outside the section groups. The first two are gated by `chkHeadHealthy` and stay as-is; `MigrationCardType` had no gate at all and showed on all six request types. Safe because `SetFieldGroupVisible` only toggles `Visible`, and the column is written unconditionally, so legacy values on non-migrant cases round-trip untouched and still print. | 2026-09-04 | active |
| 70 | تبِ «مشخصات جسمی» حذف و به‌صورت کارتِ مستقل داخل تبِ «مشخصات پرونده» منتقل شد؛ فیلدهای اختصاصیِ هر نوع پرونده هم هرکدام کارتِ جدا گرفتند | خواستهٔ صریح کاربر. ادغام در سطحِ *کارت* انجام شد نه سطحِ فیلد: هیچ فیلدی بین شبکه‌ها جابه‌جا نشد، هر کارت کانتینرِ خودش را نگه داشت، پس `TabIndex` (که در `FrmCase.cs` با شمارندهٔ جداگانه به ازای هر گروه از صفر شروع می‌شود) دست‌نخورده ماند و تضادِ دو مدلِ کنترل (`chkHeadHealthy` در برابر `ShowDisabilitySection`) پیش نیامد. جانبی: چون چک‌باکس «سالم است» حالا کنار فیلدهای معلولیت دیده می‌شود، تلهٔ A1 بسیار کم‌خطرتر شد. | 2026-09-05 | active |
| 71 | تأکیدِ بصریِ «نوع پرونده» در `ApplyCustomTheme` اعمال می‌شود، نه در Designer | `UiTheme.ApplySweep` رنگِ **همهٔ** Labelها را روی `TextDark` بازنویسی می‌کند، پس هر `ForeColor` که در Designer داده شود بی‌صدا از بین می‌رود. این را آزمون کشف کرد، نه بازبینیِ چشمی. هر تأکیدِ رنگیِ آینده روی برچسب‌ها باید بعد از ApplySweep اعمال شود. | 2026-09-05 | active |

| 72 | `RdlcExportHelper.CaseDataSql` is the single definition of the report's case query; `FrmCaseReport` consumes it | Two independent definitions of the same query is what silently broke PDF/Word/batch export in Phase 7. One definition makes the divergence structurally impossible, and `CaseReportDataPathTests` fails the build if a declared RDLC field ever goes unsupplied. | 2026-09-05 | active |
| 73 | RDLC gained scalar fields + a label/value block only — no new DataSet or Tablix | Scalars fit in the free space below the existing content without moving anything. Repeating history rows would need layout reflow of a 2,000-line hand-authored report that cannot be visually verified headlessly. | 2026-09-05 | active |
| 74 | خروجیِ «پروندهٔ کامل» از ۸ بخش به ۱۳ بخش رسید: معلولیت، ایتام، مهاجرت، اعضای خانواده، نمایندهٔ قانونی | فاز ۵.۵-E نشان داد ۹ فیلدِ معلولیت و کلِ اطلاعاتِ نمایندهٔ قانونی در هیچ خروجیِ *تک‌پرونده‌ای* دیده نمی‌شدند (فقط گزارشِ اکسلِ چندپرونده‌ای معلولیت را داشت). بخش‌های تازه از همان الگوی `CaseExportDataProvider` می‌آیند، پس اکسل و چاپ هرگز واگرا نمی‌شوند. تایم‌لاین همچنان آخرین بخش است. | 2026-09-05 | active |
| 75 | قالب‌های Word عمداً ۲۰ نگاشتِ «مرده» دارند؛ اصلاحِ آن‌ها ویرایشِ فایلِ .docx است نه تغییرِ کد | نگاشتِ بدونِ متناظر بی‌ضرر است (`GetValue` برای ستون/مقدارِ غایب رشتهٔ خالی می‌دهد و `RemoveUnusedPlaceholdersEverywhere` باقی‌مانده را پاک می‌کند). ولی نتیجه‌اش این است که ۹ فیلدِ معلولیت و همهٔ `{{Rep*}}`ها در سندِ نهایی چاپ **نمی‌شوند** — ادعای «به خروجی Word اضافه شد» فقط در لایهٔ نگاشت درست است. جای درستِ رفع، قالب است. | 2026-09-05 | **superseded by #80** — قالب تکمیل شد |
| 76 | وارسیِ محتوایِ RDLC با `WORDOPENXML` انجام می‌شود، نه CSV | رندرکنندهٔ CSV در این نسخهٔ ReportViewer پشتیبانی نمی‌شود (`ArgumentOutOfRangeException`), و حتی اگر می‌شد فقط ناحیه‌های داده را می‌داد و کادرهای متنیِ مستقل — که همهٔ فیلدهای CaseData آن‌جا هستند — حذف می‌شدند. خروجیِ WORDOPENXML باز و متن‌کاوی می‌شود. | 2026-09-05 | active |
| 77 | `RptFullCase.rdlc` نه سند چاپ می‌کند نه عکس | `DocsData` تغذیه می‌شود ولی هیچ ناحیهٔ داده‌ای آن را مصرف نمی‌کند، و گزارش صفر عنصرِ `<Image>` دارد. سه فیلدِ عکس فقط اعلام شده‌اند. رفعش افزودنِ Tablix/Image به یک RDLC ۹۰ کیلوبایتی با چیدمانِ مطلق است — پرریسک‌ترین تغییرِ گزارشیِ ممکن. خروجیِ Word عکس‌ها را چاپ می‌کند. | 2026-09-05 | active |
| 78 | Word placeholder *mappings* shipped without the template edit | `FullCaseTemplate.docx` is a binary I cannot author. Mapping an absent placeholder is a no-op, so the code half can land safely now and the values appear the moment the placeholders are inserted. | 2026-09-05 | **superseded by #80** — قالب تکمیل شد |
| 79 | Drill-down routes to `FrmAdvancedSearch`, not a new `FrmCase` overload | The band and completion filters already exist there, so this reuses working filters instead of duplicating them, and avoids widening the public surface of a 6,100-line form that a parallel session also edits. | 2026-09-05 | active |
| 80 | `FullCaseTemplate.docx` تکمیل شد: ۹ فیلدِ جزئیاتِ معلولیت، ۱۶ فیلدِ نمایندهٔ قانونی و ۸ فیلدِ وضعیتِ فاز ۵.۵-D به قالب افزوده شدند | تا پیش از این نگاشت در Exporter بود ولی قالب نداشت، پس هرگز چاپ نمی‌شد — «افزوده شد» فقط در لایهٔ نگاشت درست بود. ردیف‌ها با **کلونِ ردیفِ موجودِ همان جدول** ساخته شدند، پس عرضِ ستون‌ها، رنگ‌ها، اندازهٔ قلم و `<w:rtl/>` عیناً حفظ شد؛ جدولِ تازهٔ «نمایندهٔ قانونی» از `tblPr` همان جدول (شاملِ `<w:bidiVisual/>`) کلون شد. فیلدِ بی‌داده ردیفِ خالی می‌گذارد — همان رفتارِ از پیش موجودِ `{{StopReason}}`. | 2026-09-05 | active |
| 81 | `RptFullCase.rdlc` **بدون تغییر** به نسخه می‌رود: نه `DocsData` رندر می‌شود نه عکس | هر دو نیازمندِ افزودنِ Tablix/Image به یک RDLC ۹۰ کیلوبایتی با چیدمانِ مطلق‌اند — پرریسک‌ترین تغییرِ گزارشیِ موجود، بدون پوششِ آزمونِ چیدمان. ارزشِ کسب‌وکاری هم پایین است: فهرستِ اسناد در اکسل و چاپ کامل موجود است و عکس‌ها در خروجیِ Word چاپ می‌شوند. تصمیمِ آگاهانه، نه غفلت. | 2026-09-05 | active |
| 82 | شماره‌گذاریِ جدولِ تصمیم‌ها سراسری و یکتا شد (۱..۷۹) و ارجاع‌های مبهم اصلاح شدند | هفت بلوکِ الحاقی هرکدام از نو شماره‌گذاری کرده بودند، پس ۱۶ شماره تکراری بود و ارجاعی مثل «Decision #32» به پنج ردیفِ متفاوت می‌خورد. هیچ ردیفی حذف یا بازنویسی نشد؛ فقط ستونِ شماره و سه ارجاعِ مبهم. نگاشتِ کامل در `RELEASE_CLOSURE_REPORT.md`. | 2026-09-05 | active |
| 83 | `{{MigrationCardType}}` در قالب Word از جدولِ «مشخصات سرپرست» به جدولِ «وضعیت اجتماعی و خدمات» منتقل شد، کنارِ فیلدهای وابسته به نوع پرونده | قرینهٔ تصمیم #۴۲ در سمتِ فرم: این فیلد مخصوصِ پروندهٔ مهاجر است و جایش کنارِ «نوع درخواست» و بلوکِ معلولیت است، نه در مشخصاتِ هویتیِ سرپرست. `{{DocsCount}}` به جفتِ آزادشده منتقل شد تا ردیف نیمه‌خالی نماند. نکتهٔ فنی: سلولِ `{{DocsCount}}` در دو run جدا بود، پس جایگزینیِ متن باید **سلول‌به‌سلول** انجام شود (اولین run مقدار می‌گیرد، بقیه خالی می‌شوند)؛ جایگزینیِ موقعیتیِ ساده تکهٔ `DocsCount}}` را جا می‌گذارد. جدولِ سرپرست ۵ سلول دارد (ستونِ عکسِ ادغام‌شده) که باید دست‌نخورده بماند. | 2026-09-05 | active |
| 84 | خروجی Word ردیف‌های بی‌داده را حذف می‌کند — `OpenXmlCaseExporter.PruneEmptyPlaceholderRows` — ولی **فقط در سطحِ مقادیرِ پرونده، نه در بلوکِ کلونِ اعضا** | ردیفی فقط وقتی حذف می‌شود که هر سه شرط برقرار باشد: دستِ‌کم یک placeholder داشته باشد، **همهٔ** placeholderهایش در دیکشنری کلید داشته باشند، و **همه** خالی باشند. شرطِ اول بلوکِ «تأیید و امضا» و «ارزیابی وضعیت خانواده» را نجات می‌دهد (placeholder ندارند). شرطِ دوم `{{HeadPhoto}}`/`{{FamilyPhoto}}`/`{{MemberPhoto}}`/`{{LocationLink}}` را مستثنا می‌کند چون مسیرِ دیگری پرشان می‌کند. آخرین ردیفِ هر جدول هرگز حذف نمی‌شود (جدولِ بی‌ردیف در OpenXML نامعتبر است). **چرا در `FillFamilyBlock` اجرا نمی‌شود:** ردیف‌های عضو در «الگوی شماره ۱» کنارِ placeholder محتوای ثابتِ فرم دارند («خصوصی ☐ دولتی ☐»، «معدل سال قبل:»، «ترک تحصیل ☐ دلیل:») که با دست پر می‌شوند؛ حذفشان بخشی از فرمِ چاپی را نابود و بلوکِ اعضا را ناهم‌اندازه می‌کرد. «الگوی شماره ۲» سود می‌برد چون جدولِ ثابتِ ۹ردیفی‌اش از `caseValues` پر می‌شود، نه از کلون. | 2026-09-05 | active |
| 85 | «الگوی شماره ۱» و «الگوی شماره ۲» هم تکمیل شدند: هر کدام ۱۴ ردیف تازه — ۱۱ فیلد معلولیت و ۱۶ فیلد نمایندهٔ قانونی | تا پیش از این هر دو صفر placeholderِ `{{Rep*}}` داشتند و الگوی ۱ فقط `{{DisabilityDegree}}`. ردیف‌ها با کلونِ یک ردیفِ موجودِ همان جدول ساخته شدند، پس شبکه، حاشیه، فونت و `<w:rtl/>` عیناً حفظ شد؛ سلولِ پنجم (ستونِ عکس با `vMerge`) دست‌نخورده ماند تا ادغامِ عمودی نشکند. **افزودنِ این ردیف‌ها فقط به‌خاطر تصمیم #۸۴ بی‌خطر است:** برای پرونده‌ای که نماینده یا معلولیت ندارد هر ۱۴ ردیف در زمانِ خروجی حذف می‌شوند، پس «الگوی شماره ۲» که عمداً فرمِ فشردهٔ تک‌صفحه‌ای است برای پنج نوعِ دیگرِ پرونده فشرده می‌ماند (خروجیِ پروندهٔ خالی: ۷ ردیف). | 2026-09-05 | active |

**Rejected alternatives**
- Converting `txtRequestType`/`txtServiceStatus` combos to ID-bound `DataSource` controls — rejected: too much blast radius across `FrmCase.cs`; resolved by Name-lookup at save time instead.
- Full `TblCaseDocument` table (per the earlier Phase 2 design draft) — superseded by decision #2/adding `DocumentCategoryID` directly to the existing `TblDocs`.

**Known technical debt**
- `TblAuditLog` and `TblAuditLogs` are two overlapping audit tables — not consolidated (pre-existing).
- `Forms/` folder is empty; all forms live at project root (harmless but confusing).
- `FrmCase.cs` (~6,100 lines) and `FrmDashboard.cs` (~179 KB) are very large single files — any change here needs the full test run.
- `RptFullCase.rdlc` supplies a `DocsData` source that no Tablix renders (see Phase 5.5-E) — must keep being supplied, but produces no output.

---

# PROJECT GLOSSARY

| Term | Definition |
|---|---|
| Case (پرونده) | A household/beneficiary record — the root entity (`TblCase`). |
| Request Type (نوع درخواست) | Primary case classification: Orphan, Unsupported Child, Badly Supported Child, Disabled, Migrant, Elderly. |
| Service Status (وضعیت خدمات) | Primary workflow state of a case: Applicant → Under Review → Pending Approval → Active → Temporarily Suspended / Suspended. |
| Center (مرکز) | A physical branch office; `TblCenter`/`CenterID` scopes most data for multi-center isolation. |
| GlobalID | A GUID stamped on sync-eligible rows — the identity used across offline-sync branches (never the local integer PK). |
| Dual-write | Phase 3 pattern: writing both a legacy TEXT column and a new FK/ID column for the same fact, to avoid rewriting ~150 existing consumers. |
| Outbox (SyncOutbox) | Local queue of pending changes waiting to sync to the head office/server. |
| Timeline (تایم‌لاین) | `TblCaseTimeline` — the Phase 3 append-only, central event log for a case (case/status/document/payment/visit events). |

---

# AI INSTRUCTIONS

## Before Any Task
- Read this file completely.
- Treat it as the primary source of project knowledge.
- Follow all rules and constraints defined here.

## Repository Navigation
- Never perform a full repository scan by default.
- Inspect only files related to the task.
- Prefer targeted searches.
- Expand search scope only when required by the task.
- Large-scale analysis requires explicit user approval.

## Code Changes
- Make minimal changes.
- Preserve existing architecture.
- Avoid unnecessary refactoring.
- Avoid rewriting working code.
- Remember: old-style `.csproj` — new `.cs` files need a manual `<Compile Include>` entry or the build fails.

## Database Safety
- Never modify schema without approval.
- Never delete data without approval.
- New columns: always `EnsureColumn`, never a bare `ALTER TABLE`.
- Never `ALTER TABLE RENAME` on `TblCase` (breaks child FKs) — see Migration Rules.

## Analysis Rules
- Explain impacted files before changing them.
- Identify risks before implementation.
- Prefer evidence from code over assumptions.

## Output Rules
- Be concise.
- Show affected files.
- Show exact changes.
- Avoid unrelated recommendations.

## Memory Maintenance (part of every task)
Update this file when any of the following changed:
- Architecture
- Database schema
- Business rules
- Modules added or removed
- Development rules
- Build or deployment process

Also:
- Add important project facts discovered during work, after verifying them in code.
- Remove obsolete information instead of appending replacements.
- Never duplicate information already in this file.
- Update the "Last updated" field.
- Keep under 800 lines; facts only — no speculation, no tutorials.
- Never ask the user to update this file manually. Documentation maintenance is part of the task.

---

# QUICK REFERENCE

- Build: `& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "C:\Projects\CaseManagement\CaseManagement.csproj" /t:Build /p:Configuration=Debug /v:minimal /nologo`
- Run: `C:\Projects\CaseManagement\bin\Debug\CaseManagement.exe`
- Test: `vstest.console.exe "C:\Projects\CaseManagement.Tests\bin\Debug\net472\CaseManagement.Tests.dll" /Parallel` (kill stray `vstest.console.exe` first)
- DB file / connection: SQLite file via `App.config` connection string `CaseDb` (path resolved by `DAL/DatabaseHelper.cs`)
- Config file: `App.config`
- Logs: `%APPDATA%\CaseManagement\*.log` (default; overridable via `SettingsHelper.LogsPath`)
- Entry point: `Program.cs`
