using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NixPackTrace.Data;
using NixPackTrace.Models;

namespace NixPackTrace.UI
{
    public class ManageBoxesForm : Form
    {
        private readonly MainForm _host;
        private DataGridView grid = null!;
        private TextBox txtSearch = null!;

        public ManageBoxesForm(MainForm host)
        {
            _host = host;
            InitializeComponent();
            this.Load += async (_, __) => await LoadDataAsync("");
        }

        private void InitializeComponent()
        {
            this.Text = "Manage Old Boxes";
            this.Size = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(240, 246, 252) };
            
            topPanel.Controls.Add(new Label { Text = "Search MAC or Box No:", Location = new Point(15, 25), AutoSize = true, Font = new Font("Segoe UI", 10) });
            
            txtSearch = new TextBox { Location = new Point(180, 22), Width = 180, Font = new Font("Segoe UI", 11) };
            txtSearch.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadDataAsync(txtSearch.Text.Trim()); } };
            topPanel.Controls.Add(txtSearch);

            var btnSearch = new Button { Text = "Search", Location = new Point(370, 20), Width = 90, Height = 32, BackColor = Color.SteelBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnSearch.Click += async (_, __) => await LoadDataAsync(txtSearch.Text.Trim());
            topPanel.Controls.Add(btnSearch);

            var btnDelete = new Button { Text = "❌ Delete Selected", Location = new Point(500, 20), Width = 150, Height = 32, BackColor = Color.Crimson, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnDelete.Click += BtnDelete_Click;
            topPanel.Controls.Add(btnDelete);

            var btnEdit = new Button { Text = "✏ Edit Box No", Location = new Point(660, 20), Width = 150, Height = 32, BackColor = Color.DarkOrange, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnEdit.Click += BtnEdit_Click;
            topPanel.Controls.Add(btnEdit);

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

            this.Controls.Add(grid);
            this.Controls.Add(topPanel);
        }

        private async Task LoadDataAsync(string term)
        {
            var records = await _host.LocalDb.SearchRecordsAsync(term);
            if (string.IsNullOrEmpty(term)) 
                records = await _host.LocalDb.GetRecordsByDateRangeAsync(DateTime.Today.AddDays(-30), DateTime.Today); // show last 30 days if no search

            grid.DataSource = null;
            grid.DataSource = records;
            
            var idCol = grid.Columns["ID"];
            if (idCol != null) idCol.Visible = false;
        }

        private async void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;

            var record = grid.SelectedRows[0].DataBoundItem as PackingRecord;
            if (record == null) return;

            var res = MessageBox.Show($"Are you sure you want to delete MAC: {record.MAC_ID} from Box {record.BOX_NO}?\n\nThis will also delete the record from Firebase.", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            
            if (res == DialogResult.Yes)
            {
                Cursor = Cursors.WaitCursor;
                await _host.LocalDb.DeleteRecordAsync(record.MAC_ID);
                await _host.FirebaseService.DeletePackingAsync(record.SHORT_QR);
                Cursor = Cursors.Default;
                
                MessageBox.Show("Deleted successfully.", "Done");
                await LoadDataAsync(txtSearch.Text.Trim());
            }
        }

        private async void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;
            var record = grid.SelectedRows[0].DataBoundItem as PackingRecord;
            if (record == null) return;

            using var inputForm = new Form
            {
                Text = "Edit Box No", StartPosition = FormStartPosition.CenterParent, Size = new Size(300, 160), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
            };
            var lbl = new Label { Text = "New Box No:", Location = new Point(20, 20), AutoSize = true };
            var txt = new TextBox { Text = record.BOX_NO.ToString(), Location = new Point(20, 45), Width = 240 };
            var btnOk = new Button { Text = "Save", DialogResult = DialogResult.OK, Location = new Point(160, 80), Width = 100 };
            inputForm.Controls.AddRange(new Control[] { lbl, txt, btnOk });
            inputForm.AcceptButton = btnOk;

            if (inputForm.ShowDialog(this) == DialogResult.OK)
            {
                string newBoxNo = txt.Text.Trim().ToUpper();
                if (!string.IsNullOrEmpty(newBoxNo) && newBoxNo != record.BOX_NO)
                {
                    record.BOX_NO = newBoxNo;
                    record.SYNC_STATUS = "Pending";

                    Cursor = Cursors.WaitCursor;
                    await _host.LocalDb.UpdateRecordAsync(record);
                    
                    bool synced = await _host.FirebaseService.UpdatePackingAsync(record);
                    if (synced)
                    {
                        await _host.LocalDb.MarkAsSyncedAsync(record.MAC_ID);
                    }
                    Cursor = Cursors.Default;
                    
                    MessageBox.Show("Updated successfully.", "Done");
                    await LoadDataAsync(txtSearch.Text.Trim());
                }
            }
        }
    }
}
