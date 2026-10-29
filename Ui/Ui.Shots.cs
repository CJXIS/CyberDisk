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
    // ───────────────────────── Ui: screenshots (F12 = this page, SETTINGS > SCREENSHOT TOUR = every page) ─────────────────────────
        Button bShots; bool shooting;

        static string ShotDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CyberDisk Screenshots"); } }

        void WireShots()
        {
            bShots = F<Button>("bShots");
            bShots.Click += (_, _) => ScreenshotTour();
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.F12 || shooting) return;
                e.Handled = true;
                try { string f = SaveShot("page"); txtStatus.Text = "SCREENSHOT SAVED · " + Path.GetFileName(f).ToUpperInvariant(); } catch { txtStatus.Text = "COULD NOT SAVE THE SCREENSHOT"; }
            };
        }

        /// <summary>The whole window as a picture (twice the screen resolution for sharp text).</summary>
        BitmapSource CaptureWindow()
        {
            var root = w.Content as FrameworkElement;
            if (root == null) return null;
            w.UpdateLayout();
            double cw = root.ActualWidth, ch = root.ActualHeight, scale = 2;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Br("#070B14"), null, new Rect(0, 0, cw, ch));
                dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, cw, ch));
            }
            var rtb = new RenderTargetBitmap((int)Math.Ceiling(cw * scale), (int)Math.Ceiling(ch * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze();
            return rtb;
        }

        string SaveShot(string name)
        {
            Directory.CreateDirectory(ShotDir);
            string file = Path.Combine(ShotDir, name + ".png");
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(CaptureWindow()));
            using (var fs = File.Create(file)) enc.Save(fs);
            return file;
        }

        async Task WaitFor(Func<bool> ready, int maxMs)
        {
            for (int t = 0; t < maxMs && !ready(); t += 200) await Task.Delay(200);
        }

        async void ScreenshotTour()
        {
            if (shooting) return;
            if (!(await Confirm("SCREENSHOT TOUR", "CyberDisk opens each page one after another and saves a picture of it to:\n" + ShotDir + "\n\nThe pictures can show your PC name, drive letters and file names - look at them before you publish them. Please do not touch the window while it works (about one minute).", "START", "CANCEL", "question"))) return;
            shooting = true; int saved = 0;
            try
            {
                Action<string> click = n => { var b = F<Button>(n); if (b != null) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
                Func<string, Task> shot = async name => { await Task.Delay(350); try { SaveShot(name); saved++; } catch { } };

                click("navDrives"); await Task.Delay(1500); await shot("01-drives");
                click("navApps"); await WaitFor(() => ga.Items.Count > 0, 15000); await Task.Delay(900); await shot("02-apps");
                click("navClean"); await Task.Delay(1500); await shot("03-clean");
                click("navBoost"); await Task.Delay(3000); await shot("04-boost");
                click("navHealth");
                if (healthAll.Count == 0) DoHealth();
                await Task.Delay(500); await WaitFor(() => !checkingHealth && healthAll.Count > 0, 120000); await Task.Delay(900); await shot("05-health");
                click("navErrors"); await Task.Delay(2500); await shot("06-app-errors");
                click("navPc"); await WaitFor(() => hw != null, 90000); await Task.Delay(3500); await shot("07-my-pc");
                ShowDisks(null); await Task.Delay(3000); await shot("08-disk-health");
                click("navSettings"); await Task.Delay(1200); await shot("09-settings");
                click("navDrives");
            }
            catch (Exception ex) { await Msg("SCREENSHOT TOUR STOPPED", ex.Message, "warn"); }
            finally { shooting = false; }
            if (saved > 0 && await Confirm("SCREENSHOTS SAVED", saved + " picture(s) were saved to:\n" + ShotDir + "\n\nCheck them, then copy the ones you want into docs\\screenshots in your GitHub folder.", "SHOW FOLDER", "CLOSE", "info"))
                try { Process.Start("explorer.exe", "\"" + ShotDir + "\""); } catch { }
        }
    }
}
