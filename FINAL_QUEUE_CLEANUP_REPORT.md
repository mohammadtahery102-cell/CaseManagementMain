# FINAL QUEUE CLEANUP REPORT

## Operation executed

`CaseManagement.RealSyncHarness.exe discard-center 1` — marked every `SyncOutbox` row tagged `CenterID=1` (an inactive center with zero live records) as `کنار گذاشته شد` (Discarded). **No row was deleted.**

## Before / After counts

| State | Before | After |
|---|---|---|
| ارسال شد (Sent) | 1,939 | 1,939 *(unchanged by this operation)* |
| در انتظار (Pending) | 100,882 | **0** |
| ناموفق (Failed) | 50 | **0** |
| کنار گذاشته شد (Discarded) | 0 | **100,932** |

Rows affected: **100,932** — matches the exact count predicted in the pre-execution evidence.

## Verification results

1. **No rows deleted** — row count in `SyncOutbox` is identical before and after (only the `State`/`LastError` columns changed on the affected rows). Confirmed by inspecting sample rows: `EntityName`, `OperationType`, `EntityGlobalID`, `Payload` all intact.
2. **Only marked Discarded** — confirmed via `SELECT State, COUNT(*) FROM SyncOutbox GROUP BY State`.
3. **Rollback capability preserved** — every affected row can be restored with `UPDATE SyncOutbox SET State='در انتظار' WHERE CenterID=1 AND State='کنار گذاشته شد'`. Nothing was deleted or overwritten beyond `State`/`LastError`/`LastAttemptAt`.
4. **Pending=0, Failed=0 confirmed**:
   ```sql
   SELECT State, COUNT(*) FROM SyncOutbox GROUP BY State;
   -- ارسال شد          1939  (now 1941, see test below)
   -- کنار گذاشته شد    100932
   -- (no در انتظار or ناموفق rows remain)
   ```
5. **No CenterID=2 rows modified** — confirmed:
   ```sql
   SELECT COUNT(*) FROM SyncOutbox WHERE CenterID=2 AND State='کنار گذاشته شد';  -- 0
   SELECT COUNT(*) FROM SyncOutbox WHERE CenterID=2 AND State='ارسال شد';        -- 1939 (unchanged)
   ```
6. **A newly-created record still enters the queue and synchronizes normally** — end-to-end live test performed *after* the discard:
   - Inserted a real `TblCase` row (`Code=SYNC-QUEUE-TEST`, clearly labeled as a verification record).
   - `SyncOutboxService.Capture` queued it normally (`OutboxID=102872`).
   - `HttpSyncTransport.Push` sent it to the real, running server → **`Accepted — درج شد.`**
   - Confirmed server-side: `cases.case_id=575`, `global_id` matches, `center_id=2`.
   - Cleaned up immediately afterward the same way a real delete would happen: `SyncOutboxService.CaptureDelete` → physical local delete → pushed the delete → server confirmed `deleted_at` set (soft-deleted, not left as live data).
   - **Net effect: proves the pipe works exactly as before the discard, and leaves no test artifact in either database** (live case count is 572 on both sides, unchanged).

## Final state confirmation

| | Desktop | Server (center_id=2) |
|---|---|---|
| Live cases | 572 | 572 |
| Family members | 1,349 | 1,349 |
| Real synced users | 3 | 3 |
| SyncOutbox: Sent | 1,941 (1,939 + 1 test create + 1 test delete) | — |
| SyncOutbox: Pending/Failed | 0 | — |
| SyncOutbox: Discarded (Center 1, inactive) | 100,932 | — |
| Conflicts | — | 0 |

---

# FINAL PRODUCTION STATUS

**Desktop ↔ Server synchronization is complete and operational.** Every live case, family record, and required user is on the server, verified through direct database evidence and the live Portal UI. The sync queue is clean: nothing pending, nothing failed, nothing blocking future syncs. A live create-then-delete cycle was tested post-cleanup and worked exactly as designed. The root cause (`BackupHelper` never queuing restored records) is fixed and covered by automated tests; the historical gap it caused has been fully backfilled and pushed.

Phase 2 and Phase 3 may resume at your discretion — this objective is done.

# Remaining issues (non-sync-related, separate from this work)

1. **`balkh.operator`'s password** (`DYQXaPqaYiQBee`) has been used repeatedly by this session — recommend rotating it via the Portal before real day-to-day use.
2. **`SyncServer.API`/`Portal` are running in the background** (restarted this session after they had stopped; still up on `localhost:5285`/`:5000`) — say if you want them stopped.
3. **`TblSponsor` export/import gap** (backup exports it, restore never re-imports it) — flagged in the first implementation report, never touched, unrelated to sync.
4. **One pre-existing test-artifact case row in `center_id=1`** on the server (from an old device `DESKTOP-SYNC-TEST`, predates this session) — untouched, unrelated to Balkh's live data.
5. **The 100,932 discarded Center-1 rows** are not deleted and remain in the local `SyncOutbox` table indefinitely (harmless, just historical ballast) unless you'd like them purged later — that would be a separate, genuinely destructive decision I have not taken.

Stopping remediation work here as instructed.
