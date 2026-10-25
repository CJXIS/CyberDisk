using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;


namespace CyberDisk
{
    sealed partial class Ui
    {
    // ───────────────────────── Ui: icon buttons and report export (PDF / image / text) ─────────────────────────

        /// <summary>A line icon from <see cref="Vec"/> as a WPF element.</summary>
        static FrameworkElement IconShape(string data, Brush stroke, double size, double thickness)
        {
            var path = new System.Windows.Shapes.Path { Data = Vec.Geo(data), Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(path);
            return new Viewbox { Width = size, Height = size, Child = canvas };
        }

        /// <summary>Button content: a small icon followed by the label.</summary>
        static object IconLabel(string iconData, string text, string color = "#3DD6F5")
        {
            var icon = IconShape(iconData, Br(color), 15, 2); icon.Margin = new Thickness(0, 0, 7, 0);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(icon); sp.Children.Add(new TextBlock { Text = text });
            return sp;
        }

        // ── the EXPORT menu ──
        void ShowExportMenu(Button anchor, string title, Func<string> subtitle, FrameworkElement content, Func<string> plainText, string baseName)
        {
            var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            Action<string, string, Action> add = (icon, text, act) =>
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var ic = IconShape(icon, Br("#3DD6F5"), 15, 2); ic.Margin = new Thickness(0, 0, 10, 0);
                row.Children.Add(ic); row.Children.Add(new TextBlock { Text = Loc.T(text) });
                var mi = new MenuItem { Header = row }; mi.Click += (_, _) => act();
                menu.Items.Add(mi);
            };
            add(Vec.Export, "Save as PDF", () => ExportReport("pdf", title, subtitle(), content, plainText, baseName));
            add(Vec.Monitor, "Save as picture (PNG)", () => ExportReport("png", title, subtitle(), content, plainText, baseName));
            add(Vec.Apps, "Save as text file", () => ExportReport("txt", title, subtitle(), content, plainText, baseName));
            menu.Items.Add(new Separator());
            add(Vec.Copy, "Copy all text", () => { try { Clipboard.SetText(plainText()); txtStatus.Text = "COPIED TO THE CLIPBOARD"; } catch { txtStatus.Text = "COULD NOT COPY"; } });
            menu.IsOpen = true;
        }

        async void ExportReport(string kind, string title, string subtitle, FrameworkElement content, Func<string> plainText, string baseName)
        {
            try
            {
                if (content == null || content.ActualWidth < 10) { await Msg("NOTHING TO EXPORT", "The page is still loading.", "info"); return; }
                var dlg = new SaveFileDialog
                {
                    FileName = baseName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm"),
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), OverwritePrompt = true,
                    Filter = kind == "pdf" ? "PDF document (*.pdf)|*.pdf" : kind == "png" ? "PNG picture (*.png)|*.png" : "Text file (*.txt)|*.txt",
                    DefaultExt = "." + kind
                };
                if (dlg.ShowDialog(w) != true) return;
                string file = dlg.FileName;
                txtStatus.Text = "CREATING THE REPORT…";
                if (kind == "txt") File.WriteAllText(file, plainText(), new UTF8Encoding(true));
                else
                {
                    var bmp = RenderReport(Loc.T(title), subtitle, content);
                    if (kind == "png") { var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); using (var fs = File.Create(file)) enc.Save(fs); }
                    else WritePdf(file, bmp);
                }
                txtStatus.Text = "REPORT SAVED";
                if (await Confirm("REPORT SAVED", "The report was saved:\n" + Path.GetFileName(file), "SHOW FILE", "CLOSE", "info")) Process.Start("explorer.exe", "/select,\"" + file + "\"");
            }
            catch (Exception ex) { await Msg("COULD NOT SAVE THE REPORT", ex.Message, "danger"); }
        }

        /// <summary>Draws a branded report: header, a copy of the page content, footer. Returns a high-resolution bitmap.</summary>
        BitmapSource RenderReport(string title, string subtitle, FrameworkElement content)
        {
            double cw = content.ActualWidth, ch = content.ActualHeight, pad = 28;
            double width = cw + pad * 2;

            var root = new StackPanel { Width = width, Background = Br("#0A111F"), FlowDirection = FlowDirection.LeftToRight };

            // header
            var head = new Border { Padding = new Thickness(pad, 24, pad, 20), BorderBrush = Br("#21304A"), BorderThickness = new Thickness(0, 0, 0, 1) };
            head.Background = new LinearGradientBrush(Col("#12203A"), Col("#0E1830"), 0);
            var hg = new Grid(); hg.ColumnDefinitions.Add(new ColumnDefinition()); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            var brand = new StackPanel { Orientation = Orientation.Horizontal };
            var logo = IconShape(Vec.Drives, Br("#3DD6F5"), 20, 1.8); logo.Margin = new Thickness(0, 0, 8, 0);
            brand.Children.Add(logo); brand.Children.Add(new TextBlock { Text = "CYBERDISK", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Br("#3DD6F5"), VerticalAlignment = VerticalAlignment.Center });
            left.Children.Add(brand);
            left.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 0) });
            if (!string.IsNullOrWhiteSpace(subtitle)) left.Children.Add(new TextBlock { Text = subtitle, FontSize = 13, Foreground = Br("#8195B0"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
            hg.Children.Add(left);
            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock { Text = Environment.MachineName, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Br("#E6EEFA"), HorizontalAlignment = HorizontalAlignment.Right });
            right.Children.Add(new TextBlock { Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm"), FontSize = 12, Foreground = Br("#8195B0"), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(right, 1); hg.Children.Add(right);
            head.Child = hg; root.Children.Add(head);

            // content
            root.Children.Add(new System.Windows.Shapes.Rectangle { Width = cw, Height = ch, Margin = new Thickness(pad, 22, pad, 6), Fill = new VisualBrush(content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top } });

            // footer
            root.Children.Add(new TextBlock { Text = "Created by CyberDisk  ·  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), FontSize = 11, Foreground = Br("#5D7090"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 18) });

            root.Measure(new Size(width, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));
            root.UpdateLayout();

            double scale = 2; double h = root.DesiredSize.Height;
            while (scale > 1 && (h * scale > 15000 || width * scale > 15000)) scale -= 0.25;
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(root);
            rtb.Freeze();
            return rtb;
        }

        // ── a small PDF writer: every page is one JPEG image (no extra library needed) ──
        static void WritePdf(string file, BitmapSource bmp)
        {
            const double pageW = 595;                                              // A4 width in points; the page height follows the report, up to the PDF limit
            double scale = pageW / bmp.PixelWidth;
            int maxPx = (int)Math.Floor(14000 / scale);
            int pages = Math.Max(1, (int)Math.Ceiling((double)bmp.PixelHeight / maxPx));
            var jpgs = new List<byte[]>(); var sizes = new List<int[]>();
            for (int i = 0; i < pages; i++)
            {
                int y = i * maxPx, h = Math.Min(maxPx, bmp.PixelHeight - y);
                BitmapSource part = new CroppedBitmap(bmp, new Int32Rect(0, y, bmp.PixelWidth, h));
                part = new FormatConvertedBitmap(part, PixelFormats.Bgr24, null, 0);
                var enc = new JpegBitmapEncoder { QualityLevel = 92 }; enc.Frames.Add(BitmapFrame.Create(part));
                using (var ms = new MemoryStream()) { enc.Save(ms); jpgs.Add(ms.ToArray()); }
                sizes.Add(new[] { bmp.PixelWidth, h });
            }

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var o = new MemoryStream(); var offsets = new List<long>();
            Action<string> W = t => { var b = Encoding.ASCII.GetBytes(t); o.Write(b, 0, b.Length); };
            Action<int> begin = id => { while (offsets.Count < id) offsets.Add(0); offsets[id - 1] = o.Position; W(id + " 0 obj\n"); };

            W("%PDF-1.4\n");
            begin(1); W("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            var kids = new StringBuilder();
            for (int i = 0; i < pages; i++) kids.Append((3 + i * 3) + " 0 R ");
            begin(2); W("<< /Type /Pages /Kids [" + kids + "] /Count " + pages + " >>\nendobj\n");
            for (int i = 0; i < pages; i++)
            {
                int pg = 3 + i * 3, ct = pg + 1, im = pg + 2;
                double hPt = sizes[i][1] * scale;
                string H = hPt.ToString("0.##", inv);
                begin(pg); W("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 " + H + "] /Resources << /XObject << /Im0 " + im + " 0 R >> >> /Contents " + ct + " 0 R >>\nendobj\n");
                string stream = "q 595 0 0 " + H + " 0 0 cm /Im0 Do Q";
                begin(ct); W("<< /Length " + stream.Length + " >>\nstream\n" + stream + "\nendstream\nendobj\n");
                begin(im); W("<< /Type /XObject /Subtype /Image /Width " + sizes[i][0] + " /Height " + sizes[i][1] + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpgs[i].Length + " >>\nstream\n");
                o.Write(jpgs[i], 0, jpgs[i].Length); W("\nendstream\nendobj\n");
            }
            long xref = o.Position; int count = offsets.Count + 1;
            W("xref\n0 " + count + "\n0000000000 65535 f \n");
            foreach (var off in offsets) W(off.ToString("0000000000", inv) + " 00000 n \n");
            W("trailer\n<< /Size " + count + " /Root 1 0 R /Info << /Title (CyberDisk report) /Producer (CyberDisk) >> >>\nstartxref\n" + xref + "\n%%EOF\n");
            File.WriteAllBytes(file, o.ToArray());
        }
    }
}
