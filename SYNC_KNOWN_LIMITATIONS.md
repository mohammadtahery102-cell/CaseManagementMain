# Sync — Known Limitations

Scope: pull-side fixes C2 (commit `e101583`, SyncInbox retry) and C3 (commit `4d1c2d2`, FormNo / empty-code collisions).
These are accepted for now and intentionally left unchanged.

| # | Limitation | Where | Effect |
|---|---|---|---|
| 1 | No attempt cap on parked changes | `SyncService.RetryParked` | A change that can never apply (permanent error) stays in `SyncInbox` and is retried at the end of every completed download. Never lost, but never ages out. The error is logged only when first parked; later attempts update `Attempts` / `LastError` only. |
| 2 | Retry batch is 500 rows, cases first | `SyncService.RetryParked` (`ParkedRetryBatch`) | If more than 500 `TblCase` rows were permanently stuck, parked child rows would not be reached. |
| 3 | Duplicate-key conflict `DetectedAt` refreshes | `SyncApplier.IsDuplicateUserCode` → `SyncConflictStore.Record` | Each retry of an unresolved duplicate updates the open conflict's `DetectedAt` (no duplicate conflict rows are created). |
| 4 | FormNo collisions shown as "duplicate code" | `OfflineSyncInitializer.ConflictDuplicateCode`, `FrmSyncConflicts` | The conflict type/message says "کد اختصاصی تکراری" even when the collision is on `FormNo`. Both payloads are stored, so the admin can see the real value. |
| 5 | Update-path collisions are not surfaced as conflicts | `SyncApplier.Update` | A pulled *update* that sets a colliding Code/FormNo fails with a UNIQUE error; it is parked and logged, but no conflict is shown. Only inserts are pre-checked. |
| 6 | Extra parent lookup per child insert | `SyncApplier.Apply` / `Insert` | `ResolveParent` runs twice for child inserts (one extra indexed query). |

Also not covered: there is no UI showing how many changes are waiting in `SyncInbox`.
