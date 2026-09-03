# Photo & Family-Linking Forensic Audit

**Scope:** HTML import → record/family creation → photo import → photo storage → photo display, plus sync.
**Method:** Static code trace (no code changed) + inspection of a real sync package found on disk + queries against two candidate local databases.
**Status:** Audit only. No fixes applied. Where evidence was insufficient, this is stated explicitly rather than assumed.

---

## 0. What "عکس بی‌صاحب" actually means in this codebase

There is no string literally called "بی‌صاحب" in the UI resources. The phrase is the developer's own comment (`MediaModels.cs`... actually `Sync/FrmSyncWizard.cs:959`) describing photos whose filename-code did not match any `TblCase.Code`. In code this is the enum value `MediaAction.NoMatch`, surfaced to the user as:

- Wizard confirmation screen: `"• عکس با کدِ ناموجود: N (وارد نمی‌شود)"` ([FrmSyncWizard.cs:1380-1381](Sync/FrmSyncWizard.cs:1380))
- Per-item message: `"پرونده‌ای با کد «X» در دیتابیس نیست."` ([MediaScanner.cs:708-710](Sync/MediaScanner.cs:708))

So "orphan photo" = **a photo file whose filename (minus extension) did not resolve, via `TblCase.Code`, to any case row at the moment of scanning.** This is a *filename-matching* failure, not a database-integrity failure — the photo file itself is never "ownerless" in storage; it simply never got copied/linked because the scanner couldn't find a matching case.

---

## PART 1 — Photo Linking: Full Workflow Trace

There are **two structurally separate subsystems** in this codebase that people are likely to conflate. This is the single most important structural finding of the audit.

### 1A. HTML import (`HtmlSyncProvider.cs`) — text-only, no photo awareness at all

| Stage | Source | Destination | Table |
|---|---|---|---|
| Parse | `Guardians.html`, `FamilyMembers.html` — HTML `<table>` cells, matched by column header text | `SyncRecord.SourceValues` (in-memory dict, keyed by DB column name) | — |
| Guardian record | Row with header "کد عمومی" (or "کد اختصاصی"/"کد خانواده"/"کد فامیل"/"کد") | `TblCase` (insert or update) | `TblCase` |
| Family member record | Same "کد عمومی" column, repeated once per member row | `TblFamily`, linked via `CasID` | `TblFamily` |

Matching key for guardian↔case and member↔family: **`Code` string only** (`TblCase.Code`), compared after `SyncCodeNormalizer.Normalize()` (strips whitespace, Persian/Arabic digits → Latin, invisible bidi/format characters — [SyncCodeNormalizer.cs](Sync/SyncCodeNormalizer.cs)). Verified in [SyncComparer.cs:98,131](Sync/SyncComparer.cs:98).

**Critical fact, verified by reading the field maps directly ([HtmlSyncProvider.cs:41-107](Sync/HtmlSyncProvider.cs:41)):** the HTML files contain **no photo reference of any kind** — no filename, no photo ID, no path column. `GuardianFieldMap` and `MemberFieldRules` map only text fields (name, father's name, Tazkira number, birth date, phone, address, province/district, religion, marital status, service status). There is nothing in Part-1's HTML ingestion that touches a photo. Photo linkage is **entirely a separate, independent process** keyed only by filename.

### 1B. Photo import (`MediaScanner.cs` + `MediaSyncEngine.cs`) — filename-only, no HTML awareness at all

Photos are matched purely by **file naming convention**, scanned from a folder structure, against an in-memory index of `TblCase` built fresh at scan time (`LoadCaseIndex()`, [MediaScanner.cs:230-263](Sync/MediaScanner.cs:230)):

```sql
SELECT CasID, Code, CenterID, COALESCE(PhotoPath,''), COALESCE(FamilyPhotoPath,'')
FROM TblCase WHERE TRIM(COALESCE(Code,'')) <> ''
```

| Photo kind | Expected folder | Filename convention | Matching key | Destination column |
|---|---|---|---|---|
| Guardian ("head") photo | `Photos\` | `<Code>.jpg` | `TblCase.Code` (normalized) | `TblCase.PhotoPath` |
| Family group photo | `FamilyPhoto\` (or legacy `FamilyPhotos\`) | `<Code>.jpg` | `TblCase.Code` (normalized) | `TblCase.FamilyPhotoPath` |
| Individual member photo | `MemberPhotos\<Code>\` | `<MemberTazkiraNo>.jpg`, fallback `<MemberName>.jpg` | `TblCase.Code` (folder) → `TblFamily.MemberTazkiraNo` or `.MemberName` (file, within that case) | `TblFamily.MemberPhotoPath` |
| Document | `Documents\<Code>\` | any filename | `TblCase.Code` (folder) | `TblDocs` row |

Classification logic: [MediaScanner.cs:666-738](Sync/MediaScanner.cs:666) (`Classify`). Outcomes: `Add`, `Replace` (case already has a photo — requires explicit user confirmation, defaults to **unchecked**), `NoMatch` (code not found), `Duplicate` (>1 file for same code — **none** applied automatically), `OtherCenter` (case belongs to a different center than the current session), `Unsupported`/`Corrupt`/`InvalidName`.

Storage write: `MediaSyncEngine.cs:138,146,323` — `UPDATE TblCase SET PhotoPath=@P ...`, `UPDATE TblCase SET FamilyPhotoPath=@P ...`, `UPDATE TblFamily SET MemberPhotoPath=@P ...`, keyed by `CasId`/`FamID` resolved during the *scan*, not during display.

Display: forms (`FrmCase`/`FrmFamily`, not modified in this audit) read `TblCase.PhotoPath` / `TblCase.FamilyPhotoPath` / `TblFamily.MemberPhotoPath` directly — a plain file path column, no further matching occurs at display time. **If the path column is never populated (because the scan never found the file, or found the wrong file), display shows nothing — there is no secondary/fallback lookup at display time.**

### 1C. The two subsystems only meet through timing, not through data

`FrmSyncWizard.RunSync()` ([FrmSyncWizard.cs:1397-1470](Sync/FrmSyncWizard.cs:1397)) runs the HTML sync **first** (creates/updates `TblCase`/`TblFamily` rows), then **re-scans** the media folder a second time so that brand-new cases (which didn't exist in the DB at the first, pre-sync scan) can now match. This re-scan is a deliberate, documented bugfix ([FrmSyncWizard.cs:1432-1439](Sync/FrmSyncWizard.cs:1432)). It correctly solves "new case in this batch, photo for it in this batch" — but it does **not**, and cannot, solve the case where the photo's code has no corresponding case anywhere (HTML batch or existing DB) — that is a true `NoMatch`.

**Confirmed answer to the audit's Part 1 questions:** matching uses **`TblCase.Code`** exclusively for guardian/family-level photos, and **`TblFamily.MemberTazkiraNo`** (primary) / **`TblFamily.MemberName`** (fallback) for individual member photos, scoped to the case found by `Code`. **`CasID`, `FormNo`, `ApplicantID`, `FamilyID`, `NationalID`, and `FileName`-as-identifier are not used anywhere in this pipeline.** `FamilyID` (`FamID`) is a pure local autoincrement, never present in any import file, never used as a matching key.

---

## PART 2 — Orphan Photo Analysis

### What I found on disk (verified, not simulated)

A real, unmodified sync package exists at:
`C:\Users\Mohammad\OneDrive\Desktop\SyncPackage\` (contents dated 2026-08-08)

```
SyncPackage\
  Documents\      20 files  (documents, named "<Code>[-N].jpg", e.g. 23084-8.jpg)
  Photos\         19 files  (e.g. 410.jpg, 1449.jpg, 8552کد.jpg ...)
  Family Photos\  10 files  ← 16092, 16093, 16094, 16098, 16099, 16100, 16102, 16103, 16104, 16146
```

**This is the exact file set the task description's examples (16099.jpg, 16100.jpg, 16102.jpg) come from** — confirmed by direct match, not inference.

**No `Guardians.html` / `FamilyMembers.html` are present in this package.** This is a media-only package.

### Root cause candidate #1 — verified by code, high confidence: folder-name mismatch silently drops the entire "Family Photos" category

`MediaScanner.Scan(rootFolder, ...)` (the method used by the Wizard's "one package, one root folder" flow) looks for the family-photo folder using **exact, hardcoded names only**:

```csharp
public const string FamilyPhotosFolderName = "FamilyPhoto";       // official
public const string LegacyFamilyPhotosFolderName = "FamilyPhotos"; // legacy, no space
```
[MediaScanner.cs:42-43](Sync/MediaScanner.cs:42)

The real folder on disk is named **`Family Photos`** — plural **and with a space**. Neither `Directory.Exists("...\FamilyPhoto")` nor `Directory.Exists("...\FamilyPhotos")` matches `"...\Family Photos"`. Consequence, traced through [MediaScanner.cs:192-201](Sync/MediaScanner.cs:192):

```csharp
string familyFolder = Path.Combine(rootFolder, FamilyPhotosFolderName);      // "...\FamilyPhoto"  -> doesn't exist
if (!Directory.Exists(familyFolder))
    familyFolder = Path.Combine(rootFolder, LegacyFamilyPhotosFolderName);   // "...\FamilyPhotos" -> doesn't exist
bool hasFamilyPhotos = Directory.Exists(familyFolder);                       // false

if (hasFamilyPhotos)
    ScanFamilyPhotos(plan, familyFolder, byCode, progress, cancel);          // never runs
```

**If this package is run through the Wizard's whole-package auto-discovery, all 10 files under `Family Photos\` are never scanned at all.** They do not appear as `NoMatch`/orphan in the confirmation report either — they are invisible to the tool, silently, with zero warning. This is worse than being reported as an orphan: it produces **no error, no log line, no user-visible signal whatsoever.**

*(Note: `FrmSyncSimple.cs` has a separate, per-category upload button — "عکسِ جمعیِ خانواده" — where the user manually browses to **any** folder via `FolderBrowserDialog` and calls `ScanFamilyPhotoOnly(folder, ...)` directly ([FrmSyncSimple.cs:126-129,388-389](Sync/FrmSyncSimple.cs:126)). In that flow the folder's *name* is irrelevant — only its *contents* matter, and genuine `Code`-based `NoMatch` matching (root cause candidate #2 below) applies instead.)*

**I could not determine from the file system alone which of the two UI paths (Wizard auto-discovery vs. Simple per-category picker) was actually used to process this package** — that determines whether candidate #1 (silent, complete skip) or candidate #2 (genuine per-file `NoMatch`) is the operative mechanism for these specific 10 files. This requires user confirmation; do not assume either without asking.

### Root cause candidate #2 — verified by code, requires production data to quantify: genuine `Code` mismatch

If the folder *is* scanned (either name variant matches, or the Simple per-category flow was used), each file is matched against `TblCase.Code` after normalization. A `NoMatch` at this point means, per [MediaScanner.cs:700-711](Sync/MediaScanner.cs:700), one of:
- **True orphan**: no case with that code exists in the database at all (never imported, deleted, or the photo predates any case record).
- **False orphan**: a case exists but under a different `Code` value that normalization doesn't reconcile — e.g. leading zeros dropped/added, a code typo, or the code was assigned in a different numbering scheme than the photo filenames use (photos may be named after an *old system's* internal photo ID rather than this system's case `Code` — see Part 4).
- **Center mismatch**: a case exists with that exact code but belongs to a different center than the current session (`SecurityContext.CurrentCenterId`), and the user is not in "all centers" mode. This is reported separately as `OtherCenter`, not `NoMatch` — worth distinguishing in any user-facing "orphan" count, since it's not actually orphaned, just access-scoped.

### Exact counts — **not determinable from available data**

I checked two candidate local databases:

| Database | Location | `TblCase` rows | Codes 16092–16146 present? |
|---|---|---|---|
| Deployed app copy | `C:\Managements\مدیریت پرونده\CaseDB.sqlite` | **0** | — |
| Dev build copy | `C:\Users\Mohammad\OneDrive\Projects\CaseManagement\bin\Debug\CaseDB.sqlite` | 5 | — |

Neither database contains anywhere near the case volume implied by codes in the 16000s (a comment in `MediaScanner.cs:25` states a real production snapshot once had "۱۶۶۱ از ۱۶۶۱" — 1,661 of 1,661 — codes populated, which is consistent with a 16000-range codebase, but that database was not found on this machine). **The actual production `CaseDB.sqlite` that this SyncPackage was built against could not be located.** Categories A–E from the task (true orphan / false orphan / wrong-person / family-member / applicant) **cannot be counted without it.**

**What I need from you to complete Part 2's counts:** the path to the live production `CaseDB.sqlite` (or a current backup of it). Once available, this is a single query:
```sql
SELECT Code FROM TblCase WHERE Code IN ('16092','16093','16094','16098','16099','16100','16102','16103','16104','16146');
```
— codes that return no row are true orphans (category A); codes that *do* return a row would mean the photos should have matched and were misreported (pointing back to candidate #1, the folder-name bug) or matched under center-restriction (`OtherCenter`).

---

## PART 3 — Family Member Photo Logic

### 1. What uniquely identifies a family member

In the database: `TblFamily.FamID` (local autoincrement) is the only true unique identifier, but it **never appears in any import file** and cannot be used for matching from outside the app.

For *import-time* identification, the code uses, in order of preference ([MediaScanner.cs:438-533](Sync/MediaScanner.cs:438), comment at line 439-447 is explicit about why):
1. **`TblFamily.MemberTazkiraNo`** (national ID / Tazkira number), scoped to the case (`CasID`) — the intended primary key, chosen specifically because Persian names collide (two "Mohammad"s in one family) and half-space/diacritic variants break name matching.
2. **`TblFamily.MemberName`** (cleaned of a trailing "(id)" suffix and collapsed whitespace) — explicitly documented as a **fallback only**, kept for backward compatibility with older packages, and it emits a warning (`MatchedByName = true`) whenever used.

There is **no `FamilyID + Relation`** composite key in use, and no `NationalID` field distinct from Tazkira — Tazkira *is* the national identity document number here.

### 2. How imported photos attach

- **Family/group photo** → attaches to the **case** as a whole (`TblCase.FamilyPhotoPath`), not to any individual person.
- **Guardian/head photo** → attaches to the **case** (`TblCase.PhotoPath`).
- **Each family member photo** → attaches **individually** to that member's `TblFamily` row (`MemberPhotoPath`), via the `MemberPhotos\<Code>\<Tazkira>.jpg` convention.
- There is no separate "applicant" entity distinct from "guardian" in this schema — `TblCase` *is* the case/applicant/guardian record.

### 3. Disambiguating father / mother / child 1 / child 2

**This information does not exist in the HTML import at all.** Verified directly against `MemberFieldRules` ([HtmlSyncProvider.cs:78-86](Sync/HtmlSyncProvider.cs:78)): the members file provides `MemberName`, `MemberFatherName`, `MemberTazkiraNo`, `BirthDate`, `Gender`, `MemberSadat`. **There is no "relation to head" / "relation type" field anywhere in the parsed columns.** So the system cannot programmatically distinguish "father" from "mother" from "child N" from the HTML data — it only knows *that* a row belongs to a given family (via the repeated public code) and the member's own name/Tazkira/gender/birth date.

For **photos** specifically, disambiguation among multiple members in the same family is **not name-based or relation-based at all** — it is purely: *the photo file's name must equal (or, as fallback, resemble) that specific member's Tazkira number/name already on record in `TblFamily`.* If the photo file for "child 2" is misnamed, mislabeled with an old system's ID that doesn't equal the Tazkira in this DB, it will either attach to the wrong sibling (if two members share a similar cleaned name) or produce a `NoMatch`.

This directly matches the audit's concern: **the system has no independent way to verify a photo is attached to the "correct" family member beyond exact filename-to-Tazkira/name string equality.** There is no photo metadata, no face data, no cross-check.

---

## PART 4 — HTML Import Audit

Verified directly from `HtmlSyncProvider.cs` field maps:

| Data | Preserved? | Field / evidence |
|---|---|---|
| Original member identifier (from source system) | **No** | No ID column mapped anywhere in `MemberFieldRules`; only name/father/Tazkira/DOB/gender/Sadat text values are read. |
| Original family identifier (from source system) | **Partially** — only the "public code" (`کد عمومی`), which *is* re-used as `TblCase.Code`. No separate internal family ID from the source system is captured. | [HtmlSyncProvider.cs:145-150](Sync/HtmlSyncProvider.cs:145) |
| Original photo identifier | **No — never present in HTML at all.** Photos are correlated purely by a separate file-naming convention, entirely disconnected from the HTML parser. | Confirmed: zero photo-related fields exist in `GuardianFieldMap` or `MemberFieldRules`. |
| Record creation | Yes — one `TblCase` row per guardian row, keyed by public code | `SyncEngine.cs:101-119` |
| Family creation | Yes — one `TblFamily` row per member row, linked via `CasID` resolved from the same public code | `SyncEngine.cs:129-155` |
| Document creation | Not part of `HtmlSyncProvider` at all — handled entirely by `MediaScanner.ScanDocuments`, filename/foldername-based, same as photos | `MediaScanner.cs:313-369` |
| Attachment import | Same as documents — folder/filename based, no metadata from HTML | — |

**Key structural finding for Part 4:** the HTML importer and the photo/document importer are two independent pipelines that share exactly one thing — the **public code string**. Nothing else crosses between them. Any mismatch between how a case's `Code` is written in the HTML vs. how photo files are named for that same case will silently break photo linkage, and the HTML importer has no way to detect or report this (it doesn't look at photo files; the photo scanner runs separately and only sees the DB state after HTML import commits).

---

## PART 5 — Sync Audit

This codebase actually contains **two unrelated things both called "sync,"** and conflating them is a likely source of confusion in diagnosing this bug:

### 5A. Package/HTML sync (`FrmSyncWizard` / `FrmSyncSimple` + `HtmlSyncProvider` + `MediaScanner`/`MediaSyncEngine`)
This is the "import a folder of HTML + photos from the central system" flow described above (Parts 1–4). It is **one-directional** (external package → local DB) and **filename/code-string based** throughout. This is the flow most likely responsible for the reported orphan photos.

### 5B. Branch-to-branch DB sync (`SyncEngine`'s sibling `SyncApplier.cs`, `SyncOutboxService`, `SyncFile` table, `OfflineSyncInitializer`)
This is a **separate**, bidirectional, offline-capable sync mechanism between installations of the app itself (not from the central HTML export). It is architecturally more robust regarding file linkage:

- Records are identified by a stable **`GlobalID`** (not by the local autoincrement `CasID`/`FamID`), and local IDs are explicitly **never** overwritten by incoming data — [SyncApplier.cs:46-55](Sync/SyncApplier.cs:46) (`NeverWrite` set includes `CasID`, `FamID`, `GlobalID`).
- Files are **not** synced as raw path strings copied between machines (which would be meaningless — a local path on machine A means nothing on machine B). Instead there's a dedicated `SyncFile` table keyed by `(EntityGlobalID, ColumnName)` plus a content hash, explicitly designed so *"the matching key is not the path — a local path on another machine is meaningless"* ([OfflineSyncInitializer.cs:262-263](Sync/OfflineSyncInitializer.cs:262)).
- Conflict handling is explicit: a locally-pending unsent change blocks a remote overwrite and is recorded as a conflict rather than silently applied ([SyncApplier.cs:92-111](Sync/SyncApplier.cs:92)).

**Where IDs can change / linkage could break in 5B:** they don't, by design — `CasID`/`FamID` are excluded from every incoming write. The one true risk here is `TblCase.Code` **duplication across branches** (two offline branches independently creating the same human-typed code for two different cases) — handled explicitly as a recorded conflict, not a silent overwrite ([SyncApplier.cs:276-306](Sync/SyncApplier.cs:276)).

**Conclusion for Part 5:** the branch-to-branch sync subsystem (5B) is not implicated in the orphan-photo problem — its file-linkage design specifically avoids the failure mode described in the task. The **package/HTML import subsystem (5A) is where the risk lives**, precisely because it uses bare filename string matching with no stable identifier and no reconciliation step between the HTML content and the photo folder content.

---

## PART 6 — Root Cause Report

### 1. Exact root causes (ranked by confidence)

**RC-1 (confirmed by code + confirmed by matching real files on disk — HIGH confidence):**
`MediaScanner.Scan()`'s whole-package auto-discovery mode matches the family-photo subfolder by **exact, hardcoded name** (`"FamilyPhoto"` or legacy `"FamilyPhotos"`) with no tolerance for naming variants. A real, currently-existing package on this machine has the folder named **`Family Photos`** (space, plural) — which matches neither. If ingested via the Wizard's auto-discovery path, this silently drops the entire family-photo category with **zero diagnostic output** — not even counted as `NoMatch`/orphan. This is very likely a bigger contributor to "missing photos" than any per-file matching failure, precisely because it's invisible: nothing in the review screen tells the user this category was skipped.

**RC-2 (confirmed by code, unquantified due to missing production DB — MEDIUM-HIGH confidence):**
Genuine `Code`-based `NoMatch`: photo filenames don't correspond to any `TblCase.Code` currently in the database. Because photos carry **no identifier other than the filename itself**, and the HTML import carries **no photo identifier at all**, there is no cross-check possible between "this photo belongs to case X" as understood by the source system vs. as understood by this system's `Code` column. Any drift between the two — different numbering scheme, code typos, codes assigned after the photo was named, cases not yet imported — produces an orphan with no way to auto-recover.

**RC-3 (confirmed by code — MEDIUM confidence, contributes to *false* orphans among sibling members specifically):**
Member-photo matching falls back from Tazkira number to cleaned display name when Tazkira is absent/non-numeric. Two siblings with the same or near-identical name (a common real-world case per the code's own comment about "همه‌ی فرزندانِ آن خانواده کلید یکسان می‌گیرند") can cause a photo to attach to the wrong member, or fail to match either, depending on exact string equality after cleaning.

**RC-4 (structural, not a bug — architecture limitation, HIGH confidence):**
There is no independent, persistent, unique **photo identifier** anywhere in this pipeline. A photo's identity *is* its filename. This means: (a) any accidental rename of a source photo permanently orphans it with no recovery path, and (b) the audit's own Part 2 categories (A–E) cannot be answered definitively from data alone — they require re-deriving intent from filenames, which is inherently lossy.

### 2. False orphan rate / True orphan rate
**Not computable** — requires the live production `CaseDB.sqlite` that this real sync package (`Family Photos\16092.jpg` etc.) was built against. Not present on this machine (checked: app deployment folder, dev build folders, ClickOnce AppData locations, OneDrive project folder — none contain those case codes). See Part 2 for the exact query to run once you provide/point to that database.

### 3. Missing linkage information
- No original photo ID from the source system (never captured).
- No "relation to head of family" field in the HTML import (father/mother/child cannot be distinguished programmatically).
- No stable member ID from the source system (Tazkira is used as a proxy, but is sometimes free text like "الی برج ۶ سال ۱۴۰۳ تذکره دریافت خواهد شد" instead of a real number — handled defensively by `IsRealTazkira()`, but that defense means such members fall back to name-based keys, which is weaker).

### 4. Recommended matching strategy *(recommendation only — not implemented)*
- Make family-photo folder discovery tolerant of naming variants (case-insensitive, space/underscore-insensitive) rather than exact-string, OR fail loudly ("found a folder named X, expected FamilyPhoto — rename it or confirm") instead of silently treating it as absent.
- Surface a distinct "category not found" warning (separate from "0 files matched") in the Wizard summary so a missing subfolder is never indistinguishable from an empty one.
- Before/after any package scan, generate a diff report the user can act on: filenames present in a photo folder with zero corresponding `TblCase.Code`, listed explicitly (this exists today only as an aggregate count, not a list, in the Wizard path — the Simple path does show per-item detail).

### 5. Recommended family-member photo strategy *(recommendation only)*
- If the source (central) system can export an original photo identifier or original member identifier alongside the HTML, capturing it (even in an unused "Details"/notes column, as the code already does for the duplicate name/DOB/Tazkira columns) would make future reconciliation possible. Currently nothing preserves this even when present in the source.

### 6. Required code changes
None made. Per task instructions, this audit performed no fixes.

### 7. Risk level per finding

| Finding | Risk if left unaddressed |
|---|---|
| RC-1 (family-photo folder name mismatch, silent skip) | **High** — silent, undetected data loss; already reproduced with real files on this machine |
| RC-2 (genuine Code mismatch, unquantified) | **High** — but severity depends entirely on the true/false orphan ratio, which needs production data to establish |
| RC-3 (sibling name-collision fallback matching) | **Medium** — narrower blast radius (only affects families with Tazkira-less, similarly-named children) |
| RC-4 (no persistent photo identifier — architectural) | **Medium-High**, long-term — every future import inherits this same fragility until a stable identifier is introduced |

---

## Open questions requiring your input (not guessed)

1. Was the `SyncPackage` folder found on disk processed via the **Wizard's whole-package auto-discovery** (single root folder picker) or via **`FrmSyncSimple`'s per-category folder pickers**? This determines whether RC-1 or RC-2 is the operative failure for the 10 "Family Photos" files specifically.
2. Where is the **live production `CaseDB.sqlite`** actually located/hosted? Neither database found on this machine contains case codes in the 16000 range. I need this to compute exact true/false-orphan counts (Part 2).
3. Does the central/source system have any *original photo identifier or original member identifier* it could export alongside the HTML, even if this app doesn't currently store it?
