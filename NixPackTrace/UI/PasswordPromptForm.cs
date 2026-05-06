using System;
using System.Drawing;
using System.Windows.Forms;
using NixPackTrace.Core;

namespace NixPackTrace.UI
{
    public class PasswordPromptForm : Form
    {
        private TextBox txtPassword;
        private Button btnOk;
        private Button btnCancel;
        public bool IsAuthenticated { get; private set; } = false;

        public PasswordPromptForm(string promptMessage = "Enter Admin Password:")
        {
            this.Text = "Authentication Required";
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            Label lblTitle = new Label { Text = promptMessage, Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(40, 30) };
            
            txtPassword = new TextBox { Location = new Point(40, 60), Width = 250, Font = new Font("Segoe UI", 10), UseSystemPasswordChar = true };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Verify(); };

            btnOk = new Button { Text = "Submit", Location = new Point(110, 110), Width = 80, Height = 30, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => Verify();

            btnCancel = new Button { Text = "Cancel", Location = new Point(210, 110), Width = 80, Height = 30, BackColor = Color.LightGray, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lblTitle);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void Verify()
        {
            if (txtPassword.Text == AppState.Settings.AdminPassword)
            {
                IsAuthenticated = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Incorrect Admin Password.", "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
    }
}
