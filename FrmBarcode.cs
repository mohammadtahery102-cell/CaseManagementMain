using System;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using CaseManagement.DAL;
using CaseManagement.GuardianCardIntegration;
using CaseManagement.Helpers;
using static CaseManagement.Helpers.SqlHelpers;

namespace CaseManagement
{
    // ═════════════════════════════════════════════════════════════════════════
    // «بارکد و جستجو» — تولید/چاپِ بارکدِ Code128 برای کد پرونده یا شماره سند
    // (با همان مولدِ Code128Barcode که از قبل برای کارتِ سرپرست استفاده
    // می‌شود) و جستجوی پرونده/سند با کدِ اسکن‌شده یا تایپ‌شده.
    // ═════════════════════════════════════════════════════════════════════════
    public sealed class FrmBarcode : Form
    {
        private readonly DatabaseHelper db = new DatabaseHelper();

        private TextBox txtValue;
        private PictureBox picBarcode;
        private Button btnGenerate, btnPrint;

        private TextBox txtSearch;
        private Button btnSearch;

        private Bitmap currentBarcode;

        public FrmBarcode()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "بارکد و جستجو";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            BackColor = UiTheme.Background;
            Font = UiTheme.Font(UiTheme.SizeBody);
            UiTheme.MakeMainWindow(this, 760, 560);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.PrimaryDark };
            var title = new Label
            {
                Dock = DockStyle.Fill, Text = "بارکد و جستجو",
                ForeColor = Color.White, Font = UiTheme.FontBold(UiTheme.SizeLarge),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 0, 20, 0)
            };
            header.Controls.Add(title);

            picBarcode = new PictureBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            Panel picWrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            picWrap.Controls.Add(picBarcode);

            Panel genBar = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = UiTheme.CardBack };
            var genFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(12, 10, 12, 10)
            };
            genFlow.Controls.Add(new Label
            {
                Text = "کد پرونده یا شماره سند:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, 8, 6, 0)
            });
            txtValue = new TextBox { Width = 200, Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(0, 2, 10, 0) };
            genFlow.Controls.Add(txtValue);

            btnGenerate = UiTheme.CreateButton("تولید بارکد", "▤", UiTheme.Primary);
            btnGenerate.Size = new Size(140, 34); btnGenerate.Margin = new Padding(0, 0, 6, 0);
            btnGenerate.Click += btnGenerate_Click;
            genFlow.Controls.Add(btnGenerate);

            btnPrint = UiTheme.CreateSecondaryButton("چاپ بارکد", "⎙");
            btnPrint.Size = new Size(130, 34); btnPrint.Margin = new Padding(4, 0, 4, 0);
            btnPrint.Click += btnPrint_Click;
            genFlow.Controls.Add(btnPrint);
            genBar.Controls.Add(genFlow);

            Panel searchBar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = UiTheme.CardBack };
            var searchFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(12, 10, 12, 10)
            };
            searchFlow.Controls.Add(new Label
            {
                Text = "جستجو با کد اسکن‌شده:", AutoSize = true,
                Font = UiTheme.Font(UiTheme.SizeSmall), ForeColor = UiTheme.TextMuted,
                Margin = new Padding(0, 8, 6, 0)
            });
            txtSearch = new TextBox { Width = 220, Font = UiTheme.Font(UiTheme.SizeBody), Margin = new Padding(0, 2, 10, 0) };
            txtSearch.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; btnSearch_Click(s, EventArgs.Empty); }
            };
            searchFlow.Controls.Add(txtSearch);

            btnSearch = UiTheme.CreateButton("جستجو و بازکردن", "⌕", UiTheme.Primary);
            btnSearch.Size = new Size(150, 34); btnSearch.Margin = new Padding(0, 0, 6, 0);
            btnSearch.Click += btnSearch_Click;
            searchFlow.Controls.Add(btnSearch);
            searchBar.Controls.Add(searchFlow);

            Controls.Add(picWrap);
            Controls.Add(searchBar);
            Controls.Add(genBar);
            Controls.Add(header);
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            string value = txtValue.Text.Trim();
            if (value == "")
            {
                Msg.Show("کد را وارد کنید");
                txtValue.Focus();
                return;
            }

            if (currentBarcode != null)
            {
                var old = currentBarcode;
                currentBarcode = null;
                picBarcode.Image = null;
                old.Dispose();
            }

            currentBarcode = Code128Barcode.Generate(value);
            picBarcode.Image = currentBarcode;
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (!CaseManagement.Enterprise.PermissionService.Require("Barcode.Print"))
            {
                Msg.Show("کاربر اجازه چاپ بارکد را ندارد.");
                return;
            }

            if (currentBarcode == null)
            {
                Msg.Show("اول یک بارکد تولید کنید");
                return;
            }

            using (PrintDocument doc = new PrintDocument())
            using (PrintDialog dlg = new PrintDialog { Document = doc })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                doc.PrintPage += delegate (object s, PrintPageEventArgs pe)
                {
                    pe.Graphics.DrawImage(currentBarcode, pe.MarginBounds.Left, pe.MarginBounds.Top,
                        currentBarcode.Width, currentBarcode.Height);
                };

                try
                {
                    doc.Print();
                }
                catch (Exception ex)
                {
                    Msg.Show("خطا در چاپ: " + ex.Message);
                }
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            string code = txtSearch.Text.Trim();
            if (code == "")
            {
                Msg.Show("کد را وارد یا اسکن کنید");
                txtSearch.Focus();
                return;
            }

            try
            {
                int casId;
                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT CasID FROM TblCase WHERE Code = @Code AND (@CID = 0 OR CenterID = @CID) LIMIT 1", con))
                {
                    AddNVarChar(cmd, "@Code", code, 100);
                    AddInt(cmd, "@CID", SecurityContext.CenterFilterId);
                    con.Open();

                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        casId = Convert.ToInt32(result);
                        using (FrmCase frm = new FrmCase(casId))
                            frm.ShowDialog(this);
                        return;
                    }
                }

                using (SQLiteConnection con = db.GetConnection())
                using (SQLiteCommand cmd = new SQLiteCommand(
                    "SELECT CasID FROM TblDocs WHERE DocNo = @DocNo LIMIT 1", con))
                {
                    AddNVarChar(cmd, "@DocNo", code, 50);
                    con.Open();

                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        casId = Convert.ToInt32(result);
                        using (FrmCase frm = new FrmCase(casId))
                            frm.ShowDialog(this);
                        return;
                    }
                }

                Msg.Show("هیچ پرونده یا سندی با این کد پیدا نشد");
            }
            catch (Exception ex)
            {
                Msg.Show("خطا در جستجو: " + ex.Message);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (currentBarcode != null)
            {
                currentBarcode.Dispose();
                currentBarcode = null;
            }
            base.OnFormClosed(e);
        }
    }
}
