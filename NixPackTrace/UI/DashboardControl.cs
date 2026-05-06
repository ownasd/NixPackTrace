using System;
using System.Drawing;
using System.Windows.Forms;

namespace NixPackTrace.UI
{
    public class DashboardControl : UserControl
    {
        private MainForm _parent;
        private Label lblTotalPacked = null!;

        public DashboardControl(MainForm parent)
        {
            _parent = parent;
            InitializeComponent();
            this.Load += (s, e) => LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            int total = await _parent.LocalDb.GetTodayCountAsync();
            lblTotalPacked.Text = total.ToString();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.White;
            
            Label lblTitle = new Label { Text = "Dashboard", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, Location = new Point(20, 20) };
            
            Panel card = new Panel { Location = new Point(20, 80), Size = new Size(250, 120), BackColor = Color.FromArgb(0, 120, 215) };
            Label lblCardTitle = new Label { Text = "Total Packed Today", ForeColor = Color.White, Font = new Font("Segoe UI", 12), AutoSize = true, Location = new Point(15, 15) };
            lblTotalPacked = new Label { Text = "0", ForeColor = Color.White, Font = new Font("Segoe UI", 36, FontStyle.Bold), AutoSize = true, Location = new Point(15, 40) };
            
            card.Controls.Add(lblCardTitle);
            card.Controls.Add(lblTotalPacked);

            this.Controls.Add(lblTitle);
            this.Controls.Add(card);
        }
    }
}
