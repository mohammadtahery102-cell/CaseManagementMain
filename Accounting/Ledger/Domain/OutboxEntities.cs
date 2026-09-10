using System;
using System.Collections.Generic;

namespace CaseManagement.Accounting.Ledger.Domain
{
    public class AccOutboxRow
    {
        public long OutboxId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string SourceModule { get; set; }
        public string DocumentType { get; set; }
        public long DocumentId { get; set; }
        public string Operation { get; set; }
        public string Payload { get; set; }
        public string Status { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public string NextAttemptAt { get; set; }
        public string LastError { get; set; }
        public string LastErrorCode { get; set; }
        public string CreatedAt { get; set; }
        public long RowVersion { get; set; }
    }

    public class OutboxSnapshot
    {
        public int Pending { get; set; }
        public int Processing { get; set; }
        public int Failed { get; set; }
        public int DeadLetter { get; set; }
        public int Completed { get; set; }
        public IList<AccOutboxRow> Recent { get; set; }
    }

    public static class OutboxRetryPolicy
    {
        public static TimeSpan DelayAfterFailure(int attemptCount)
        {
            if (attemptCount <= 1) return TimeSpan.FromSeconds(5);
            if (attemptCount == 2) return TimeSpan.FromSeconds(30);
            if (attemptCount == 3) return TimeSpan.FromMinutes(2);
            if (attemptCount == 4) return TimeSpan.FromMinutes(10);
            if (attemptCount == 5) return TimeSpan.FromMinutes(30);
            return TimeSpan.FromHours(2);
        }
    }
}
