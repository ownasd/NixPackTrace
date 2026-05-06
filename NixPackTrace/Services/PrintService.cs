using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using NixPackTrace.Core;
using NixPackTrace.Models;
using System.Collections.Generic;
using System.Linq;

namespace NixPackTrace.Services
{
    public class PrintService
    {
        /// <summary>
        /// Prints or previews a completed-box label.
        /// </summary>
        public void PrintBoxLabel(string boxNo, int itemCount, List<PackingRecord>? items, bool showPreview)
        {
            var pd = new PrintDocument();

            if (!string.IsNullOrEmpty(AppState.Settings.PrinterName))
                pd.PrinterSettings.PrinterName = AppState.Settings.PrinterName;

            // 40mm = 1.57 inches -> 157 hundredths of an inch
            // 20mm = 0.79 inches -> 79 hundredths of an inch
            var paperSize = new PaperSize("Godex40x20", 157, 79);
            pd.DefaultPageSettings.PaperSize = paperSize;
            pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            pd.PrinterSettings.DefaultPageSettings.PaperSize = paperSize;

            pd.PrintPage += (_, e) => DrawLabel(e.Graphics!, boxNo, itemCount, items);

            if (showPreview)
            {
                using var dlg = new PrintPreviewDialog { Document = pd, Width = 500, Height = 400 };
                dlg.ShowDialog();
            }
            else
            {
                bool success = false;
                while (!success)
                {
                    try
                    {
                        // Check if printer is valid
                        if (!pd.PrinterSettings.IsValid)
                        {
                            throw new Exception("Printer is offline or not found.");
                        }
                        
                        pd.Print();
                        success = true;
                    }
                    catch (Exception)
                    {
                        using var errForm = new UI.PrinterErrorForm();
                        if (errForm.ShowDialog() != DialogResult.Retry)
                        {
                            break; // User canceled
                        }
                    }
                }
            }
        }

        private static void DrawLabel(Graphics g, string boxNo, int itemCount, List<PackingRecord>? items)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit; // Better for thermal printers

            using var titleFont  = new Font("Arial", 8, FontStyle.Bold);
            using var prefixFont = new Font("Arial", 11, FontStyle.Bold);   // e.g. "E26"
            using var seqFont    = new Font("Arial", 24, FontStyle.Bold);   // e.g. "001"
            using var smallFont  = new Font("Arial", 6, FontStyle.Bold);
            using var regFont    = new Font("Arial", 7);

            // Modern Header (Inverted colors)
            g.FillRectangle(Brushes.Black, 0, 0, 157, 15);
            g.DrawString(AppState.Settings.ProductName, titleFont, Brushes.White, 2, 1);

            // Left side: Split Box Number into prefix + sequence
            // boxNo format: "E26001"  =>  prefix = "E26",  seq = "001"
            string prefix = boxNo.Length >= 3 ? boxNo.Substring(0, 3) : boxNo;
            string seq    = boxNo.Length >  3 ? boxNo.Substring(3)    : "";

            g.DrawString("BOX", smallFont, Brushes.Black, 4, 18);
            g.DrawString(prefix, prefixFont, Brushes.Black, 2, 26);   // "E26" – medium, line 1
            g.DrawString(seq,    seqFont,    Brushes.Black, 2, 42);   // "001" – large,  line 2

            // Divider Line
            int rx = 68;
            g.DrawLine(Pens.Black, rx - 3, 18, rx - 3, 75);

            // Right side: Details
            g.DrawString($"Qty : {itemCount}/{AppState.Settings.BoxSize}", titleFont, Brushes.Black, rx, 20);

            string op = AppState.CurrentUser.Length > 10 ? AppState.CurrentUser.Substring(0, 10) : AppState.CurrentUser;
            g.DrawString($"By  : {op}", regFont, Brushes.Black, rx, 35);

            g.DrawString($"{DateTime.Now:dd-MMM HH:mm}", regFont, Brushes.Black, rx, 50);
        }
    }
}
