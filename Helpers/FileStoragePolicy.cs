using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CaseManagement.Helpers
{
    public static class FileKindPolicy
    {
        private static readonly HashSet<string> PhotoKinds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                FileKinds.HeadGuardian, FileKinds.OrphanGuardian, FileKinds.Family,
                FileKinds.Member, FileKinds.Representative, FileKinds.Visit
            };

        public static string FromSection(string section)
        {
            if (string.Equals(section, FileHelper.SectionHeadPhoto, StringComparison.OrdinalIgnoreCase))
                return FileKinds.HeadGuardian;
            if (string.Equals(section, FileHelper.SectionGuardianPhotos, StringComparison.OrdinalIgnoreCase))
                return FileKinds.OrphanGuardian;
            if (string.Equals(section, FileHelper.SectionFamilyPhoto, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Family;
            if (string.Equals(section, FileHelper.SectionMemberPhotos, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Member;
            if (string.Equals(section, FileHelper.SectionRepresentativePhotos, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Representative;
            if (string.Equals(section, FileHelper.SectionVisitPhotos, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Visit;
            if (string.Equals(section, FileHelper.SectionDocs, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Document;
            if (string.Equals(section, FileHelper.SectionCaseFiles, StringComparison.OrdinalIgnoreCase))
                return FileKinds.CaseFile;
            if (string.Equals(section, FileHelper.SectionExports, StringComparison.OrdinalIgnoreCase))
                return FileKinds.Export;
            return FileKinds.Other;
        }

        public static string SectionForKind(string kind)
        {
            if (string.Equals(kind, FileKinds.HeadGuardian, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionHeadPhoto;
            if (string.Equals(kind, FileKinds.OrphanGuardian, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionGuardianPhotos;
            if (string.Equals(kind, FileKinds.Family, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionFamilyPhoto;
            if (string.Equals(kind, FileKinds.Member, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionMemberPhotos;
            if (string.Equals(kind, FileKinds.Representative, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionRepresentativePhotos;
            if (string.Equals(kind, FileKinds.Visit, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionVisitPhotos;
            if (string.Equals(kind, FileKinds.Document, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionDocs;
            if (string.Equals(kind, FileKinds.CaseFile, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionCaseFiles;
            if (string.Equals(kind, FileKinds.Export, StringComparison.OrdinalIgnoreCase))
                return FileHelper.SectionExports;
            return FileHelper.SectionDocs;
        }

        public static bool IsPhoto(string kind)
        {
            return PhotoKinds.Contains(kind ?? "");
        }

        public static long MaxSize(string kind)
        {
            return IsPhoto(kind) ? FileHelper.MaxPhotoFileSizeBytes : FileHelper.MaxDocumentFileSizeBytes;
        }
    }

    public static class FileNamingPolicy
    {
        public static string Build(string caseCode, string kind, string context,
            DateTime date, int sequence, string extension)
        {
            string code = FileHelper.CleanName(caseCode);
            string type = FileHelper.CleanName(string.IsNullOrWhiteSpace(kind) ? FileKinds.Other : kind)
                .Replace(" ", "");
            string ctx = string.IsNullOrWhiteSpace(context)
                ? "" : "_" + FileHelper.CleanName(context).Replace(" ", "_");
            string ext = (extension ?? "").Trim().TrimStart('.').ToLowerInvariant();
            int safeSequence = Math.Max(1, sequence);
            return string.Format(CultureInfo.InvariantCulture,
                "{0}_{1}{2}_{3:yyyy-MM-dd}_{4:D2}{5}",
                code, type, ctx, date.Date, safeSequence,
                string.IsNullOrWhiteSpace(ext) ? "" : "." + ext);
        }

        public static string NextAvailablePath(string folder, string caseCode, string kind,
            string context, DateTime date, string extension)
        {
            Directory.CreateDirectory(folder);
            for (int sequence = 1; sequence < 10000; sequence++)
            {
                string path = Path.Combine(folder,
                    Build(caseCode, kind, context, date, sequence, extension));
                if (!File.Exists(path)) return path;
            }
            throw new IOException("تعداد فایل‌های هم‌نام از حد مجاز بیشتر است.");
        }
    }

    public static class ExportPathProvider
    {
        public static string GetCaseFilesFolder(string caseCode, string childFolder)
        {
            return GetSafeChild(FileHelper.GetSectionFolder(caseCode, FileHelper.SectionCaseFiles), childFolder);
        }

        public static string GetExportsFolder(string caseCode, string childFolder)
        {
            return GetSafeChild(FileHelper.GetSectionFolder(caseCode, FileHelper.SectionExports), childFolder);
        }

        public static string GetSystemReportsFolder()
        {
            string folder = Path.Combine(FileHelper.GetBaseRootFolder(), "_Reports");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetSafeChild(string parent, string child)
        {
            if (string.IsNullOrWhiteSpace(parent)) return "";
            if (string.IsNullOrWhiteSpace(child)) return parent;
            string folder = Path.Combine(parent, FileHelper.CleanName(child));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
