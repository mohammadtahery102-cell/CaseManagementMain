# Sync Live Verification Report — Real Desktop DB × Real Server DB

Verification only. No code changed, no data changed, no server started, no configuration touched. All evidence below was queried directly from:
- **Desktop**: `C:\Projects\CaseManagement\bin\x64\Debug\CaseDB.sqlite` (the real installation's live database)
- **Server**: the real `syncserver` PostgreSQL database (not the `syncserver_test` database used by automated tests)

**Overall result: readiness checklist is NOT satisfied. No real desktop record has ever reached the server. End-to-end sync has not happened.**

## 1–12. Checklist, item by item

| # | Item | Result | Evidence |
|---|---|---|---|
| 2 | Migration 014 applied | ✅ **YES** | `schema_migrations` on the real `syncserver` DB: `014_AddBalkhCenter.sql`, `applied_at = 2026-09-21 14:59:19.797669+04:30`. (This means `SyncServer.API` was started at least once after the migration file was added — not done by me; my one attempt to write to this database directly was blocked by this session's own sandbox.) |
| 3 | Balkh center exists on server | ✅ **YES** | `centers`: `center_id=2, code='002', name='بلخ', is_active=true`. |
| 4 | Real desktop device registered | ❌ **NO** | Desktop's actual `DeviceGuid` (from `SyncState`) is `39d9d0fd-5a4f-4f20-b1a5-8d4712f9109c`. The server's `devices` table has 6 rows total — none with this GUID. The only `kind='sync'` device on the server is `device_id=4`, GUID `f810f782-1d14-4308-9638-1a1d85997bd5`, machine name `DESKTOP-SYNC-TEST`, `center_id=1` — a different, older test artifact (created 2026-08-09, per its own prior-phase testing), not the production Balkh desktop. Every other server device row is `kind='portal'` (browser logins to the Portal). |
| 5 | Real desktop device approved | ❌ **N/A / NO** | Cannot be approved — it does not exist on the server (see #4). |
| 6 | ServerUrl configured | ❌ **NO** | Desktop `SyncState` table contains exactly 3 keys: `DeviceGuid`, `AutoSyncEnabled=1`, `AutoSyncIntervalMinutes=15`. **No `ServerUrl` key exists.** `HttpSyncTransport.IsConfigured` reads this same key and would evaluate `false`. Auto-sync is toggled on, but with no `ServerUrl` it has nothing to connect to. |
| 7 | Operator account exists, correct center | ❌ **NO** | Server `users` table has exactly 2 rows: `admin` (SuperAdmin, `center_id=1`) and `مدیر` (Operator, `center_id=1`). **Zero users belong to `center_id=2` (Balkh).** The `مدیر` row's username coincidentally matches the desktop's local SuperAdmin's display name, but it is a different account (Operator role, center 1) with unknown provenance — flagged in the prior implementation report, still unresolved. |
| 8 | Execute a real synchronization test | ❌ **NOT POSSIBLE right now** | Four independent blockers, all present simultaneously: (a) `SyncServer.API` is **not currently running** — `tasklist` shows zero `dotnet.exe` processes, no listener on any of the usual ports; (b) no `ServerUrl` on the desktop (#6); (c) no operator account for Balkh (#7); (d) no registered/approved device for Balkh (#4/#5). I did not start the server, configure a URL, create a user, or approve a device — those are the operational steps flagged as pending in the implementation report, requiring a human with real credentials and a real network/deployment decision. |
| 9 | At least one real desktop record reaches the server | ❌ **NOT DEMONSTRATED** | `cases` table on the server: `center_id=1 → 1 row`, `center_id=2 → 0 rows`. The one existing row belongs to the old `DESKTOP-SYNC-TEST` device/center-1 test artifact, not the real Balkh desktop. Zero real production records have ever reached the server. |
| 10 | Record visible via Portal/API | ❌ **N/A** | Nothing exists for Balkh to be visible. |
| 11 | SyncOutbox count decreases | ❌ **NO CHANGE** | Desktop `SyncOutbox`: **100,953 rows total, all in state `در انتظار` (Pending)**. Zero rows in any "sent"/"applied" state. No successful push has ever occurred on this installation, so there is nothing for the count to decrease from. |
| 12 | `sync_baseline` / `device_inbox` updated correctly | ❌ **NO relevant activity** | Both tables have rows for exactly one device: `device_id=4` (the old test artifact) — `sync_baseline`: 1 row; `device_inbox`: 2 rows. Nothing tied to the real Balkh desktop or its actual `DeviceGuid`. |

## Historical backfill gap — also re-verified fresh, unchanged

Direct set-comparison between `TblCase`/`TblFamily`/`TblUsers` and `SyncOutbox.EntityLocalID` on the real desktop database, just now:

- `TblCase`: **566 of 572** rows still have zero outbox history.
- `TblFamily`: **1,349 of 1,349** rows still have zero outbox history.
- `TblUsers`: **4 of 4** rows still have zero outbox history.

Identical to the figures in the original audits — confirming the "بازپرِ صفِ همگام‌سازی" (sync backfill) button built in the implementation phase has **not** been run on this installation yet.

## Why nothing has changed since the implementation report

Every remaining item in that report's §7 ("Remaining recommendations") is still outstanding:
1. Real Balkh operator account — not created.
2. Real `ServerUrl` — not set.
3. First login / device approval — never attempted (no device row exists).
4. Backfill — not run.
5. Real sync — never executed; `SyncServer.API` isn't even running right now.

Migration 014 is the only step that has actually progressed since the report (the API was evidently started once, applying it, then stopped).

## What I did not do, and why

I did not start `SyncServer.API`, set a `ServerUrl`, create a user, approve a device, or click the backfill button. Every one of those changes real state (a running process, a real user account and password, production SQLite configuration, a first-ever push of real data to the server) and falls outside "verification" — you asked for verification only and explicitly said not to continue implementation, so I stopped at evidence-gathering. If you want me to actually perform those operational steps next and produce a genuine end-to-end proof with real data, say so explicitly and I'll proceed through them one at a time with evidence at each step.

## One incidental finding from this verification pass (not fixed, per your instruction)

`SyncBackfillService.Preview()`/`Run()` (and the ad-hoc check above) select missing rows via `NOT EXISTS (... WHERE EntityLocalID = ...)`. The desktop's only index on `SyncOutbox` covering `EntityName` is `IX_SyncOutbox_Entity (EntityName, EntityGlobalID)` — it does not cover `EntityLocalID`, so this lookup is an unindexed scan per row. On this installation's 100,953-row table it was still fast enough in testing (sub-second for TblCase), but it's worth knowing if the table grows much larger. No code changed for this — flagging only, per your "no new implementation" instruction.
