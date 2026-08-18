using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // نتیجه اجرای یک گذار. آموزش: به‌جای bool ساده برگردانده می‌شود تا فرم
    // بتواند بین «انجام شد»، «به تأیید نیاز دارد» و «مجاز نیستی» تفاوت بگذارد.
    public class WorkflowActionResult
    {
        public bool   Applied         { get; set; } // مرحله واقعاً تغییر کرد
        public bool   PendingApproval { get; set; } // درخواست تأیید ساخته شد
        public string Message         { get; set; }

        public static WorkflowActionResult Fail(string message)
        {
            return new WorkflowActionResult { Applied = false, Message = message };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۱ — موتور گردش‌کار.
    // آموزش: موتور «عمومی» است؛ هیچ جای این کلاس نامِ TblCase هاردکد نشده و
    // همه‌چیز با (EntityName, EntityID) کار می‌کند. در فاز یک فقط پرونده به آن
    // وصل می‌شود ولی ماژول‌های دیگر بدون تغییر کد موتور قابل اتصال‌اند.
    //
    // این سرویس هیچ ستون یا جدول موجودی را تغییر نمی‌دهد؛ وضعیت گردش‌کار در
    // EntWorkflowInstance نگهداری می‌شود و ServiceStatus فعلی TblCase دست‌نخورده
    // باقی می‌ماند (سازگاری با گزارش‌ها و داشبورد موجود).
    // ─────────────────────────────────────────────────────────────────────────
    public static class WorkflowService
    {
        // نقطه اتصال به سامانه تأیید چندسطحی (ویژگی ۲). در فاز اول null است و
        // گذارهای «نیازمند تأیید» مستقیم اجرا می‌شوند؛ ApprovalService هنگام
        // راه‌اندازی خودش را این‌جا ثبت می‌کند و از آن به بعد چنین گذارهایی
        // به‌جای اجرای مستقیم، درخواست تأیید می‌سازند.
        // خروجی true یعنی «درخواست ساخته شد، گذار را الان اجرا نکن».
        public static Func<WorkflowInstanceModel, WorkflowTransitionModel, string, bool> ApprovalRequestHook;

        // ─── تعریف‌ها ──────────────────────────────────────────────────────
        public static WorkflowModel GetActiveWorkflow(string entityName)
        {
            DataTable table = EntDb.Query(@"
SELECT WorkflowID, Code, Name, EntityName, Description, IsActive
FROM   EntWorkflow
WHERE  EntityName = @Entity AND IsActive = 1
ORDER  BY WorkflowID
LIMIT  1;", "@Entity", entityName);

            if (table.Rows.Count == 0) return null;
            return ReadWorkflow(table.Rows[0]);
        }

        public static List<WorkflowModel> GetWorkflows()
        {
            List<WorkflowModel> list = new List<WorkflowModel>();

            DataTable table = EntDb.Query(@"
SELECT WorkflowID, Code, Name, EntityName, Description, IsActive
FROM   EntWorkflow
ORDER  BY EntityName, Name;");

            foreach (DataRow row in table.Rows)
                list.Add(ReadWorkflow(row));

            return list;
        }

        public static List<WorkflowStateModel> GetStates(int workflowId)
        {
            List<WorkflowStateModel> list = new List<WorkflowStateModel>();

            DataTable table = EntDb.Query(@"
SELECT StateID, WorkflowID, Code, Name, IsInitial, IsFinal, SortOrder, Color
FROM   EntWorkflowState
WHERE  WorkflowID = @WF
ORDER  BY SortOrder, StateID;", "@WF", workflowId);

            foreach (DataRow row in table.Rows)
                list.Add(new WorkflowStateModel
                {
                    StateID    = EntDb.ToInt (row["StateID"]),
                    WorkflowID = EntDb.ToInt (row["WorkflowID"]),
                    Code       = EntDb.ToText(row["Code"]),
                    Name       = EntDb.ToText(row["Name"]),
                    IsInitial  = EntDb.ToBool(row["IsInitial"]),
                    IsFinal    = EntDb.ToBool(row["IsFinal"]),
                    SortOrder  = EntDb.ToInt (row["SortOrder"]),
                    Color      = EntDb.ToText(row["Color"])
                });

            return list;
        }

        public static List<WorkflowTransitionModel> GetTransitions(int workflowId)
        {
            List<WorkflowTransitionModel> list = new List<WorkflowTransitionModel>();

            DataTable table = EntDb.Query(@"
SELECT t.TransitionID, t.WorkflowID, t.FromStateID, t.ToStateID, t.Name,
       t.RequiredPermission, t.RequiresApproval, t.ApprovalChainID, t.SortOrder,
       sf.Name AS FromStateName, st.Name AS ToStateName
FROM   EntWorkflowTransition t
LEFT   JOIN EntWorkflowState sf ON sf.StateID = t.FromStateID
LEFT   JOIN EntWorkflowState st ON st.StateID = t.ToStateID
WHERE  t.WorkflowID = @WF
ORDER  BY t.SortOrder, t.TransitionID;", "@WF", workflowId);

            foreach (DataRow row in table.Rows)
                list.Add(ReadTransition(row));

            return list;
        }

        // ─── نمونه‌ها ──────────────────────────────────────────────────────
        public static WorkflowInstanceModel GetInstance(string entityName, int entityId)
        {
            DataTable table = EntDb.Query(@"
SELECT i.InstanceID, i.WorkflowID, i.EntityName, i.EntityID, i.CurrentStateID,
       i.Status, i.CenterID, i.StartedBy, i.StartedAt,
       s.Name AS StateName, s.Code AS StateCode, s.Color AS StateColor, s.IsFinal
FROM   EntWorkflowInstance i
LEFT   JOIN EntWorkflowState s ON s.StateID = i.CurrentStateID
WHERE  i.EntityName = @Entity AND i.EntityID = @Id
ORDER  BY i.InstanceID DESC
LIMIT  1;", "@Entity", entityName, "@Id", entityId);

            if (table.Rows.Count == 0) return null;
            return ReadInstance(table.Rows[0]);
        }

        // نمونه را در صورت نبود می‌سازد و در مرحله شروع قرار می‌دهد.
        // اگر برای این موجودیت گردش‌کار فعالی تعریف نشده باشد null برمی‌گردد و
        // فراخوان باید بی‌سروصدا از آن عبور کند (رفتار قبلی برنامه حفظ شود).
        public static WorkflowInstanceModel EnsureInstance(string entityName, int entityId, int centerId)
        {
            WorkflowInstanceModel existing = GetInstance(entityName, entityId);
            if (existing != null) return existing;

            WorkflowModel workflow = GetActiveWorkflow(entityName);
            if (workflow == null) return null;

            WorkflowStateModel initial = null;
            foreach (WorkflowStateModel state in GetStates(workflow.WorkflowID))
                if (state.IsInitial) { initial = state; break; }

            if (initial == null) return null;

            long instanceId = EntDb.Insert(@"
INSERT INTO EntWorkflowInstance
    (WorkflowID, EntityName, EntityID, CurrentStateID, Status, CenterID, StartedBy)
VALUES
    (@WF, @Entity, @Id, @State, 'جاری', @Center, @By);",
                "@WF",     workflow.WorkflowID,
                "@Entity", entityName,
                "@Id",     entityId,
                "@State",  initial.StateID,
                "@Center", centerId > 0 ? (object)centerId : null,
                "@By",     SecurityContext.Username);

            if (instanceId > 0)
                EntDb.Exec(@"
INSERT INTO EntWorkflowHistory (InstanceID, FromStateID, ToStateID, ActionBy, Note)
VALUES (@Inst, NULL, @To, @By, 'شروع گردش‌کار');",
                    "@Inst", instanceId,
                    "@To",   initial.StateID,
                    "@By",   SecurityContext.Username);

            return GetInstance(entityName, entityId);
        }

        // گذارهای مجاز از مرحله جاری، پس از اعمال کنترل دسترسی.
        public static List<WorkflowTransitionModel> GetAvailableTransitions(WorkflowInstanceModel instance)
        {
            List<WorkflowTransitionModel> list = new List<WorkflowTransitionModel>();
            if (instance == null) return list;

            DataTable table = EntDb.Query(@"
SELECT t.TransitionID, t.WorkflowID, t.FromStateID, t.ToStateID, t.Name,
       t.RequiredPermission, t.RequiresApproval, t.ApprovalChainID, t.SortOrder,
       sf.Name AS FromStateName, st.Name AS ToStateName
FROM   EntWorkflowTransition t
LEFT   JOIN EntWorkflowState sf ON sf.StateID = t.FromStateID
LEFT   JOIN EntWorkflowState st ON st.StateID = t.ToStateID
WHERE  t.WorkflowID = @WF AND t.FromStateID = @From
ORDER  BY t.SortOrder, t.TransitionID;",
                "@WF", instance.WorkflowID, "@From", instance.CurrentStateID);

            foreach (DataRow row in table.Rows)
            {
                WorkflowTransitionModel transition = ReadTransition(row);
                if (CanUseTransition(transition))
                    list.Add(transition);
            }

            return list;
        }

        // ─── اجرای گذار ────────────────────────────────────────────────────
        public static WorkflowActionResult ApplyTransition(
            WorkflowInstanceModel instance, int transitionId, string note)
        {
            if (instance == null)
                return WorkflowActionResult.Fail("گردش‌کار برای این رکورد فعال نیست.");

            if (!string.Equals(instance.Status, "جاری", StringComparison.Ordinal))
                return WorkflowActionResult.Fail("گردش‌کار این رکورد پایان یافته است.");

            WorkflowTransitionModel transition = GetTransition(transitionId);

            if (transition == null || transition.WorkflowID != instance.WorkflowID)
                return WorkflowActionResult.Fail("گذار انتخاب‌شده معتبر نیست.");

            if (transition.FromStateID != instance.CurrentStateID)
                return WorkflowActionResult.Fail("این گذار از مرحله فعلی قابل اجرا نیست.");

            if (!CanUseTransition(transition))
                return WorkflowActionResult.Fail("شما مجوز اجرای این گذار را ندارید.");

            // نیازمند تأیید چندسطحی؟ (ویژگی ۲ — در صورت فعال بودن)
            if (transition.RequiresApproval && ApprovalRequestHook != null)
            {
                bool created = ApprovalRequestHook(instance, transition, note);
                if (created)
                    return new WorkflowActionResult
                    {
                        Applied         = false,
                        PendingApproval = true,
                        Message         = "درخواست تأیید ثبت شد و در انتظار تأییدکنندگان است."
                    };
            }

            return MoveTo(instance, transition.ToStateID, transition.TransitionID, note);
        }

        // انتقال واقعی مرحله. از ApplyTransition و از ApprovalService (پس از
        // تأیید نهایی) فراخوانی می‌شود.
        public static WorkflowActionResult MoveTo(
            WorkflowInstanceModel instance, int toStateId, int? transitionId, string note)
        {
            if (instance == null)
                return WorkflowActionResult.Fail("گردش‌کار برای این رکورد فعال نیست.");

            WorkflowStateModel target = GetState(toStateId);
            if (target == null)
                return WorkflowActionResult.Fail("مرحله مقصد پیدا نشد.");

            int fromStateId = instance.CurrentStateID;

            EntDb.Helper().ExecuteInTransaction(delegate (System.Data.SQLite.SQLiteConnection con,
                                                          System.Data.SQLite.SQLiteTransaction tr)
            {
                using (var cmd = new System.Data.SQLite.SQLiteCommand(@"
UPDATE EntWorkflowInstance
SET    CurrentStateID = @To,
       Status         = @Status,
       CompletedAt    = CASE WHEN @Final = 1 THEN datetime('now') ELSE CompletedAt END
WHERE  InstanceID = @Inst;", con, tr))
                {
                    cmd.Parameters.AddWithValue("@To",     toStateId);
                    cmd.Parameters.AddWithValue("@Status", target.IsFinal ? "پایان‌یافته" : "جاری");
                    cmd.Parameters.AddWithValue("@Final",  target.IsFinal ? 1 : 0);
                    cmd.Parameters.AddWithValue("@Inst",   instance.InstanceID);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new System.Data.SQLite.SQLiteCommand(@"
INSERT INTO EntWorkflowHistory
    (InstanceID, FromStateID, ToStateID, TransitionID, ActionBy, Note)
VALUES
    (@Inst, @From, @To, @Tr, @By, @Note);", con, tr))
                {
                    cmd.Parameters.AddWithValue("@Inst", instance.InstanceID);
                    cmd.Parameters.AddWithValue("@From", fromStateId > 0 ? (object)fromStateId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@To",   toStateId);
                    cmd.Parameters.AddWithValue("@Tr",   transitionId.HasValue ? (object)transitionId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@By",   SecurityContext.Username ?? "");
                    cmd.Parameters.AddWithValue("@Note", string.IsNullOrWhiteSpace(note) ? (object)DBNull.Value : note.Trim());
                    cmd.ExecuteNonQuery();
                }
            });

            // ثبت در سامانه رویدادهای موجود — بدون تغییر در AuditLogger.
            AuditLogger.Log("Workflow", instance.EntityName, instance.EntityID,
                            GetStateName(fromStateId), target.Name);

            // ویژگی ۴ — قواعد رویداد «گذار گردش‌کار». مرحله از قبل تغییر کرده،
            // پس نتیجه فقط برای اطلاع‌رسانی/ساخت وظیفه است و گذار را برنمی‌گرداند.
            RuleEngine.Run(instance.EntityName, RuleEngine.EventTransition, instance.EntityID);

            // به‌روزرسانی مدل در حافظه تا فرم بلافاصله مقدار درست را نشان دهد.
            instance.CurrentStateID    = target.StateID;
            instance.CurrentStateName  = target.Name;
            instance.CurrentStateCode  = target.Code;
            instance.CurrentStateColor = target.Color;
            instance.IsFinal           = target.IsFinal;
            instance.Status            = target.IsFinal ? "پایان‌یافته" : "جاری";

            return new WorkflowActionResult
            {
                Applied = true,
                Message = "مرحله به «" + target.Name + "» تغییر کرد."
            };
        }

        public static DataTable GetHistory(int instanceId)
        {
            return EntDb.Query(@"
SELECT h.ActionAt   AS 'تاریخ',
       sf.Name      AS 'از مرحله',
       st.Name      AS 'به مرحله',
       h.ActionBy   AS 'کاربر',
       h.Note       AS 'توضیح'
FROM   EntWorkflowHistory h
LEFT   JOIN EntWorkflowState sf ON sf.StateID = h.FromStateID
LEFT   JOIN EntWorkflowState st ON st.StateID = h.ToStateID
WHERE  h.InstanceID = @Inst
ORDER  BY h.HistoryID DESC;", "@Inst", instanceId);
        }

        // ─── کمکی‌ها ───────────────────────────────────────────────────────
        public static WorkflowStateModel GetState(int stateId)
        {
            DataTable table = EntDb.Query(@"
SELECT StateID, WorkflowID, Code, Name, IsInitial, IsFinal, SortOrder, Color
FROM   EntWorkflowState WHERE StateID = @Id;", "@Id", stateId);

            if (table.Rows.Count == 0) return null;
            DataRow row = table.Rows[0];

            return new WorkflowStateModel
            {
                StateID    = EntDb.ToInt (row["StateID"]),
                WorkflowID = EntDb.ToInt (row["WorkflowID"]),
                Code       = EntDb.ToText(row["Code"]),
                Name       = EntDb.ToText(row["Name"]),
                IsInitial  = EntDb.ToBool(row["IsInitial"]),
                IsFinal    = EntDb.ToBool(row["IsFinal"]),
                SortOrder  = EntDb.ToInt (row["SortOrder"]),
                Color      = EntDb.ToText(row["Color"])
            };
        }

        public static WorkflowTransitionModel GetTransition(int transitionId)
        {
            DataTable table = EntDb.Query(@"
SELECT t.TransitionID, t.WorkflowID, t.FromStateID, t.ToStateID, t.Name,
       t.RequiredPermission, t.RequiresApproval, t.ApprovalChainID, t.SortOrder,
       sf.Name AS FromStateName, st.Name AS ToStateName
FROM   EntWorkflowTransition t
LEFT   JOIN EntWorkflowState sf ON sf.StateID = t.FromStateID
LEFT   JOIN EntWorkflowState st ON st.StateID = t.ToStateID
WHERE  t.TransitionID = @Id;", "@Id", transitionId);

            if (table.Rows.Count == 0) return null;
            return ReadTransition(table.Rows[0]);
        }

        private static string GetStateName(int stateId)
        {
            if (stateId <= 0) return "";
            WorkflowStateModel state = GetState(stateId);
            return state == null ? "" : state.Name;
        }

        // کنترل دسترسی گذار.
        // آموزش: در فاز یک بر پایه نقش‌های موجود SecurityContext کار می‌کند تا
        // هیچ وابستگی جدیدی ایجاد نشود؛ در ویژگی ۹ (ماتریس مجوزها) همین یک
        // نقطه به PermissionService وصل می‌شود و بقیه کد دست‌نخورده می‌ماند.
        private static bool CanUseTransition(WorkflowTransitionModel transition)
        {
            if (transition == null) return false;
            if (string.IsNullOrWhiteSpace(transition.RequiredPermission)) return true;

            return PermissionGate == null
                ? SecurityContext.IsAdmin()
                : PermissionGate(transition.RequiredPermission);
        }

        // نقطه اتصال به ماتریس مجوزها (ویژگی ۹).
        public static Func<string, bool> PermissionGate;

        private static WorkflowModel ReadWorkflow(DataRow row)
        {
            return new WorkflowModel
            {
                WorkflowID  = EntDb.ToInt (row["WorkflowID"]),
                Code        = EntDb.ToText(row["Code"]),
                Name        = EntDb.ToText(row["Name"]),
                EntityName  = EntDb.ToText(row["EntityName"]),
                Description = EntDb.ToText(row["Description"]),
                IsActive    = EntDb.ToBool(row["IsActive"])
            };
        }

        private static WorkflowTransitionModel ReadTransition(DataRow row)
        {
            return new WorkflowTransitionModel
            {
                TransitionID       = EntDb.ToInt (row["TransitionID"]),
                WorkflowID         = EntDb.ToInt (row["WorkflowID"]),
                FromStateID        = EntDb.ToInt (row["FromStateID"]),
                ToStateID          = EntDb.ToInt (row["ToStateID"]),
                Name               = EntDb.ToText(row["Name"]),
                RequiredPermission = EntDb.ToText(row["RequiredPermission"]),
                RequiresApproval   = EntDb.ToBool(row["RequiresApproval"]),
                ApprovalChainID    = EntDb.ToInt (EntDb.Col(row, "ApprovalChainID")),
                SortOrder          = EntDb.ToInt (row["SortOrder"]),
                FromStateName      = EntDb.ToText(EntDb.Col(row, "FromStateName")),
                ToStateName        = EntDb.ToText(EntDb.Col(row, "ToStateName"))
            };
        }

        private static WorkflowInstanceModel ReadInstance(DataRow row)
        {
            return new WorkflowInstanceModel
            {
                InstanceID        = EntDb.ToInt (row["InstanceID"]),
                WorkflowID        = EntDb.ToInt (row["WorkflowID"]),
                EntityName        = EntDb.ToText(row["EntityName"]),
                EntityID          = EntDb.ToInt (row["EntityID"]),
                CurrentStateID    = EntDb.ToInt (row["CurrentStateID"]),
                Status            = EntDb.ToText(row["Status"]),
                CenterID          = EntDb.ToInt (row["CenterID"]),
                StartedBy         = EntDb.ToText(row["StartedBy"]),
                StartedAt         = EntDb.ToDate(row["StartedAt"]),
                CurrentStateName  = EntDb.ToText(EntDb.Col(row, "StateName")),
                CurrentStateCode  = EntDb.ToText(EntDb.Col(row, "StateCode")),
                CurrentStateColor = EntDb.ToText(EntDb.Col(row, "StateColor")),
                IsFinal           = EntDb.ToBool(EntDb.Col(row, "IsFinal"))
            };
        }
    }
}
