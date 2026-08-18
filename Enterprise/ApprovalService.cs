using System;
using System.Collections.Generic;
using System.Data;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // ویژگی ۲ — سامانه تأیید چندسطحی.
    //
    // هر «زنجیره تأیید» چند سطح دارد و درخواست، سطح‌به‌سطح جلو می‌رود: تا وقتی
    // سطح آخر تأیید نکند، درخواست تأیید نهایی نمی‌شود. رد شدن در هر سطح،
    // درخواست را بلافاصله رد می‌کند.
    //
    // اتصال به گردش‌کار: گذارهایی که RequiresApproval دارند به‌جای اجرای
    // مستقیم، از این‌جا یک درخواست می‌سازند؛ پس از تأیید سطح آخر، همان گذار
    // به‌صورت خودکار روی گردش‌کار اعمال می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ApprovalService
    {
        public const string StatusPending  = "در انتظار";
        public const string StatusApproved = "تأیید شده";
        public const string StatusRejected = "رد شده";
        public const string StatusCanceled = "لغو شده";

        public const string DecisionApprove = "تأیید";
        public const string DecisionReject  = "رد";

        // نصب نقطه اتصال روی موتور گردش‌کار. از EnterpriseInitializer صدا زده
        // می‌شود؛ چندبار فراخوانی بی‌ضرر است.
        public static void Install()
        {
            WorkflowService.ApprovalRequestHook = CreateFromWorkflow;
        }

        // ─── ساخت درخواست از یک گذار گردش‌کار ─────────────────────────────
        // خروجی true یعنی درخواست ساخته شد و گذار نباید مستقیم اجرا شود.
        private static bool CreateFromWorkflow(
            WorkflowInstanceModel instance, WorkflowTransitionModel transition, string note)
        {
            if (instance == null || transition == null) return false;

            ApprovalChainModel chain = transition.ApprovalChainID > 0
                ? GetChain(transition.ApprovalChainID)
                : GetActiveChain(instance.EntityName);

            // زنجیره‌ای تعریف نشده؟ گذار مثل قبل مستقیم اجرا می‌شود تا کار
            // کاربر متوقف نشود (رفتار محافظه‌کارانه و بدون شکستن گردش‌کار).
            if (chain == null || GetLevels(chain.ChainID).Count == 0)
                return false;

            // درخواست باز تکراری برای همان گذار ساخته نشود.
            long duplicate = EntDb.ToInt64(EntDb.Scalar(@"
SELECT COUNT(*) FROM EntApprovalRequest
WHERE  EntityName = @Entity AND EntityID = @Id
  AND  TransitionID = @Tr AND Status = @Status;",
                "@Entity", instance.EntityName,
                "@Id",     instance.EntityID,
                "@Tr",     transition.TransitionID,
                "@Status", StatusPending));

            if (duplicate > 0) return true;

            EntDb.Exec(@"
INSERT INTO EntApprovalRequest
    (ChainID, EntityName, EntityID, Title, Reason, Status, CurrentLevelNo,
     WorkflowInstanceID, TransitionID, TargetStateID, CenterID,
     RequestedBy, RequestedByUserID)
VALUES
    (@Chain, @Entity, @Id, @Title, @Reason, @Status, 1,
     @Inst, @Tr, @Target, @Center, @By, @ByID);",
                "@Chain",  chain.ChainID,
                "@Entity", instance.EntityName,
                "@Id",     instance.EntityID,
                "@Title",  transition.Name,
                "@Reason", note,
                "@Status", StatusPending,
                "@Inst",   instance.InstanceID,
                "@Tr",     transition.TransitionID,
                "@Target", transition.ToStateID,
                "@Center", instance.CenterID > 0 ? (object)instance.CenterID : null,
                "@By",     SecurityContext.Username,
                "@ByID",   SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null);

            AuditLogger.Log("ApprovalRequested", instance.EntityName, instance.EntityID,
                            instance.CurrentStateName, transition.Name);

            // ویژگی ۳ — وظیفه خودکار برای تأییدکننده سطح اول.
            RaiseTaskForCurrentLevel(GetOpenRequest(instance.EntityName, instance.EntityID, transition.TransitionID));

            return true;
        }

        // ─── تصمیم‌گیری ───────────────────────────────────────────────────
        public static WorkflowActionResult Decide(int requestId, bool approve, string comment)
        {
            ApprovalRequestModel request = GetRequest(requestId);

            if (request == null)
                return WorkflowActionResult.Fail("درخواست پیدا نشد.");

            if (!request.IsPending)
                return WorkflowActionResult.Fail("این درخواست قبلاً تعیین تکلیف شده است.");

            if (!SecurityContext.CanAccessCenter(request.CenterID))
                return WorkflowActionResult.Fail("این درخواست متعلق به مرکز دیگری است.");

            ApprovalLevelModel level = GetLevel(request.ChainID, request.CurrentLevelNo);

            if (level == null)
                return WorkflowActionResult.Fail("سطح تأیید جاری تعریف نشده است.");

            if (!CanApprove(level))
                return WorkflowActionResult.Fail("شما تأییدکننده این سطح نیستید.");

            // تصمیم ثبت می‌شود (چه تأیید چه رد).
            EntDb.Exec(@"
INSERT INTO EntApprovalAction
    (RequestID, LevelNo, Decision, Comment, ActionBy, ActionByUserID)
VALUES
    (@Req, @Level, @Decision, @Comment, @By, @ByID);",
                "@Req",      request.RequestID,
                "@Level",    request.CurrentLevelNo,
                "@Decision", approve ? DecisionApprove : DecisionReject,
                "@Comment",  comment,
                "@By",       SecurityContext.Username,
                "@ByID",     SecurityContext.UserId > 0 ? (object)SecurityContext.UserId : null);

            // وظیفه خودکارِ سطحی که همین حالا تصمیم گرفت، بسته می‌شود.
            TaskService.CloseBySource(TaskService.SourceApproval, request.RequestID,
                                      approve ? DecisionApprove : DecisionReject);

            if (!approve)
            {
                Close(request.RequestID, StatusRejected);

                AuditLogger.Log("ApprovalRejected", request.EntityName, request.EntityID,
                                request.Title, comment);

                return new WorkflowActionResult { Applied = true, Message = "درخواست رد شد." };
            }

            int nextLevel = NextLevelNo(request.ChainID, request.CurrentLevelNo);

            if (nextLevel > 0)
            {
                EntDb.Exec("UPDATE EntApprovalRequest SET CurrentLevelNo = @Level WHERE RequestID = @Id;",
                    "@Level", nextLevel, "@Id", request.RequestID);

                // وظیفه خودکار برای تأییدکننده سطح بعدی.
                RaiseTaskForCurrentLevel(GetRequest(request.RequestID));

                return new WorkflowActionResult
                {
                    Applied         = true,
                    PendingApproval = true,
                    Message         = "تأیید شما ثبت شد؛ درخواست به سطح بعدی منتقل شد."
                };
            }

            // سطح آخر تأیید کرد → درخواست نهایی می‌شود.
            Close(request.RequestID, StatusApproved);

            AuditLogger.Log("ApprovalApproved", request.EntityName, request.EntityID,
                            request.Title, comment);

            string message = "درخواست به‌طور کامل تأیید شد.";

            // اگر از یک گذار گردش‌کار آمده بود، همان گذار حالا اعمال می‌شود.
            if (request.WorkflowInstanceID > 0 && request.TargetStateID > 0)
            {
                WorkflowInstanceModel instance =
                    WorkflowService.GetInstance(request.EntityName, request.EntityID);

                if (instance != null && instance.InstanceID == request.WorkflowInstanceID)
                {
                    WorkflowActionResult moved = WorkflowService.MoveTo(
                        instance, request.TargetStateID,
                        request.TransitionID > 0 ? (int?)request.TransitionID : null,
                        "تأیید نهایی درخواست #" + request.RequestID);

                    if (moved.Applied)
                        message += " " + moved.Message;
                }
            }

            return new WorkflowActionResult { Applied = true, Message = message };
        }

        public static WorkflowActionResult Cancel(int requestId)
        {
            ApprovalRequestModel request = GetRequest(requestId);

            if (request == null)   return WorkflowActionResult.Fail("درخواست پیدا نشد.");
            if (!request.IsPending) return WorkflowActionResult.Fail("این درخواست باز نیست.");

            // فقط درخواست‌دهنده یا مدیر سیستم می‌تواند لغو کند.
            bool isOwner = string.Equals(request.RequestedBy, SecurityContext.Username,
                                         StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !SecurityContext.IsAdmin())
                return WorkflowActionResult.Fail("لغو این درخواست فقط توسط درخواست‌دهنده یا مدیر سیستم ممکن است.");

            Close(requestId, StatusCanceled);
            TaskService.CloseBySource(TaskService.SourceApproval, requestId, "درخواست لغو شد");

            return new WorkflowActionResult { Applied = true, Message = "درخواست لغو شد." };
        }

        // آیا کاربر جاری تأییدکننده این سطح است؟
        public static bool CanApprove(ApprovalLevelModel level)
        {
            if (level == null) return false;

            // مدیر کل همیشه می‌تواند (قفل‌نشدن سیستم وقتی تأییدکننده در دسترس نیست).
            if (SecurityContext.IsSuperAdmin()) return true;

            if (level.ApproverUserID > 0)
                return level.ApproverUserID == SecurityContext.UserId;

            if (!string.IsNullOrWhiteSpace(level.ApproverRole))
                return string.Equals(level.ApproverRole, SecurityContext.Role,
                                     StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(level.RequiredPermission))
                return WorkflowService.PermissionGate == null
                    ? SecurityContext.IsAdmin()
                    : WorkflowService.PermissionGate(level.RequiredPermission);

            return SecurityContext.IsAdmin();
        }

        // ─── خواندن ───────────────────────────────────────────────────────
        public static ApprovalChainModel GetChain(int chainId)
        {
            return ReadChain(EntDb.Query(@"
SELECT ChainID, Code, Name, EntityName, IsActive
FROM   EntApprovalChain WHERE ChainID = @Id;", "@Id", chainId));
        }

        public static ApprovalChainModel GetActiveChain(string entityName)
        {
            return ReadChain(EntDb.Query(@"
SELECT ChainID, Code, Name, EntityName, IsActive
FROM   EntApprovalChain
WHERE  EntityName = @Entity AND IsActive = 1
ORDER  BY ChainID LIMIT 1;", "@Entity", entityName));
        }

        public static List<ApprovalChainModel> GetChains()
        {
            List<ApprovalChainModel> list = new List<ApprovalChainModel>();

            DataTable table = EntDb.Query(@"
SELECT ChainID, Code, Name, EntityName, IsActive
FROM   EntApprovalChain ORDER BY EntityName, Name;");

            foreach (DataRow row in table.Rows)
                list.Add(new ApprovalChainModel
                {
                    ChainID    = EntDb.ToInt (row["ChainID"]),
                    Code       = EntDb.ToText(row["Code"]),
                    Name       = EntDb.ToText(row["Name"]),
                    EntityName = EntDb.ToText(row["EntityName"]),
                    IsActive   = EntDb.ToBool(row["IsActive"])
                });

            return list;
        }

        public static List<ApprovalLevelModel> GetLevels(int chainId)
        {
            List<ApprovalLevelModel> list = new List<ApprovalLevelModel>();

            DataTable table = EntDb.Query(@"
SELECT LevelID, ChainID, LevelNo, Name, ApproverRole, ApproverUserID, RequiredPermission
FROM   EntApprovalLevel WHERE ChainID = @Id ORDER BY LevelNo;", "@Id", chainId);

            foreach (DataRow row in table.Rows)
                list.Add(ReadLevel(row));

            return list;
        }

        public static ApprovalLevelModel GetLevel(int chainId, int levelNo)
        {
            DataTable table = EntDb.Query(@"
SELECT LevelID, ChainID, LevelNo, Name, ApproverRole, ApproverUserID, RequiredPermission
FROM   EntApprovalLevel WHERE ChainID = @Id AND LevelNo = @No;",
                "@Id", chainId, "@No", levelNo);

            return table.Rows.Count == 0 ? null : ReadLevel(table.Rows[0]);
        }

        public static ApprovalRequestModel GetRequest(int requestId)
        {
            DataTable table = EntDb.Query(@"
SELECT RequestID, ChainID, EntityName, EntityID, Title, Reason, Status, CurrentLevelNo,
       WorkflowInstanceID, TransitionID, TargetStateID, CenterID, RequestedBy, RequestedAt
FROM   EntApprovalRequest WHERE RequestID = @Id;", "@Id", requestId);

            if (table.Rows.Count == 0) return null;
            DataRow row = table.Rows[0];

            return new ApprovalRequestModel
            {
                RequestID          = EntDb.ToInt (row["RequestID"]),
                ChainID            = EntDb.ToInt (row["ChainID"]),
                EntityName         = EntDb.ToText(row["EntityName"]),
                EntityID           = EntDb.ToInt (row["EntityID"]),
                Title              = EntDb.ToText(row["Title"]),
                Reason             = EntDb.ToText(row["Reason"]),
                Status             = EntDb.ToText(row["Status"]),
                CurrentLevelNo     = EntDb.ToInt (row["CurrentLevelNo"]),
                WorkflowInstanceID = EntDb.ToInt (row["WorkflowInstanceID"]),
                TransitionID       = EntDb.ToInt (row["TransitionID"]),
                TargetStateID      = EntDb.ToInt (row["TargetStateID"]),
                CenterID           = EntDb.ToInt (row["CenterID"]),
                RequestedBy        = EntDb.ToText(row["RequestedBy"]),
                RequestedAt        = EntDb.ToDate(row["RequestedAt"])
            };
        }

        // درخواست‌های باز که کاربر جاری واقعاً تأییدکننده سطح فعلی آن‌هاست.
        public static DataTable GetMyInbox()
        {
            DataTable all = GetRequests(StatusPending);
            DataTable mine = all.Clone();

            foreach (DataRow row in all.Rows)
            {
                int chainId = EntDb.ToInt(EntDb.Col(row, "ChainID"));
                int levelNo = EntDb.ToInt(EntDb.Col(row, "LevelNo"));

                if (CanApprove(GetLevel(chainId, levelNo)))
                    mine.ImportRow(row);
            }

            return mine;
        }

        public static DataTable GetRequests(string status)
        {
            return EntDb.Query(@"
SELECT r.RequestID      AS 'شناسه',
       r.ChainID        AS 'ChainID',
       r.CurrentLevelNo AS 'LevelNo',
       c.Name           AS 'زنجیره',
       r.Title          AS 'عنوان',
       r.EntityName     AS 'موجودیت',
       r.EntityID       AS 'شناسه رکورد',
       l.Name           AS 'سطح جاری',
       r.Status         AS 'وضعیت',
       r.RequestedBy    AS 'درخواست‌دهنده',
       r.RequestedAt    AS 'تاریخ درخواست',
       r.Reason         AS 'توضیح'
FROM   EntApprovalRequest r
LEFT   JOIN EntApprovalChain c ON c.ChainID = r.ChainID
LEFT   JOIN EntApprovalLevel l ON l.ChainID = r.ChainID AND l.LevelNo = r.CurrentLevelNo
WHERE  (IFNULL(@Status, '') = '' OR r.Status = @Status)
  AND  (@Center = 0 OR IFNULL(r.CenterID, 0) = @Center)
ORDER  BY r.RequestID DESC;",
                "@Status", status ?? "",
                "@Center", SecurityContext.CenterFilterId);
        }

        public static DataTable GetActions(int requestId)
        {
            return EntDb.Query(@"
SELECT LevelNo  AS 'سطح',
       Decision AS 'تصمیم',
       ActionBy AS 'کاربر',
       ActionAt AS 'تاریخ',
       Comment  AS 'توضیح'
FROM   EntApprovalAction
WHERE  RequestID = @Id
ORDER  BY ActionID;", "@Id", requestId);
        }

        // ─── وظایف خودکار (ویژگی ۳) ───────────────────────────────────────
        // برای تأییدکننده سطح جاریِ یک درخواست، وظیفه می‌سازد. اگر سطح به یک
        // کاربر مشخص وصل باشد وظیفه شخصی است، وگرنه به نقش تخصیص می‌یابد.
        private static void RaiseTaskForCurrentLevel(ApprovalRequestModel request)
        {
            if (request == null || !request.IsPending) return;

            ApprovalLevelModel level = GetLevel(request.ChainID, request.CurrentLevelNo);
            if (level == null) return;

            string role = level.ApproverUserID > 0 ? null : level.ApproverRole;

            TaskService.CreateAuto(
                "تأیید: " + request.Title,
                "درخواست تأیید #" + request.RequestID + " در سطح «" + level.Name + "»",
                request.EntityName, request.EntityID,
                level.ApproverUserID, role,
                TaskService.SourceApproval, request.RequestID);
        }

        private static ApprovalRequestModel GetOpenRequest(string entityName, int entityId, int transitionId)
        {
            int requestId = EntDb.ToInt(EntDb.Scalar(@"
SELECT MAX(RequestID) FROM EntApprovalRequest
WHERE  EntityName = @Entity AND EntityID = @Id
  AND  TransitionID = @Tr AND Status = @Status;",
                "@Entity", entityName, "@Id", entityId,
                "@Tr", transitionId, "@Status", StatusPending));

            return requestId <= 0 ? null : GetRequest(requestId);
        }

        // ─── کمکی‌ها ──────────────────────────────────────────────────────
        private static void Close(int requestId, string status)
        {
            EntDb.Exec(@"
UPDATE EntApprovalRequest
SET    Status = @Status, CompletedAt = datetime('now')
WHERE  RequestID = @Id;", "@Status", status, "@Id", requestId);
        }

        private static int NextLevelNo(int chainId, int currentLevelNo)
        {
            return EntDb.ToInt(EntDb.Scalar(@"
SELECT MIN(LevelNo) FROM EntApprovalLevel
WHERE  ChainID = @Id AND LevelNo > @No;", "@Id", chainId, "@No", currentLevelNo));
        }

        private static ApprovalChainModel ReadChain(DataTable table)
        {
            if (table.Rows.Count == 0) return null;
            DataRow row = table.Rows[0];

            return new ApprovalChainModel
            {
                ChainID    = EntDb.ToInt (row["ChainID"]),
                Code       = EntDb.ToText(row["Code"]),
                Name       = EntDb.ToText(row["Name"]),
                EntityName = EntDb.ToText(row["EntityName"]),
                IsActive   = EntDb.ToBool(row["IsActive"])
            };
        }

        private static ApprovalLevelModel ReadLevel(DataRow row)
        {
            return new ApprovalLevelModel
            {
                LevelID            = EntDb.ToInt (row["LevelID"]),
                ChainID            = EntDb.ToInt (row["ChainID"]),
                LevelNo            = EntDb.ToInt (row["LevelNo"]),
                Name               = EntDb.ToText(row["Name"]),
                ApproverRole       = EntDb.ToText(row["ApproverRole"]),
                ApproverUserID     = EntDb.ToInt (row["ApproverUserID"]),
                RequiredPermission = EntDb.ToText(row["RequiredPermission"])
            };
        }
    }
}
