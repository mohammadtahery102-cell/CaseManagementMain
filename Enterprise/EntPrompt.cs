using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CaseManagement.Helpers;

namespace CaseManagement.Enterprise
{
    // نوع ورودی در دیالوگ عمومی ویرایش.
    public enum EntFieldKind { Text, Multiline, Number, Check, Combo }

    // توصیف یک فیلد در دیالوگ عمومی ویرایش.
    public class EntField
    {
        public string       Key   { get; set; }
        public string       Label { get; set; }
        public EntFieldKind Kind  { get; set; }
        public string       Value { get; set; }

        // برای Combo: فهرست گزینه‌ها به شکل (مقدار ذخیره‌شده → متن نمایشی)
        public List<KeyValuePair<string, string>> Items { get; set; }

        public static EntField Text(string key, string label, string value)
        {
            return new EntField { Key = key, Label = label, Kind = EntFieldKind.Text, Value = value };
        }

        public static EntField Multiline(string key, string label, string value)
        {
            return new EntField { Key = key, Label = label, Kind = EntFieldKind.Multiline, Value = value };
        }

        public static EntField Number(string key, string label, string value)
        {
            return new EntField { Key = key, Label = label, Kind = EntFieldKind.Number, Value = value };
        }

        public static EntField Check(string key, string label, bool value)
        {
            return new EntField { Key = key, Label = label, Kind = EntFieldKind.Check, Value = value ? "1" : "0" };
        }

        public static EntField Combo(string key, string label, string value,
                                     List<KeyValuePair<string, string>> items)
        {
            return new EntField { Key = key, Label = label, Kind = EntFieldKind.Combo, Value = value, Items = items };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // دیالوگ عمومی «ویرایش چند فیلد» برای فرم‌های مدیریتی هسته سازمانی.
    // آموزش — چرا یک دیالوگ عمومی: فرم‌های این فاز (مراحل گردش‌کار، سطوح تأیید،
    // وظیفه، قاعده، ماژول و ...) همگی به یک پنجره ساده «چند فیلد + تأیید/انصراف»
    // نیاز دارند. به‌جای ساختن ده فرم تقریباً یکسان، همین یکی بازاستفاده می‌شود.
    // ظاهر و راست‌چینی دقیقاً از UiTheme پروژه پیروی می‌کند.
    // ─────────────────────────────────────────────────────────────────────────
    public static class EntPrompt
    {
        // خروجی: null اگر کاربر انصراف داد؛ در غیر این صورت مقدار هر فیلد با کلید آن.
        public static Dictionary<string, string> Edit(IWin32Window owner, string title, params EntField[] fields)
        {
            if (fields == null || fields.Length == 0) return null;

            using (Form dialog = new Form())
            {
                dialog.Text              = title;
                dialog.RightToLeft       = RightToLeft.Yes;
                dialog.RightToLeftLayout = true;
                dialog.BackColor         = UiTheme.Background;
                dialog.Font              = UiTheme.Font(UiTheme.SizeBody);
                dialog.FormBorderStyle   = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox       = false;
                dialog.MinimizeBox       = false;
                dialog.StartPosition     = FormStartPosition.CenterParent;
                dialog.ShowInTaskbar     = false;

                const int labelWidth = 130;
                const int inputWidth = 300;
                const int rowHeight  = 34;

                int y = 16;
                Dictionary<string, Control> inputs = new Dictionary<string, Control>();

                foreach (EntField field in fields)
                {
                    Label label = new Label
                    {
                        Text      = field.Label,
                        AutoSize  = false,
                        Width     = labelWidth,
                        Height    = 24,
                        Left      = 16 + inputWidth + 12,
                        Top       = y + 3,
                        ForeColor = UiTheme.TextDark,
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    dialog.Controls.Add(label);

                    Control input = CreateInput(field, inputWidth);
                    input.Left = 16;
                    input.Top  = y;
                    dialog.Controls.Add(input);
                    inputs[field.Key] = input;

                    y += Math.Max(rowHeight, input.Height + 8);
                }

                Button ok = UiTheme.CreateButton("تأیید", "✔", UiTheme.Primary);
                ok.Width = 110; ok.Top = y + 8; ok.Left = 16;
                ok.DialogResult = DialogResult.OK;

                Button cancel = UiTheme.CreateSecondaryButton("انصراف", "✖");
                cancel.Width = 110; cancel.Top = y + 8; cancel.Left = 16 + 120;
                cancel.DialogResult = DialogResult.Cancel;

                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                dialog.ClientSize = new Size(16 + inputWidth + 12 + labelWidth + 16, y + 8 + ok.Height + 16);

                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return null;

                Dictionary<string, string> result = new Dictionary<string, string>();

                foreach (EntField field in fields)
                    result[field.Key] = ReadInput(field, inputs[field.Key]);

                return result;
            }
        }

        // میان‌بر برای حالت تک‌فیلدی (پرکاربردترین حالت).
        public static string AskText(IWin32Window owner, string title, string label, string value)
        {
            Dictionary<string, string> result = Edit(owner, title, EntField.Text("v", label, value));
            return result == null ? null : result["v"];
        }

        private static Control CreateInput(EntField field, int width)
        {
            switch (field.Kind)
            {
                case EntFieldKind.Check:
                    return new CheckBox
                    {
                        Width       = width,
                        Height      = 24,
                        Checked     = field.Value == "1",
                        Text        = "",
                        RightToLeft = RightToLeft.Yes
                    };

                case EntFieldKind.Combo:
                    {
                        ComboBox combo = new ComboBox
                        {
                            Width         = width,
                            DropDownStyle = ComboBoxStyle.DropDownList,
                            RightToLeft   = RightToLeft.Yes
                        };

                        if (field.Items != null)
                            foreach (KeyValuePair<string, string> item in field.Items)
                                combo.Items.Add(new ComboEntry(item.Key, item.Value));

                        for (int i = 0; i < combo.Items.Count; i++)
                            if (string.Equals(((ComboEntry)combo.Items[i]).Key, field.Value, StringComparison.Ordinal))
                            {
                                combo.SelectedIndex = i;
                                break;
                            }

                        if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                            combo.SelectedIndex = 0;

                        return combo;
                    }

                case EntFieldKind.Multiline:
                    {
                        TextBox box = new TextBox
                        {
                            Width       = width,
                            Height      = 70,
                            Multiline   = true,
                            ScrollBars  = ScrollBars.Vertical,
                            Text        = field.Value ?? "",
                            RightToLeft = RightToLeft.Yes
                        };
                        UiTheme.StyleTextBox(box);
                        return box;
                    }

                default:
                    {
                        TextBox box = new TextBox
                        {
                            Width       = width,
                            Text        = field.Value ?? "",
                            RightToLeft = RightToLeft.Yes
                        };
                        UiTheme.StyleTextBox(box);

                        if (field.Kind == EntFieldKind.Number)
                            box.KeyPress += delegate (object sender, KeyPressEventArgs e)
                            {
                                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '-')
                                    e.Handled = true;
                            };

                        return box;
                    }
            }
        }

        private static string ReadInput(EntField field, Control input)
        {
            CheckBox check = input as CheckBox;
            if (check != null) return check.Checked ? "1" : "0";

            ComboBox combo = input as ComboBox;
            if (combo != null)
            {
                ComboEntry entry = combo.SelectedItem as ComboEntry;
                return entry == null ? "" : entry.Key;
            }

            return (input.Text ?? "").Trim();
        }

        // آیتم ComboBox که مقدار ذخیره‌شده و متن نمایشی را جدا نگه می‌دارد.
        private class ComboEntry
        {
            public string Key  { get; private set; }
            private readonly string _display;

            public ComboEntry(string key, string display)
            {
                Key = key;
                _display = display;
            }

            public override string ToString() { return _display; }
        }
    }
}
