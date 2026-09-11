using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Accounting;
using CaseManagement.Accounting.Ledger.Application;
using CaseManagement.Accounting.Ledger.Domain;
using CaseManagement.Helpers;

namespace CaseManagement.Accounting.Ledger.Adapters
{
    public sealed class FrmDimensionMaster : Form
    {
        private readonly bool _costCenter;
        private readonly ILedgerIdentity _identity;
        private readonly ICostCenterService _cc;
        private readonly IProjectService _pr;
        private TreeView _tree;
        private string _filter = "";

        public static FrmDimensionMaster CostCenters()
        {
            return new FrmDimensionMaster(true);
        }

        public static FrmDimensionMaster Projects()
        {
            return new FrmDimensionMaster(false);
        }

        private FrmDimensionMaster(bool costCenter)
        {
            _costCenter = costCenter;
            _identity = DesktopLedgerIdentity.FromSession();
            _cc = new CostCenterService();
            _pr = new ProjectService();
            string heading = costCenter ? "مراکز هزینه" : "پروژه‌ها";
            Text = heading;
            AccountingChrome.MakeWorkspace(this, 720, 560);

            _tree = new TreeView { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
            TextBox search = new TextBox { Width = 180 };
            search.TextChanged += delegate { _filter = search.Text ?? ""; Reload(); };
            FlowLayoutPanel flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 52, RightToLeft = RightToLeft.Yes,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(6)
            };
            flow.Controls.Add(new Label { Text = "جستجو", AutoSize = true, Padding = new Padding(8, 8, 4, 0) });
            flow.Controls.Add(search);
            flow.Controls.Add(Btn("فرزند", AddChild));
            flow.Controls.Add(Btn("غیرفعال", delegate { SetActive(false); }));
            flow.Controls.Add(Btn("فعال", delegate { SetActive(true); }));
            flow.Controls.Add(Btn("تازه‌سازی", Reload));
            Controls.Add(_tree);
            Controls.Add(flow);
            Controls.Add(AccountingChrome.BuildStatusBar());
            Controls.Add(AccountingChrome.BuildHeader(heading, AccountingChrome.Breadcrumb("دفتر کل", heading)));
            AccountingChrome.Polish(this);
            Reload();
        }

        private void Reload()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            Dictionary<long, TreeNode> map = new Dictionary<long, TreeNode>();
            if (_costCenter)
            {
                IList<GlCostCenter> list = _cc.List(Company(), true);
                for (int pass = 0; pass < 16; pass++)
                {
                    int added = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        GlCostCenter n = list[i];
                        if (map.ContainsKey(n.CostCenterId)) continue;
                        if (n.ParentCostCenterId.HasValue && !map.ContainsKey(n.ParentCostCenterId.Value)) continue;
                        if (!DimMatches(n.Code, n.Name)) continue;
                        TreeNode node = Node(n.CostCenterId, n.Code, n.Name, n.IsActive, n.IsLeaf, n.RowVersion);
                        if (!n.ParentCostCenterId.HasValue) _tree.Nodes.Add(node);
                        else map[n.ParentCostCenterId.Value].Nodes.Add(node);
                        map[n.CostCenterId] = node;
                        added++;
                    }
                    if (added == 0) break;
                }
            }
            else
            {
                IList<GlProject> list = _pr.List(Company(), true);
                for (int pass = 0; pass < 16; pass++)
                {
                    int added = 0;
                    for (int i = 0; i < list.Count; i++)
                    {
                        GlProject n = list[i];
                        if (map.ContainsKey(n.ProjectId)) continue;
                        if (n.ParentProjectId.HasValue && !map.ContainsKey(n.ParentProjectId.Value)) continue;
                        if (!DimMatches(n.Code, n.Name)) continue;
                        TreeNode node = Node(n.ProjectId, n.Code, n.Name, n.IsActive, n.IsLeaf, n.RowVersion);
                        if (!n.ParentProjectId.HasValue) _tree.Nodes.Add(node);
                        else map[n.ParentProjectId.Value].Nodes.Add(node);
                        map[n.ProjectId] = node;
                        added++;
                    }
                    if (added == 0) break;
                }
            }
            _tree.ExpandAll();
            _tree.EndUpdate();
        }

        private bool DimMatches(string code, string name)
        {
            string q = (_filter ?? "").Trim();
            if (q.Length == 0) return true;
            return (code ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                || (name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddChild()
        {
            long? parent = SelectedId();
            using (Form dlg = new Form
            {
                Text = "ایجاد", Width = 360, Height = 220, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true,
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            })
            {
                AccountingChrome.PrepareDialog(dlg);
                TextBox code = Field(dlg, "کد", 20, 40);
                TextBox name = Field(dlg, "نام", 20, 90);
                CheckBox leaf = new CheckBox { Text = "برگ (قابل ثبت روی سند)", Left = 20, Top = 125, Width = 280, Checked = true, Parent = dlg };
                Button ok = UiTheme.CreateButton("ثبت", "", UiTheme.PrimaryLight);
                ok.Left = 20; ok.Top = 150;
                ok.Click += delegate
                {
                    CreateDimensionCommand cmd = new CreateDimensionCommand
                    {
                        CompanyId = Company(),
                        Code = code.Text,
                        Name = name.Text,
                        ParentId = parent,
                        IsLeaf = leaf.Checked
                    };
                    LedgerResult r = _costCenter ? _cc.Create(cmd, _identity) : _pr.Create(cmd, _identity);
                    if (r.Ok) { dlg.DialogResult = DialogResult.OK; dlg.Close(); }
                    else UiTheme.ShowWarning(this, LedgerUiText.Error(r.ErrorCode, r.Message));
                };
                dlg.Controls.Add(ok);
                dlg.ShowDialog(this);
            }
            Reload();
        }

        private void SetActive(bool on)
        {
            TreeNode node = _tree.SelectedNode;
            if (node == null) return;
            DimTag tag = node.Tag as DimTag;
            if (tag == null) return;
            SoftDeleteCommand cmd = new SoftDeleteCommand { EntityId = tag.Id, ExpectedRowVersion = tag.RowVersion };
            LedgerResult r = _costCenter
                ? (on ? _cc.Activate(cmd, _identity) : _cc.Deactivate(cmd, _identity))
                : (on ? _pr.Activate(cmd, _identity) : _pr.Deactivate(cmd, _identity));
            if (!r.Ok) UiTheme.ShowWarning(this, LedgerUiText.Error(r.ErrorCode, r.Message));
            Reload();
        }

        private TreeNode Node(long id, string code, string name, bool active, bool leaf, long rv)
        {
            TreeNode n = new TreeNode(code + "  " + name + (active ? "" : " [غیرفعال]") + (leaf ? "" : " [سرفصل]"));
            n.Tag = new DimTag { Id = id, RowVersion = rv };
            return n;
        }

        private long? SelectedId()
        {
            if (_tree.SelectedNode == null) return null;
            DimTag tag = _tree.SelectedNode.Tag as DimTag;
            return tag == null ? (long?)null : tag.Id;
        }

        private int Company()
        {
            return _identity.CompanyId > 0 ? _identity.CompanyId : LedgerCodes.DefaultCompanyId;
        }

        private Button Btn(string text, Action click)
        {
            Button b = UiTheme.CreateButton(text, "", UiTheme.PrimaryLight);
            b.AutoSize = true;
            b.Click += delegate { click(); };
            return b;
        }

        private static TextBox Field(Form dlg, string label, int x, int y)
        {
            dlg.Controls.Add(new Label { Text = label, Left = x, Top = y - 18, AutoSize = true });
            TextBox t = new TextBox { Left = x, Top = y, Width = 300 };
            dlg.Controls.Add(t);
            return t;
        }

        private sealed class DimTag
        {
            public long Id;
            public long RowVersion;
        }
    }
}
