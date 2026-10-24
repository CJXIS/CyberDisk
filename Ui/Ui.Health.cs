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
        async void DoHealth()
        {
            if (checkingHealth) return;
            checkingHealth = true; bHealth.IsEnabled = false; bFixAll.Visibility = Visibility.Collapsed;
            itemsHealth.ItemsSource = null; itemsCats.ItemsSource = null; txtScore.Text = "…"; txtGrade.Text = ""; txtHealthCounts.Text = ""; txtHealthMeta.Text = ""; healthArc.Data = DriveVm.Arc(1);
            txtHealthStatus.Text = "Starting the check…";
            List<Finding> res;
            try { res = await Task.Run(() => Health.Run(s => w.Dispatcher.BeginInvoke(new Action(() => txtHealthStatus.Text = s)))); }
            catch (Exception ex) { res = new List<Finding>(); await Msg("CHECK FAILED", ex.Message, "danger"); }
            healthList = res.OrderByDescending(x => x.Level == 3 ? -1 : x.Level).ToList();
            checkingHealth = false; bHealth.IsEnabled = true; lastHealth = DateTime.Now;
            if (healthList.Count == 0) { txtScore.Text = "—"; txtHealthStatus.Text = "No problems found ✦"; }
            itemsHealth.ItemsSource = healthList;
            Anim(itemsHealth);
            UpdateHealthSummary();
            txtStatus.Text = "HEALTH CHECK COMPLETE · SCORE " + Health.Score(healthList);
        }

        // ───────── health: fixing ─────────
        /// <summary>Redraws the score ring, grade, category bars and counters from the current findings.</summary>
        void UpdateHealthSummary()
        {
            if (healthList == null || bFixAll == null) return;
            var rep = HealthScorer.Evaluate(healthList);
            if (healthList.Count > 0)
            {
                txtScore.Text = rep.Score.ToString();
                ScoreTheme.StyleRing(healthArc, rep.Score, rep.Score);
                txtGrade.Text = rep.Grade; txtGrade.Foreground = ScoreTheme.Solid(rep.Score);
                txtHealthStatus.Text = rep.Problems == 0 && rep.Warnings == 0 ? "No problems found ✦" : rep.Problems + " problem(s), " + rep.Warnings + " warning(s) found";
                txtHealthCounts.Text = rep.Problems + " problems  ·  " + rep.Warnings + " warnings  ·  " + rep.Passed + " passed";
                foreach (var c in rep.Categories) c.Name = c.Name.ToUpperInvariant();
                itemsCats.ItemsSource = rep.Categories;
                txtHealthMeta.Text = "LAST CHECK " + lastHealth.ToString("HH:mm");
            }
            int fixable = healthList.Count(f => f.AutoSafe && !f.IsFixed && f.FixId != null && f.Level > 0);
            bFixAll.Visibility = fixable > 0 && !checkingHealth ? Visibility.Visible : Visibility.Collapsed;
            bFixAll.Content = "⚙  FIX ALL (" + fixable + ")";
        }

        async Task<Fixer.Result> ExecFix(Finding f)
        {
            string was = f.Title;
            f.Busy = true; f.Result = "Fixing… this can take a moment."; f.ResultBrush = Br("#8195B0");
            txtStatus.Text = "FIXING…";
            Fixer.Result r;
            try { r = await Task.Run(() => Fixer.Run(f)); }
            catch (Exception ex) { r = new Fixer.Result { Text = "The fix failed: " + ex.Message }; }
            f.Busy = false;
            if (r.Ok)
            {
                f.Title = "Fixed: " + was;
                f.Detail = r.Text + (r.Restart ? "\nRestart Windows to finish the repair." : "");
                f.SetLevel(0); f.IsFixed = true; f.Result = null;
            }
            else
            {
                f.Result = (r.Partial ? "! " : "✖ ") + r.Text + (r.Restart ? "\nRestart Windows to finish the repair." : "");
                f.ResultBrush = Br(r.Partial ? "#FFB84D" : "#FF6B8A");
            }
            UpdateHealthSummary();
            txtStatus.Text = r.Ok ? "FIX COMPLETE" : "FIX NEEDS ATTENTION";
            return r;
        }

        async void RunFix(Finding f)
        {
            if (f == null || f.Busy || f.IsFixed || f.FixId == null) return;
            switch (f.FixId)
            {
                case "go:clean": Go(vClean); EnterClean(); Analyze(); return;
                case "go:boost": Go(vBoost); return;
                case "go:errors": Go(vErrors); EnterErrors(); return;
                case "go:disk": ShowDisks(null); return;
                case "reboot": await DoRestart(); return;
            }
            string q = f.Title + "\n\nWhat this fix will do:\n" + (f.FixInfo ?? "");
            if (!(await Confirm("CONFIRM FIX", q, "FIX NOW", "CANCEL", "question"))) return;
            var r = await ExecFix(f);
            if (r.Restart) await OfferRestart();
        }

        async void FixAll()
        {
            var todo = healthList.Where(f => f.AutoSafe && !f.IsFixed && f.FixId != null && f.Level > 0 && !f.Busy).ToList();
            if (todo.Count == 0) return;
            string q = "These fixes will run one after another:\n" + string.Join("\n", todo.Select(f => "• " + f.Title)) + "\n\nEach fix is described on its card. No file is deleted.";
            if (!(await Confirm("FIX ALL ISSUES", q, "FIX ALL", "CANCEL", "question"))) return;
            bFixAll.IsEnabled = false; bHealth.IsEnabled = false;
            int ok = 0, fail = 0; bool restart = false;
            foreach (var f in todo)
            {
                var r = await ExecFix(f);
                if (r.Ok) ok++; else fail++;
                if (r.Restart) restart = true;
            }
            bFixAll.IsEnabled = true; bHealth.IsEnabled = true;
            UpdateHealthSummary();
            txtStatus.Text = "FIX ALL FINISHED · " + ok + " FIXED · " + fail + " NEED ATTENTION";
            if (restart) await OfferRestart();
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
