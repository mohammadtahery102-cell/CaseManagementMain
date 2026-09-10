using System.Collections.Generic;

namespace CaseManagement.Assets.Domain
{
    public static class AssetCodes
    {
        public const string EntityAcquisition = "FaAcquisition";
        public const string EntityDisposal = "FaDisposal";
        public const string EntityDepreciation = "FaDepreciation";
        public const string DocAcquisition = "FaAcquisition";
        public const string DocDisposal = "FaDisposal";
        public const string DocDepreciation = "FaDepreciation";
        public const string StatusActive = "Active";
        public const string StatusDisposed = "Disposed";
        public const string AccountAsset = "1500";
        public const string AccountAccum = "1590";
        public const string AccountCash = "1101";
        public const string AccountDepExp = "5500";
        public const string AccountDisposalLoss = "5510";
    }

    public static class AssetPermissions
    {
        public const string View = "Assets.View";
        public const string Create = "Assets.Create";
        public const string Approve = "Assets.Approve";
        public const string Post = "Assets.Post";
    }

    public class FaAsset
    {
        public long AssetId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public long CategoryId { get; set; }
        public long LocationId { get; set; }
        public long CustodianId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public long CostMinor { get; set; }
        public long AccumDepMinor { get; set; }
        public int UsefulLifeMonths { get; set; }
        public string Status { get; set; }
        public long RowVersion { get; set; }
        public long NbvMinor { get { return CostMinor - AccumDepMinor; } }
    }

    public class FaAcquisition
    {
        public long AcquisitionId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string DocDate { get; set; }
        public long AssetId { get; set; }
        public long AmountMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class FaDisposal
    {
        public long DisposalId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string DocDate { get; set; }
        public long AssetId { get; set; }
        public long CostMinor { get; set; }
        public long AccumMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class FaDepreciation
    {
        public long DepreciationId { get; set; }
        public int CompanyId { get; set; }
        public int CenterId { get; set; }
        public string DocNo { get; set; }
        public string Status { get; set; }
        public string DocDate { get; set; }
        public long TotalMinor { get; set; }
        public long RowVersion { get; set; }
    }

    public class AssetRegisterRow
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public long CostMinor { get; set; }
        public long AccumDepMinor { get; set; }
        public long NbvMinor { get; set; }
    }

    public class DepreciationReportRow
    {
        public string DocNo { get; set; }
        public string DocDate { get; set; }
        public long TotalMinor { get; set; }
        public string Status { get; set; }
    }

    public class AssetMovementRow
    {
        public string Kind { get; set; }
        public string DocNo { get; set; }
        public string DocDate { get; set; }
        public string AssetCode { get; set; }
    }
}
