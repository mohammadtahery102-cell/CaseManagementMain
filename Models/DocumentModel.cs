namespace CaseManagement.Models
{
    // ساختار ساده متناظر با جدول TblDocs.
    public class DocumentModel
    {
        public int DocID { get; set; }
        public int CasID { get; set; }
        public string DocType { get; set; }
        public string OriginalFileName { get; set; }
        public string DocFilePath { get; set; }
        public string RelatedCaseRef { get; set; }
        public string DocDescription { get; set; }
        public string DocCategory { get; set; }
        public string DocTags { get; set; }
    }
}
