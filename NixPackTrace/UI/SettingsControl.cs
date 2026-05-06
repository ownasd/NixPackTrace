using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using NixPackTrace.Core;

namespace NixPackTrace.UI
{
    /// <summary>
    /// Settings screen.  Allows configuring Box Size, Firebase RTDB URL,
    /// Printer Name, and Operator name persisted to appsettings.json.
    /// </summary>
    public class SettingsControl : UserControl
    {
        private readonly MainForm _host;

        private TextBox txtBoxSize    = null!;
        private TextBox txtFbUrl      = null!;
        private TextBox txtProductName= null!;
        private CheckBox chkReqLongQr = null!;
        private CheckBox chkReqTestingQr = null!;
        private ComboBox cbPrinters   = null!;

        private TextBox txtMacLength       = null!;
        private TextBox txtMacText         = null!;
        private TextBox txtLongQrLength    = null!;
        private TextBox txtLongQrText      = null!;
        private TextBox txtTestingQrLength = null!;
        private TextBox txtTestingQrText   = null!;

        public SettingsControl(MainForm host)
        {
            _host = host;
            InitializeComponent();
            this.Load += (_, __) => PopulateFields();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            this.Padding   = new Padding(30);

            int y = 20;

            AddTitle("Settings", ref y);

            // ── Product Name
            AddLabel("Product Name:", 30, y);
            txtProductName = AddTextBox(250, y, 300); y += 40;

            // ── Box Size
            AddLabel("Box Size (items per box):", 30, y);
            txtBoxSize = AddTextBox(250, y, 80); y += 40;

            // ── Scrap Options & Validations
            AddLabel("Scanning Needs & Validations:", 30, y);
            y += 30;
            
            AddLabel("MAC ID Min Length:", 30, y);
            txtMacLength = AddTextBox(180, y, 50);
            AddLabel("Must Contain:", 240, y);
            txtMacText = AddTextBox(340, y, 100);
            y += 35;

            chkReqLongQr = new CheckBox { Text = "Require Full Serial (Long QR)", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(chkReqLongQr); 
            y += 30;
            AddLabel("Long QR Min Length:", 30, y);
            txtLongQrLength = AddTextBox(180, y, 50);
            AddLabel("Must Contain:", 240, y);
            txtLongQrText = AddTextBox(340, y, 100);
            y += 35;
            
            chkReqTestingQr = new CheckBox { Text = "Require Testing QR", Location = new Point(30, y), AutoSize = true, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(chkReqTestingQr); 
            y += 30;
            AddLabel("Testing QR Min Length:", 30, y);
            txtTestingQrLength = AddTextBox(180, y, 50);
            AddLabel("Must Contain:", 240, y);
            txtTestingQrText = AddTextBox(340, y, 100);
            y += 40;

            // ── Firebase RTDB URL
            AddLabel("Firebase Realtime DB URL:", 30, y);
            txtFbUrl = AddTextBox(250, y, 420); y += 40;

            var lblHint = new Label
            {
                Text      = "e.g.  https://my-project-default-rtdb.firebaseio.com/",
                Location  = new Point(250, y - 18),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8),
                ForeColor = Color.Gray
            };

            // ── Printer
            AddLabel("Label Printer:", 30, y);
            cbPrinters = new ComboBox { Location = new Point(250, y), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 10) };
            cbPrinters.Items.Add("(Default System Printer)");
            foreach (string p in PrinterSettings.InstalledPrinters) cbPrinters.Items.Add(p);
            this.Controls.Add(cbPrinters);
            y += 45;

            // ── Test Firebase connection
            var btnTest = MakeButton("🔗 Test Firebase", Color.DarkOrange, new Point(250, y), 180);
            btnTest.Click += async (_, __) =>
            {
                AppState.Settings.FirebaseUrl = txtFbUrl.Text.Trim();
                // Simple ping – read a known path
                try
                {
                    using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var r = await http.GetAsync(AppState.Settings.FirebaseUrl.TrimEnd('/') + "/.json?shallow=true");
                    MessageBox.Show(r.IsSuccessStatusCode ? "✔ Firebase reachable!" : $"✗ HTTP {(int)r.StatusCode}", "Firebase Test");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"✗ {ex.Message}", "Firebase Test");
                }
            };

            // ── Save
            var btnSave = MakeButton("💾 Save Settings", Color.FromArgb(0, 120, 215), new Point(250, y + 60), 180);
            btnSave.Click += SaveClick;

            var btnManageOld = MakeButton("📂 Manage Old Boxes", Color.MediumPurple, new Point(30, y + 60), 200);
            btnManageOld.Click += (_, __) =>
            {
                using var prompt = new PasswordPromptForm("Enter Admin Password:");
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    new ManageBoxesForm(_host).ShowDialog();
                }
            };

            this.Controls.AddRange(new Control[]
            {
                new Label { Text = "Settings", Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(30, 15) },
                lblHint, btnTest, btnSave, btnManageOld
            });
        }

        private void PopulateFields()
        {
            txtProductName.Text = AppState.Settings.ProductName;
            txtBoxSize.Text = AppState.Settings.BoxSize.ToString();
            chkReqLongQr.Checked = AppState.Settings.RequireLongQr;
            chkReqTestingQr.Checked = AppState.Settings.RequireTestingQr;

            txtMacLength.Text = AppState.Settings.MacIdMinLength.ToString();
            txtMacText.Text = AppState.Settings.MacIdRequiredText;
            txtLongQrLength.Text = AppState.Settings.LongQrMinLength.ToString();
            txtLongQrText.Text = AppState.Settings.LongQrRequiredText;
            txtTestingQrLength.Text = AppState.Settings.TestingQrMinLength.ToString();
            txtTestingQrText.Text = AppState.Settings.TestingQrRequiredText;

            txtFbUrl.Text   = AppState.Settings.FirebaseUrl;

            string printer = AppState.Settings.PrinterName;
            if (cbPrinters.Items.Contains(printer)) cbPrinters.SelectedItem = printer;
            else cbPrinters.SelectedIndex = 0;
        }

        private void SaveClick(object? sender, EventArgs e)
        {
            AppState.Settings.ProductName = txtProductName.Text.Trim();
            AppState.Settings.RequireLongQr = chkReqLongQr.Checked;
            AppState.Settings.RequireTestingQr = chkReqTestingQr.Checked;

            int.TryParse(txtMacLength.Text, out int macLen);
            AppState.Settings.MacIdMinLength = macLen;
            AppState.Settings.MacIdRequiredText = txtMacText.Text.Trim();

            int.TryParse(txtLongQrLength.Text, out int longQrLen);
            AppState.Settings.LongQrMinLength = longQrLen;
            AppState.Settings.LongQrRequiredText = txtLongQrText.Text.Trim();

            int.TryParse(txtTestingQrLength.Text, out int testQrLen);
            AppState.Settings.TestingQrMinLength = testQrLen;
            AppState.Settings.TestingQrRequiredText = txtTestingQrText.Text.Trim();

            if (int.TryParse(txtBoxSize.Text, out int sz) && sz > 0)
                AppState.Settings.BoxSize = sz;

            string url = txtFbUrl.Text.Trim();
            if (!url.StartsWith("https://"))
            {
                MessageBox.Show("Firebase URL must start with https://", "Validation");
                return;
            }
            AppState.Settings.FirebaseUrl = url.EndsWith('/') ? url : url + "/";

            AppState.Settings.PrinterName = cbPrinters.SelectedIndex > 0
                ? cbPrinters.SelectedItem?.ToString() ?? ""
                : "";

            AppState.SaveSettings();
            MessageBox.Show("Settings saved.", "Done");
        }

        // ── helpers
        private void AddTitle(string text, ref int y)
        {
            this.Controls.Add(new Label { Text = text, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(30, y) });
            y += 55;
        }

        private void AddLabel(string text, int x, int y)
        {
            this.Controls.Add(new Label { Text = text, Location = new Point(x, y + 5), AutoSize = true, Font = new Font("Segoe UI", 10) });
        }

        private TextBox AddTextBox(int x, int y, int w)
        {
            var t = new TextBox { Location = new Point(x, y), Width = w, Font = new Font("Segoe UI", 10) };
            this.Controls.Add(t);
            return t;
        }

        private static Button MakeButton(string text, Color back, Point loc, int w)
        {
            var b = new Button { Text = text, Location = loc, Width = w, Height = 38, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            return b;
        }
    }
}
