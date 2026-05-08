using System;
using System.Drawing;
using System.Windows.Forms;
using NixPackTrace.Core;
using NixPackTrace.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NixPackTrace.UI
{
    public class DispatchControl : UserControl
    {
        private readonly MainForm _host;
        private TextBox txtFromBox = null!;
        private TextBox txtToBox = null!;
        private DateTimePicker dtDispatchDate = null!;
        private TextBox txtRemarks = null!;
        private DataGridView grid = null!;
        private List<DispatchRecord> _data = new();

        public DispatchControl(MainForm host)
        {
            _host = host;
            InitializeComponent();
            this.Load += (_, __) => LoadDataAsync();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;

            // ── Form Area ──
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 160, BackColor = Color.FromArgb(240, 246, 252), Padding = new Padding(20) };

            var titleLabel = new Label { Text = "📦 Dispatch Boxes", Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(35, 48, 65), AutoSize = true, Location = new Point(20, 15) };

            var lblFrom = new Label { Text = "From Box No:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(20, 65) };
            txtFromBox = new TextBox { Location = new Point(120, 62), Width = 110, Font = new Font("Segoe UI", 10) };
            txtFromBox.PlaceholderText = "e.g. E26001";

            var lblTo = new Label { Text = "To Box No:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(250, 65) };
            txtToBox = new TextBox { Location = new Point(340, 62), Width = 110, Font = new Font("Segoe UI", 10) };
            txtToBox.PlaceholderText = "e.g. E26010";

            var lblDate = new Label { Text = "Dispatch Date:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(470, 65) };
            dtDispatchDate = new DateTimePicker { Location = new Point(570, 62), Width = 140, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10) };
            dtDispatchDate.Value = DateTime.Today;

            var lblRemarks = new Label { Text = "Remarks:", AutoSize = true, Font = new Font("Segoe UI", 10), Location = new Point(20, 115) };
            txtRemarks = new TextBox { Location = new Point(120, 112), Width = 560, Font = new Font("Segoe UI", 10) };

            // Format hint label
            var lblHint = new Label
            {
                Text = "Format: [MonthLetter][Year][Seq]  e.g. A26001 = Jan 2026, Box 1   |   E26001 = May 2026, Box 1",
                Location = new Point(20, 142),
                AutoSize = true,
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.DimGray
            };

            var btnSave = MakeButton("💾 Save Dispatch", Color.SeaGreen, new Point(720, 60), 160);
            btnSave.Height = 65;
            btnSave.Click += async (_, __) => await SaveDispatchAsync();

            topPanel.Controls.AddRange(new Control[] { titleLabel, lblFrom, txtFromBox, lblTo, txtToBox, lblDate, dtDispatchDate, lblRemarks, txtRemarks, lblHint, btnSave });

            // ── Grid Area ──
            var gridPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            // Delete button toolbar above the grid
            var toolBar = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.White, Padding = new Padding(5, 5, 5, 0) };
            var btnDelete = new Button
            {
                Text = "❌ Delete Selected Dispatch",
                Location = new Point(5, 5),
                Width = 210,
                Height = 34,
                BackColor = Color.Crimson,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDelete.FlatAppearance.BorderSize = 0;
            btnDelete.Click += BtnDeleteDispatch_Click;
            toolBar.Controls.Add(btnDelete);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                DefaultCellStyle = { Font = new Font("Consolas", 9) },
                ColumnHeadersDefaultCellStyle = { Font = new Font("Segoe UI", 9, FontStyle.Bold) }
            };

            grid.Columns.Add(col("colDispatchId", "Dispatch ID", 18));
            grid.Columns.Add(col("colDate", "Dispatch Date", 12));
            grid.Columns.Add(col("colFrom", "From Box", 10));
            grid.Columns.Add(col("colTo", "To Box", 10));
            grid.Columns.Add(col("colBy", "Dispatched By", 15));
            grid.Columns.Add(col("colSync", "Sync", 8));
            grid.Columns.Add(col("colRemarks", "Remarks", 20));

            gridPanel.Controls.Add(grid);
            gridPanel.Controls.Add(toolBar);

            this.Controls.Add(gridPanel);
            this.Controls.Add(topPanel);
        }

        // ── Box ID format validator: must match [A-L][0-9]{2}[0-9]{3+} OR pure numbers for old boxes
        private static readonly Regex _boxIdRegex = new Regex(@"^([A-La-l]\d{2}\d{3,}|\d+)$");

        private async System.Threading.Tasks.Task SaveDispatchAsync()
        {
            string from = txtFromBox.Text.Trim().ToUpper();
            string to   = txtToBox.Text.Trim().ToUpper();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                MessageBox.Show("Please enter both From and To Box Numbers.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_boxIdRegex.IsMatch(from))
            {
                MessageBox.Show($"Invalid From Box format: '{from}'\n\nExpected format: MonthLetter + YY + 3-digit sequence\nExample: E26001 (May 2026, Box 1)", "Invalid Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_boxIdRegex.IsMatch(to))
            {
                MessageBox.Show($"Invalid To Box format: '{to}'\n\nExpected format: MonthLetter + YY + 3-digit sequence\nExample: E26010 (May 2026, Box 10)", "Invalid Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (int.TryParse(from, out int fromNum) && int.TryParse(to, out int toNum))
            {
                if (fromNum > toNum)
                {
                    MessageBox.Show("'From Box No' cannot be greater than 'To Box No'.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                if (string.Compare(from, to) > 0 && from.Length == to.Length)
                {
                    MessageBox.Show("'From Box No' cannot be after 'To Box No' alphabetically.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            var record = new DispatchRecord
            {
                FromBoxNo    = from,
                ToBoxNo      = to,
                DispatchDate = dtDispatchDate.Value,
                DispatchedBy = AppState.CurrentUser,
                Remarks      = txtRemarks.Text.Trim()
            };

            bool saved = await _host.LocalDb.InsertDispatchAsync(record);
            if (saved)
            {
                int quantity = 0;
                if (int.TryParse(from, out int fNum) && int.TryParse(to, out int tNum))
                {
                    quantity = tNum - fNum + 1;
                }
                else if (from.Length > 3 && to.Length > 3)
                {
                    string fSeq = from.Substring(3);
                    string tSeq = to.Substring(3);
                    if (int.TryParse(fSeq, out int f) && int.TryParse(tSeq, out int t))
                        quantity = t - f + 1;
                }

                // Sync to Firebase
                bool synced = await _host.FirebaseService.UpdateDispatchAsync(record);
                if (synced)
                {
                    await _host.LocalDb.MarkDispatchAsSyncedAsync(record.DispatchId);
                    if (quantity > 0) _ = _host.FirebaseService.IncrementQuantityMetricAsync("Dispatched", quantity);
                }

                MessageBox.Show($"Dispatch record saved.\nBoxes {from} → {to} marked as dispatched.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtFromBox.Clear();
                txtToBox.Clear();
                txtRemarks.Clear();
                LoadDataAsync();
            }
            else
            {
                MessageBox.Show("Failed to save dispatch record (possibly a duplicate).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnDeleteDispatch_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a dispatch record to delete.", "No Selection");
                return;
            }

            string dispatchId = grid.SelectedRows[0].Cells["colDispatchId"].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(dispatchId)) return;

            string fromBox = grid.SelectedRows[0].Cells["colFrom"].Value?.ToString() ?? "";
            string toBox   = grid.SelectedRows[0].Cells["colTo"].Value?.ToString() ?? "";

            var result = MessageBox.Show(
                $"Are you sure you want to DELETE this dispatch record?\n\nBoxes: {fromBox} → {toBox}\n\nThis will also delete it from Firebase.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Cursor = Cursors.WaitCursor;
                await _host.LocalDb.DeleteDispatchAsync(dispatchId);
                await _host.FirebaseService.DeleteDispatchAsync(dispatchId);
                Cursor = Cursors.Default;

                MessageBox.Show("Dispatch record deleted successfully.", "Done");
                LoadDataAsync();
            }
        }

        private async void LoadDataAsync()
        {
            _data = await _host.LocalDb.GetRecentDispatchesAsync();
            grid.Rows.Clear();
            foreach (var r in _data)
            {
                grid.Rows.Add(
                    r.DispatchId,
                    r.DispatchDate.ToString("dd-MM-yyyy"),
                    r.FromBoxNo,
                    r.ToBoxNo,
                    r.DispatchedBy,
                    r.SYNC_STATUS,
                    r.Remarks
                );
            }
        }

        private static DataGridViewTextBoxColumn col(string name, string header, int fw)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = header, FillWeight = fw };

        private static Button MakeButton(string text, Color back, Point loc, int w)
        {
            var b = new Button { Text = text, Location = loc, Width = w, Height = 34, BackColor = back, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }
    }
}
