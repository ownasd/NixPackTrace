using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NixPackTrace.Models;

namespace NixPackTrace.UI
{
    /// <summary>
    /// Reports screen with date-range filter and CSV export.
    /// Columns mirror the Excel sheet supplied by the user.
    /// </summary>
    public class ReportsControl : UserControl
    {
        private readonly MainForm       _host;
        private DateTimePicker          dtFrom  = null!;
        private DateTimePicker          dtTo    = null!;
        private DataGridView            grid    = null!;
        private Label                   lblTotal= null!;
        private List<PackingRecord>     _data   = new();

        public ReportsControl(MainForm host)
        {
            _host = host;
            InitializeComponent();
            this.Load += (_, __) => LoadDataAsync();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;

            // ── toolbar
            var bar = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = Color.FromArgb(240, 246, 252), Padding = new Padding(10, 8, 10, 8) };

            var lblFrom = new Label { Text = "From:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(10, 18) };
            dtFrom = new DateTimePicker { Location = new Point(55, 14), Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10) };
            dtFrom.ValueChanged += (_, __) => LoadDataAsync();

            var lblTo = new Label { Text = "To:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(200, 18) };
            dtTo = new DateTimePicker { Location = new Point(225, 14), Width = 130, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10) };
            dtTo.Value = DateTime.Today;
            dtTo.ValueChanged += (_, __) => LoadDataAsync();

            var btnLoad = MakeButton("🔍 Load", Color.SteelBlue, new Point(370, 12), 90);
            btnLoad.Click += (_, __) => LoadDataAsync();

            var btnCsv  = MakeButton("📥 CSV Export", Color.SeaGreen, new Point(470, 12), 130);
            btnCsv.Click += (_, __) => ExportCsv();

            lblTotal = new Label { Text = "Total: 0", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(620, 18), ForeColor = Color.DimGray };

            bar.Controls.AddRange(new Control[] { lblFrom, dtFrom, lblTo, dtTo, btnLoad, btnCsv, lblTotal });

            // ── grid – columns matching user's Excel exactly
            grid = new DataGridView
            {
                Dock                = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows  = false,
                ReadOnly            = true,
                SelectionMode       = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor     = Color.White,
                RowHeadersVisible   = false,
                DefaultCellStyle    = { Font = new Font("Consolas", 9) },
                ColumnHeadersDefaultCellStyle = { Font = new Font("Segoe UI", 9, FontStyle.Bold) }
            };

            grid.Columns.Add(col("colSr",      "Sr. No",         6));
            grid.Columns.Add(col("colMac",      "MAC ID",        18));
            grid.Columns.Add(col("colMacLen",   "MAC Length",     9));
            grid.Columns.Add(col("colLongQr",   "QR Code (Long)",30));
            grid.Columns.Add(col("colQrLen",    "QR Length",      8));
            grid.Columns.Add(col("colShortQr",  "QR Code (Short)",14));
            grid.Columns.Add(col("colTestQr",   "Testing QR",     20));
            grid.Columns.Add(col("colTestLen",  "Test QR Len.",    9));
            grid.Columns.Add(col("colBox",      "Box No",         7));
            grid.Columns.Add(col("colDispatchDate", "Dispatched Date", 12));
            grid.Columns.Add(col("colDispatchBy",   "Dispatched By",   12));
            grid.Columns.Add(col("colDispatchRemarks","Dispatch Remarks",15));
            grid.Columns.Add(col("colStatus",   "OK Sticker",     9));
            grid.Columns.Add(col("colDate",     "Packing Date",  15));
            grid.Columns.Add(col("colPackedBy", "Packed By",     12));
            grid.Columns.Add(col("colSync",     "Sync Status",   10));
            grid.Columns.Add(col("colRemarks",  "Remarks",       10));

            this.Controls.Add(grid);
            this.Controls.Add(bar);
        }

        // ── Load ── ─────────────────────────────────────────────────────────────
        private async void LoadDataAsync()
        {
            _data = await _host.LocalDb.GetRecordsByDateRangeAsync(dtFrom.Value, dtTo.Value);
            grid.Rows.Clear();
            int sr = 1;
            foreach (var r in _data)
            {
                grid.Rows.Add(
                    sr++,
                    r.MAC_ID,
                    r.MAC_LENGTH,
                    r.LONG_QR,
                    r.QR_LENGTH,
                    r.SHORT_QR,
                    r.TESTING_QR,
                    r.TESTING_QR_LENGTH,
                    r.BOX_NO,
                    r.DispatchDate?.ToString("dd-MM-yyyy HH:mm:ss") ?? "",
                    r.DispatchBy ?? "",
                    r.DispatchRemarks ?? "",
                    r.STATUS,
                    r.TIMESTAMP.ToString("dd-MM-yyyy  HH:mm:ss"),
                    r.PACKED_BY,
                    r.SYNC_STATUS,
                    r.Remarks
                );
            }
            lblTotal.Text = $"Total: {_data.Count}";
        }

        // ── CSV Export ──────────────────────────────────────────────────────────
        private void ExportCsv()
        {
            if (_data.Count == 0) { MessageBox.Show("No data to export."); return; }

            using var sfd = new SaveFileDialog
            {
                Filter   = "CSV Files|*.csv",
                FileName = $"PackingReport_{dtFrom.Value:yyyyMMdd}_to_{dtTo.Value:yyyyMMdd}.csv"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            var lines = new System.Collections.Generic.List<string>
            {
                "Sr. No,MAC ID,MAC Length,QR Code (Long),QR Length,QR Code (Short),Testing QR,Testing QR Length,Box No,Dispatched Date,Dispatched By,Dispatch Remarks,OK Sticker,Packing Date,Packed By,Sync Status,Remarks"
            };

            int sr = 1;
            lines.AddRange(_data.Select(r =>
                string.Join(",",
                    sr++,
                    Q(r.MAC_ID),
                    r.MAC_LENGTH,
                    Q(r.LONG_QR),
                    r.QR_LENGTH,
                    Q(r.SHORT_QR),
                    Q(r.TESTING_QR),
                    r.TESTING_QR_LENGTH,
                    r.BOX_NO,
                    Q(r.DispatchDate?.ToString("dd-MM-yyyy HH:mm:ss")),
                    Q(r.DispatchBy),
                    Q(r.DispatchRemarks),
                    Q(r.STATUS),
                    Q(r.TIMESTAMP.ToString("dd-MM-yyyy HH:mm:ss")),
                    Q(r.PACKED_BY),
                    Q(r.SYNC_STATUS),
                    Q(r.Remarks)
                )));

            File.WriteAllLines(sfd.FileName, lines, System.Text.Encoding.UTF8);
            MessageBox.Show("CSV exported successfully.", "Export Done");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static DataGridViewTextBoxColumn col(string name, string header, int fw)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = fw };

        private static Button MakeButton(string text, Color back, Point loc, int w)
        {
            var b = new Button { Text = text, Location = loc, Width = w, Height = 34, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        /// <summary>Wraps a value in CSV quotes if it contains commas.</summary>
        private static string Q(string? s) => s != null && s.Contains(',') ? $"\"{s}\"" : (s ?? "");
    }
}
