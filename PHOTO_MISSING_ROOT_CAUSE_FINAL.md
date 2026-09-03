# Photo Missing — Root Cause Final Report

**Follow-up to:** [PHOTO_AND_FAMILY_LINKING_AUDIT.md](PHOTO_AND_FAMILY_LINKING_AUDIT.md)
**Method:** This report supersedes one specific conclusion from that earlier audit, based on new hard evidence found after locating the actual database this real package was imported into. No code was changed. No fixes applied.

**Headline correction:** the earlier audit flagged the `"Family Photos"` vs `"FamilyPhoto"/"FamilyPhotos"` folder-name mismatch as the likely (high-confidence) cause of missing photos. **Direct evidence now disproves that for the actual import that ran.** The real cause is different and, in one respect, more concerning: files can silently vanish from the pipeline even when the folder name, the case code, and the image file are all completely correct.

---

## Evidence base for this report

- **Live database located:** `C:\Projects\CaseManagement\bin\x64\Debug\CaseDB.sqlite` (437 `TblCase` rows) — this is the actual database the `SyncPackage` in the previous audit was imported into. Confirmed by exact match: all 10 codes from `Family Photos\` (16092, 16093, 16094, 16098, 16099, 16100, 16102, 16103, 16104, 16146) exist as `TblCase.Code` values here.
- **`TblAuditLog`** — persisted, timestamped record of every HTML sync and every media sync run, including aggregate counts.
- **Filesystem timestamps** on both the source package (`SyncPackage\Family Photos\*.jpg`) and the copied output (`CaseFiles\<code>\<code>-FamilyPhoto\<code>-Family.jpg`).
- **Image integrity check** (Pillow) on all 10 source files — all valid, readable JPEGs, no corruption.

---

## 1. Was the `Family Photos` folder skipped?

**No — disproven by direct evidence.**

If `MediaScanner.Scan()`'s whole-package auto-discovery had been used (the mechanism that requires an exact folder name `FamilyPhoto` or `FamilyPhotos`), the outcome for a folder named `Family Photos` would be strictly all-or-nothing: either the folder is found (name matches) and every file in it gets scanned, or it isn't found and **zero** files in it are ever touched.

What actually happened: **5 of the 10 files in `Family Photos\` were successfully imported** — real copies exist on disk, real `TblCase.FamilyPhotoPath` values point to them, and the copies are dated **2026-08-18 15:01:59–15:02:30**. A whole-folder skip cannot produce a 5/10 split. The folder was found and scanned.

## 2. Image counts by folder name

| Folder (exact name) | Exists on this machine? | Image count |
|---|---|---|
| `Family Photos` (with space, the real folder) | Yes — `C:\Users\Mohammad\OneDrive\Desktop\SyncPackage\Family Photos\` | **10** (16092, 16093, 16094, 16098, 16099, 16100, 16102, 16103, 16104, 16146) |
| `FamilyPhoto` (singular, no space — the "official" name `MediaScanner` looks for) | **Does not exist anywhere on this machine** (checked Desktop, OneDrive Projects, Managements deployment) | 0 |
| `FamilyPhotos` (plural, no space — the "legacy" name `MediaScanner` falls back to) | **Does not exist anywhere on this machine** | 0 |

**Implication:** no folder with either of the two names `MediaScanner.Scan()` recognizes has ever existed on this machine. If the Wizard's whole-package auto-discovery path (`FrmSyncWizard` → `MediaScanner.Scan(rootFolder,...)`) were ever used for family photos, it would find **nothing**, 100% of the time, regardless of this specific bug — because the source folder is consistently named `Family Photos`. This is a real latent defect (confirmed in the prior audit), but it is not what produced the gap being investigated here, because the evidence shows a *different* code path was actually used (see §6).

## 3. How many images were imported

- **Within the sample folder (`Family Photos`, 10 files):** **5 imported** — 16099, 16100, 16102, 16103, 16146. Verified by: `TblCase.FamilyPhotoPath` populated with a real path, and the target file physically exists at that exact path.
- **Database-wide** (437 cases currently in this DB, aggregating photos from all packages ever imported into it, not just this one folder): **316 of 437 cases (72.3%) have `FamilyPhotoPath` set**; all 316 of those files were verified to still exist on disk (0 broken links). **188 of 437 (43.0%) have a head/guardian `PhotoPath` set.**

## 4. How many images were ignored specifically because of the folder-name mismatch

**Zero, for this run.** The folder-name mismatch (§2) did not cause any of the 5 missing files in this sample. It remains a real, separate, latent bug (see §5's caveat and the original audit's RC-1) that would cause a 100% loss **if and only if** the Wizard's whole-package auto-discovery is used for a folder named `Family Photos` — but that is not what happened here.

## 5. Does the folder-name mismatch alone explain the missing ~30%?

**No.** Two independent lines of evidence rule it out as the (sole or main) explanation for the gap actually observed:

1. **It cannot produce a partial result.** The mismatch is a `Directory.Exists()` check on the whole folder — binary, not per-file. A 5/10 split is structurally impossible from this mechanism alone.
2. **The database-wide shortfall (121 of 437 cases, 27.7%, missing a family photo) is in the right order of magnitude to match a "~30% missing" impression**, but per §1–§4 it cannot be attributed to the naming bug, since the naming bug — if triggered — would produce a much larger, all-or-nothing loss for whichever packages went through the Wizard path, not a scattered per-file pattern.

The real explanation is in §8 below: a **silent partial-scan/partial-copy loss** that leaves no error for some files and a logged-but-undetailed error for others — occurring even when the code, the file, and the folder name are all correct.

## 6. Wizard import or Simple import?

**Strong evidence for `FrmSyncSimple` (the per-category folder picker), not `FrmSyncWizard`'s whole-package auto-discovery.**

Reasoning, all evidence-based:
- §2 established that no folder named exactly `FamilyPhoto` or `FamilyPhotos` has ever existed on this machine. If the Wizard's auto-discovery had been used against a root folder containing `Family Photos`, the result would be **0 imported, 0 attempted, 0 errors for family photos** — not 5 imported.
- `FrmSyncSimple.cs` has a dedicated "عکسِ جمعیِ خانواده (پوشه‌ی FamilyPhoto)" row where the user manually browses to **any** folder via `FolderBrowserDialog` and the code calls `MediaScanner.ScanFamilyPhotoOnly(folder, ...)` directly on whatever folder was picked ([FrmSyncSimple.cs:126-129, 388-389](Sync/FrmSyncSimple.cs:126)) — folder *name* is irrelevant in this path, only contents matter. This is fully consistent with a folder named `Family Photos` producing real, partial results.

**This does not rule out that the Wizard was used for the HTML (text) part of the sync** — `TblAuditLog` shows a "ویرایش (همگام‌سازی HTML)" entry immediately before each "همگام‌سازی رسانه" entry, which is the Wizard's own documented flow (HTML first, then media). It is plausible the operator used the Wizard for guardians/members and the Simple screen's per-category buttons for photos, or that the Wizard was used and its rescan-after-HTML-sync step is what actually invoked `ScanFamilyPhotoOnly` under the hood with a manually-configured, non-default folder mapping. **This exact detail — which literal screen the operator clicked through — cannot be fully reconstructed from disk artifacts alone** and should be confirmed with whoever ran the import.

## 7. Trace of `16099.jpg`

| Stage | Result | Evidence |
|---|---|---|
| **Folder detected?** | Yes | Source file present at `SyncPackage\Family Photos\16099.jpg` (847 KB… actually 420,369 bytes, valid JPEG, 1500×692) |
| **Scanned?** | Yes | It is one of the 5 that produced a real output file |
| **Matched?** | Yes | `TblCase` row exists: `CasID=19610, Code='16099', CenterID=1` |
| **Imported (copied + DB updated)?** | Yes | `TblCase.PhotoPath = C:\Projects\CaseManagement\bin\x64\Debug\CaseFiles\16099\16099-HeadPhoto\16099-Head.jpg` and `TblCase.FamilyPhotoPath = ...\16099\16099-FamilyPhoto\16099-Family.jpg` — **both** populated. Physical files confirmed present at both paths, copied 2026-08-18 15:02:30. |
| **Displayed?** | Would display correctly | `FrmCase.cs` loads photos via `LoadImageToPictureBox(savedFamilyPhotoPath, picFamilyPhoto)` ([FrmCase.cs:2339](FrmCase.cs:2339)), which does `File.Exists(filePath)` then `Image.FromStream(...)` ([FrmCase.cs:963-981](FrmCase.cs:963)). Since the file exists at the exact stored path, this case's family photo **will** render when case 16099 is opened in the form. |

**Conclusion for the traced sample: `16099.jpg` is not one of the missing/orphan cases at all — it succeeded end-to-end.** The task's example set (16099, 16100, 16102) turns out to be a mix: some of the cited examples fully succeeded (99, 100, 102, 103, 146) and some genuinely failed (92, 93, 94, 98, 104) within the very same folder — which is itself the most important finding of this report (§8).

## 8. Additional root causes beyond the folder-name mismatch

Breaking down the 10-file sample by exact outcome, using the presence/absence of the `CaseFiles\<code>\<code>-FamilyPhoto\` folder as a forensic marker (that folder is only created by `FileHelper.SaveFileToCaseFolder` → `BuildSectionFolderPath` → `Directory.CreateDirectory`, i.e., only when a copy is actually attempted):

| Codes | Outcome | Folder scaffold created? | File copied? |
|---|---|---|---|
| 16099, 16100, 16102, 16103, 16146 | **Success** | Yes | Yes |
| **16104** | **Matched but copy failed** | Yes (empty) | **No** |
| **16092, 16093, 16094, 16098** | **Never attempted at all** | **No** | No |

Both failure modes are genuine, distinct, additional root causes:

### 8a. Match-succeeded-but-copy-failed (confirmed real, mechanism identified)
16104's case-folder/section-folder scaffold was created (`16104-FamilyPhoto\`, `16104-HeadPhoto\` both exist, both empty) — meaning `MediaScanner` correctly matched it (`Code='16104'` exists, file is clean per the integrity check in the evidence base) and `MediaSyncEngine.Apply()` began the copy, but the copy never completed. This is exactly what `MediaSyncEngine.cs:92-96` counts as an **error**:
```csharp
else { report.Errors++; report.Add("عکسِ «" + item.FileName + "» ذخیره نشد: " + FileHelper.LastError); }
```
`TblAuditLog` confirms **162 such errors** were logged for this exact run (`'عکس: 504+0 | سند: 214+0 | خطا: 162'`, logged twice — identically — for two separate full re-syncs today, 2026-08-23 11:17:50 and 11:53:39, proving this is a **deterministic, reproducible** failure, not random I/O flakiness).

**The specific reason 16104's copy failed could not be determined** — `FileHelper.LastError` and `report.Add(...)`'s per-file message are only ever shown transiently in the sync UI (`Messages` list, [MediaSyncEngine.cs:95](Sync/MediaSyncEngine.cs:95)) and are **never persisted to any log file or database table**. Once the dialog closes, the specific reason for each of the 162 errors is unrecoverable — only the aggregate count survives, in `TblAuditLog.NewValue` as a plain string.

### 8b. Silent, complete non-attempt on correctly-named, correctly-matched files (new, higher-severity finding)
16092, 16093, 16094, 16098 have **no trace whatsoever** in `CaseFiles` — not even an empty folder. Yet:
- Their `TblCase.Code` rows exist and match exactly.
- Their source files (`16092.jpg` etc.) are valid, uncorrupted, cleanly named, sitting in the same folder as the 5 that succeeded.
- Nothing distinguishes them from the successful 5 at the data level.

This means these four files **never entered the scan's result list at all** — not classified as `NoMatch`, not `Duplicate`, not `Corrupt`, nothing. They are simply absent, with **zero diagnostic trace**. Given the pattern (these are the four files with the *latest* filesystem modification timestamps in the source folder — i.e., whatever process/loop was walking the folder appears to have stopped before reaching them), the most defensible explanation is that the **scan or import step was interrupted partway through** (cancelled by the operator, or the run ended/errored before completing enumeration of `Directory.GetFiles()`'s result), and no error, warning, or partial-completion notice was surfaced for the remainder. **This is inferred from the pattern, not proven from a log — there is no persisted evidence of a cancellation event**, because none is written to disk. This should be confirmed with whoever operated the import (did they close the app, click Cancel, or see any dialog mid-run?).

**Severity note:** 8b is more concerning than the folder-name mismatch it supersedes as "the" root cause, because it defeats *even a correctly named folder with correctly matching, valid files* — with absolutely no user-visible signal that anything was skipped. The Wizard/Simple screens' confirmation summaries only report counts for items that made it into the scan's result list; items that never made it in are invisible by construction.

### 8c. No persisted per-file failure log (systemic gap enabling 8a/8b to go undiagnosed)
Every layer of this pipeline reports *aggregate counts only* to durable storage (`TblAuditLog`). Per-file detail (`report.Messages`, `FileHelper.LastError`) lives only in memory for the duration of one UI session. This is why reconstructing today's exact 162 failures required this forensic session rather than a direct log lookup, and why it cannot be done for any past run.

### 8d. Filename suffix collisions (separate, latent — not proven active in this run)
A different folder found during this investigation, `C:\Users\Mohammad\OneDrive\Desktop\All\Cases\Photos1\`, contains files named `16092(1).jpg`, `16099(1).jpg`, etc. — the `(1)` suffix Windows appends to avoid overwriting an existing download/copy. `SyncCodeNormalizer.Normalize()` does not strip parentheses or their contents, so `"16092(1)"` would **never** match `TblCase.Code = "16092"` — a guaranteed `NoMatch` if this folder is ever used as a photo source. This folder was not the one imported in the run analyzed above, but it is sitting on disk as a landmine for a future import attempt.

---

## Summary — corrected root-cause ranking

| # | Cause | Status vs. prior audit | Confirmed for this run? |
|---|---|---|---|
| 1 | Folder-name mismatch (`Family Photos` vs `FamilyPhoto`/`FamilyPhotos`) | **Downgraded** — real latent bug, but proven *not* the cause of this run's gap | No (disproven by partial-success evidence) |
| 2 | Silent partial non-attempt on valid, matching files (§8b) | **New — primary finding of this report** | Yes, for 4 of 10 sampled files |
| 3 | Matched-but-copy-failed with no retrievable reason (§8a) | **New** | Yes, for 1 of 10 sampled files, and 162 total across this run |
| 4 | No persisted per-file failure log (§8c) | **New — systemic, enables 2 & 3 to stay invisible** | Yes, structural (verified absent in code and on disk) |
| 5 | `(1)`-suffix filename collisions (§8d) | Carried over from prior audit, now location-confirmed | Latent, not active in this specific run |

**Not implemented:** no code changes were made. This report is diagnostic only, per instructions.

## Open items needing your input

1. Do you (or whoever ran this import) recall clicking "Cancel," closing the app, or seeing an error dialog partway through the `Family Photos` import on 2026-08-18? This would confirm the §8b hypothesis directly.
2. Can you check whether the live production database (still not located on this machine — see the original audit's open question #2) shows the same pattern (some correctly-named, correctly-matched photos silently absent with no folder scaffold at all)? If so, §8b is very likely the dominant cause of your ~30% figure, not the folder-naming issue.
