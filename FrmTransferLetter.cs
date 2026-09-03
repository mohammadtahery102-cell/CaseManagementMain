using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.Helpers;

namespace CaseManagement
{
    // ─────────────────────────────────────────────────────────────────────────
    // «نامهٔ انتقالی» یک پرونده — چرخهٔ کامل:
    //
    //   خروجی Word / PDF  →  چاپ و امضاء  →  آپلود سند امضاشده در پرونده
    //                                        + ثبت در تاریخچهٔ انتقال
    //
    // آموزش — چرا جدولِ تازه‌ای ساخته نشد: TblCaseTransferHistory از قبل در
    // DatabaseInitializer وجود دارد (با ستون TransferLetterNo که دقیقاً برای
    // همین نامه گذاشته شده) ولی تا امروز هیچ‌جای برنامه از آن استفاده
    // نمی‌کرد. پس این فورم فقط همان جدولِ خالیِ آماده را پر می‌کند و هیچ
    // تغییرِ ساختاری در پایگاه داده لازم ندارد.
    //
    // آموزش — چرا چند خانه دستی است و از دیتابیس خوانده نمی‌شود: «تا برج …
    // سال … شهریه دریافت نموده» در پایگاه داده وجود ندارد؛ AccStipend تجمعی
    // است (تعداد خانوار × مبلغ) و به CasID وصل نمی‌شود. حدس زدنِ آن یعنی
    // نوشتنِ عددِ نادرست روی نامه‌ای که امضاء و به ولایت دیگر فرستاده می‌شود.
    // ─────────────────────────────────────────────────────────────────────────
    public class FrmTransferLetter : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();
        private readonly int caseId;
        private string caseCode = "";

        private ComboBox cmbHonorific, cmbTo;
        private TextBox txtLetterNo, txtLetterDate, txtHeadName, txtFatherName,
                        txtCode, txtOrphanCount, txtFrom, txtLastMonth, txtLastYear,
                        txtPageCount, txtReason;

        // تاریخچه فقط یک‌بار در هر نشست ثبت می‌شود، حتی اگر کاربر هم Word و هم
        // PDF بگیرد — وگرنه یک انتقال دو ردیف در تاریخچه می‌سازد.
        private bool historyRecorded;

        // آخرین فایلی که ساخته شد؛ برای پیشنهادِ مسیر هنگام آپلودِ نسخهٔ امضاشده.
        private string lastOutputPath = "";

        public FrmTransferLetter(int caseId)
        {
            this.caseId = caseId;
            BuildUi();
            LoadCase();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ساخت رابط کاربری
        // ═══════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Text = "نامهٔ انتقالی پرونده";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(620, 560);
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);

            int y = 12;

            AddSectionLabel("مشخصات پرونده  (از پروندهٔ باز خوانده شد)", ref y);
            txtHeadName   = AddField("نام سرپرست", ref y, true);
            txtFatherName = AddField("نام پدر", ref y, true);
            txtCode       = AddField("کد عمومی", ref y, true);
            txtFrom       = AddField("ولایت مبدأ", ref y, false);
            txtOrphanCount = AddField("تعداد ایتام تحت پوشش", ref y, false);

            y += 8;
            AddSectionLabel("مشخصات نامه", ref y);

            cmbHonorific = AddCombo("عنوان", ref y, new[] { "محترم", "محترمه" });
            cmbHonorific.SelectedIndex = 0;

            txtLetterNo   = AddField("شمارهٔ نامه", ref y, false);
            txtLetterDate = AddField("تاریخ نامه", ref y, false);

            cmbTo = AddCombo("ولایت / مرکز مقصد", ref y, GetDestinations());

            txtLastMonth = AddField("شهریه گرفته تا برج", ref y, false);
            txtLastYear  = AddField("سال", ref y, false);
            txtPageCount = AddField("تعداد ورق کاپی ضمیمه", ref y, false);
            txtReason    = AddField("دلیل انتقال  (فقط در تاریخچه ثبت می‌شود)", ref y, false);

            y += 10;

            var btnWord = UiTheme.CreateButton("خروجی Word", "➤", UiTheme.Primary);
            btnWord.SetBounds(14, y, 140, 34);
            btnWord.Click += delegate { Export(false); };

            var btnPdf = UiTheme.CreateButton("خروجی PDF", "➤", UiTheme.Primary);
            btnPdf.SetBounds(164, y, 140, 34);
            btnPdf.Click += delegate { Export(true); };

            var btnAttach = UiTheme.CreateSecondaryButton("ثبت سند امضاشده", "▤");
            btnAttach.SetBounds(314, y, 170, 34);
            btnAttach.Click += delegate { AttachSignedDocument(); };

            var btnClose = UiTheme.CreateSecondaryButton("بستن", "✕");
            btnClose.SetBounds(494, y, 100, 34);
            btnClose.Click += delegate { Close(); };

            Controls.Add(btnWord);
            Controls.Add(btnPdf);
            Controls.Add(btnAttach);
            Controls.Add(btnClose);
            CancelButton = btnClose;

            ClientSize = new Size(620, y + 56);
        }

        private void AddSectionLabel(string text, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.Primary,
                Font = UiTheme.FontBold(UiTheme.SizeBody),
                BackColor = Color.Transparent
            };
            lbl.SetBounds(14, y, 580, 26);
            Controls.Add(lbl);
            y += 30;
        }

        private TextBox AddField(string caption, ref int y, bool readOnly)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            lbl.SetBounds(14, y, 230, 26);

            var box = new TextBox { RightToLeft = RightToLeft.Yes, ReadOnly = readOnly };
            box.SetBounds(250, y, 344, 26);
            if (readOnly) box.BackColor = UiTheme.Background;

            Controls.Add(lbl);
            Controls.Add(box);
            y += 30;
            return box;
        }

        private ComboBox AddCombo(string caption, ref int y, IEnumerable<string> items)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = UiTheme.TextMuted,
                BackColor = Color.Transparent
            };
            lbl.SetBounds(14, y, 230, 26);

            // DropDown (نه DropDownList) تا اگر مقصد در فهرست نبود، کاربر
            // بتواند خودش تایپ کند.
            var cmb = new ComboBox { RightToLeft = RightToLeft.Yes, DropDownStyle = ComboBoxStyle.DropDown };
            cmb.SetBounds(250, y, 344, 26);
            foreach (string item in items) cmb.Items.Add(item);

            Controls.Add(lbl);
            Controls.Add(cmb);
            y += 30;
            return cmb;
        }

        // مراکز ثبت‌شده + ولایات افغانستان، بدون تکرار.
        private string[] GetDestinations()
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.CurrentCulture);

            try
            {
                DataTable dt = db.Query("SELECT CenterName FROM TblCenter WHERE IsActive = 1 ORDER BY CenterName");
                foreach (DataRow r in dt.Rows)
                {
                    string name = Convert.ToString(r["CenterName"]).Trim();
                    if (name.Length > 0 && seen.Add(name)) list.Add(name);
                }
            }
            catch { /* نبودنِ فهرست مراکز نباید جلوی نوشتنِ نامه را بگیرد. */ }

            // همان فهرستِ ولایاتی که فورم پرونده استفاده می‌کند (TblLookup)،
            // تا مقصدِ نامه با ولایتِ ثبت‌شده در پرونده هم‌واژه بماند.
            try
            {
                foreach (string p in LookupHelper.GetValues("Province"))
                    if (p != null && p.Trim().Length > 0 && seen.Add(p.Trim())) list.Add(p.Trim());
            }
            catch { /* فهرست ولایات اختیاری است؛ کاربر می‌تواند تایپ کند. */ }

            return list.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════════
        // خواندن پرونده
        // ═══════════════════════════════════════════════════════════════════
        private void LoadCase()
        {
            DataTable dt = db.Query(
                "SELECT Code, HeadFullName, HeadFatherName, Province FROM TblCase WHERE CasID = @id",
                new SQLiteParameter("@id", caseId));

            if (dt.Rows.Count == 0)
            {
                UiTheme.ShowError(this, "پرونده پیدا نشد.");
                return;
            }

            DataRow row = dt.Rows[0];
            caseCode           = Convert.ToString(row["Code"]).Trim();
            txtCode.Text       = caseCode;
            txtHeadName.Text   = Convert.ToString(row["HeadFullName"]).Trim();
            txtFatherName.Text = Convert.ToString(row["HeadFatherName"]).Trim();
            txtFrom.Text       = Convert.ToString(row["Province"]).Trim();

            // تعداد ایتام: از اعضای خانواده با نقش «یتیم». قابلِ ویرایش
            // می‌ماند چون در بعضی پرونده‌های قدیمی نقش اعضا ثبت نشده است.
            object count = db.ExecuteScalar(
                "SELECT COUNT(*) FROM TblFamily WHERE CasID = @id AND COALESCE(MemberRole,'') = 'یتیم'",
                new SQLiteParameter("@id", caseId));
            txtOrphanCount.Text = Convert.ToString(count);

            txtLetterDate.Text = PersianDateHelper.ToPersianDateString(DateTime.Now);
        }

        // ═══════════════════════════════════════════════════════════════════
        // خروجی
        // ═══════════════════════════════════════════════════════════════════
        private void Export(bool asPdf)
        {
            if (!ValidateInput()) return;

            if (!TransferLetterExport.TemplateExists)
            {
                UiTheme.ShowError(this, "قالب نامهٔ انتقالی پیدا نشد:" + Environment.NewLine +
                                        TransferLetterExport.TemplatePath);
                return;
            }

            string suggested = "نامه انتقالی - " + FileHelper.CleanName(
                (caseCode.Length > 0 ? caseCode : txtHeadName.Text.Trim()));

            using (var sfd = new SaveFileDialog
            {
                Filter = asPdf ? "فایل PDF|*.pdf" : "سند ورد|*.docx",
                FileName = suggested + (asPdf ? ".pdf" : ".docx")
            })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                Cursor previous = Cursor;
                Cursor = Cursors.WaitCursor;
                try
                {
                    // ساختِ docx و ساختِ pdf هر دو در DocxFormExport انجام
                    // می‌شوند؛ مسیرِ PDF خودش فایلِ Word میانی را می‌سازد و
                    // پاک می‌کند، پس اینجا چیزی برای پاکسازی نمی‌ماند.
                    if (asPdf) TransferLetterExport.WritePdf(sfd.FileName, BuildData());
                    else       TransferLetterExport.Write(sfd.FileName, BuildData());

                    string finalPath = sfd.FileName;
                    lastOutputPath = finalPath;
                    RecordHistoryOnce();

                    UiTheme.ShowSuccess(this, "نامهٔ انتقالی ساخته شد:" + Environment.NewLine + finalPath);
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خطا در ساخت نامهٔ انتقالی: " + ex.Message);
                }
                finally
                {
                    Cursor = previous;
                }
            }
        }

        // نامش عمداً ValidateInput است نه Validate — چون Form.Validate از
        // ContainerControl ارث می‌رسد و هم‌نام‌شدن با آن یعنی پنهان‌کردنِ
        // متدی که خودِ WinForms هنگام تغییر فوکوس صدا می‌زند.
        private bool ValidateInput()
        {
            if (caseId <= 0)
            {
                UiTheme.ShowWarning(this, "پرونده مشخص نیست.");
                return false;
            }
            if (cmbTo.Text.Trim().Length == 0)
            {
                UiTheme.ShowWarning(this, "ولایت / مرکز مقصد را وارد کنید.");
                cmbTo.Focus();
                return false;
            }
            if (txtLetterNo.Text.Trim().Length == 0)
            {
                UiTheme.ShowWarning(this, "شمارهٔ نامه را وارد کنید.");
                txtLetterNo.Focus();
                return false;
            }
            return true;
        }

        private TransferLetterExport.LetterData BuildData()
        {
            return new TransferLetterExport.LetterData
            {
                Honorific    = cmbHonorific.Text,
                LetterNo     = txtLetterNo.Text,
                LetterDate   = txtLetterDate.Text,
                HeadName     = txtHeadName.Text,
                FatherName   = txtFatherName.Text,
                Code         = txtCode.Text,
                OrphanCount  = txtOrphanCount.Text,
                FromProvince = txtFrom.Text,
                ToProvince   = cmbTo.Text,
                LastMonth    = txtLastMonth.Text,
                LastYear     = txtLastYear.Text,
                PageCount    = txtPageCount.Text
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        // تاریخچهٔ انتقال
        // ═══════════════════════════════════════════════════════════════════
        private void RecordHistoryOnce()
        {
            if (historyRecorded) return;

            try
            {
                string destination = cmbTo.Text.Trim();

                // ToCenterID فقط وقتی مقدار واقعی می‌گیرد که مقصد دقیقاً یکی
                // از مراکز ثبت‌شده باشد. اگر کاربر نام یک ولایت را تایپ کرده
                // باشد، شناسه‌ای وجود ندارد؛ در آن حالت صفر ثبت می‌شود و نامِ
                // مقصد در TransferReason می‌آید تا هیچ اطلاعاتی گم نشود.
                int toCenterId = 0;
                object found = db.ExecuteScalar(
                    "SELECT CenterID FROM TblCenter WHERE CenterName = @n",
                    new SQLiteParameter("@n", destination));
                if (found != null && found != DBNull.Value)
                    toCenterId = Convert.ToInt32(found);

                string note = "مقصد: " + destination;
                if (txtReason.Text.Trim().Length > 0)
                    note += "  —  دلیل: " + txtReason.Text.Trim();

                db.ExecuteNonQuery(@"
INSERT INTO TblCaseTransferHistory
    (CasID, FromCenterID, ToCenterID, TransferDate, TransferReason, TransferLetterNo, TransferredBy, UserID)
VALUES
    (@cas, @from, @to, @date, @reason, @letter, @by, @uid)",
                    new SQLiteParameter("@cas", caseId),
                    new SQLiteParameter("@from", SecurityContext.CurrentCenterId),
                    new SQLiteParameter("@to", toCenterId),
                    new SQLiteParameter("@date", txtLetterDate.Text.Trim()),
                    new SQLiteParameter("@reason", note),
                    new SQLiteParameter("@letter", txtLetterNo.Text.Trim()),
                    new SQLiteParameter("@by", SecurityContext.Username ?? ""),
                    new SQLiteParameter("@uid", SecurityContext.UserId));

                historyRecorded = true;
            }
            catch (Exception ex)
            {
                // نامه ساخته شده و در دستِ کاربر است؛ نرسیدنِ ردیفِ تاریخچه
                // نباید مثل شکستِ کاملِ عملیات به‌نظر برسد، ولی بی‌صدا هم
                // نمی‌ماند.
                UiTheme.ShowWarning(this,
                    "نامه ساخته شد، اما ثبت در تاریخچهٔ انتقال انجام نشد:" +
                    Environment.NewLine + ex.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // آپلود نسخهٔ امضاشده به اسناد پرونده
        // ═══════════════════════════════════════════════════════════════════
        private void AttachSignedDocument()
        {
            if (caseId <= 0)
            {
                UiTheme.ShowWarning(this, "پرونده مشخص نیست.");
                return;
            }
            if (caseCode.Length == 0)
            {
                UiTheme.ShowWarning(this, "کد اختصاصی پرونده مشخص نیست؛ سند ذخیره نمی‌شود.");
                return;
            }

            using (var ofd = new OpenFileDialog
            {
                Title = "نسخهٔ امضاشدهٔ نامهٔ انتقالی را انتخاب کنید",
                Filter = "اسناد|*.pdf;*.docx;*.jpg;*.jpeg;*.png|همه فایل‌ها|*.*"
            })
            {
                if (lastOutputPath.Length > 0 && File.Exists(lastOutputPath))
                    ofd.InitialDirectory = Path.GetDirectoryName(lastOutputPath);

                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    // همان مسیرِ ذخیره‌سازیِ استانداردِ اسنادِ پرونده که
                    // FrmDocs استفاده می‌کند — فایل به پوشهٔ Docs همان پرونده
                    // کپی می‌شود، نه اینکه فقط مسیرش ثبت گردد.
                    string savedPath = FileHelper.SaveFileToCaseFolder(
                        ofd.FileName,
                        caseCode,
                        FileHelper.SectionDocs,
                        FileHelper.CleanName(caseCode) + "-نامه انتقالی");

                    if (string.IsNullOrWhiteSpace(savedPath))
                    {
                        UiTheme.ShowError(this, "فایل سند ذخیره نشد: " + FileHelper.LastError);
                        return;
                    }

                    long docId = db.ExecuteInsertReturningId(@"
INSERT INTO TblDocs (CasID, DocType, OriginalFileName, DocFilePath, DocDescription)
VALUES (@cas, @type, @orig, @path, @desc)",
                        new SQLiteParameter("@cas", caseId),
                        new SQLiteParameter("@type", "نامه انتقالی"),
                        new SQLiteParameter("@orig", Path.GetFileName(ofd.FileName)),
                        new SQLiteParameter("@path", savedPath),
                        new SQLiteParameter("@desc",
                            "نامهٔ انتقالی شمارهٔ " + txtLetterNo.Text.Trim() +
                            " به " + cmbTo.Text.Trim()));

                    try { AuditLogger.Log("ثبت", "TblDocs", (int)docId, "", "نامه انتقالی امضاشده"); }
                    catch { }

                    UiTheme.ShowSuccess(this,
                        "سند امضاشده در اسناد پرونده ثبت شد." + Environment.NewLine +
                        "برای دیدن آن، تب «اسناد» پرونده را تازه کنید.");
                }
                catch (Exception ex)
                {
                    UiTheme.ShowError(this, "خطا در ثبت سند: " + ex.Message);
                }
            }
        }
    }
}
