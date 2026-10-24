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
        // ───────── app errors ─────────
        void EnterErrors()
        {
            newErrors = 0; UpdateBadge();
            if (errApps == null) ScanErrors();
            RefreshHung();
            hungTm.Start();
        }

        void UpdateBadge()
        {
            if (errBadge == null) return;
            errBadge.Visibility = newErrors > 0 ? Visibility.Visible : Visibility.Collapsed;
            errBadgeText.Text = newErrors > 99 ? "99+" : newErrors.ToString();
        }

        async void ScanErrors()
        {
            if (errScanning) return;
            errScanning = true; bErrScan.IsEnabled = false;
            foreach (var kv in new[] { Tuple.Create(bErr1, 1), Tuple.Create(bErr7, 7), Tuple.Create(bErr30, 30) })
                kv.Item1.Background = Br(kv.Item2 == errDays ? "#333DD6F5" : "#111E31");
            txtErrHead.Text = "Scanning the Windows event log…";
            int total = 0; List<ErrApp> res;
            try
            {
                int days = errDays;
                var r = await Task.Run(() => { int t; var l = AppErr.Scan(days, out t); return Tuple.Create(l, t); });
                res = r.Item1; total = r.Item2;
            }
            catch { res = new List<ErrApp>(); }
            errApps = res;
            itemsErrors.ItemsSource = null; itemsErrors.ItemsSource = res;
            txtErrEmpty.Visibility = res.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RenderErrorSummary(res);
            txtErrHead.Text = res.Count == 0 ? "No problems found ✦" : total + " event(s) from " + res.Count + " app(s)";
            errScanning = false; bErrScan.IsEnabled = true;
            txtStatus.Text = res.Count == 0 ? "NO APP ERRORS FOUND" : "APP ERRORS · " + res.Count + " APP(S) AFFECTED";
        }

        /// <summary>Stability ring, key numbers and the activity chart at the top of the App Errors page.</summary>
        void RenderErrorSummary(List<ErrApp> res)
        {
            int score = AppErr.Stability(res, errDays);
            ScoreTheme.StyleRing(errArc, score, score);
            txtErrScore.Text = score.ToString();
            txtErrGrade.Text = ScoreTheme.Grade(score); txtErrGrade.Foreground = ScoreTheme.Solid(score);
            txtErrVerdict.Text = AppErr.StabilityVerdict(score); txtErrVerdict.Foreground = ScoreTheme.Solid(score);
            int crashes = res.Sum(a => a.Crashes), hangs = res.Sum(a => a.Hangs);
            string period = errDays == 1 ? "last 24 hours" : "last " + errDays + " days";
            var worst = res.FirstOrDefault();
            itemsErrTiles.ItemsSource = new List<TileVm>
            {
                new TileVm { Label = "CRASHES", Value = crashes.ToString(), Sub = period, Brush = Br(crashes > 0 ? "#FF6B8A" : "#3DDC97") },
                new TileVm { Label = "HANGS", Value = hangs.ToString(), Sub = period, Brush = Br(hangs > 0 ? "#FFB84D" : "#3DDC97") },
                new TileVm { Label = "APPS AFFECTED", Value = res.Count.ToString(), Sub = "programs", Brush = Br("#E6EEFA") },
                new TileVm { Label = "MOST UNSTABLE", Value = worst == null ? "—" : worst.Name, Sub = worst == null ? "" : (worst.Crashes + worst.Hangs) + " event(s)", Brush = Br(worst == null ? "#E6EEFA" : "#FFB84D") },
                new TileVm { Label = "LAST EVENT", Value = res.Count == 0 ? "—" : AppErr.Ago(res.Max(a => a.Last)), Sub = "", Brush = Br("#E6EEFA") },
            };
            itemsErrBars.ItemsSource = AppErr.Bars(res, errDays);
        }

        async void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { await Msg("COULD NOT OPEN THE LINK", "Copy it and open it in your browser:\n" + url, "warn"); }
        }

        async void RefreshHung()
        {
            try
            {
                var l = await Task.Run(() => AppErr.Hung());
                itemsHung.ItemsSource = l;
                hungPanel.Visibility = l.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        async void EndHung(HungVm h)
        {
            string nm = h.Name.Replace(".exe", "");
            if (Procs.Protected.Contains(nm) || Procs.Protected.Contains(h.Name) || h.Pid <= 4 || h.Pid == Process.GetCurrentProcess().Id)
            { await Msg("WINDOWS NEEDS THIS", h.Name + " is a core Windows process (or this app). Ending it could crash the PC or log you off, so it is protected.", "warn"); return; }
            if (!(await Confirm("END TASK", "End  " + h.Name + "  (PID " + h.Pid + ")?\n\nUnsaved work in that program will be lost.", "END TASK", "CANCEL", "warn"))) return;
            try
            {
                int pid = h.Pid;
                await Task.Run(() => { using (var pr = Process.GetProcessById(pid)) { pr.Kill(); pr.WaitForExit(3000); } });
                txtStatus.Text = "ENDED " + h.Name.ToUpper();
            }
            catch (Exception ex) { await Msg("COULD NOT END THE TASK", ex.Message, "danger"); }
            RefreshHung();
        }

        async void ErrClick(ErrApp a, string what)
        {
            try
            {
                switch (what)
                {
                    case "toggle": a.Open = !a.Open; break;
                    case "folder":
                        if (!string.IsNullOrEmpty(a.ExePath) && File.Exists(a.ExePath)) Process.Start("explorer.exe", "/select,\"" + a.ExePath + "\"");
                        break;
                    case "app":
                        {
                            string q = Path.GetFileNameWithoutExtension(a.Name);
                            try { if (!string.IsNullOrEmpty(a.ExePath)) { var vi = FileVersionInfo.GetVersionInfo(a.ExePath); if (!string.IsNullOrWhiteSpace(vi.ProductName)) q = vi.ProductName.Trim(); } } catch { }
                            txtSearch.Text = q; if (appsLoaded) Chip(null); Go(vApps); if (!appsLoaded) LoadApps();
                            break;
                        }
                    case "action":
                        if (a.ActionUri == "cd://health") Go(vHealth);
                        else if (!string.IsNullOrEmpty(a.ActionUri)) Process.Start(new ProcessStartInfo(a.ActionUri) { UseShellExecute = true });
                        break;
                }
            }
            catch (Exception ex) { await Msg("COULD NOT OPEN IT", ex.Message, "warn"); }
        }

        async void ExportErrors()
        {
            try
            {
                if (errApps == null) { await Msg("NOTHING TO EXPORT", "Run SCAN NOW first.", "info"); return; }
                string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(dir, "CyberDisk-AppErrors-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt");
                File.WriteAllText(file, AppErr.Report(errApps, errDays), new UTF8Encoding(true));
                if (await Confirm("REPORT SAVED", "The report was saved to your desktop:\n" + Path.GetFileName(file), "SHOW FILE", "CLOSE", "info")) Process.Start("explorer.exe", "/select,\"" + file + "\"");
            }
            catch (Exception ex) { await Msg("COULD NOT SAVE THE REPORT", ex.Message, "danger"); }
        }

        void OnCrash(CrashEvt c)
        {
            lastBalloonErr = true;
            if (Settings.CrashAlerts && tray != null)
            {
                try
                {
                    tray.ShowBalloonTip(6000, Loc.T(c.Kind == "Hang" ? "An app stopped responding" : "An app crashed"),
                        Loc.T(c.App + " - open CyberDisk > App Errors for the cause and a fix."), System.Windows.Forms.ToolTipIcon.Warning);
                }
                catch { }
            }
            if (vErrors.Visibility == Visibility.Visible) ScanErrors();
            else { errApps = null; newErrors++; UpdateBadge(); }
        }
    }
}
