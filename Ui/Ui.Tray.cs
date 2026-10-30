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
        // ── tray icon ──
        System.Windows.Forms.NotifyIcon tray; bool exiting, trayOnClose = true, trayHinted;

        sealed class DarkColors : System.Windows.Forms.ProfessionalColorTable
        {
            static System.Drawing.Color C(int r, int g, int b) { return System.Drawing.Color.FromArgb(r, g, b); }
            public override System.Drawing.Color ToolStripDropDownBackground { get { return C(16, 26, 43); } }
            public override System.Drawing.Color ImageMarginGradientBegin { get { return C(16, 26, 43); } }
            public override System.Drawing.Color ImageMarginGradientMiddle { get { return C(16, 26, 43); } }
            public override System.Drawing.Color ImageMarginGradientEnd { get { return C(16, 26, 43); } }
            public override System.Drawing.Color MenuBorder { get { return C(42, 61, 94); } }
            public override System.Drawing.Color MenuItemBorder { get { return C(61, 214, 245); } }
            public override System.Drawing.Color MenuItemSelected { get { return C(30, 56, 84); } }
            public override System.Drawing.Color MenuItemSelectedGradientBegin { get { return C(30, 56, 84); } }
            public override System.Drawing.Color MenuItemSelectedGradientEnd { get { return C(30, 56, 84); } }
            public override System.Drawing.Color CheckBackground { get { return C(28, 74, 92); } }
            public override System.Drawing.Color CheckSelectedBackground { get { return C(28, 74, 92); } }
            public override System.Drawing.Color CheckPressedBackground { get { return C(28, 74, 92); } }
            public override System.Drawing.Color MenuItemPressedGradientBegin { get { return C(30, 56, 84); } }
            public override System.Drawing.Color MenuItemPressedGradientEnd { get { return C(30, 56, 84); } }
            public override System.Drawing.Color SeparatorDark { get { return C(42, 61, 94); } }
            public override System.Drawing.Color SeparatorLight { get { return C(42, 61, 94); } }
        }

        sealed class DarkRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { }
            protected override void OnRenderItemText(System.Windows.Forms.ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = System.Drawing.Color.FromArgb(230, 238, 250);
                base.OnRenderItemText(e);
            }
        }

        void InitTray()
        {
            try
            {
                trayOnClose = Settings.TrayOnClose;
                tray = new System.Windows.Forms.NotifyIcon { Text = "CyberDisk", Visible = true };
                tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var menu = new System.Windows.Forms.ContextMenuStrip { Renderer = new DarkRenderer(), ShowImageMargin = true, ShowCheckMargin = false, ImageScalingSize = new System.Drawing.Size(18, 18), Font = new System.Drawing.Font("Segoe UI", 9.5f) };
                trayMenu = menu; FillTray();
                tray.ContextMenuStrip = menu;
                tray.MouseDoubleClick += (s, e) => ShowFromTray();
                w.Closing += (s, e) => { if (!exiting && trayOnClose && tray != null) { e.Cancel = true; HideToTray(); } };
                // a second launch asks this instance to show its window instead of opening a copy
                var evt = new EventWaitHandle(false, EventResetMode.AutoReset, "CyberDisk.ShowWindow");
                var th = new Thread(() => { while (true) { evt.WaitOne(); w.Dispatcher.BeginInvoke(new Action(ShowFromTray)); } }) { IsBackground = true };
                th.Start();
            }
            catch { tray = null; }
        }

        void HideToTray()
        {
            w.Hide();
            if (!trayHinted && tray != null)
            {
                trayHinted = true;
                tray.ShowBalloonTip(2500, Loc.T("CyberDisk is still running"), Loc.T("Double-click the tray icon to open it again."), System.Windows.Forms.ToolTipIcon.Info);
            }
        }

        void ShowFromTray()
        {
            w.Show();
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Activate(); w.Topmost = true; w.Topmost = false; w.Focus();
        }

        void ExitApp()
        {
            exiting = true;
            foreach (var d in scanning.ToList()) { if (d.Cts != null) d.Cts.Cancel(); }
            try { if (tray != null) { tray.Visible = false; tray.Dispose(); } } catch { }
            Application.Current.Shutdown();
        }

        async void TrayBoost()
        {
            long t0, a0; Mem.Status(out t0, out a0);
            var r = await Task.Run(() => { int c; long f = Mem.TrimAll(out c); return Tuple.Create(f, c); });
            long t1, a1; Mem.Status(out t1, out a1);
            long freed = Math.Max(r.Item1, a1 - a0);
            if (tray != null) tray.ShowBalloonTip(2500, Loc.T("Boost complete"), Loc.T(freed > 0 ? "Freed about " + Fmt(freed) + " of memory." : "Memory was already optimized."), System.Windows.Forms.ToolTipIcon.Info);
        }

        void WireMisc()
        {
            bSelSafe = F<Button>("bSelSafe"); bSelNone = F<Button>("bSelNone"); bSelAll = F<Button>("bSelAll"); txtSelCount = F<TextBlock>("txtSelCount");
            bSelSafe.Click += (_, _) => SelectSafe();
            bSelNone.Click += (_, _) => SelectNone();
            bSelAll.Click += (_, _) => SelectAll();
            InitTray();
        }

        // ───────── tray menu (rebuilt when the language changes) ─────────
        System.Windows.Forms.ToolStripMenuItem trayChk;

        /// <summary>A vector icon as a bitmap for the tray menu (drawn at twice the size so it stays sharp).</summary>
        static System.Drawing.Image MenuIcon(string vecPath, string hex)
        {
            const int px = 36;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(px / 24.0, px / 24.0));
                dc.DrawGeometry(null, new Pen(Br(hex), 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Vec.Geo(vecPath));
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var ms = new MemoryStream())
            {
                enc.Save(ms); ms.Position = 0;
                using (var tmp = new System.Drawing.Bitmap(ms)) return new System.Drawing.Bitmap(tmp);
            }
        }
        void FillTray()
        {
            if (trayMenu == null) return;
            trayMenu.Items.Clear();
            trayMenu.RightToLeft = Loc.Ar ? System.Windows.Forms.RightToLeft.Yes : System.Windows.Forms.RightToLeft.No;
            Func<string, string, string, EventHandler, System.Windows.Forms.ToolStripMenuItem> item = (text, icon, color, click) =>
            {
                var it = new System.Windows.Forms.ToolStripMenuItem(Loc.T(text), MenuIcon(icon, color), click) { Padding = new System.Windows.Forms.Padding(2, 5, 2, 5) };
                trayMenu.Items.Add(it);
                return it;
            };
            var open = item("Open CyberDisk", Vec.Drives, "#3DD6F5", (s, e) => ShowFromTray());
            open.Font = new System.Drawing.Font(trayMenu.Font, System.Drawing.FontStyle.Bold);
            item("Boost memory now", Vec.Boost, "#3DD6F5", (s, e) => TrayBoost());
            item("Cleanup", Vec.Clean, "#3DD6F5", (s, e) => { ShowFromTray(); Go(vClean); EnterClean(); });
            item("Health check", Vec.Health, "#3DD6F5", (s, e) => { ShowFromTray(); Go(vHealth); });
            item("My PC", Vec.Cpu, "#3DD6F5", (s, e) => { ShowFromTray(); Go(vPc); EnterPc(); });
            item("App errors", Vec.Errors, "#3DD6F5", (s, e) => { ShowFromTray(); Go(vErrors); EnterErrors(); });
            item("Settings", Vec.Settings, "#3DD6F5", (s, e) => { ShowFromTray(); Go(vSettings); SyncSettings(); });
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayChk = item("Close button hides to the tray", trayOnClose ? Vec.Check : Vec.Close, trayOnClose ? "#3DDC97" : "#8195B0", (s, e) => SetTrayOnClose(!trayOnClose));
            item("Exit", Vec.Power, "#FF6B8A", (s, e) => ExitApp());
        }
    }
}
