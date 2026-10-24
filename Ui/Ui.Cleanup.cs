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
        // ── cleanup: select safe / none / all ──
        Button bSelSafe, bSelNone, bSelAll; TextBlock txtSelCount;

        List<CleanItem> TabItems() { return svBrowsers.Visibility == Visibility.Visible ? browsers.SelectMany(b => b.Types).ToList() : clean; }
        void AfterSelect(string msg) { UpdateReclaim(); txtStatus.Text = msg + " · " + allItems.Count(c => c.Checked) + " OF " + allItems.Count + " SELECTED"; }
        void SelectSafe() { foreach (var c in TabItems()) c.Checked = c.Badge == "SAFE"; AfterSelect("SAFE ITEMS SELECTED"); }
        void SelectNone() { foreach (var c in TabItems()) c.Checked = false; AfterSelect("SELECTION CLEARED"); }
        void SelectAll() { foreach (var c in TabItems()) c.Checked = true; AfterSelect("EVERYTHING SELECTED"); }

        void OnCleanClick(object src)
        {
            var b = src as Button;
            if (b != null && b.Tag is string)                  // the SAFE / NONE buttons of a group header
            {
                string grp = (string)b.Tag; bool safe = Convert.ToString(b.Content) == "SAFE";
                foreach (var c in clean.Where(x => x.Group == grp)) c.Checked = safe && c.Badge == "SAFE";
                AfterSelect((safe ? "SAFE ITEMS SELECTED IN " : "CLEARED ") + grp);
                return;
            }
            UpdateReclaim();
        }

        // ── cleanup lab ──
        static string Big(long b) => b <= 0 ? "0 B" : Fmt(b);
        Point Center(FrameworkElement e) => e.TranslatePoint(new Point(e.ActualWidth / 2, e.ActualHeight / 2), fx);

        void CleanTab(bool br)
        {
            svJunk.Visibility = br ? Visibility.Collapsed : Visibility.Visible;
            svBrowsers.Visibility = br ? Visibility.Visible : Visibility.Collapsed;
            bTabJunk.Background = br ? Br("#111E31") : Br("#333DD6F5");
            bTabBrowsers.Background = br ? Br("#333DD6F5") : Br("#111E31");
            Anim(br ? svBrowsers : svJunk);
        }

        void EnterClean()
        {
            if (cleanInit) return;
            cleanInit = true;
            clean = Sweep.Make();
            browsers = Browsers.Detect();
            allItems = clean.Concat(browsers.SelectMany(b => b.Types)).ToList();
            junkView = new System.Windows.Data.ListCollectionView(clean);
            junkView.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("Group"));
            itemsClean.ItemsSource = junkView;
            itemsBrowsers.ItemsSource = browsers;
            foreach (var b in browsers) b.Refresh();
            CleanTab(false);
            UpdateReclaim();
            Idle();
            blinkTm.Start();
        }

        void SetBar(double p, int ms = 380)
        {
            p = Math.Max(0, Math.Min(100, p));
            cleanBar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(p, TimeSpan.FromMilliseconds(Math.Max(1, ms))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            txtCleanPct.Text = ((int)Math.Round(p)) + "%";
        }

        void CountUp(long to)
        {
            if (cuTm != null) cuTm.Stop();
            long start = cuFrom; int step = 0; const int N = 24;
            cuTm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            cuTm.Tick += (s, e) =>
            {
                step++;
                double t = 1 - Math.Pow(1 - Math.Min(1.0, step / (double)N), 3);
                cuFrom = start + (long)((to - start) * t);
                txtReclaim.Text = Big(cuFrom);
                if (step >= N) { cuTm.Stop(); cuFrom = to; txtReclaim.Text = Big(to); }
            };
            cuTm.Start();
        }

        void UpdateReclaim()
        {
            if (allItems.Count == 0) return;
            txtSelCount.Text = allItems.Count(c => c.Checked) + " of " + allItems.Count + " selected";
            CountUp(allItems.Where(c => c.Checked).Sum(c => c.Size));
            if (!cleaning && !analyzing) bClean.IsEnabled = analyzed && allItems.Any(c => c.Checked && (c.Size > 0 || c.Kind >= 2));
        }

        async void Analyze()
        {
            if (cleaning || analyzing) return;
            EnterClean();
            analyzing = true; bAnalyze.IsEnabled = false; bClean.IsEnabled = false; itemsClean.IsEnabled = false; itemsBrowsers.IsEnabled = false;
            txtCleanStatus.Foreground = Br("#C7D6EE"); txtCleanStatus.Text = "Hunting for junk…"; txtFreed.Text = " ";
            SetBar(0, 1);
            if (junkView != null) junkView.Filter = null;
            foreach (var it in allItems) { it.Status = "scanning…"; it.StatusBrush = Br("#8195B0"); it.SizeText = "…"; it.Percent = 0; }
            var cr = browsers.Where(b => !b.IsFirefox).ToList();
            int stepsTotal = allItems.Count + cr.Count, done = 0;
            var tasks = new List<Task>();
            tasks.AddRange(allItems.Select(async it =>
            {
                var r = await Task.Run(() => { long n; long sz = Sweep.Measure(it, out n); return Tuple.Create(sz, n); });
                it.Size = r.Item1; it.Files = r.Item2;
                it.Status = r.Item1 > 0 ? r.Item2.ToString("N0") + (r.Item2 == 1 ? " file" : " files")
                          : it.Kind == 2 ? "size computed by Windows" : it.Kind == 3 ? "runs when you clean" : "nothing to clean";
                done++; SetBar(100.0 * done / stepsTotal);
            }));
            tasks.AddRange(cr.Select(async b =>
            {
                var sites = await Task.Run(() => Browsers.Inspect(b));
                b.Sites = sites; b.HasSites = sites.Count > 0;
                done++; SetBar(100.0 * done / stepsTotal);
            }));
            await Task.WhenAll(tasks);
            foreach (var b in browsers) b.Refresh();
            long all = allItems.Sum(c => c.Size);
            foreach (var it in allItems) it.Percent = all > 0 ? 100.0 * it.Size / all : 0;
            analyzed = true; analyzing = false;
            bAnalyze.IsEnabled = true; itemsClean.IsEnabled = true; itemsBrowsers.IsEnabled = true;
            if (junkView != null) junkView.Filter = o => { var c = (CleanItem)o; return c.Size > 0 || c.Kind >= 2; };
            UpdateReclaim();
            txtCleanStatus.Text = all > 0 ? "Found " + Fmt(all) + " of junk · pick what to clean" : "Your system is already sparkling clean ✦";
            Burst(Center(mascot), 12, false);
        }

        void SyncDone(long b) { if (Interlocked.Read(ref doneBytes) < b) Interlocked.Exchange(ref doneBytes, b); }

        async void DoClean()
        {
            if (cleaning || analyzing) return;
            var sel = allItems.Where(c => c.Checked && (c.Size > 0 || c.Kind >= 2)).ToList();
            if (sel.Count == 0) { await Msg("NOTHING TO CLEAN", analyzed ? "Nothing is selected. Tick at least one item first." : "Run ANALYZE first so the app knows what to clean.", "info"); return; }
            long total = sel.Sum(c => c.Weight);
            var sb = new StringBuilder("Permanently delete the selected items (about " + Fmt(sel.Sum(c => c.Size)) + " measured)?\n");
            if (sel.Any(c => c.Kind == 1)) sb.Append("\n• The Recycle Bin will be emptied permanently.");
            if (sel.Any(c => c.Browser != null && c.Badge != "SAFE")) sb.Append("\n• Browser cookies / history / site data will be erased (you may be signed out of websites).");
            if (sel.Any(c => c.Kind == 2)) sb.Append("\n• Windows Disk Cleanup (cleanmgr) will run for the selected system items - this can take a few minutes.");
            if (!(await Confirm("CONFIRM CLEANUP", sb.ToString(), "CLEAN UP", "CANCEL", "warn"))) return;

            var running = browsers.Where(b => b.IsRunning() && b.Types.Any(t => sel.Contains(t))).ToList();
            if (running.Count > 0)
            {
                int r = await Ask("BROWSER OPEN", string.Join(", ", running.Select(b => b.Name)) + (running.Count == 1 ? " is" : " are") + " still running.\n\nClose it now so every file can be cleaned, or clean anyway (files in use are skipped).", "warn", "CLOSE & CLEAN", "CLEAN ANYWAY", "CANCEL");
                if (r == 2) return;
                if (r == 0) { foreach (var b in running) b.Close(); await Task.Delay(3000); }
            }

            cleaning = true; cleanTotal = total; shown = 0; creepBudget = 0; creepGiven = 0; Interlocked.Exchange(ref doneBytes, 0); Interlocked.Exchange(ref realFreed, 0);
            bAnalyze.IsEnabled = false; bClean.IsEnabled = false; itemsClean.IsEnabled = false; itemsBrowsers.IsEnabled = false;
            txtCleanStatus.Foreground = Br("#C7D6EE"); txtCleanStatus.Text = "Munching junk…"; txtFreed.Text = "0 B freed";
            txtStatus.Text = "CLEANING…";
            SetBar(0, 1);
            Burst(Center(bClean), 22, false);
            Lid(true);
            cleanTm.Start();

            var green = Br("#3DDC97"); var amber = Br("#FFB84D");
            long baseDone = 0, freedAll = 0;
            foreach (var it in sel.Where(c => c.Kind <= 1))
            {
                it.Status = "cleaning…"; it.StatusBrush = it.Accent;
                long est = it.Size, freed = 0;
                try 
                { 
                    freed = await Task.Run(() => Sweep.Clean(it, b => { Interlocked.Add(ref doneBytes, b); Interlocked.Add(ref realFreed, b); })); 
                } 
                catch { }
                freedAll += freed; baseDone += est; SyncDone(baseDone);
                it.Size = Math.Max(0, est - freed); it.Percent = 0;
                if (freed >= est * 0.97) { it.Status = "✓ cleaned " + Fmt(freed); it.StatusBrush = green; }
                else if (freed > 0) { it.Status = "✓ " + Fmt(freed) + " freed · rest in use"; it.StatusBrush = amber; }
                else { it.Status = "in use · skipped"; it.StatusBrush = amber; }
            }
            foreach (var it in sel.Where(c => c.Kind == 3))
            {
                it.Status = "running…"; it.StatusBrush = it.Accent;
                long wgt = it.Weight, freed = 0;
                creepBudget = wgt; creepGiven = 0;
                try { freed = await Task.Run(() => Sweep.RunCommand(it)); } catch { }
                creepBudget = 0; baseDone += wgt; SyncDone(baseDone); freedAll += freed; Interlocked.Add(ref realFreed, freed);
                it.Size = 0; it.Percent = 0; it.Status = "✓ done" + (freed > 0 ? " · " + Fmt(freed) + " freed" : ""); it.StatusBrush = green;
            }
            var hs = sel.Where(c => c.Kind == 2).ToList();
            if (hs.Count > 0)
            {
                foreach (var h in hs) { h.Status = "Windows is cleaning…"; h.StatusBrush = h.Accent; }
                long wgt = hs.Sum(h => h.Weight), freed = 0;
                creepBudget = wgt; creepGiven = 0;
                txtCleanStatus.Text = "Windows Disk Cleanup is working…";
                try { freed = await Task.Run(() => Sweep.RunHandlers(hs.Select(h => h.HandlerKey).ToList())); } catch { }
                creepBudget = 0; baseDone += wgt; SyncDone(baseDone); freedAll += freed; Interlocked.Add(ref realFreed, freed);
                foreach (var h in hs) { h.Size = 0; h.Percent = 0; h.Status = "✓ cleaned by Windows"; h.StatusBrush = green; }
            }
            Interlocked.Exchange(ref doneBytes, cleanTotal);
            while (shown < 99.9) await Task.Delay(40);      // let the bar finish smoothly, however fast the delete was
            cleanTm.Stop(); SetBar(100, 200); Lid(false);

            txtFreed.Text = Big(freedAll) + " freed";
            if (freedAll > 0)
            {
                txtCleanStatus.Foreground = green; txtCleanStatus.Text = "All done! " + Fmt(freedAll) + " reclaimed ✦";
                Hop();
                var c = Center(mascot);
                Burst(c, 36, false);
                Burst(new Point(c.X, c.Y - 70), 46, true);
            }
            else { txtCleanStatus.Foreground = amber; txtCleanStatus.Text = "Done. Nothing measurable was freed (files in use or already clean)."; }
            txtStatus.Text = "CLEANUP COMPLETE · " + Big(freedAll) + " freed";
            foreach (var b in browsers) { b.Refresh(); b.Sites = new List<SiteVm>(); b.HasSites = false; }
            cleaning = false; itemsClean.IsEnabled = true; itemsBrowsers.IsEnabled = true; bAnalyze.IsEnabled = true;
            UpdateReclaim();
            LoadDrives();
        }

        void CleanTick()
        {
            double target = cleanTotal > 0 ? Math.Min(100.0, 100.0 * Interlocked.Read(ref doneBytes) / cleanTotal) : 100.0;
            if (creepBudget > 0 && creepGiven < creepBudget * 9 / 10)       // steps whose size Windows computes: the bar creeps along
            {
                long inc = Math.Max(1, creepBudget / 60);
                creepGiven += inc; Interlocked.Add(ref doneBytes, inc);
            }
            shown = Math.Min(target, shown + 3.0);
            SetBar(shown, 140);
            txtFreed.Text = Big(Interlocked.Read(ref realFreed)) + " freed" + (creepBudget > 0 ? "  ·  Windows is still measuring…" : "");   // never the bar estimate: only what was really deleted
            Fly(); if (rnd.Next(2) == 0) Fly();
        }
    }
}
