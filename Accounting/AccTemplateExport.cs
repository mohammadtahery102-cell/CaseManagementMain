using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace CaseManagement.Accounting
{
    // ─────────────────────────────────────────────────────────────────────────
    // خروجی اکسل روی «قالب رسمی» مؤسسه.
    //
    // آموزش — چرا قالب و نه ساختِ شیت از صفر: فایل Templates\FinancialForms.xlsx
    // همان فورم رسمی چاپی است (سربرگ، رنگ‌بندی، کنترول توازن، تنظیمات چاپ A4
    // راست‌به‌چپ). این کلاس آن را باز می‌کند و فقط خانه‌های «نام‌گذاری‌شده»
    // (Defined Name) را پر می‌کند؛ اگر روزی چیدمانِ قالب عوض شود، اکسل خودش
    // نام‌ها را جابه‌جا می‌کند و این کد دست‌نخورده می‌ماند.
    //
    // مسیرهای خروجی قبلیِ AccReports (ExportGeneralStatementExcel و…) هیچ
    // تغییری نکرده‌اند؛ این یک مسیر موازی است.
    //
    // آموزش — چرا آدرس‌ها از RefersTo خوانده می‌شوند: به‌جای اتکا به APIهای
    // کم‌کاربردِ ClosedXML، رشته‌ی مرجعِ نام (مثل 'صورت حساب کلی'!$B$8) خوانده
    // و به سطر/ستون تبدیل می‌شود؛ نوشتن با همان ws.Cell(row, col) انجام می‌گیرد
    // که در سراسر پروژه استفاده شده است.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class AccTemplateExport
    {
        public const string TemplateFileName = "FinancialForms.xlsx";

        public static string TemplatePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", TemplateFileName); }
        }

        public static bool TemplateExists { get { return File.Exists(TemplatePath); } }

        // داده‌ی موردنیاز گزارش ۱ (صورت حساب کلی). همه‌ی اعداد از AccReports
        // می‌آیند که خودش آن‌ها را از AccountingRepo خوانده است.
        public sealed class GeneralStatementData
        {
            public string OrgName;
            public string HeaderText;
            public string Center;
            public string Province;
            public string District;
            public string Manager;
            public string PreparedBy;
            public string PeriodTitle;
            public double Dollar;
            public double Rate;
            public double Received;
            public double Opening;
            public double TotalPaid;
            public List<KeyValuePair<string, double>> Expenses = new List<KeyValuePair<string, double>>();
        }

        private const string SheetGeneral = "صورت حساب کلی";
        private const int MaxExpenseRows = 9;
        private const int MaxStipendMonthRows = 6;

        public static void WriteGeneralStatement(string outPath, GeneralStatementData d)
        {
            if (d == null) throw new ArgumentNullException("d");
            if (!TemplateExists)
                throw new FileNotFoundException("قالب رسمی پیدا نشد:" + Environment.NewLine + TemplatePath);

            using (var wb = new XLWorkbook(TemplatePath))
            {
                var ws = wb.Worksheet(SheetGeneral);

                // ── سربرگ: در قالب به شیت «فهرست و راهنما» لینک است؛ چون آن شیت
                //    در خروجی حذف می‌شود، مقدار ثابت نوشته می‌شود.
                ws.Cell(1, 1).Value = Safe(d.OrgName);
                ws.Cell(2, 1).Value = Safe(d.HeaderText);
                ws.Cell(5, 7).Value = BuildCenterLine(d);

                // عنوان: در قالب فورمولی است که «برج» و «سال» را از خانه‌های
                // ماه/سال می‌سازد. اینجا دوره یک عنوان آمادهٔ متنی است، پس
                // عنوان به‌صورت ثابت نوشته می‌شود تا کلمهٔ «برج» تکرار نشود.
                ws.Cell(3, 1).Value = "صورت حساب کلی ایتام   –   " + Safe(d.PeriodTitle);

                // ── ۱) مشخصات سند ──
                Put(wb, ws, "GL_Center", Safe(d.Center));
                Put(wb, ws, "GL_Province", Safe(d.Province));
                Put(wb, ws, "GL_District", Safe(d.District));
                Put(wb, ws, "GL_Manager", Safe(d.Manager));
                Put(wb, ws, "GL_PreparedBy", Safe(d.PreparedBy));
                Put(wb, ws, "GL_Month", Safe(d.PeriodTitle));
                Put(wb, ws, "GL_Year", "");

                // ── ۲) خلاصهٔ مالی ──
                // این خانه‌ها در قالب به شیت «تفکیک» لینک‌اند؛ با مقدارِ محاسبه‌شده
                // جایگزین می‌شوند تا برگه پس از حذفِ شیت‌های دیگر مستقل بماند.
                double available = d.Received + d.Opening;
                double due = d.TotalPaid > available ? d.TotalPaid - available : 0;
                double balance = available > d.TotalPaid ? available - d.TotalPaid : 0;

                if (d.Dollar > 0)
                {
                    Put(wb, ws, "GL_RecvUsd", d.Dollar);
                    Put(wb, ws, "GL_RecvRate", d.Rate);
                }
                else
                {
                    Put(wb, ws, "GL_RecvUsd", "—");
                    Put(wb, ws, "GL_RecvRate", "—");
                }
                Put(wb, ws, "GL_RecvAfn", d.Received);
                Put(wb, ws, "GL_PrevBalance", d.Opening);
                Put(wb, ws, "GL_PrevDue", 0d);
                Put(wb, ws, "GL_Available", available);
                Put(wb, ws, "GL_CurrentDue", due);
                Put(wb, ws, "GL_CurrentBalance", balance);
                // GL_TotalPaid در قالب برابرِ جمعِ جدول هزینه‌هاست و دست‌نخورده می‌ماند.

                // ── ۳) جدول تفکیک ماهانهٔ شهریه: در این گزارش داده‌ای ندارد ──
                BlankColumn(wb, ws, "GL_Mth_Month", MaxStipendMonthRows);
                BlankColumn(wb, ws, "GL_Mth_Year", MaxStipendMonthRows);
                BlankColumn(wb, ws, "GL_Mth_Recv", MaxStipendMonthRows);
                BlankColumn(wb, ws, "GL_Mth_Paid", MaxStipendMonthRows);

                // ── ۴) جدول کلی هزینه ها ──
                int tRow, tCol, aRow, aCol, nRow, nCol, pRow, pCol;
                Loc(wb, "GL_Exp_Title", out tRow, out tCol);
                Loc(wb, "GL_Exp_Amount", out aRow, out aCol);
                Loc(wb, "GL_Exp_Note", out nRow, out nCol);
                Loc(wb, "GL_Exp_Province", out pRow, out pCol);
                // ستون «ولایت / ولسوالی» در قالب به خانه‌ی ولایتِ بالای برگه
                // لینک است؛ چون آن خانه در این گزارش پر نمی‌شود، مقدار ثابت
                // نوشته می‌شود تا به‌جای صفر، نام مرکز نمایش داده شود.
                string scope = string.IsNullOrWhiteSpace(d.Province) ? Safe(d.Center) : d.Province;
                for (int i = 0; i < MaxExpenseRows; i++)
                {
                    bool has = i < d.Expenses.Count;
                    ws.Cell(tRow + i, tCol).Value = has ? d.Expenses[i].Key : "";
                    if (has) ws.Cell(aRow + i, aCol).Value = d.Expenses[i].Value;
                    else ws.Cell(aRow + i, aCol).Value = "";
                    ws.Cell(pRow + i, pCol).Value = has ? scope : "";
                    ws.Cell(nRow + i, nCol).Value = "";
                }

                // ── ۵) کنترول توازن: چهار آزمونِ آخر به شیت‌های حذف‌شده ارجاع
                //     می‌دهند؛ «—» می‌شوند. آزمون اول درون‌شیتی است و می‌ماند.
                int cRow, cCol, cLastRow, cLastCol;
                LocRange(wb, "GL_Checks", out cRow, out cCol, out cLastRow, out cLastCol);
                for (int r = cRow + 1; r <= cLastRow; r++)
                    ws.Cell(r, cCol).Value = "—";

                // ── نام‌ها و شیت‌های اضافی حذف می‌شوند: خروجی یک برگهٔ مستقل است ──
                wb.DefinedNames.DeleteAll();
                foreach (string name in wb.Worksheets.Select(w => w.Name).ToList())
                    if (name != SheetGeneral) wb.Worksheet(name).Delete();

                wb.CalculateMode = XLCalculateMode.Auto;
                wb.SaveAs(outPath);
            }
        }

        // ── کمکی‌ها ──────────────────────────────────────────────────────────
        // ═══════════════════════════════════════════════════════════════════
        // سند پرداخت وجه  (شیت «سند پرداخت وجه»)
        // ═══════════════════════════════════════════════════════════════════
        // آموزش — چرا مبلغ به‌صورت «مقدار × قیمت» نوشته می‌شود و نه مستقیم در
        // ستون مجموع: خانهٔ مجموع در قالب فورمول دارد (D×E) و جمع کل هم از
        // همان ستون گرفته می‌شود. اگر فورمول را با عدد بازنویسی کنیم، ساختار
        // کنترلیِ خودِ سند از بین می‌رود. با نوشتن مقدار=۱ و قیمت=مبلغ،
        // فورمول‌ها دست‌نخورده می‌مانند و جمع کل خودش درست درمی‌آید.
        public sealed class VoucherData
        {
            public string OrgName;
            public string HeaderText;
            public string Center;
            public string Province;
            public string District;
            public string DocNo;
            public string Date;
            public string Account;      // کود حساب / سرفصل بودجه
            public string PayType;      // نوع پرداخت (دریافت/پرداخت)
            public string TransferNo;   // شماره حواله
            public string Subject;      // بابت
            public string Currency;
            public string ItemDesc;
            public string ItemNote;
            public double Amount;
            public string AmountWords;  // متن آماده؛ چون شیت «داده» حذف می‌شود
            public string Receiver;
            public string ReceiverTazkira;
            public string ReceiverPhone;
            public string Attachments;
        }

        private const string SheetVoucher = "سند پرداخت وجه";
        private const int MaxVoucherItemRows = 10;

        public static void WriteVoucher(string outPath, VoucherData d)
        {
            if (d == null) throw new ArgumentNullException("d");
            if (!TemplateExists)
                throw new FileNotFoundException("قالب رسمی پیدا نشد:" + Environment.NewLine + TemplatePath);

            using (var wb = new XLWorkbook(TemplatePath))
            {
                var ws = wb.Worksheet(SheetVoucher);

                // سربرگ: در قالب به شیت «فهرست و راهنما» لینک است و آن شیت در
                // خروجی حذف می‌شود، پس مقدار ثابت نوشته می‌شود.
                ws.Cell(1, 1).Value = Safe(d.OrgName);
                ws.Cell(2, 1).Value = Safe(d.HeaderText);
                ws.Cell(3, 1).Value = "سند پرداخت وجه";
                ws.Cell(5, 7).Value = BuildCenterLineFrom(d.Center, d.Province, d.District);

                Put(wb, ws, "PV_DocNo", Safe(d.DocNo));
                Put(wb, ws, "PV_Date", Safe(d.Date));
                Put(wb, ws, "PV_Account", Safe(d.Account));
                Put(wb, ws, "PV_PayType", Safe(d.PayType));
                Put(wb, ws, "PV_TransferNo", Safe(d.TransferNo));
                Put(wb, ws, "PV_Subject", Safe(d.Subject));
                Put(wb, ws, "PV_Currency", Safe(d.Currency));

                // ── جدول اقلام: یک قلم، بقیهٔ سطرها خالی ──
                int dRow, dCol, qRow, qCol, pRow, pCol, nRow, nCol;
                Loc(wb, "PV_Item_Desc", out dRow, out dCol);
                Loc(wb, "PV_Item_Qty", out qRow, out qCol);
                Loc(wb, "PV_Item_Price", out pRow, out pCol);
                Loc(wb, "PV_Item_Note", out nRow, out nCol);

                ws.Cell(dRow, dCol).Value = Safe(d.ItemDesc);
                ws.Cell(qRow, qCol).Value = 1d;
                ws.Cell(pRow, pCol).Value = d.Amount;
                ws.Cell(nRow, nCol).Value = Safe(d.ItemNote);

                for (int i = 1; i < MaxVoucherItemRows; i++)
                {
                    ws.Cell(dRow + i, dCol).Value = "";
                    ws.Cell(qRow + i, qCol).Value = "";
                    ws.Cell(pRow + i, pCol).Value = "";
                    ws.Cell(nRow + i, nCol).Value = "";
                }

                // «مبلغ به حروف» در قالب از شیت مخفیِ «داده» خوانده می‌شود.
                // آن شیت پایین‌تر حذف می‌گردد، پس متنِ آماده جایگزین می‌شود —
                // وگرنه خروجی #REF! می‌دهد.
                Put(wb, ws, "PV_AmountWords", Safe(d.AmountWords));

                Put(wb, ws, "PV_Receiver", Safe(d.Receiver));
                Put(wb, ws, "PV_ReceiverTazkira", Safe(d.ReceiverTazkira));
                Put(wb, ws, "PV_ReceiverPhone", Safe(d.ReceiverPhone));
                Put(wb, ws, "PV_Attachments", Safe(d.Attachments));

                FinishSingleSheet(wb, SheetVoucher, outPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // جدول معاشات کارمندان  (شیت «حقوق»)
        // ═══════════════════════════════════════════════════════════════════
        public sealed class SalaryRow
        {
            public string Name;
            public string Position;
            public string PayType;   // معاش / حق الزحمه / حق الماموریت / اضافه کاری / سایر
            public string Note;
            public double Amount;
            public string Status;
        }

        public sealed class SalarySheetData
        {
            public string OrgName;
            public string HeaderText;
            public string Center;
            public string Province;
            public string District;
            public string DocNo;
            public string Date;
            public string PeriodTitle;
            public List<SalaryRow> Rows = new List<SalaryRow>();
        }

        private const string SheetSalary = "حقوق";

        // ظرفیت واقعی جدول در قالب: سطر ۸ تا ۳۲. جمعِ کل و جدولِ خلاصه به
        // همین بازه قفل‌اند، پس نوشتن زیر سطر ۳۲ یعنی مبلغی که در جمع نمی‌آید.
        public const int MaxSalaryRows = 25;

        public static void WriteSalarySheet(string outPath, SalarySheetData d)
        {
            if (d == null) throw new ArgumentNullException("d");
            if (!TemplateExists)
                throw new FileNotFoundException("قالب رسمی پیدا نشد:" + Environment.NewLine + TemplatePath);

            if (d.Rows.Count > MaxSalaryRows)
                throw new InvalidOperationException(
                    "جدول معاشاتِ قالب رسمی حداکثر " + MaxSalaryRows + " کارمند جا می‌دهد، " +
                    "اما این دوره " + d.Rows.Count + " ردیف دارد." + Environment.NewLine +
                    "نوشتن بیش از این ظرفیت، جمع کل و کنترول توازن قالب را می‌شکند." + Environment.NewLine +
                    "برای این دوره از «خروجی اکسل» معمولی استفاده کنید.");

            using (var wb = new XLWorkbook(TemplatePath))
            {
                var ws = wb.Worksheet(SheetSalary);

                ws.Cell(1, 1).Value = Safe(d.OrgName);
                ws.Cell(2, 1).Value = Safe(d.HeaderText);
                ws.Cell(3, 1).Value = "جدول معاشات کارمندان   –   " + Safe(d.PeriodTitle);
                ws.Cell(5, 7).Value = BuildCenterLineFrom(d.Center, d.Province, d.District);
                ws.Cell(5, 2).Value = Safe(d.DocNo);
                ws.Cell(5, 5).Value = Safe(d.Date);

                int nRow, nCol, poRow, poCol, tRow, tCol, deRow, deCol, aRow, aCol, stRow, stCol;
                Loc(wb, "PAY_Name", out nRow, out nCol);
                Loc(wb, "PAY_Position", out poRow, out poCol);
                Loc(wb, "PAY_Type", out tRow, out tCol);
                Loc(wb, "PAY_Desc", out deRow, out deCol);
                Loc(wb, "PAY_Amount", out aRow, out aCol);
                Loc(wb, "PAY_Status", out stRow, out stCol);

                for (int i = 0; i < MaxSalaryRows; i++)
                {
                    bool has = i < d.Rows.Count;
                    SalaryRow r = has ? d.Rows[i] : null;

                    ws.Cell(nRow + i, nCol).Value = has ? Safe(r.Name) : "";
                    ws.Cell(poRow + i, poCol).Value = has ? Safe(r.Position) : "";
                    ws.Cell(tRow + i, tCol).Value = has ? Safe(r.PayType) : "";
                    ws.Cell(deRow + i, deCol).Value = has ? Safe(r.Note) : "";
                    ws.Cell(stRow + i, stCol).Value = has ? Safe(r.Status) : "";

                    if (has) ws.Cell(aRow + i, aCol).Value = r.Amount;
                    else ws.Cell(aRow + i, aCol).Value = "";
                }

                FinishSingleSheet(wb, SheetSalary, outPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // تفکیک شهریه و هزینه ها  (شیت «تفکیک شهریه و هزینه ها»)
        // ═══════════════════════════════════════════════════════════════════
        // آموزش — چرا نیمی از این برگه «—» می‌شود: این شیت دریافتی را جدا برای
        // «شهریه» و جدا برای «حقوق و هزینه ها» می‌خواهد، به‌علاوهٔ باقی‌ماندهٔ
        // قبلی و طلبِ قبلیِ هر بخش. دیتابیس چنین تفکیکی ندارد — دسته‌های درآمد
        // «بودجه / کمک خیرین / انتقال / برگشت وجه / سایر» هستند و AccPeriod یک
        // OpeningBalance واحد دارد. پرداختی‌ها واقعی و کامل‌اند، پس نوشته
        // می‌شوند؛ خانه‌های دریافتی خالی می‌مانند و خانه‌های محاسباتیِ وابسته
        // به آن‌ها (طلب/باقی‌مانده/کنترول توازن) صادقانه «—» می‌شوند تا هیچ
        // عددِ ساختگی روی سندی که امضاء می‌شود ننشیند.
        public sealed class SplitData
        {
            public string OrgName;
            public string HeaderText;
            public string Center;
            public string Province;
            public string District;
            public string DocNo;
            public string Date;
            public string PeriodTitle;
            public double StipendPaid;    // مجموع پرداختی شهریه
            public double OtherPaid;      // مجموع پرداختی حقوق و هزینه ها
        }

        private const string SheetSplit = "تفکیک شهریه و هزینه ها";

        public static void WriteSplitStatement(string outPath, SplitData d)
        {
            if (d == null) throw new ArgumentNullException("d");
            if (!TemplateExists)
                throw new FileNotFoundException("قالب رسمی پیدا نشد:" + Environment.NewLine + TemplatePath);

            using (var wb = new XLWorkbook(TemplatePath))
            {
                var ws = wb.Worksheet(SheetSplit);

                ws.Cell(1, 1).Value = Safe(d.OrgName);
                ws.Cell(2, 1).Value = Safe(d.HeaderText);
                ws.Cell(3, 1).Value = "جدول تفکیک شدهٔ شهریهٔ ایتام و هزینه ها   –   " + Safe(d.PeriodTitle);
                ws.Cell(5, 7).Value = BuildCenterLineFrom(d.Center, d.Province, d.District);
                ws.Cell(5, 2).Value = Safe(d.DocNo);
                ws.Cell(5, 5).Value = Safe(d.Date);
                ws.Cell(8, 2).Value = Safe(d.Center);
                ws.Cell(8, 5).Value = BuildProvinceLine(d.Province, d.District);
                ws.Cell(8, 8).Value = "";

                // ── پرداختی‌ها: عددِ واقعی. این دو خانه در قالب به «صورت حساب
                //    کلی» لینک‌اند و آن شیت حذف می‌شود، پس باید مقدار بگیرند.
                ws.Cell(16, 6).Value = d.StipendPaid;   // مجموع پرداختی شهریه
                ws.Cell(26, 6).Value = d.OtherPaid;     // مجموع پرداختی حقوق و هزینه ها

                // ── دریافتی و باقی‌مانده/طلبِ قبلی: در دیتابیس تفکیک‌شده نیست ──
                foreach (int row in new[] { 12, 13, 14, 22, 23, 24 })
                {
                    ws.Cell(row, 3).Value = "—";   // مبلغ دالری
                    ws.Cell(row, 5).Value = "—";   // نرخ دالر
                    ws.Cell(row, 6).Value = "—";   // مبلغ افغانی
                }

                // ── خانه‌های محاسباتیِ وابسته به دریافتی ──
                foreach (int row in new[] { 15, 17, 18, 25, 27, 28, 32, 33, 34, 35, 37, 38 })
                    ws.Cell(row, 6).Value = "—";
                ws.Cell(32, 3).Value = "—";
                ws.Cell(32, 5).Value = "—";

                // جمعِ پرداختیِ کل واقعی است و می‌ماند.
                ws.Cell(36, 6).Value = d.StipendPaid + d.OtherPaid;

                // ── کنترول توازن: بدون دریافتی قابل اثبات نیست ──
                for (int row = 41; row <= 44; row++)
                    ws.Cell(row, 4).Value = "—";
                ws.Cell(45, 1).Value =
                    "توجه: ارقام دریافتی، باقی‌مانده و طلبِ هر بخش در سیستم به‌تفکیک ثبت نمی‌شود؛ " +
                    "این خانه‌ها دستی تکمیل و سپس کنترول توازن بازبینی گردد.";

                FinishSingleSheet(wb, SheetSplit, outPath);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // مشترک: حذف نام‌ها و شیت‌های اضافی و ذخیره
        // ═══════════════════════════════════════════════════════════════════
        private static void FinishSingleSheet(XLWorkbook wb, string keepSheet, string outPath)
        {
            wb.DefinedNames.DeleteAll();
            foreach (string name in wb.Worksheets.Select(w => w.Name).ToList())
                if (name != keepSheet) wb.Worksheet(name).Delete();

            wb.CalculateMode = XLCalculateMode.Auto;
            wb.SaveAs(outPath);
        }

        private static string BuildCenterLineFrom(string center, string province, string district)
        {
            string s = Safe(center);
            if (!string.IsNullOrWhiteSpace(province))
                s = (s.Length > 0 ? s + "  –  " : "") + province;
            if (!string.IsNullOrWhiteSpace(district))
                s = s + " / " + district;
            return s;
        }

        private static string BuildProvinceLine(string province, string district)
        {
            string s = Safe(province);
            if (!string.IsNullOrWhiteSpace(district))
                s = (s.Length > 0 ? s + " / " : "") + district;
            return s;
        }

        private static string Safe(string s) { return s ?? ""; }

        private static string BuildCenterLine(GeneralStatementData d)
        {
            string s = Safe(d.Center);
            if (!string.IsNullOrWhiteSpace(d.Province))
                s = (s.Length > 0 ? s + "  –  " : "") + d.Province;
            if (!string.IsNullOrWhiteSpace(d.District))
                s = s + " / " + d.District;
            return s;
        }

        private static void Put(XLWorkbook wb, IXLWorksheet ws, string name, string value)
        {
            int r, c; Loc(wb, name, out r, out c);
            ws.Cell(r, c).Value = value;
        }

        private static void Put(XLWorkbook wb, IXLWorksheet ws, string name, double value)
        {
            int r, c; Loc(wb, name, out r, out c);
            ws.Cell(r, c).Value = value;
        }

        private static void BlankColumn(XLWorkbook wb, IXLWorksheet ws, string name, int rows)
        {
            int r, c; Loc(wb, name, out r, out c);
            for (int i = 0; i < rows; i++) ws.Cell(r + i, c).Value = "";
        }

        private static void Loc(XLWorkbook wb, string name, out int row, out int col)
        {
            int lr, lc;
            LocRange(wb, name, out row, out col, out lr, out lc);
        }

        private static void LocRange(XLWorkbook wb, string name,
                                     out int firstRow, out int firstCol,
                                     out int lastRow, out int lastCol)
        {
            var dn = wb.DefinedName(name);
            if (dn == null)
                throw new InvalidOperationException("خانه‌ی نام‌گذاری‌شده‌ی «" + name + "» در قالب پیدا نشد.");

            string refersTo = dn.RefersTo ?? "";
            int bang = refersTo.LastIndexOf('!');
            string a1 = (bang >= 0 ? refersTo.Substring(bang + 1) : refersTo).Replace("$", "").Trim();

            string first = a1, last = a1;
            int colon = a1.IndexOf(':');
            if (colon >= 0) { first = a1.Substring(0, colon); last = a1.Substring(colon + 1); }

            ParseA1(name, first, out firstRow, out firstCol);
            ParseA1(name, last, out lastRow, out lastCol);
        }

        private static void ParseA1(string name, string a1, out int row, out int col)
        {
            int i = 0; col = 0;
            while (i < a1.Length && char.IsLetter(a1[i]))
            {
                col = col * 26 + (char.ToUpperInvariant(a1[i]) - 'A' + 1);
                i++;
            }
            if (i == 0 || i >= a1.Length ||
                !int.TryParse(a1.Substring(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
                throw new InvalidOperationException("مرجع نامعتبر برای «" + name + "»: " + a1);
        }
    }
}
