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
        List<Finding> healthAll = new List<Finding>();       // every result of the last check
        bool showIgnored, fixRunning, fixStop;
        Border panelFix; TextBlock txtFixTitle, txtFixStep; ProgressBar barFix; Button bFixStop, bShowIgnored; TextBlock txtIgnored;

        void WireHealthExtra()
        {
            panelFix = F<Border>("panelFix"); txtFixTitle = F<TextBlock>("txtFixTitle"); txtFixStep = F<TextBlock>("txtFixStep"); barFix = F<ProgressBar>("barFix");
            bFixStop = F<Button>("bFixStop"); bShowIgnored = F<Button>("bShowIgnored"); txtIgnored = F<TextBlock>("txtIgnored");
            bFixStop.Click += (_, _) => { fixStop = true; Fixer.CancelRequested = true; bFixStop.IsEnabled = false; txtFixStep.Text = "Stopping…"; };
            bShowIgnored.Click += (_, _) => { showIgnored = !showIgnored; RebindHealth(); };
            bHealth.Content = IconLabel(Vec.Health, "RUN CHECK", "#06111D");
        }

        async void DoHealth()
        {
            if (checkingHealth || fixRunning) return;
            checkingHealth = true; bHealth.IsEnabled = false; bFixAll.Visibility = Visibility.Collapsed;
            itemsHealth.ItemsSource = null; itemsCats.ItemsSource = null; txtScore.Text = "…"; txtGrade.Text = ""; txtHealthCounts.Text = ""; txtHealthMeta.Text = ""; healthArc.Data = DriveVm.Arc(1);
            bShowIgnored.Visibility = Visibility.Collapsed; txtIgnored.Visibility = Visibility.Collapsed;
            txtHealthStatus.Text = "Starting the check…";
            List<Finding> res;
            try { res = await Task.Run(() => Health.Run(s => w.Dispatcher.BeginInvoke(new Action(() => txtHealthStatus.Text = s)))); }
            catch (Exception ex) { res = new List<Finding>(); await Msg("CHECK FAILED", ex.Message, "danger"); }
            foreach (var f in res) { f.MakeIgnoreKey(); f.Ignored = f.Level > 0 && Health.Ignored.Contains(f.IgnoreKey); }
            healthAll = res.OrderByDescending(x => x.Level == 3 ? -1 : x.Level).ToList();
            checkingHealth = false; bHealth.IsEnabled = true; lastHealth = DateTime.Now;
            RebindHealth(true);
            txtStatus.Text = "HEALTH CHECK COMPLETE · SCORE " + Health.Score(healthList);
        }

        /// <summary>Rebuilds the list shown on the page. Ignored findings are left out of the score and of FIX ALL.</summary>
        void RebindHealth(bool animate = false)
        {
            healthList = healthAll.Where(f => !f.Ignored).ToList();
            var shown = showIgnored ? healthAll : healthList;
            itemsHealth.ItemsSource = null; itemsHealth.ItemsSource = shown;
            if (animate) Anim(itemsHealth);
            int hidden = healthAll.Count(f => f.Ignored);
            bShowIgnored.Visibility = txtIgnored.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
            txtIgnored.Text = hidden + " ignored finding(s) are left out of the score";
            bShowIgnored.Content = showIgnored ? "HIDE IGNORED" : "SHOW IGNORED (" + hidden + ")";
            UpdateHealthSummary();
        }

        void ToggleIgnore(Finding f)
        {
            if (f == null || f.Busy || fixRunning) return;
            f.Ignored = !f.Ignored;
            Health.SetIgnored(f.IgnoreKey, f.Ignored);
            txtStatus.Text = f.Ignored ? "IGNORED · LEFT OUT OF THE SCORE" : "RESTORED · COUNTED AGAIN";
            RebindHealth();
        }

        // ───────── health: fixing ─────────
        /// <summary>Redraws the score ring, grade, category bars and counters from the current findings.</summary>
        void UpdateHealthSummary()
        {
            if (healthList == null || bFixAll == null) return;
            if (healthAll.Count == 0) { txtScore.Text = "—"; txtHealthStatus.Text = "No problems found ✦"; }
            else
            {
                var rep = HealthScorer.Evaluate(healthList);
                txtScore.Text = rep.Score.ToString();
                ScoreTheme.StyleRing(healthArc, rep.Score, rep.Score);
                txtGrade.Text = rep.Grade; txtGrade.Foreground = ScoreTheme.Solid(rep.Score);
                txtHealthStatus.Text = rep.Problems == 0 && rep.Warnings == 0 ? "No problems found ✦" : rep.Problems + " problem(s), " + rep.Warnings + " warning(s) found";
                txtHealthCounts.Text = rep.Problems + " problems  ·  " + rep.Warnings + " warnings  ·  " + rep.Passed + " passed";
                foreach (var c in rep.Categories) c.Name = c.Name.ToUpperInvariant();
                itemsCats.ItemsSource = rep.Categories;
                txtHealthMeta.Text = "LAST CHECK " + lastHealth.ToString("HH:mm");
            }
            int fixable = healthList.Count(IsBatchFix);
            bFixAll.Visibility = fixable > 0 && !checkingHealth ? Visibility.Visible : Visibility.Collapsed;
            bFixAll.Content = IconLabel(Vec.Boost, "FIX ALL (" + fixable + ")", "#3DDC97");
            bFixAll.IsEnabled = !fixRunning;
        }

        /// <summary>Fixes that FIX ALL may run by itself: safe repairs only - never "mark as reviewed", page links, restarts or memory tests.</summary>
        static bool IsBatchFix(Finding f)
        {
            return f.AutoSafe && !f.IsFixed && !f.Ignored && !f.Busy && f.FixId != null && f.Level > 0
                && f.FixId != "ack" && f.FixId != "reboot" && f.FixId != "memdiag" && !f.FixId.StartsWith("go:");
        }

        async Task<Fixer.Result> ExecFix(Finding f)
        {
            string was = f.Title; bool longFix = Fixer.IsLong(f.FixId);
            f.Queued = false; f.Busy = true;
            f.Result = longFix ? "Fixing… this repair can take several minutes. Keep CyberDisk open." : "Fixing… this can take a moment.";
            f.ResultBrush = Br("#8195B0");
            txtStatus.Text = "FIXING…";
            Fixer.Result r;
            var clock = Stopwatch.StartNew();
            try { r = await Task.Run(() => Fixer.Run(f)); }
            catch (Exception ex) { r = new Fixer.Result { Text = "The fix failed: " + ex.Message }; }
            f.Busy = false;
            string took = clock.Elapsed.TotalSeconds >= 10 ? "  (" + (clock.Elapsed.TotalMinutes >= 1 ? (int)clock.Elapsed.TotalMinutes + " min " + clock.Elapsed.Seconds + " s" : (int)clock.Elapsed.TotalSeconds + " s") + ")" : "";
            if (r.Ok)
            {
                f.Title = "Fixed: " + was;
                f.Detail = r.Text + (r.Restart ? "\nRestart Windows to finish the repair." : "") + took;
                f.SetLevel(0); f.IsFixed = true; f.Result = null;
            }
            else
            {
                f.Result = (r.Partial ? "! " : "✖ ") + r.Text + (r.Restart ? "\nRestart Windows to finish the repair." : "") + "\nDetails are in the fix log (SETTINGS > OPEN FIX LOG).";
                f.ResultBrush = Br(r.Partial ? "#FFB84D" : "#FF6B8A");
                f.Refresh();
            }
            UpdateHealthSummary();
            txtStatus.Text = r.Ok ? "FIX COMPLETE" : "FIX NEEDS ATTENTION";
            return r;
        }

        async void RunFix(Finding f)
        {
            if (f == null || f.Busy || f.IsFixed || f.FixId == null || f.Ignored) return;
            if (fixRunning) { txtStatus.Text = "ANOTHER FIX IS RUNNING - PLEASE WAIT"; return; }
            switch (f.FixId)
            {
                case "go:clean": Go(vClean); EnterClean(); Analyze(); return;
                case "go:boost": Go(vBoost); return;
                case "go:errors": Go(vErrors); EnterErrors(); return;
                case "go:disk": ShowDisks(null); return;
                case "reboot": await DoRestart(); return;
            }
            string q = f.Title + "\n\nWhat this fix will do:\n" + (f.FixInfo ?? "") + (Fixer.IsLong(f.FixId) ? "\n\nThis can take several minutes. You can keep using the PC." : "");
            if (!(await Confirm("CONFIRM FIX", q, "FIX NOW", "CANCEL", "question"))) return;
            fixRunning = true; fixStop = false; Fixer.CancelRequested = false; bFixAll.IsEnabled = false; bHealth.IsEnabled = false;
            Fixer.Result r;
            try { r = await ExecFix(f); }
            finally { fixRunning = false; bHealth.IsEnabled = true; Fixer.CancelRequested = false; UpdateHealthSummary(); }
            if (r.Restart) await OfferRestart();
        }

        void SetFixPanel(bool on)
        {
            panelFix.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            bFixStop.IsEnabled = true; bHealth.IsEnabled = !on; bFixAll.IsEnabled = !on;
        }

        async void FixAll()
        {
            if (fixRunning || checkingHealth) return;
            var todo = healthList.Where(IsBatchFix).OrderBy(f => Fixer.IsLong(f.FixId) ? 1 : 0).ToList();
            if (todo.Count == 0) return;
            bool anyLong = todo.Any(f => Fixer.IsLong(f.FixId));
            string q = "These repairs will run one after another, quick ones first:\n" + string.Join("\n", todo.Select(f => "• " + f.Title + (Fixer.IsLong(f.FixId) ? "   (can take several minutes)" : "")))
                     + "\n\nEach repair is described on its card. No personal file is deleted." + (anyLong ? "\nYou can press STOP at any time." : "");
            if (!(await Confirm("FIX ALL ISSUES", q, "FIX ALL", "CANCEL", "question"))) return;

            fixRunning = true; fixStop = false; Fixer.CancelRequested = false;
            foreach (var f in todo) { f.Queued = true; f.Result = "Waiting in the queue…"; f.ResultBrush = Br("#8195B0"); }
            txtFixTitle.Text = "FIXING"; SetFixPanel(true);
            var results = new List<Tuple<Finding, Fixer.Result>>(); bool restart = false;
            try
            {
                for (int i = 0; i < todo.Count && !fixStop; i++)
                {
                    var f = todo[i];
                    txtFixStep.Text = "Step " + (i + 1) + " of " + todo.Count + "\n" + f.Title; barFix.Value = 100.0 * i / todo.Count;
                    var r = await ExecFix(f);
                    results.Add(Tuple.Create(f, r)); if (r.Restart) restart = true;
                }
                barFix.Value = 100;
            }
            catch (Exception ex) { Fixer.Log("FIX ALL crashed: " + ex); }
            finally
            {
                foreach (var f in todo) if (f.Queued) { f.Queued = false; f.Result = null; }                  // steps that never started
                fixRunning = false; Fixer.CancelRequested = false; SetFixPanel(false); UpdateHealthSummary();
            }

            int ok = results.Count(x => x.Item2.Ok), part = results.Count(x => !x.Item2.Ok && x.Item2.Partial), bad = results.Count(x => !x.Item2.Ok && !x.Item2.Partial), skipped = todo.Count - results.Count;
            txtStatus.Text = "FIX ALL FINISHED · " + ok + " FIXED · " + (part + bad) + " NEED ATTENTION" + (skipped > 0 ? " · " + skipped + " STOPPED" : "");
            var sb = new StringBuilder();
            foreach (var x in results) sb.AppendLine((x.Item2.Ok ? "✓  " : x.Item2.Partial ? "!  " : "✖  ") + x.Item1.Title.Replace("Fixed: ", ""));
            if (skipped > 0) sb.AppendLine("•  " + skipped + " repair(s) were not started because you stopped FIX ALL.");
            sb.AppendLine(); sb.Append(ok + " fixed  ·  " + part + " partly fixed  ·  " + bad + " failed.");
            if (restart) sb.Append("\nWindows must restart to finish some repairs.");
            string[] btns = restart ? new[] { "RESTART NOW", "RE-CHECK", "CLOSE" } : new[] { "RE-CHECK", "CLOSE" };
            int pick = await Ask("FIX ALL FINISHED", sb.ToString(), bad + part > 0 ? "warn" : "info", btns);
            string chosen = pick >= 0 && pick < btns.Length ? btns[pick] : "CLOSE";
            if (chosen == "RESTART NOW") await DoRestart(); else if (chosen == "RE-CHECK") DoHealth();
        }

        async Task OfferRestart()
        {
            int ch = await Ask("RESTART NEEDED", "Windows must restart to finish the repair. Save your work first.", "warn", "RESTART NOW", "LATER");
            if (ch == 0) await DoRestart();
        }

        async Task DoRestart()
        {
            if (!(await Confirm("RESTART WINDOWS", "Restart the PC now?\n\nWindows shows a 30-second warning first. Save your work.", "RESTART", "CANCEL", "warn"))) return;
            string err = await Task.Run(() => Fixer.Restart());
            if (err != null) { await Msg("COULD NOT RESTART", err, "danger"); return; }
            int ch = await Ask("RESTARTING IN 30 SECONDS", "Windows will restart in 30 seconds. Save your work now.", "warn", "OK", "CANCEL RESTART");
            if (ch == 1) { await Task.Run(() => Fixer.CancelRestart()); txtStatus.Text = "RESTART CANCELLED"; }
        }
    }
}
