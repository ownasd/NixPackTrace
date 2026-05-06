using System;
using System.Drawing;
using System.Windows.Forms;
using NixPackTrace.Core;
using NixPackTrace.Data;
using NixPackTrace.Services;

namespace NixPackTrace.UI
{
    public class MainForm : Form
    {
        private Panel contentPanel = null!;
        private Panel sidebarPanel = null!;

        public LocalDbService    LocalDb         { get; }
        public FirebaseService   FirebaseService  { get; }
        public SyncManager       SyncManager      { get; }
        public PrintService      PrintService     { get; }

        public MainForm()
        {
            LocalDb        = new LocalDbService();
            FirebaseService= new FirebaseService();
            SyncManager    = new SyncManager(LocalDb, FirebaseService);
            PrintService   = new PrintService();

            InitializeComponent();
            SyncManager.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SyncManager.Stop();
            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.Text            = $"NixPackTrace  ─  Operator: {AppState.CurrentUser}";
            this.Size            = new Size(1100, 750);
            this.MinimumSize     = new Size(900, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = Color.White;

            // ── Sidebar
            sidebarPanel = new Panel { Dock = DockStyle.Left, Width = 190, BackColor = Color.FromArgb(25, 35, 50) };

            // App title in sidebar
            sidebarPanel.Controls.Add(new Label
            {
                Text      = "NixPackTrace",
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                Location  = new Point(12, 18),
                AutoSize  = true
            });
            sidebarPanel.Controls.Add(new Label
            {
                Text      = $"👤 {AppState.CurrentUser}",
                ForeColor = Color.FromArgb(160, 190, 220),
                Font      = new Font("Segoe UI", 9),
                Location  = new Point(12, 46),
                AutoSize  = true
            });

            // Nav buttons
            var btnScan      = NavBtn("📦  Scan / Packing",  80);
            var btnDashboard = NavBtn("📊  Dashboard",       135);
            var btnDispatch  = NavBtn("🚚  Dispatch",        190);
            var btnReports   = NavBtn("📋  Reports",          245);
            var btnSettings  = NavBtn("⚙  Settings",         300);

            btnScan.Click      += (_, __) => LoadControl(new ScanPackingControl(this));
            btnDashboard.Click += (_, __) => LoadControl(new DashboardControl(this));
            btnDispatch.Click  += (_, __) => LoadControl(new DispatchControl(this));
            btnReports.Click   += (_, __) => LoadControl(new ReportsControl(this));
            btnSettings.Click  += (_, __) => 
            {
                using var prompt = new PasswordPromptForm("Enter Admin Password:");
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    LoadControl(new SettingsControl(this));
                }
            };

            // Sync status indicator at bottom of sidebar
            var lblSync = new Label
            {
                Text      = "● Sync active",
                ForeColor = Color.LightGreen,
                Font      = new Font("Segoe UI", 8),
                Location  = new Point(12, 640),
                AutoSize  = true
            };

            sidebarPanel.Controls.AddRange(new Control[] { btnScan, btnDashboard, btnDispatch, btnReports, btnSettings, lblSync });

            // ── Content area
            contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            this.Controls.Add(contentPanel);
            this.Controls.Add(sidebarPanel);

            // Start on scan screen
            LoadControl(new ScanPackingControl(this), bypassCheck: true);
        }

        public void LoadControl(UserControl c, bool bypassCheck = false)
        {
            if (!bypassCheck && contentPanel.Controls.Count > 0 && contentPanel.Controls[0] is ScanPackingControl scanCtrl)
            {
                if ((scanCtrl.IsBoxInProgress || scanCtrl.IsScanInProgress) && c.GetType() != typeof(ScanPackingControl))
                {
                    MessageBox.Show("Please complete the current box and print its label before leaving the scanning screen.", "Action Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            contentPanel.Controls.Clear();
            c.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(c);
        }

        private Button NavBtn(string text, int y)
        {
            var b = new Button
            {
                Text      = text,
                Location  = new Point(0, y),
                Width     = 190,
                Height    = 46,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(200, 215, 230),
                BackColor = Color.FromArgb(35, 48, 65),
                Font      = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(15, 0, 0, 0),
                Cursor    = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (_, __) => b.BackColor = Color.FromArgb(55, 75, 100);
            b.MouseLeave += (_, __) => b.BackColor = Color.FromArgb(35, 48, 65);
            return b;
        }
    }
}
