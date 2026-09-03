using System;

namespace CaseManagement.Enterprise
{
    // ─────────────────────────────────────────────────────────────────────────
    // مدل‌های ساده (DTO) هسته سازمانی — متناظر یک‌به‌یک با جداول Ent*.
    // آموزش: مثل Models/CaseModel.cs فقط ساختار داده هستند و هیچ منطقی ندارند؛
    // منطق در سرویس‌ها (WorkflowService و ...) قرار دارد.
    // ─────────────────────────────────────────────────────────────────────────

    // ─── ویژگی ۱: گردش‌کار ───────────────────────────────────────────────────
    public class WorkflowModel
    {
        public int    WorkflowID  { get; set; }
        public string Code        { get; set; }
        public string Name        { get; set; }
        public string EntityName  { get; set; }
        public string Description { get; set; }
        public bool   IsActive    { get; set; }
    }

    public class WorkflowStateModel
    {
        public int    StateID    { get; set; }
        public int    WorkflowID { get; set; }
        public string Code       { get; set; }
        public string Name       { get; set; }
        public bool   IsInitial  { get; set; }
        public bool   IsFinal    { get; set; }
        public int    SortOrder  { get; set; }
        public string Color      { get; set; }

        public override string ToString() { return Name; }
    }

    public class WorkflowTransitionModel
    {
        public int    TransitionID       { get; set; }
        public int    WorkflowID         { get; set; }
        public int    FromStateID        { get; set; }
        public int    ToStateID          { get; set; }
        public string Name               { get; set; }
        public string RequiredPermission { get; set; }
        public bool   RequiresApproval   { get; set; }
        public int    ApprovalChainID    { get; set; }
        public int    SortOrder          { get; set; }

        // برای نمایش در فرم‌ها (پر می‌شود توسط WorkflowService)
        public string FromStateName { get; set; }
        public string ToStateName   { get; set; }

        public override string ToString() { return Name; }
    }

    public class WorkflowInstanceModel
    {
        public int      InstanceID     { get; set; }
        public int      WorkflowID     { get; set; }
        public string   EntityName     { get; set; }
        public int      EntityID       { get; set; }
        public int      CurrentStateID { get; set; }
        public string   Status         { get; set; }
        public int      CenterID       { get; set; }
        public string   StartedBy      { get; set; }
        public DateTime? StartedAt     { get; set; }

        // نام مرحله جاری (پر می‌شود توسط WorkflowService)
        public string CurrentStateName  { get; set; }
        public string CurrentStateCode  { get; set; }
        public string CurrentStateColor { get; set; }
        public bool   IsFinal           { get; set; }
    }

    // ─── ویژگی ۲: تأیید چندسطحی ─────────────────────────────────────────────
    public class ApprovalChainModel
    {
        public int    ChainID    { get; set; }
        public string Code       { get; set; }
        public string Name       { get; set; }
        public string EntityName { get; set; }
        public bool   IsActive   { get; set; }

        public override string ToString() { return Name; }
    }

    public class ApprovalLevelModel
    {
        public int    LevelID            { get; set; }
        public int    ChainID            { get; set; }
        public int    LevelNo            { get; set; }
        public string Name               { get; set; }
        public string ApproverRole       { get; set; }
        public int    ApproverUserID     { get; set; }
        public string RequiredPermission { get; set; }
    }

    public class ApprovalRequestModel
    {
        public int      RequestID          { get; set; }
        public int      ChainID            { get; set; }
        public string   EntityName         { get; set; }
        public int      EntityID           { get; set; }
        public string   Title              { get; set; }
        public string   Reason             { get; set; }
        public string   Status             { get; set; }
        public int      CurrentLevelNo     { get; set; }
        public int      WorkflowInstanceID { get; set; }
        public int      TransitionID       { get; set; }
        public int      TargetStateID      { get; set; }
        public int      CenterID           { get; set; }
        public string   RequestedBy        { get; set; }
        public DateTime? RequestedAt       { get; set; }

        public bool IsPending
        {
            get { return string.Equals(Status, ApprovalService.StatusPending, StringComparison.Ordinal); }
        }
    }
}
