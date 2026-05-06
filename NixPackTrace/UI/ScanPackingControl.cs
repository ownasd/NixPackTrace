using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using NixPackTrace.Core;
using NixPackTrace.Data;
using NixPackTrace.Models;
using NixPackTrace.Services;

namespace NixPackTrace.UI
{
    /// <summary>
    /// Main scanning screen with two-step flow:
    ///   Step 1 – Scan MAC ID  → validate from Firebase
    ///   Step 2 – Scan Long QR → extract short QR, save & sync
    /// After every 5 items the box is auto-completed.
    /// </summary>
    public class ScanPackingControl : UserControl
    {
        // ── State machine ──────────────────────────────────────────────────────
        private enum ScanState { WaitingForMac, WaitingForQr, WaitingForTestingQr }
        private ScanState _state = ScanState.WaitingForMac;
        private string    _pendingMacId = "";
        private string    _pendingLongQr = "";

        // ── Box state ──────────────────────────────────────────────────────────
        private string _currentBoxNo    = "";
        private int    _currentBoxCount = 0;
        private List<PackingRecord> _boxItems = new();

        public bool IsBoxInProgress => _currentBoxCount > 0;
        public bool IsScanInProgress => _state != ScanState.WaitingForMac;

        // ── UI controls ────────────────────────────────────────────────────────
        private TextBox      txtScan    = null!;
        private Label        lblPrompt  = null!;
        private Label        lblStatus  = null!;
        private Label        lblBoxNo   = null!;
        private Label        lblCount   = null!;
        private ProgressBar  pbCount    = null!;
        private DataGridView grid       = null!;
        private Panel        rightPanel = null!;

        private readonly MainForm _host;

        public ScanPackingControl(MainForm host)
        {
            _host = host;
            InitializeComponent();
            this.Load += async (_, __) => await RestoreBoxStateAsync();
        }

        // ── Restore last box number from database ----------------------------------
        private async Task RestoreBoxStateAsync()
        {
            int lastSeq = await _host.LocalDb.GetLastBoxSequenceAsync();
            _currentBoxNo    = GenerateBoxId(DateTime.Now, lastSeq + 1);
            _currentBoxCount = await _host.LocalDb.GetBoxCountAsync(_currentBoxNo);
            // If the count is 0 for a NEW box, we are fine; but if there's already a box
            // with this ID in DB (because the box is in progress), we use it directly.
            if (_currentBoxCount == 0 && lastSeq > 0)
            {
                // Check if there's a partially filled box already
                string existingBox = GenerateBoxId(DateTime.Now, lastSeq);
                int existingCount = await _host.LocalDb.GetBoxCountAsync(existingBox);
                if (existingCount > 0 && existingCount < AppState.Settings.BoxSize)
                {
                    _currentBoxNo    = existingBox;
                    _currentBoxCount = existingCount;
                }
            }
            UpdateBoxUI();
            SetState(ScanState.WaitingForMac);
        }

        /// <summary>Generates a Box ID like E26001 from a date and sequence number.</summary>
        private static string GenerateBoxId(DateTime date, int sequence)
        {
            char monthChar = (char)('A' + date.Month - 1);
            string yearStr = date.ToString("yy");
            return $"{monthChar}{yearStr}{sequence:D3}";
        }

        /// <summary>Advances to the next box ID, e.g. E26001 -> E26002.</summary>
        private static string NextBoxId(string currentBoxNo)
        {
            if (currentBoxNo.Length < 3) return currentBoxNo;
            string prefix = currentBoxNo.Substring(0, 3); // e.g. "E26"
            string seqPart = currentBoxNo.Substring(3);   // e.g. "001"
            if (int.TryParse(seqPart, out int seq))
                return $"{prefix}{seq + 1:D3}";
            return currentBoxNo;
        }

        // ── UI Builder ─────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.BackColor = Color.White;

            // ── top input strip
            var topStrip = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(240, 246, 252), Padding = new Padding(15) };

            lblPrompt = new Label
            {
                Text     = "▶  Scan MAC ID",
                Font     = new Font("Segoe UI", 13, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(15, 10),
                ForeColor= Color.FromArgb(40, 80, 160)
            };

            txtScan = new TextBox
            {
                Location    = new Point(15, 42),
                Width       = 500,
                Height      = 38,
                Font        = new Font("Segoe UI", 16),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtScan.KeyDown += TxtScan_KeyDown;

            lblStatus = new Label
            {
                Text      = "Ready",
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Color.DimGray,
                AutoSize  = true,
                Location  = new Point(15, 90)
            };

            topStrip.Controls.AddRange(new Control[] { lblPrompt, txtScan, lblStatus });

            // ── right info panel
            rightPanel = new Panel { Dock = DockStyle.Right, Width = 230, BackColor = Color.FromArgb(248, 249, 250), Padding = new Padding(15) };

            lblBoxNo = new Label { Text = $"Box: {_currentBoxNo}", Font = new Font("Segoe UI", 13, FontStyle.Bold), AutoSize = true, Location = new Point(15, 20) };
            lblCount  = new Label { Text = $"Items: 0 / {AppState.Settings.BoxSize}", Font = new Font("Segoe UI", 11), AutoSize = true, Location = new Point(15, 55) };

            pbCount = new ProgressBar { Location = new Point(15, 80), Width = 195, Height = 18, Maximum = AppState.Settings.BoxSize };

            var btnComplete = MakeButton("✔ Complete Box", Color.SeaGreen, new Point(15, 120));
            btnComplete.Click += (_, __) =>
            {
                using var prompt = new PasswordPromptForm("Enter Admin Password to Complete Box:");
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    CompleteBox(manual: true);
                }
            };

            var btnReprint = MakeButton("⎙ Reprint Label", Color.DimGray, new Point(15, 170));
            btnReprint.Click += (_, __) =>
            {
                using var prompt = new PasswordPromptForm("Enter Admin Password to Reprint Label:");
                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    _host.PrintService.PrintBoxLabel(_currentBoxNo, _currentBoxCount, _boxItems, showPreview: false);
                }
            };

            var btnSyncNow = MakeButton("↑ Sync Now", Color.SteelBlue, new Point(15, 220));
            btnSyncNow.Click += async (_, __) =>
            {
                var pending = await _host.LocalDb.GetPendingSyncRecordsAsync();
                foreach (var r in pending)
                {
                    bool ok = await _host.FirebaseService.UpdatePackingAsync(r);
                    if (ok) await _host.LocalDb.MarkAsSyncedAsync(r.MAC_ID);
                }
                ShowStatus($"Sync done. Pending: {(await _host.LocalDb.GetPendingSyncRecordsAsync()).Count}", success: true);
            };

            rightPanel.Controls.AddRange(new Control[] { lblBoxNo, lblCount, pbCount, btnComplete, btnReprint, btnSyncNow });

            // ── data grid
            grid = new DataGridView
            {
                Dock                    = DockStyle.Fill,
                AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows      = false,
                ReadOnly                = true,
                SelectionMode           = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor         = Color.White,
                RowHeadersVisible       = false,
                DefaultCellStyle        = { Font = new Font("Consolas", 9) },
                ColumnHeadersDefaultCellStyle = { Font = new Font("Segoe UI", 9, FontStyle.Bold) }
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSr",    HeaderText = "Sr.",      FillWeight = 6  });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMac",   HeaderText = "MAC ID",   FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colShort", HeaderText = "Short QR", FillWeight = 12 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLong",  HeaderText = "Long QR",  FillWeight = 25 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTest",  HeaderText = "Testing QR",FillWeight = 17 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBox",   HeaderText = "Box",      FillWeight = 7  });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSync",  HeaderText = "Sync",     FillWeight = 7  });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTime",  HeaderText = "Time",     FillWeight = 12 });

            this.Controls.Add(grid);
            this.Controls.Add(rightPanel);
            this.Controls.Add(topStrip);
        }

        // ── Key handler ────────────────────────────────────────────────────────
        private bool _processing = false;
        private async void TxtScan_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter || _processing) return;
            e.Handled = e.SuppressKeyPress = true;

            string input = txtScan.Text.Trim();
            txtScan.Clear();

            if (string.IsNullOrEmpty(input)) return;

            _processing = true;
            txtScan.Enabled = false;

            await HandleInputAsync(input);

            txtScan.Enabled = true;
            txtScan.Focus();
            _processing = false;
        }

        // ── State machine ─────────────────────────────────────────────────────
        private async Task HandleInputAsync(string input)
        {
            switch (_state)
            {
                case ScanState.WaitingForMac:
                    await ProcessMacAsync(input);
                    break;

                case ScanState.WaitingForQr:
                    await ProcessQrAsync(input);
                    break;

                case ScanState.WaitingForTestingQr:
                    await ProcessTestingQrAsync(input);
                    break;
            }
        }

        private async Task ProcessMacAsync(string macId)
        {
            if (macId.Length < AppState.Settings.MacIdMinLength)
            {
                ShowStatus($"✗ MAC too short (min {AppState.Settings.MacIdMinLength})", success: false);
                return;
            }
            if (!string.IsNullOrEmpty(AppState.Settings.MacIdRequiredText) && 
                !macId.Contains(AppState.Settings.MacIdRequiredText, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus($"✗ MAC must contain '{AppState.Settings.MacIdRequiredText}'", success: false);
                return;
            }

            // Local duplicate check first (instant)
            string? existingBox = await _host.LocalDb.GetLocalBoxNoForMacAsync(macId);
            if (existingBox != null)
            {
                ShowStatus($"Already packed locally in Box {existingBox}", success: false);
                return;
            }

            // Firebase validation — reads AssemblyApp.json and checks status = OK
            ShowStatus("Validating with Firebase (reading AssemblyApp.json)…", success: true);
            var (ok, error, assemblyInfo) = await _host.FirebaseService.ValidateMacAsync(macId);

            if (!ok && !error.Contains("Offline", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus($"✗ {error}", success: false);
                return;
            }

            string detail = "";
            if (ok && assemblyInfo != null)
            {
                detail = $"  |  Assembled by: {assemblyInfo.Operator}  Shift: {assemblyInfo.Shift}  Station: {assemblyInfo.StationName}  @{assemblyInfo.Timestamp}";
            }

            _pendingMacId = macId;
            _pendingLongQr = ""; // Reset in case it is skipped

            if (AppState.Settings.RequireLongQr)
            {
                ShowStatus(ok ? $"✔ Assembly OK — MAC: {macId}{detail}   →  Now scan Long QR Code" : $"⚠ Firebase offline – packing offline. Scan Long QR now.", success: true);
                SetState(ScanState.WaitingForQr);
            }
            else if (AppState.Settings.RequireTestingQr)
            {
                ShowStatus(ok ? $"✔ Assembly OK — MAC: {macId}{detail}   →  Now scan Testing QR" : $"⚠ Firebase offline – packing offline. Scan Testing QR now.", success: true);
                SetState(ScanState.WaitingForTestingQr);
            }
            else
            {
                // Both disabled, pack immediately
                await CommitPackingRecordAsync("");
            }
        }

        private async Task ProcessQrAsync(string longQr)
        {
            if (longQr.Length < AppState.Settings.LongQrMinLength)
            {
                ShowStatus($"✗ QR Code too short (min {AppState.Settings.LongQrMinLength})", success: false);
                return;
            }
            if (!string.IsNullOrEmpty(AppState.Settings.LongQrRequiredText) && 
                !longQr.Contains(AppState.Settings.LongQrRequiredText, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus($"✗ QR must contain '{AppState.Settings.LongQrRequiredText}'", success: false);
                return;
            }

            _pendingLongQr = longQr;
            
            if (AppState.Settings.RequireTestingQr)
            {
                ShowStatus($"✔ Full Serial QR OK — Now scan Testing QR", success: true);
                SetState(ScanState.WaitingForTestingQr);
            }
            else
            {
                // Testing QR not required, pack immediately
                await CommitPackingRecordAsync("");
            }
        }

        private async Task ProcessTestingQrAsync(string testingQr)
        {
            if (testingQr.Length < AppState.Settings.TestingQrMinLength)
            {
                ShowStatus($"✗ Testing QR too short (min {AppState.Settings.TestingQrMinLength})", success: false);
                return;
            }
            if (!string.IsNullOrEmpty(AppState.Settings.TestingQrRequiredText) && 
                !testingQr.Contains(AppState.Settings.TestingQrRequiredText, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus($"✗ Testing QR must contain '{AppState.Settings.TestingQrRequiredText}'", success: false);
                return;  // Stay in WaitingForTestingQr state
            }

            await CommitPackingRecordAsync(testingQr);
        }

        private async Task CommitPackingRecordAsync(string testingQr)
        {
            // Extract short QR = last 11 characters of full serial (if we scanned a long QR)
            string shortQr = "";
            if (!string.IsNullOrEmpty(_pendingLongQr) && _pendingLongQr.Length >= 11)
            {
                shortQr = _pendingLongQr[^11..];
            }

            var record = new PackingRecord
            {
                MAC_ID     = _pendingMacId,
                MAC_LENGTH = _pendingMacId.Length,
                LONG_QR    = _pendingLongQr,
                QR_LENGTH  = _pendingLongQr.Length,
                SHORT_QR   = shortQr,
                TESTING_QR = testingQr,
                TESTING_QR_LENGTH = testingQr.Length,
                BOX_NO     = _currentBoxNo,
                STATUS     = "OK",
                TIMESTAMP  = DateTime.Now,
                PACKED_BY  = AppState.CurrentUser,
                Remarks    = "",
                SYNC_STATUS= "Pending"
            };

            // Save locally
            bool inserted = await _host.LocalDb.InsertRecordAsync(record);
            if (!inserted)
            {
                ShowStatus("✗ DB insert failed (possible duplicate)", success: false);
                SetState(ScanState.WaitingForMac);
                return;
            }

            // Try Firebase sync immediately
            bool synced = await _host.FirebaseService.UpdatePackingAsync(record);
            if (synced)
            {
                await _host.LocalDb.MarkAsSyncedAsync(record.MAC_ID);
                record.SYNC_STATUS = "Synced";
            }

            // Update UI
            _currentBoxCount++;
            _boxItems.Add(record);

            int sr = grid.Rows.Count + 1;
            grid.Rows.Insert(0,
                sr,
                record.MAC_ID,
                record.SHORT_QR,
                record.LONG_QR,
                record.TESTING_QR,
                record.BOX_NO,
                record.SYNC_STATUS,
                record.TIMESTAMP.ToString("HH:mm:ss"));

            UpdateBoxUI();
            ShowStatus($"✔ Packed  MAC: {_pendingMacId}" + (string.IsNullOrEmpty(shortQr) ? "" : $"   Short QR: {shortQr}") + $"   Box: {_currentBoxNo}", success: true);

            // Auto-complete box when full
            if (_currentBoxCount >= AppState.Settings.BoxSize)
                CompleteBox(manual: false);
            else
                SetState(ScanState.WaitingForMac);
        }

        // ── Box completion ────────────────────────────────────────────────────
        private void CompleteBox(bool manual)
        {
            if (_currentBoxCount == 0)
            {
                if (manual) ShowStatus("Box is empty – nothing to complete.", success: false);
                return;
            }

            _host.PrintService.PrintBoxLabel(_currentBoxNo, _currentBoxCount, _boxItems, showPreview: false);

            _currentBoxNo = NextBoxId(_currentBoxNo);
            _currentBoxCount = 0;
            _boxItems.Clear();
            grid.Rows.Clear(); // Clear the screen context

            UpdateBoxUI();
            ShowStatus($"Box completed. New box: {_currentBoxNo}", success: true);
            SetState(ScanState.WaitingForMac);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void SetState(ScanState state)
        {
            _state = state;
            switch (state)
            {
                case ScanState.WaitingForMac:
                    lblPrompt.Text      = "▶  Step 1 – Scan MAC ID";
                    lblPrompt.ForeColor = Color.FromArgb(40, 80, 160);
                    txtScan.BackColor   = Color.White;
                    break;
                case ScanState.WaitingForQr:
                    lblPrompt.Text      = "▶  Step 2 – Scan Full Serial QR";
                    lblPrompt.ForeColor = Color.FromArgb(30, 130, 60);
                    txtScan.BackColor   = Color.FromArgb(240, 255, 245);
                    break;
                case ScanState.WaitingForTestingQr:
                    lblPrompt.Text      = "▶  Step 3 – Scan Testing QR";
                    lblPrompt.ForeColor = Color.FromArgb(180, 80, 20);
                    txtScan.BackColor   = Color.FromArgb(255, 245, 235);
                    break;
            }
            txtScan.Focus();
        }

        private void ShowStatus(string msg, bool success)
        {
            lblStatus.Text      = msg;
            lblStatus.ForeColor = success ? Color.FromArgb(20, 140, 60) : Color.Crimson;
            if (success) SoundService.PlaySuccess();
            else         SoundService.PlayError();
        }

        private void UpdateBoxUI()
        {
            lblBoxNo.Text  = $"Box:  {_currentBoxNo}";
            lblCount.Text  = $"Items: {_currentBoxCount} / {AppState.Settings.BoxSize}";
            pbCount.Maximum= AppState.Settings.BoxSize;
            pbCount.Value  = Math.Min(_currentBoxCount, AppState.Settings.BoxSize);
        }

        private static Button MakeButton(string text, Color back, Point loc) =>
            new Button
            {
                Text       = text,
                Location   = loc,
                Width      = 195,
                Height     = 38,
                BackColor  = back,
                ForeColor  = Color.White,
                FlatStyle  = FlatStyle.Flat,
                Font       = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor     = Cursors.Hand
            }.Also(b => b.FlatAppearance.BorderSize = 0);
    }

    // tiny fluent helper to avoid extra variables
    public static class ControlExt
    {
        public static T Also<T>(this T t, Action<T> a) { a(t); return t; }
    }
}
