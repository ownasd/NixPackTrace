using System;
using System.Drawing;
using System.Windows.Forms;
using NixPackTrace.Core;

namespace NixPackTrace.UI
{
    public class LoginForm : Form
    {
        private TextBox txtUsername = null!;
        private TextBox txtPassword = null!;
        private Button btnLogin = null!;
        private LinkLabel lnkCreateAccount = null!;

        public LoginForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "NixPackTrace - Login";
            this.Size = new Size(400, 320);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            Label lblTitle = new Label { Text = "Welcome to NixPackTrace", Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true, Location = new Point(50, 30) };
            
            Label lblUser = new Label { Text = "Operator Name:", Location = new Point(50, 90), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtUsername = new TextBox { Location = new Point(170, 88), Width = 150, Font = new Font("Segoe UI", 10) };
            
            Label lblPass = new Label { Text = "Password:", Location = new Point(50, 130), AutoSize = true, Font = new Font("Segoe UI", 10) };
            txtPassword = new TextBox { Location = new Point(170, 128), Width = 150, Font = new Font("Segoe UI", 10), UseSystemPasswordChar = true };
            txtPassword.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) await PerformLogin(); };

            btnLogin = new Button { Text = "Login", Location = new Point(170, 180), Width = 150, Height = 35, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += async (s, e) => await PerformLogin();

            lnkCreateAccount = new LinkLabel { Text = "Create Account", Location = new Point(190, 230), AutoSize = true, Font = new Font("Segoe UI", 9) };
            lnkCreateAccount.LinkClicked += async (s, e) => await PerformCreateAccount();

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblUser);
            this.Controls.Add(txtUsername);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnLogin);
            this.Controls.Add(lnkCreateAccount);
            
            this.AcceptButton = btnLogin;
        }

        private async System.Threading.Tasks.Task PerformLogin()
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please enter Operator Name and Password.", "Login");
                return;
            }

            // Using local db service from a dummy host sinceLoginForm doesn't have it directly. 
            // We can just construct a LocalDbService for auth check.
            var db = new Data.LocalDbService();
            
            // If no users exist, allow login? No, require creation first.
            if (!await db.HasAnyUserAsync())
            {
                MessageBox.Show("No accounts found. Please Create an Account first.", "Login");
                return;
            }

            bool valid = await db.ValidateUserAsync(txtUsername.Text.Trim(), txtPassword.Text);
            if (!valid)
            {
                MessageBox.Show("Invalid Username or Password.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            AppState.CurrentUser = txtUsername.Text.Trim();
            AppState.LoadSettings();

            MainForm mainForm = new MainForm();
            mainForm.FormClosed += (s, e) => this.Close();
            mainForm.Show();
            this.Hide();
        }

        private async System.Threading.Tasks.Task PerformCreateAccount()
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Please enter a new Operator Name and Password to create an account.", "Create Account");
                return;
            }

            AppState.LoadSettings(); // ensure we have admin password loaded
            
            var prompt = new PasswordPromptForm("Enter Admin Password to Create Account:");
            if (prompt.ShowDialog() != DialogResult.OK)
                return; // Admin password check failed or cancelled

            var db = new Data.LocalDbService();
            bool created = await db.CreateUserAsync(txtUsername.Text.Trim(), txtPassword.Text);
            if (created)
            {
                MessageBox.Show("Account created successfully. You can now login.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to create account. The username might already exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
