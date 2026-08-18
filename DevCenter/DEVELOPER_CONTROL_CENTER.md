# Developer Control Center — User Guide

**Module:** Hidden Developer Control Center
**Application:** Guardian / CaseManagement (WinForms, .NET Framework 4.7.2, SQLite)
**Audience:** Super Administrators and technical support staff
**Window title (on screen):** `مرکز کنترل توسعه‌دهنده`

---

## Table of Contents

1. [What This Module Is](#1-what-this-module-is)
2. [How to Open It](#2-how-to-open-it)
3. [Who Can Open It](#3-who-can-open-it)
4. [Window Layout](#4-window-layout)
5. [Tab 1 — System Overview](#5-tab-1--system-overview)
6. [Tab 2 — Database Doctor](#6-tab-2--database-doctor)
7. [Tab 3 — Maintenance](#7-tab-3--maintenance)
8. [Tab 4 — Log Center](#8-tab-4--log-center)
9. [Tab 5 — Diagnostics](#9-tab-5--diagnostics)
10. [Tab 6 — Database Explorer](#10-tab-6--database-explorer)
11. [Tab 7 — Developer Tools](#11-tab-7--developer-tools)
12. [Support Package](#12-support-package)
13. [Exporting Data](#13-exporting-data)
14. [Auditing — What Gets Logged](#14-auditing--what-gets-logged)
15. [Behaviour on Damaged or Old Databases](#15-behaviour-on-damaged-or-old-databases)
16. [Common Workflows](#16-common-workflows)
17. [Troubleshooting](#17-troubleshooting)
18. [Safety Rules](#18-safety-rules)
19. [Technical Reference](#19-technical-reference)

---

## 1. What This Module Is

The Developer Control Center is a **hidden diagnostic and maintenance console** built into the
application. It is intended for technical support and system administration, not for daily users.

It answers questions such as:

- Is this installation healthy? How healthy, and why?
- Is the database intact? Are there orphan records, broken references, missing files?
- What errors have occurred recently? Who did what, and when?
- What exactly is this machine running (Windows, .NET, SQLite versions)?
- What is actually stored in a given table right now?
- Can I safely compact the database, rebuild indexes, or clear temporary files?
- Can I hand the developer one file that contains everything needed to diagnose a problem?

**It does not appear anywhere in the normal user interface** — not in the sidebar, dashboard,
menus, settings, ribbon, toolbar, search, or navigation. It is opened by one hidden keyboard
shortcut and is available only to a Super Administrator.

---

## 2. How to Open It

Press:

```
Ctrl + Shift + Alt + D
```

Notes:

- The shortcut works from **any window** in the application (dashboard, case form, settings, etc.).
  It is installed as an application-wide keyboard message filter at startup.
- The `D` stands for *Developer*. The combination is deliberately four keys so it cannot be
  pressed by accident and does not collide with any standard Windows or application shortcut.
- **Single instance:** if the window is already open, pressing the shortcut again brings the
  existing window to the front instead of opening a second copy.
- The window opens **maximized** and is **non-modal** — you can keep using the rest of the
  application while it is open.
- To close it, use the normal window close button. Nothing is left running in the background.

---

## 3. Who Can Open It

| Role | Result of pressing the shortcut |
|---|---|
| **SuperAdmin** (`مدیر کل`) | Developer Control Center opens |
| Admin (`مدیر سیستم`) | Nothing happens |
| Operator (`کاربر عملیاتی`) | Nothing happens |
| Viewer (`ناظر`) | Nothing happens |
| Not logged in | Nothing happens |

For every non-SuperAdmin the shortcut is a **completely silent no-op**: no window, no message,
no error, no beep, no log entry. The key press is passed through to the application untouched, so
the user cannot even tell that the shortcut means anything. This is deliberate — the feature must
not be discoverable by experimentation.

Two independent layers enforce this:

1. The keyboard filter checks the role before doing anything.
2. The window itself re-checks the role in its constructor and closes immediately if the check
   fails, even if something tried to open it directly.

> **Note:** Access is tied to the **role**, not to the shortcut. Knowing the key combination is
> useless without a Super Administrator account.

---

## 4. Window Layout

```
┌──────────────────────────────────────────────────────────────────────┐
│  [ Overview | Doctor | Maintenance | Logs | Diagnostics | Explorer |  │
│    Developer Tools ]                                    ← tab strip  │
├──────────────────────────────────────────────────────────────────────┤
│  Toolbar (buttons for the current tab)                               │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Tab content — cards, grids, or output log                           │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│  hh:mm:ss  status message   |  user: xxx | machine: xxx   [progress] │
└──────────────────────────────────────────────────────────────────────┘
```

**Status bar** (bottom): shows the timestamped result of the last action, plus the current user
name and machine name — useful when taking screenshots for a support ticket.

**Progress bar** (bottom right): appears only while a long operation is running. While it is
visible, the tab strip is disabled to prevent starting a second operation at the same time. The
application does **not** freeze — long work runs on a background thread.

The interface is right-to-left and uses the application's standard fonts and colours. There is no
custom theming.

---

## 5. Tab 1 — System Overview

**Tab label:** `نمای کلی`

The landing tab. Gives a one-screen answer to "is this installation healthy?"

### Health Score

A progress bar from 0 to 100 with a text summary and a colour:

| Score | Rating (on screen) | Bar colour |
|---|---|---|
| 85–100 | `عالی` — Excellent | Green |
| 70–84 | `قابل قبول` — Acceptable | Amber |
| 50–69 | `نیازمند رسیدگی` — Needs attention | Red |
| 0–49 | `بحرانی` — Critical | Red |

The score is **not arbitrary**. It starts at 100 and subtracts measured penalties:

| Condition | Penalty |
|---|---|
| Database status is not healthy, or cannot be read | −40 |
| Unresolved errors in the error log | −1 per error, capped at −20 |
| Orphan records (cases with no family members) | −1 per record, capped at −15 |
| Missing files (recorded in the database, absent on disk) | −1 per file, capped at −10 |
| No backup has ever been recorded | −10 |
| A backup exists but was not taken today | −5 |

Every penalty that was applied is listed in parentheses next to the score, so the number is always
explainable.

> **Important:** a metric that **could not be measured** is never penalised. On an older database
> where a table is missing, that check contributes nothing to the score instead of dragging it
> down. This prevents a healthy-but-old installation from being reported as "critical".

### Information Cards

| Card (Persian label) | Meaning |
|---|---|
| `نسخهٔ نرم‌افزار` | Application assembly version |
| `نسخهٔ دیتابیس` | SQLite `user_version` and `schema_version` pragmas |
| `وضعیت دیتابیس` | Result of `PRAGMA quick_check` — green if `سالم` (healthy) |
| `حجم دیتابیس` | Size of `CaseDB.sqlite` on disk |
| `مجموع رکوردها` | Total row count across every table |
| `تعداد کاربران` | Rows in the users table |
| `کاربران فعال` | Distinct users currently holding a non-expired record lock |
| `مصرف حافظه` | Working set of the application process |
| `مصرف فضای ذخیره‌سازی` | Used / total space on the application drive, with percentage |
| `مدت اجرای برنامه` | Time since the process started (`hh:mm:ss`) |
| `وضعیت کارایی` | Performance rating derived from the health score |

Any card that cannot be computed shows `در دسترس نیست` ("Not available") instead of a wrong value.

> **About "Active users":** this application has no session or heartbeat table, so a true
> concurrent-user count does not exist. This card shows the closest real measurement available —
> distinct users holding an active record lock. Treat it as an indicator, not an exact figure.

### Buttons

| Button (Persian) | English | Action |
|---|---|---|
| `تازه‌سازی` | Refresh | Recomputes every card and the health score |
| `ساخت بستهٔ پشتیبانی` | Build Support Package | See [Support Package](#12-support-package) |

---

## 6. Tab 2 — Database Doctor

**Tab label:** `دکتر دیتابیس`

Runs 11 independent health checks against the database and file storage.

**This tab is empty until you press the run button** — the checks are expensive and are not run
automatically when the window opens.

### Buttons

| Button (Persian) | English | Action |
|---|---|---|
| `اجرای بررسی کامل` | Run Full Check | Runs all 11 checks in the background |
| `خروجی CSV` | Export CSV | Saves the result grid to a CSV file |

### The 11 Checks

| # | Check (Persian) | What it verifies |
|---|---|---|
| 1 | `یکپارچگی دیتابیس` | SQLite `PRAGMA integrity_check` — structural corruption |
| 2 | `کیفیت داده` | Cases with incomplete or invalid field data |
| 3 | `رکوردهای تکراری` | Suspected duplicate case pairs (name/ID/phone similarity) |
| 4 | `ارجاع‌های شکسته` | Family / document / assistance rows pointing at a case that no longer exists |
| 5 | `رکوردهای یتیم` | Cases with no family members recorded |
| 6 | `اسناد گمشده` | Document file paths in the database that are absent on disk |
| 7 | `عکس‌های گمشده` | Head, family, and member photo paths that are absent on disk |
| 8 | `پیوست‌های بدون استفاده` | Files on disk with no matching database record |
| 9 | `شماره‌گذاری نامعتبر` | Empty or duplicated case codes / form numbers |
| 10 | `سازگاری بایگانی` | Archive flag set without archive date/user, or vice versa |
| 11 | `سازگاری بارکد` | Case codes that are empty or contain characters Code128 cannot encode |

### Result Columns

| Column | Meaning |
|---|---|
| `بررسی` | Check name |
| `نتیجه` | Human-readable result |
| `تعداد` | Number of affected records (blank when zero) |
| `وضعیت` | Status — see below |

### Status Values

| Status | Colour | Meaning |
|---|---|---|
| `سالم` | Green | Healthy — nothing found |
| `نیازمند بررسی` | Red | Problems found; see the count |
| `در دسترس نیست` | Grey | Could not run — a required table or column does not exist in this database |
| `خطا` | Red | The check itself failed; the error message is shown in the result column |

> **Critical design rule:** a check that could not run is **never** shown as healthy. Grey means
> "unknown", not "fine". This distinction is what makes the tab trustworthy on older databases.

Each check is fully independent. If one fails, the other ten still run and report normally.

---

## 7. Tab 3 — Maintenance

**Tab label:** `نگهداری`

Seven maintenance operations. Results are appended to a timestamped output log in the lower half
of the tab.

**Every operation asks for confirmation before running.** Confirmation dialogs are Persian
(`بله` / `انصراف`).

| Button (Persian) | English | What it does | Modifies data? |
|---|---|---|---|
| `بهینه‌سازی دیتابیس` | Optimize Database | Runs `VACUUM` — compacts the file and reclaims free pages. Reports size before → after | Rewrites the file; no data change |
| `بازسازی Indexها` | Rebuild Indexes | Runs `REINDEX` — rebuilds every index | No |
| `به‌روزرسانی آمار` | Refresh Statistics | Runs `ANALYZE` — updates the query planner's statistics | No |
| `بررسی پیوست‌ها` | Verify Attachments | Counts missing document files and orphaned files on disk | **Read-only** |
| `بررسی مسیرهای ذخیره‌سازی` | Verify Storage | Checks that every configured folder exists; reports free/total drive space | **Read-only** |
| `بررسی بکاپ` | Verify Backup | Opens a folder picker, validates `CaseManagementBackup.xml`, lists tables and row counts | **Read-only** |
| `پاکسازی فایل‌های موقت` | Clear Temporary Files | Deletes files from the configured temp folder and `\Temp` next to the application | **Deletes temp files only** |

### Recommended timing

- `VACUUM` — after deleting many records, or a few times a year. Requires free disk space roughly
  equal to the current database size while it runs.
- `REINDEX` and `ANALYZE` — safe at any time; useful if queries have become noticeably slower.
- Verification operations — safe at any time; they never write.

> **Always take a backup before running VACUUM on a production database.** The operation is safe
> and standard, but any whole-file rewrite deserves a backup first.

---

## 8. Tab 4 — Log Center

**Tab label:** `مرکز لاگ`

A unified viewer for the application's four log sources. Loads automatically when the window opens.

### Controls

| Control (Persian) | English | Options |
|---|---|---|
| `نوع` | Type | `لاگ خطا` (Error) · `لاگ ممیزی` (Audit) · `لاگ امنیتی` (Security) · `لاگ سیستم` (System) |
| `بازه` | Range | `۷ روز` (7 days) · `۳۰ روز` (30 days) · `۹۰ روز` (90 days) · `همه` (All) |
| `جست‌وجو` | Search | Free text; filters instantly across every column |
| `تازه‌سازی` | Refresh | Re-queries the database |
| `خروجی CSV` | Export CSV | Saves the currently loaded rows |

### The four sources

| Source | Contains |
|---|---|
| **Error Log** | Unhandled and handled exceptions: timestamp, severity, source, form, exception type, message, user, machine, resolved status |
| **Audit Log** | Business actions: who created/edited/deleted which record, with old and new values |
| **Security Log** | Logins, failed logins, logouts, permission denials, password changes, lock overrides, and **all Developer Control Center activity** |
| **System Log** | Low-level record-change tracking (table, action type, record ID, user) |

### Notes

- Each source returns at most **2000 rows**, newest first. Narrow the date range to see older
  entries.
- Search filters the rows **already loaded** in memory, so it is instant — but it only searches
  within those 2000 rows.
- Changing Type or Range re-queries immediately; you do not need to press Refresh.
- If a log table does not exist in the current database, the grid shows a single
  `در دسترس نیست` row instead of throwing an error, and the other sources remain selectable.

---

## 9. Tab 5 — Diagnostics

**Tab label:** `عیب‌یابی`

Environment and runtime information. Loads automatically when the window opens.

### Reported values

| Item (Persian) | Meaning |
|---|---|
| `نسخهٔ ویندوز` | Windows version string |
| `سیستم ۶۴ بیتی` | Whether the OS is 64-bit |
| `نسخهٔ .NET` | CLR version |
| `نسخهٔ SQLite` | SQLite engine version (queried from the engine itself) |
| `معماری پردازنده` | Whether this process is running as x64 or x86 |
| `تعداد هسته` | Logical processor count |
| `نام رایانه` | Machine name |
| `کاربر ویندوز` | Windows account running the application |
| `مصرف حافظه` | Process working set |
| `مدت اجرای برنامه` | Uptime since process start |
| `مسیر برنامه` | Installation directory |
| `ماژول‌های فعال` | Number of enabled application modules |
| `اسمبلی‌های بارگذاری‌شده` | Number of loaded .NET assemblies |
| `فرم‌های باز` | Names of every currently open window |
| `قفل‌های فعال رکورد` | Number of active record locks |
| `کار پس‌زمینه — بکاپ خودکار` | Last automatic backup run |

Each row is computed independently — if one value cannot be read, that single row shows
`در دسترس نیست` and the rest of the report is unaffected.

### Buttons

| Button (Persian) | English | Opens |
|---|---|---|
| `تازه‌سازی` | Refresh | Recomputes the table |
| `ماژول‌های نصب‌شده` | Installed Modules | Pop-up listing every registered module and its state |
| `پلاگین‌های بارگذاری‌شده` | Loaded Plugins | Pop-up listing every loaded assembly with version and path |
| `قفل‌های فعال` | Active Locks | Pop-up listing current record locks (entity, ID, holder, expiry) |
| `خروجی CSV` | Export CSV | Saves the diagnostics table |

---

## 10. Tab 6 — Database Explorer

**Tab label:** `کاوشگر دیتابیس`

A **strictly read-only** browser for the raw database.

### Controls

| Control (Persian) | English | Purpose |
|---|---|---|
| `جدول` | Table | Dropdown of every table in the database |
| `جست‌وجو` | Search | Free text, matched against **every column**; press Enter or the display button |
| `نمایش` | Show | Loads the selected table |
| `تعداد رکورد جدول‌ها` | Table Row Counts | Pop-up listing every table with its row count |
| `خروجی CSV` | Export CSV | Saves the currently displayed rows |

### Read-only guarantees

Editing is impossible, enforced at three levels:

1. The grid is `ReadOnly`; adding and deleting rows is disabled.
2. There is **no code path that writes** in the browse function — it can only `SELECT`.
3. The table name is validated against the live list from `sqlite_master` before use, so a crafted
   name cannot be used for SQL injection.

A permanent notice at the bottom of the tab states that the view is read-only and shows the row
count for the current table.

### Limits

- A maximum of **1000 rows** is displayed per table. The notice tells you when this cap applies.
  Use Search to narrow down, or Export CSV from the Log Center / Doctor for full datasets.
- The list includes tables belonging to **all** modules (cases, accounting, enterprise). This is
  intentional for a developer tool.

> **Every table you open is written to the security log** — this is the most sensitive capability
> in the module, so its use is auditable.

---

## 11. Tab 7 — Developer Tools

**Tab label:** `ابزار توسعه‌دهنده`

Eight runtime actions. Results are appended to a timestamped output log. **Every action asks for
confirmation.**

| Button (Persian) | English | Effect |
|---|---|---|
| `فعال‌سازی حالت اشکال‌زدایی` | Enable Debug Mode | Sets the `DevDebugMode` setting to 1 |
| `غیرفعال‌سازی حالت اشکال‌زدایی` | Disable Debug Mode | Sets `DevDebugMode` to 0 |
| `بارگذاری مجدد پیکربندی` | Reload Configuration | Clears the settings cache; next read comes from the database |
| `بارگذاری مجدد مجوزها` | Reload Permissions | Clears the permission and module caches |
| `بارگذاری مجدد جدول‌های مرجع` | Reload Lookup Tables | Clears the lookup (dropdown values) cache |
| `تست اعلان‌ها` | Test Notifications | Writes a test entry through the real notification/audit path |
| `تست ایمیل` | Test Email | Reports whether email is configured |
| `تست پیامک` | Test SMS | Reports whether an SMS number is configured |

The current debug-mode state is shown in the output log when the tab first opens.

### When to use the reload actions

If you change settings, permissions, or lookup values **directly in the database** (not through the
application UI), the running application will still serve cached values. The reload buttons discard
those caches so the new values take effect without restarting the application.

> **Honest limitations, please read:**
> - **Debug Mode** currently only stores a flag. No application code reads it yet — it is a
>   placeholder for future diagnostic verbosity. Turning it on changes no behaviour today.
> - **Test Email / Test SMS** report whether the configuration exists. This application has **no
>   SMTP or SMS sender implemented**, so nothing is actually transmitted. The buttons say so
>   plainly rather than pretending to send.

---

## 12. Support Package

**Button:** `ساخت بستهٔ پشتیبانی` (System Overview tab)

Produces a single `.zip` file containing everything a developer needs to diagnose a problem
remotely. Default filename: `SupportPackage_yyyyMMdd_HHmmss.zip`

### Contents

| File | Contents |
|---|---|
| `01_ApplicationInfo.txt` | Version, install path, uptime, memory, health score and reasons |
| `02_DatabaseInfo.txt` | DB version, status, file size, total records, per-table row counts |
| `03_Configuration.txt` | All application settings (**secrets redacted** — see below) |
| `04_SystemInfo.txt` | Full diagnostics report (OS, .NET, SQLite, hardware, open forms, locks) |
| `05_Modules.csv` | Installed modules and their enabled state |
| `06_Plugins.csv` | Every loaded assembly with version and path |
| `07_ErrorLog.csv` | Last 90 days of errors |
| `08_AuditLog.csv` | Last 90 days of audit entries |
| `09_HealthReport.csv` | Full Database Doctor result |

### Redaction

Setting keys whose name contains any of the following are exported as `[پنهان شد]` ("redacted")
instead of their value:

```
password    secret    token    apikey    license    hash
```

### Robustness

Each section is generated independently. If one section fails, its file contains the error message
and **the package is still produced** — which matters, because the situations where you most need a
support package are exactly the situations where something is broken.

### Privacy warning

The audit log contains **real beneficiary data** (names, phone numbers, ID numbers) in its old/new
value columns. Treat the support package as confidential. Transfer it over a secure channel and
delete it when the investigation is finished.

---

## 13. Exporting Data

Every grid-based tab (Doctor, Log Center, Diagnostics, Explorer) has a `خروجی CSV` button.

- Format: **CSV, UTF-8 with BOM** — opens correctly in Excel with Persian text intact.
- Values containing commas, quotes, or line breaks are properly quoted and escaped.
- The export saves **exactly what is currently displayed** in the grid, including any active filter
  or search.
- If the grid is empty, a warning appears and no file is written.
- Every export is written to the security log.

---

## 14. Auditing — What Gets Logged

Every action taken in the Developer Control Center is recorded in the application's existing
security audit trail with event type `مرکز کنترل توسعه‌دهنده` and severity `بالا` (high).

Each entry records:

- **User** — the account name
- **Machine** — the computer name
- **Date and time**
- **Action** — a description of what was done

Logged actions include:

| Action | Logged as |
|---|---|
| Opening the module | `باز کردن مرکز کنترل توسعه‌دهنده` |
| Running the Doctor | `اجرای دکتر دیتابیس` |
| Any maintenance operation | `نگهداری: <operation>` |
| Any developer tool | `ابزار توسعه‌دهنده: <tool>` |
| Viewing a raw table | `مشاهدهٔ جدول: <table>` |
| Building a support package | `ساخت بستهٔ پشتیبانی` |
| Any CSV export | `خروجی گرفتن: <name>` |

You can review this trail in two places:

1. **Log Center → Security Log** (inside this module)
2. The application's existing **Security Audit** screen (`ممیزی امنیتی`)

There is no way to use this module without leaving a trail.

---

## 15. Behaviour on Damaged or Old Databases

This module is a diagnostic tool, so it is built to work precisely when things are broken.

**The window always opens.** This is guaranteed by design:

- Before running any query, the module asks the database which tables and columns actually exist
  (via `sqlite_master` and `PRAGMA table_info`). Nothing is assumed.
- If a required table or column is missing, that feature reports `در دسترس نیست` ("Not available")
  with the reason — it never throws.
- Each tab is built independently. If one tab fails to build, it shows a short red message and the
  other six work normally.
- Each check, each diagnostic row, each overview card, and each support-package section is
  individually guarded.

**Verified against:**

| Scenario | Result |
|---|---|
| Brand-new empty database | Opens; all 7 tabs work |
| Existing database with data | Opens; all 7 tabs work |
| Old database with all enterprise tables missing | Opens; affected features show "Not available"; no check reports a false "healthy" |
| Damaged database with core tables dropped | Opens; affected checks show "Not available" |
| Tables containing only NULL values | Opens; no check errors |
| Empty tables | Opens; no check errors |

---

## 16. Common Workflows

### A. "The customer says the application is slow"

1. Open the Control Center → **System Overview** → check the health score and its reasons.
2. **Diagnostics** → check memory usage and free disk space.
3. **Database Doctor** → run the full check; look at record counts and duplicates.
4. **Maintenance** → run `به‌روزرسانی آمار` (ANALYZE), then `بازسازی Indexها` (REINDEX).
5. If the database file is large relative to its content, take a backup and run
   `بهینه‌سازی دیتابیس` (VACUUM).

### B. "The customer reports an error but cannot describe it"

1. **Log Center** → Type = `لاگ خطا`, Range = `۷ روز`.
2. Search for the form name or a keyword.
3. Review timestamp, source, form, message, and machine.
4. Export to CSV if you need to analyse it elsewhere.

### C. "Photos or documents are not opening"

1. **Database Doctor** → run the full check.
2. Look at `اسناد گمشده` (missing documents) and `عکس‌های گمشده` (missing photos).
3. **Maintenance** → `بررسی مسیرهای ذخیره‌سازی` to confirm the configured folders still exist —
   a moved or disconnected network drive is the usual cause.

### D. "Send everything to the developer"

1. **System Overview** → `ساخت بستهٔ پشتیبانی`.
2. Choose a location, wait for the progress bar to finish.
3. Send the ZIP over a secure channel.

### E. "I changed data directly in the database"

1. **Developer Tools** → `بارگذاری مجدد پیکربندی` / `بارگذاری مجدد مجوزها` /
   `بارگذاری مجدد جدول‌های مرجع`, as appropriate.
2. No restart is required.

### F. "A record is locked and nobody can edit it"

1. **Diagnostics** → `قفل‌های فعال` to see who holds the lock and when it expires.
2. Release it from the application's existing **Record Locks** screen (`قفل رکوردها`) — this
   module only reports locks, it does not release them.

### G. "Verify a backup before relying on it"

1. **Maintenance** → `بررسی بکاپ`.
2. Select the backup folder.
3. Confirm the table list and row counts look correct.

---

## 17. Troubleshooting

**The shortcut does nothing.**
You are not logged in as a Super Administrator. This is the intended behaviour. Check your role in
the application header — it must read `مدیر کل`.

**A tab shows a red "this section did not load" message.**
That tab failed to build; the other tabs still work. The exact exception is written to the error
log — check **Log Center → Error Log**.

**A Doctor check shows grey `در دسترس نیست`.**
The database is missing a table or column that check needs. This is normal on older databases.
The reason is shown in the result column. It is **not** a failure of your data.

**A Doctor check shows `خطا`.**
The check ran but failed. The message is in the result column and the full exception is in the
error log. The other ten checks are unaffected.

**The window seems frozen during a check.**
It is not. Long operations run in the background with the progress bar visible and the tab strip
temporarily disabled. Duplicate detection on a large database can take a while. Wait for the
progress bar to disappear.

**Export produced an empty file.**
The grid was empty when you exported. Load data first.

**"Active users" shows 0 or "Not available".**
Zero means nobody currently holds a record lock. "Not available" means the lock table does not
exist in this database. Neither indicates a problem.

---

## 18. Safety Rules

1. **Take a backup before any maintenance operation**, especially `VACUUM`.
2. **Never share a support package over an insecure channel** — it contains real beneficiary data
   in the audit log.
3. **Do not leave the window open** on an unattended machine; it exposes raw table data.
4. **Do not use this module for routine work.** Case editing, reporting, and settings all have
   proper screens with proper validation. This module deliberately bypasses none of them — it is
   read-only apart from the maintenance operations — but it is not a substitute for them.
5. **Do not run maintenance while other users are actively working** on a shared database.
   `VACUUM` in particular locks the file for the duration.
6. **Every action is logged.** That is a feature, not a warning — but be aware of it.

---

## 19. Technical Reference

### Files

| File | Role |
|---|---|
| `DevCenter/DevCenterAccess.cs` | Hidden access gate — keyboard message filter and role check |
| `DevCenter/DevCenterService.cs` | All data gathering, schema reflection, maintenance operations |
| `DevCenter/FrmDevCenter.cs` | The seven-tab window |

Integration is limited to a single line in `Program.cs`
(`DevCenterAccess.Install()`) plus the compile entries in the project file. No existing form,
menu, permission, workflow, or database object was modified.

### Reused services

This module deliberately reuses existing application services rather than reimplementing them:

`DataQualityChecker` · `DuplicateDetector` · `FileCleanupHelper` · `ErrorLogger` ·
`SecurityAudit` · `LockService` · `ModuleService` · `PermissionService` · `LookupHelper` ·
`SettingsHelper` · `DatabaseHelper`

### Database objects read

Tables: `TblCase`, `TblFamily`, `TblDocs`, `TblAssistance`, `TblUsers`, `TblAuditLog`,
`TblAuditLogs`, `TblAppSettings`, `EntErrorLog`, `EntSecurityEvent`, `EntRecordLock`, `EntModule`,
plus `sqlite_master` for the table list.

Every one of these is checked for existence before use.

### Writes performed

The module writes in only three situations, all confirmed and logged:

1. Maintenance operations (`VACUUM`, `REINDEX`, `ANALYZE`, temp-file deletion)
2. The `DevDebugMode` setting
3. Its own audit entries in the security log

Nothing else in the module writes to the database.

### Known limitations

| Area | Limitation |
|---|---|
| Progress | Indeterminate (marquee), not a percentage |
| Cancellation | Long operations cannot be cancelled once started |
| Active users | Derived from record locks; no true session tracking exists |
| Debug Mode | Stores a flag only; no code consumes it yet |
| Test Email / SMS | Report configuration state; no sender is implemented |
| Export format | CSV only |
| Database version | Reported from SQLite pragmas; the application keeps no explicit schema-version value |
| Explorer | 1000-row display cap; lists tables from all modules |
| Log Center | 2000-row cap per source; search filters loaded rows only |
| Schema validation | Existence of tables and columns is verified; column *types* are not |

---

*End of guide.*
