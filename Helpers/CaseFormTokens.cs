using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Text.RegularExpressions;
using CaseManagement.DAL;

namespace CaseManagement.Helpers
{
    // ─────────────────────────────────────────────────────────────────────────
    // دادهٔ فورم‌های رسمیِ پرونده (فورم ۱ تا ۷).
    //
    // خروجی یک دیکشنریِ «نامِ توکن → مقدار» است که مستقیماً به
    // FrmDocxForm.Hidden یا DocxFormExport.WriteDocx داده می‌شود.
    //
    // آموزش — چرا *همهٔ* توکن‌های هر پنج قالب یک‌جا ساخته می‌شوند و نه فقط
    // توکن‌های قالبِ انتخاب‌شده: DocxFormExport.AssertNoTokensLeft اگر حتی یک
    // توکن جایگزین‌نشده در سند بماند خطا می‌دهد. کلیدِ اضافی هیچ هزینه‌ای
    // ندارد (روی متنی که در سند نیست اثری نمی‌گذارد)، ولی کلیدِ *کم* یعنی
    // فورمِ خراب. پس این کلاس همیشه فهرستِ کامل را برمی‌گرداند.
    //
    // آموزش — Ltr: شمارهٔ تذکره، تلفن و تاریخ در پاراگرافِ راست‌به‌چپِ Word
    // تکه‌تکه و برعکس چاپ می‌شوند («۱۴۰۱-۲۲۳۳-۴۴۵۵۱» می‌شود
    // «۴۴۵۵۱-۲۲۳۳-۱۴۰۱»)، چون Word رقم‌ها را در بافتِ فارسی به‌صورت AN
    // می‌بیند و خط تیره میانشان خنثی می‌شود. گذاشتن U+200E دو طرفِ مقدار این
    // را درست می‌کند و هیچ گلیفی هم چاپ نمی‌شود (برخلاف U+2066/U+2069 که در
    // فونت‌های B Nazanin مربعِ خالی می‌دهند).
    // ─────────────────────────────────────────────────────────────────────────
    public static class CaseFormTokens
    {
        public const string Checked   = "☒";   // ☒
        public const string Unchecked = "☐";   // ☐

        private const string LRM = "‎";
        private static readonly Regex ArabicLetters = new Regex(@"[؀-ۿ]");
        private static readonly Regex HasDigit      = new Regex(@"[0-9۰-۹]");

        public const int MaxFamilyRows = 8;

        // ── نقطهٔ ورود ───────────────────────────────────────────────────────
        public static Dictionary<string, string> Build(DatabaseHelper db, int caseId)
        {
            if (db == null) throw new ArgumentNullException("db");

            var t = new Dictionary<string, string>(StringComparer.Ordinal);

            DataRow c   = SingleRow(db, "SELECT * FROM TblCase        WHERE CasID = @id", caseId);
            DataRow orp = SingleRow(db, "SELECT * FROM TblOrphan      WHERE CasID = @id", caseId);
            DataRow dis = SingleRow(db, "SELECT * FROM TblDisability  WHERE CasID = @id", caseId);
            DataRow mig = SingleRow(db, "SELECT * FROM TblMigrant     WHERE CasID = @id", caseId);
            DataTable fam = db.Query(
                "SELECT * FROM TblFamily WHERE CasID = @id ORDER BY FamID LIMIT " + MaxFamilyRows,
                new SQLiteParameter("@id", caseId));

            if (c == null)
                throw new InvalidOperationException("پرونده با شناسهٔ " + caseId + " پیدا نشد.");

            string typeCode = RequestTypeCode(db, S(c, "RequestTypeID"));

            Header(t, c, db, typeCode);
            Head(t, c, dis, fam);
            Orphan(t, c, orp);
            Disability(t, c, dis);
            Migrant(t, c, mig);
            Family(t, fam);
            Survey(t, c);
            GuardianProxy(t, db, caseId);
            BlankGroups(t);

            return t;
        }

        // ── ورقهٔ وکالت موقت سرپرستی ایتام ───────────────────────────────────
        // آموزش — چرا توکن‌های این ورقه هم اینجا ساخته می‌شوند و نه در فرمِ
        // فراخواننده: تا امروز «وکالت موقت» تنها فورمی بود که داده‌اش را از
        // کادرهای روی صفحهٔ FrmCase می‌خواند، پس بیرونِ آن فرم قابلِ استفاده
        // نبود و با بقیهٔ فورم‌های رسمی یک‌جا جمع نمی‌شد. با آمدن به همین
        // کلاس، مرکزِ فورم‌های رسمی می‌تواند آن را مثلِ پنج فورمِ دیگر باز کند.
        //
        // نام/نام‌پدر/تذکرهٔ «سرپرست» در قالبِ این ورقه با توکن‌های Guardian*
        // علامت خورده، ولی همان مقدارِ Head* است — پس این‌ها مستعارند، نه
        // کوئریِ تازه.
        private static void GuardianProxy(Dictionary<string, string> t, DatabaseHelper db, int caseId)
        {
            // ⚠ «پرکردن فقط اگر خالی باشد» عمدی است: فورم ۱ همین توکن‌ها را از
            // TblOrphan.GuardianName پر می‌کند و بازنویسیِ آن، خروجیِ آن فورم را
            // خراب می‌کرد. اینجا فقط جای خالی پر می‌شود.
            Fill(t, "GuardianName", Get(t, "HeadName"));
            Fill(t, "GuardianFather", Get(t, "HeadFather"));
            Fill(t, "GuardianTazkira", Get(t, "HeadTazkira"));
            Fill(t, "GuardianTazkiraType", Get(t, "HeadIdCardType"));

            // تعدادِ ایتامِ تحتِ سرپرستی — همان شمارشی که FrmCase می‌کرد.
            int orphans = 0;
            try
            {
                object v = db.ExecuteScalar(
                    "SELECT COUNT(*) FROM TblFamily WHERE CasID = @id AND COALESCE(MemberRole,'') = 'یتیم'",
                    new SQLiteParameter("@id", caseId));
                if (v != null && v != DBNull.Value) orphans = Convert.ToInt32(v);
            }
            catch { }

            Fill(t, "OrphanCount", Ltr(orphans.ToString(CultureInfo.InvariantCulture)));

            // خانه‌های دستی؛ مقدارِ اولیه دارند تا دیالوگ خالی باز نشود، ولی
            // کاربر همه را می‌تواند عوض کند.
            string today = PersianDateHelper.ToPersianDateString(DateTime.Now);
            Fill(t, "IssueDate", Ltr(today));
            Fill(t, "FromDate", Ltr(today));
            Fill(t, "ToDate", "");
            Fill(t, "Reason", "");
            Fill(t, "ProxyName", "");
            Fill(t, "ProxyFather", "");
            Fill(t, "ProxyTazkira", "");
        }

        private static string Get(Dictionary<string, string> t, string key)
        {
            string v;
            return t.TryGetValue(key, out v) ? v : "";
        }

        // کلید را فقط وقتی می‌نویسد که وجود نداشته باشد یا خالی باشد.
        private static void Fill(Dictionary<string, string> t, string key, string value)
        {
            string current;
            if (t.TryGetValue(key, out current) && !string.IsNullOrWhiteSpace(current)) return;
            t[key] = value ?? "";
        }

        // ── سربرگ ────────────────────────────────────────────────────────────
        private static void Header(Dictionary<string, string> t, DataRow c,
                                   DatabaseHelper db, string typeCode)
        {
            // نامِ مؤسسه در سربرگِ هر پنج فورم چاپ می‌شود. اگر تنظیم نشده
            // باشد، به نامِ لاتین برمی‌گردیم — همان مؤسسه است، فقط به خطِ
            // دیگر. به CenterName برنمی‌گردیم چون آن نامِ *شعبه* است
            // («کابل»، «بلخ») و چاپش به‌جای نامِ مؤسسه سندِ رسمی را غلط
            // می‌کند.
            t["OrgName"] = Coalesce(SettingsHelper.Get(SettingsHelper.OrgName),
                                    SettingsHelper.Get(SettingsHelper.OrgNameEn));
            t["Province"]   = S(c, "Province");
            t["District"]   = S(c, "District");
            t["Zone"]       = S(c, "Zone");
            t["AcceptDate"] = Ltr(S(c, "CaseDate"));
            t["Code"]       = Ltr(S(c, "Code"));
            t["CaseNo"]     = Ltr(Coalesce(S(c, "CaseNo"), S(c, "FormNo")));
            t["PrintDate"]  = Ltr(PersianDateHelper.ToPersianDateString(DateTime.Now));
            t["FilledBy"]   = SecurityContext.Username ?? "";
            t["RequestTypeName"] = RequestTypeName(db, S(c, "RequestTypeID"), S(c, "RequestType"));

            string priority = S(c, "PriorityLevel");
            t["PR1"] = Chk(priority == "اول");
            t["PR2"] = Chk(priority == "دوم");
            t["PR3"] = Chk(priority == "سوم");

            t["C_Orphan"]      = Chk(typeCode == "ORPHAN");
            t["C_Disabled"]    = Chk(typeCode == "DISABLED");
            t["C_Migrant"]     = Chk(typeCode == "MIGRANT");
            t["C_BadGuardian"] = Chk(typeCode == "BADLY_SUPPORTED_CHILD");
            t["C_Vulnerable"]  = Chk(typeCode == "UNSUPPORTED_CHILD");
            t["C_Elderly"]     = Chk(typeCode == "ELDERLY");
            t["C_Widow"]       = Unchecked;   // نوعِ «ارامل» در TblRequestType وجود ندارد
            t["C_Other"]       = Chk(typeCode.Length == 0);
        }

        // ── سرپرست / متقاضی ──────────────────────────────────────────────────
        private static void Head(Dictionary<string, string> t, DataRow c,
                                 DataRow dis, DataTable fam)
        {
            t["HeadName"]           = S(c, "HeadFullName");
            t["HeadFather"]         = S(c, "HeadFatherName");
            t["HeadTazkira"]        = Ltr(S(c, "HeadTazkiraNo"));
            t["HeadIdCardType"]     = S(c, "HeadIdCardType");
            t["HeadBirthDate"]      = Ltr(S(c, "HeadBirthDate"));
            t["HeadPhone"]          = Ltr(S(c, "Phone"));
            t["RelativePhone"]      = Ltr(S(c, "RelativePhone"));
            t["Job"]                = S(c, "Job");
            t["Skill"]              = S(c, "Skill");
            t["Education"]          = S(c, "EducationLevel");
            t["Covered"]            = Coalesce(S(c, "CoveredByOrgNames"), S(c, "CoveredByOrg"));
            t["RelationToChildren"] = S(c, "RelationshipToFamily");
            t["OriginRes"]          = S(c, "HeadOriginalResidence");
            t["CurrentRes"]         = S(c, "HeadCurrentResidence");
            t["LocationLink"]       = Ltr(S(c, "LocationAddress"));
            t["PhysicalNotes"]      = S(c, "PhysicalStatusNotes");
            t["Dependents"]         = Ltr(fam.Rows.Count.ToString(CultureInfo.InvariantCulture));
            t["Referrer"]           = S(c, "ReferrerName");
            t["ReferrerPhone"]      = Ltr(S(c, "ReferrerPhone"));

            string sadat = S(c, "HeadSadat");
            t["SD_Sadat"] = Chk(sadat.Contains("سادات"));
            t["SD_Aam"]   = Chk(sadat.Length > 0 && !sadat.Contains("سادات"));

            string religion = S(c, "Religion");
            t["RG_Shia"]  = Chk(religion.Contains("تشیع") || religion.Contains("شیعه"));
            t["RG_Sunni"] = Chk(religion.Contains("تسنن") || religion.Contains("سنی"));

            string marital = S(c, "MaritalStatus");
            t["MR_Single"]  = Chk(marital.Contains("مجرد"));
            t["MR_Married"] = Chk(marital.Contains("متاهل") || marital.Contains("متأهل"));

            // جنسیتِ سرپرست در TblCase نگه‌داری نمی‌شود؛ با قلم تیک می‌خورد.
            t["GN_Male"]   = Unchecked;
            t["GN_Female"] = Unchecked;

            // «سالم» یعنی هیچ نوعِ معلولیتی ثبت نشده — همان قاعده‌ای که
            // FrmCase.UpdateHeadPhysicalState روی chkHeadHealthy اعمال می‌کند.
            bool disabled = S(c, "DisabilityType").Length > 0 ||
                            (dis != null && S(dis, "DisabilityType").Length > 0);
            t["PH_Healthy"]  = Chk(!disabled);
            t["PH_Disabled"] = Chk(disabled);

            // فورم ۷ — همراهِ بیمار در پایگاه داده نیست؛ در دیالوگ تایپ می‌شود.
            t["CompanionName"]     = "";
            t["CompanionFather"]   = "";
            t["CompanionTazkira"]  = "";
            t["CompanionPhone"]    = "";
            t["CompanionAddress"]  = "";
            t["CompanionRelation"] = "";
        }

        // ── پدرِ ایتام / سرپرستِ فعلی ─────────────────────────────────────────
        private static void Orphan(Dictionary<string, string> t, DataRow c, DataRow orp)
        {
            t["FaName"]         = "";
            t["FaFather"]       = "";
            t["FaKnownAs"]      = "";
            t["FaTazkira"]      = "";
            t["FaFatehaPlace"]  = "";
            t["FaDeathPlace"]   = "";
            t["FaOriginRes"]    = Pick(orp, "MainResidenceProvince", c, "MainResidenceProvince");
            t["FaDeathCause"]   = Pick(orp, "FatherDeathCause", c, "FatherDeathCause");
            t["FaDeathDate"]    = Ltr(Pick(orp, "FatherDeathDate", c, "FatherDeathDate"));
            t["OrphanCount"]    = "";
            t["GuardianName"]     = orp != null ? S(orp, "GuardianName") : "";
            t["GuardianRelation"] = orp != null ? S(orp, "GuardianRelationship") : "";

            t["FaSD_Aam"]   = Unchecked;
            t["FaSD_Sadat"] = Unchecked;
            t["FaRG_Shia"]  = Unchecked;
            t["FaRG_Sunni"] = Unchecked;

            string mother = orp != null ? S(orp, "MotherStatus") : "";
            t["MO_Alive"]     = Chk(mother.Contains("حیات") || mother.Contains("زنده"));
            t["MO_Dead"]      = Chk(mother.Contains("متوفی") || mother.Contains("فوت"));
            t["MO_Remarried"] = Chk(mother.Contains("مجدد"));
        }

        // ── معلولیت ──────────────────────────────────────────────────────────
        private static void Disability(Dictionary<string, string> t, DataRow c, DataRow dis)
        {
            string type   = Pick(dis, "DisabilityType",   c, "DisabilityType");
            string degree = Pick(dis, "DisabilityDegree", c, "DisabilityDegree");

            t["DisabilityCause"]       = Pick(dis, "DisabilityCause", c, "DisabilityCause");
            t["DisabilityDescription"] = Pick(dis, "DisabilityDescription", c, "DisabilityDescription");
            t["SpecialNeeds"]          = Pick(dis, "SpecialNeeds", c, "SpecialNeeds");
            t["DisabilityCardNo"]      = Ltr(Pick(dis, "DisabilityCardNumber", c, "DisabilityCardNumber"));

            t["D_Phys"]  = Chk(type.Contains("جسمی") || type.Contains("حرکتی"));
            t["D_Hear"]  = Chk(type.Contains("شنوایی"));
            t["D_Vis"]   = Chk(type.Contains("بینایی"));
            t["D_Ment"]  = Chk(type.Contains("ذهنی") || type.Contains("روانی"));
            t["D_Multi"] = Chk(type.Contains("چندگانه"));

            t["DS_Mild"]    = Chk(degree.Contains("خفیف") || degree == "اول");
            t["DS_Mod"]     = Chk(degree.Contains("متوسط") || degree == "دوم");
            t["DS_Severe"]  = Chk(degree.Contains("شدید") && !degree.Contains("خیلی"));
            t["DS_VSevere"] = Chk(degree.Contains("خیلی شدید"));

            // مقایسهٔ کارتِ معلولیت دقیقاً برابر است نه Contains — «ندارد»
            // شاملِ «دارد» است و همان اشتباهی است که تصمیم #۵۳ پروژه هشدار داده.
            string card = Pick(dis, "HasDisabilityCard", c, "DisabilityCardStatus");
            t["DC_Yes"]     = Chk(card == "دارد");
            t["DC_No"]      = Chk(card == "ندارد");
            t["DC_Pending"] = Chk(card == "در حال اقدام");

            t["DN_Rehab"] = Unchecked;
            t["DN_Aids"]  = Unchecked;
            t["DN_Fin"]   = Unchecked;
            t["DN_House"] = Unchecked;

            t["I_Lt2000"]  = Unchecked;
            t["I_2to7"]    = Unchecked;
            t["I_Gt8"]     = Unchecked;
            t["IncomeNote"] = "";
        }

        // ── مهاجرت ───────────────────────────────────────────────────────────
        private static void Migrant(Dictionary<string, string> t, DataRow c, DataRow mig)
        {
            string cardType = Pick(mig, "MigrationCardType", c, "MigrationCardType");

            t["MDocName"] = cardType;
            t["MDocNo"]   = Ltr(Pick(mig, "MigrationCardNumber", c, "MigrationCardNumber"));
            t["OriginCountry"]      = mig != null ? S(mig, "OriginCountry") : "";
            t["DestinationCountry"] = mig != null ? S(mig, "DestinationCountry") : "";
            t["DepartureDate"]      = Ltr(Pick(mig, "DepartureDate", c, "DepartureDate"));
            t["ArrivalDate"]        = Ltr(Pick(mig, "ArrivalDate", c, "ArrivalDate"));
            t["AssistanceMonths"]   = Ltr(Pick(mig, "AssistanceDuration", c, "AssistanceDurationMonths"));
            t["OutsideDuration"] = "";
            t["InsideDuration"]  = "";
            t["PrimaryNeed"]     = "";

            t["MD_Gov"]   = Chk(cardType.Contains("دولتی"));
            t["MD_Intl"]  = Chk(cardType.Contains("بین"));
            t["MD_Other"] = Chk(cardType.Length > 0 &&
                                !cardType.Contains("دولتی") && !cardType.Contains("بین"));

            t["MID_Tazkira"]  = Chk(S(c, "HeadIdCardType").Contains("تذکره"));
            t["MID_Passport"] = Chk(S(c, "HeadIdCardType").Contains("پاسپورت"));
            t["MID_None"]     = Unchecked;

            t["MH_Own"]    = Unchecked;
            t["MH_Rent"]   = Unchecked;
            t["MH_Sawabi"] = Unchecked;
            t["MH_Gerawi"] = Unchecked;
            t["MS_Temp"]    = Unchecked;
            t["MS_Perm"]    = Unchecked;
            t["MS_Unknown"] = Unchecked;
            t["ME_Forced"]     = Unchecked;
            t["ME_Voluntary"]  = Unchecked;
        }

        // ── عائله ────────────────────────────────────────────────────────────
        private static void Family(Dictionary<string, string> t, DataTable fam)
        {
            for (int i = 1; i <= MaxFamilyRows; i++)
            {
                DataRow r = i <= fam.Rows.Count ? fam.Rows[i - 1] : null;
                string p = "F" + i.ToString(CultureInfo.InvariantCulture);

                t[p + "Name"] = r == null ? "" : S(r, "MemberName");
                t[p + "Fath"] = r == null ? "" : S(r, "MemberFatherName");
                t[p + "Tazk"] = r == null ? "" : Ltr(S(r, "MemberTazkiraNo"));
                t[p + "Bir"]  = r == null ? "" : Ltr(S(r, "BirthDate"));
                t[p + "Gen"]  = r == null ? "" : S(r, "Gender");
                t[p + "Sad"]  = r == null ? "" : S(r, "MemberSadat");
                t[p + "Edu"]  = r == null ? "" : S(r, "MemberEducation");
                t[p + "Phy"]  = r == null ? "" : S(r, "PhysicalStatus");
            }
        }

        // ── بررسی و بازدید ───────────────────────────────────────────────────
        private static void Survey(Dictionary<string, string> t, DataRow c)
        {
            t["Surveyors"]     = S(c, "Surveyors");
            t["SurveyManager"] = "";
            t["SurveyDate"]    = Ltr(S(c, "SurveyDate"));
            t["VisitNo"]       = "";
            t["SurveyResult"]  = "";

            string band = S(c, "VulnerabilityBand");
            t["VB_High"] = Chk(band.Contains("پرخطر") || band.Contains("بالا"));
            t["VB_Mid"]  = Chk(band.Contains("متوسط"));
            t["VB_Low"]  = Chk(band.Contains("کم"));

            string urgent = S(c, "UrgentSituation");
            t["U_Urgent"]    = Chk(urgent.Contains("عاجل") && !urgent.Contains("غیر"));
            t["U_NonUrgent"] = Chk(urgent.Contains("غیرعاجل") || urgent.Contains("غیر عاجل"));
        }

        // ── گروه‌هایی که پشتوانهٔ دیتابیسی ندارند ─────────────────────────────
        // این‌ها یا با قلم در محلِ بازدید تیک می‌خورند یا در دیالوگ تایپ
        // می‌شوند. حتماً باید کلید داشته باشند وگرنه AssertNoTokensLeft
        // فورم را رد می‌کند.
        private static void BlankGroups(Dictionary<string, string> t)
        {
            string[] boxes =
            {
                // فورم ۴ — اقتصاد خانواده
                "IS_Daily","IS_Vendor","IS_Farm","IS_Livestock","IS_Craft","IS_Salary",
                "IS_Relatives","IS_Charity","IS_None",
                "INC_VeryLow","INC_Low","INC_Mid","INC_High",
                "SUF_No","SUF_Barely","SUF_Yes",
                "DBT_Yes","DBT_No",
                "HS_Own","HS_Rent","HS_Gerawi","HS_Sawabi","HS_Tent",
                "FC_Water","FC_Power","FC_Toilet","FC_Kitchen","FC_Heating",
                "AS_Land","AS_House","AS_Vehicle","AS_Livestock","AS_None",
                "HL_Healthy","HL_Chronic","HL_Disabled","HL_Critical",
                "MA_Easy","MA_Hard","MA_None",
                "V_Approve","V_Reject","V_Refer",
                "IQ_Approve","IQ_Reject",
                // فورم ۷ — نوع خدمت درمانی
                "SV_Visit","SV_ShortNoAdmit","SV_Rx","SV_AdmitLt24","SV_Observation",
                "SV_AdmitGt24","SV_HospitalCare","SV_MinorSurgery","SV_MajorSurgery",
                "SV_Chronic","SV_RepeatRx","SV_Physio","SV_Rehab","SV_Referral",
                "SV_FollowUp","SV_Counseling",
                // سیاههٔ مدارک و اسکن
                "DK_Identity","DK_FamilyPhoto","DK_DeathDoc","DK_SurveyForm","DK_Migration",
                "DK_Photo","DK_Disability","DK_Medical",
                "SC_RequestForm","SC_SurveyForm","SC_Tazkira","SC_Medical",
            };
            foreach (string b in boxes)
                if (!t.ContainsKey(b)) t[b] = Unchecked;

            string[] texts =
            {
                // فورم ۳ — اطلاعات درمانی
                "DiseaseType","DrugAllergy","HeartHistory","BloodGroup","SurgeryHistory",
                "CurrentCondition","CurrentMeds","TreatmentNeeded","RequestDesc",
                // فورم ۷ — نظر داکتر
                "DoctorOpinion","DoctorName","DoctorSpecialty","DoctorDate",
                // فورم ۴ — ارقام اقتصادی
                "EarnersCount","MonthlyIncome","IncomePerCapita","ExpFood","ExpRent",
                "ExpHealth","ExpEducation","ExpOther","ExpTotal","DebtAmount","DebtReason",
                "RentAmount","RoomCount","ResidentCount","PatientName","MedicalCost",
                "DisabledCount","SchoolAgeCount","InSchoolCount","DropoutCount","DropoutReason",
                "InquiryPlace","InquiryNo","InquiryDate","InquiryResponder",
            };
            foreach (string k in texts)
                if (!t.ContainsKey(k)) t[k] = "";
        }

        // ── کمکی‌ها ──────────────────────────────────────────────────────────
        public static string Chk(bool on) { return on ? Checked : Unchecked; }

        /// <summary>مقدارِ عددی/لاتین را در بافتِ راست‌به‌چپ سرِ جایش نگه می‌دارد.</summary>
        public static string Ltr(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (!HasDigit.IsMatch(value)) return value;
            if (ArabicLetters.IsMatch(value)) return value;   // متنِ فارسی — دست نخورد
            return LRM + value + LRM;
        }

        private static DataRow SingleRow(DatabaseHelper db, string sql, int caseId)
        {
            try
            {
                DataTable dt = db.Query(sql, new SQLiteParameter("@id", caseId));
                return dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch
            {
                return null;   // جدولِ ماژول ممکن است در نصب‌های قدیمی نباشد
            }
        }

        private static string S(DataRow r, string column)
        {
            if (r == null || !r.Table.Columns.Contains(column)) return "";
            object v = r[column];
            return v == null || v == DBNull.Value ? "" : Convert.ToString(v).Trim();
        }

        /// <summary>اول جدولِ ماژول، بعد ستونِ همتای روی TblCase.</summary>
        private static string Pick(DataRow module, string moduleColumn, DataRow c, string caseColumn)
        {
            string v = S(module, moduleColumn);
            return v.Length > 0 ? v : S(c, caseColumn);
        }

        private static string Coalesce(string a, string b)
        {
            return string.IsNullOrWhiteSpace(a) ? (b ?? "") : a;
        }

        private static string RequestTypeCode(DatabaseHelper db, string requestTypeId)
        {
            if (string.IsNullOrWhiteSpace(requestTypeId)) return "";
            try
            {
                object v = db.ExecuteScalar(
                    "SELECT Code FROM TblRequestType WHERE RequestTypeID = @id",
                    new SQLiteParameter("@id", requestTypeId));
                return v == null || v == DBNull.Value ? "" : Convert.ToString(v).Trim();
            }
            catch { return ""; }
        }

        private static string RequestTypeName(DatabaseHelper db, string requestTypeId, string fallback)
        {
            try
            {
                object v = db.ExecuteScalar(
                    "SELECT Name FROM TblRequestType WHERE RequestTypeID = @id",
                    new SQLiteParameter("@id", requestTypeId));
                string name = v == null || v == DBNull.Value ? "" : Convert.ToString(v).Trim();
                return name.Length > 0 ? name : (fallback ?? "");
            }
            catch { return fallback ?? ""; }
        }
    }
}
