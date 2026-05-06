using System;
using System.Drawing;
using System.Windows.Forms;

namespace NixPackTrace.UI
{
    public class PrinterErrorForm : Form
    {
        private Button btnPrint;
        private Button btnCancel;

        public PrinterErrorForm()
        {
            this.Text = "Printer Error";
            this.Size = new Size(400, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;

            Label lblError = new Label 
            { 
                Text = "⚠ Printer is offline or not connected.", 
                Font = new Font("Segoe UI", 12, FontStyle.Bold), 
                ForeColor = Color.Crimson,
                AutoSize = true, 
                Location = new Point(30, 30) 
            };

            Label lblInstruction = new Label 
            { 
                Text = "Please connect the printer and press the Print button below.", 
                Font = new Font("Segoe UI", 10), 
                AutoSize = true, 
                Location = new Point(30, 70),
                MaximumSize = new Size(320, 0)
            };
            
            btnPrint = new Button { Text = "Print", Location = new Point(100, 130), Width = 100, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Click += (s, e) => { this.DialogResult = DialogResult.Retry; this.Close(); };

            btnCancel = new Button { Text = "Cancel", Location = new Point(220, 130), Width = 80, Height = 35, BackColor = Color.LightGray, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9) };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.Add(lblError);
            this.Controls.Add(lblInstruction);
            this.Controls.Add(btnPrint);
            this.Controls.Add(btnCancel);
        }
    }
}
