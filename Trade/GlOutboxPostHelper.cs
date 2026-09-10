using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;

namespace CaseManagement.Trade
{
    public static class GlOutboxPostHelper
    {
        public static LedgerResult PostOrApprove(IGeneralLedger gl, PostJournalCommand cmd, ILedgerIdentity identity)
        {
            LedgerResult posted = gl.Post(cmd, identity);
            if (posted.Ok) return posted;
            if (posted.ErrorCode != LedgerErrorCodes.ApprovalRequired) return posted;
            if (!identity.HasPermission(LedgerPermissions.Create) || !identity.HasPermission(LedgerPermissions.Approve))
                return posted;
            SaveDraftJournalCommand draft = new SaveDraftJournalCommand
            {
                CompanyId = cmd.CompanyId,
                CenterId = cmd.CenterId,
                PostingDate = cmd.PostingDate,
                DocumentDate = cmd.DocumentDate,
                ReferenceNumber = cmd.ReferenceNumber,
                Description = cmd.Description,
                JournalSource = cmd.JournalSource,
                SourceModule = cmd.SourceModule,
                SourceDocumentType = cmd.SourceDocumentType,
                SourceDocumentId = cmd.SourceDocumentId,
                Lines = cmd.Lines
            };
            LedgerResult saved = gl.SaveDraft(draft, identity);
            if (!saved.Ok) return saved;
            LedgerResult approved = gl.Approve(new JournalStatusCommand { JournalId = saved.JournalId, ExpectedRowVersion = saved.RowVersion }, identity);
            if (!approved.Ok) return approved;
            return gl.Post(new PostJournalCommand
            {
                JournalId = approved.JournalId > 0 ? approved.JournalId : saved.JournalId,
                ExpectedRowVersion = approved.RowVersion,
                CompanyId = cmd.CompanyId,
                CenterId = cmd.CenterId
            }, identity);
        }
    }
}
