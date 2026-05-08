using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using NixPackTrace.Core;
using NixPackTrace.Models;
using System.Collections.Generic;
using System.Linq;
using QRCoder;

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
            using var boxFont    = new Font("Arial", 14, FontStyle.Bold);   // ✅ Single font for full boxNo
            using var smallFont  = new Font("Arial", 6, FontStyle.Bold);
            using var regFont    = new Font("Arial", 7);

            // Modern Header (Inverted colors)
            g.FillRectangle(Brushes.Black, 0, 0, 157, 15);
            g.DrawString(AppState.Settings.ProductName, titleFont, Brushes.White, 2, 1);

            // Left side: Show COMPLETE box number in same font (e.g., "E26001")
            g.DrawString("BOX", smallFont, Brushes.Black, 4, 18);
            
            // ✅ Single DrawString for full boxNo - adjusted Y position for better vertical balance
            g.DrawString(boxNo, boxFont, Brushes.Black, 2, 28);

            // Date below box number (Y position adjusted upward)
            g.DrawString($"{DateTime.Now:dd-MMM HH:mm}", regFont, Brushes.Black, 4, 52);

            // Divider Line (adjusted end Y to match new content height)
            int rx = 80;
            g.DrawLine(Pens.Black, rx, 18, rx, 58);

            // Right side: QR Code
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(boxNo, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var qrCodeImage = qrCode.GetGraphic(2))
                    {
                        // Draw QR Code centered on the right side
                        g.DrawImage(qrCodeImage, new Rectangle(rx + 10, 18, 55, 55));
                    }
                }
            }
        }
    }
}