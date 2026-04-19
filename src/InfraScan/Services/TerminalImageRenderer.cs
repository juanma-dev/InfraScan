using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.Versioning;

namespace InfraScan.Services
{
    [SupportedOSPlatform("windows")]
    public static class TerminalImageRenderer
    {
        /// <summary>
        /// Renders terminal text output as a professional-looking terminal screenshot image.
        /// Dark background with monospaced text, mimicking a real Linux terminal.
        /// </summary>
        public static byte[] RenderTerminalOutput(string text, string title = "")
        {
            if (string.IsNullOrWhiteSpace(text))
                text = "(sin salida)";

            var lines = text.Split('\n');
            int maxLineLength = 0;
            foreach (var line in lines)
                if (line.Length > maxLineLength) maxLineLength = line.Length;

            // Size calculation
            float fontSize = 11f;
            int charWidth = 7;
            int lineHeight = 16;
            int padding = 20;
            int titleBarHeight = title.Length > 0 ? 32 : 0;

            int width = Math.Max(maxLineLength * charWidth + padding * 2, 700);
            int height = lines.Length * lineHeight + padding * 2 + titleBarHeight + 10;

            // Cap size
            width = Math.Min(width, 1200);
            height = Math.Min(height, 1800);

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Background - dark terminal style
            var bgColor = Color.FromArgb(255, 30, 30, 30);
            g.Clear(bgColor);

            // Title bar if provided
            if (titleBarHeight > 0)
            {
                var titleBg = Color.FromArgb(255, 50, 50, 50);
                using var titleBrush = new SolidBrush(titleBg);
                g.FillRectangle(titleBrush, 0, 0, width, titleBarHeight);

                // Terminal dots
                int dotY = titleBarHeight / 2;
                g.FillEllipse(Brushes.IndianRed, 12, dotY - 5, 10, 10);
                g.FillEllipse(Brushes.Gold, 28, dotY - 5, 10, 10);
                g.FillEllipse(Brushes.LimeGreen, 44, dotY - 5, 10, 10);

                // Title text
                using var titleFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                using var titleTextBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
                var titleSize = g.MeasureString(title, titleFont);
                g.DrawString(title, titleFont, titleTextBrush,
                    (width - titleSize.Width) / 2, (titleBarHeight - titleSize.Height) / 2);
            }

            // Border
            using var borderPen = new Pen(Color.FromArgb(60, 60, 60), 1);
            g.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);

            // Terminal text
            using var font = new Font("Consolas", fontSize, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(220, 220, 220));
            using var greenBrush = new SolidBrush(Color.FromArgb(100, 255, 100));
            using var redBrush = new SolidBrush(Color.FromArgb(255, 120, 120));
            using var yellowBrush = new SolidBrush(Color.FromArgb(255, 220, 100));

            float y = titleBarHeight + padding;
            foreach (var line in lines)
            {
                if (y + lineHeight > height - padding) break;

                // Color code basic lines
                Brush brush = textBrush;
                if (line.TrimStart().StartsWith("[root@"))
                    brush = greenBrush;
                else if (line.Contains("ERROR") || line.Contains("FAIL") || line.Contains("error"))
                    brush = redBrush;
                else if (line.Contains("WARNING") || line.Contains("warn"))
                    brush = yellowBrush;

                g.DrawString(line, font, brush, padding, y);
                y += lineHeight;
            }

            // Convert to PNG bytes
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}
