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
        // ── BOOST + HEALTH ──
        FrameworkElement vBoost, vHealth;
        Canvas fxBoost;
        System.Windows.Shapes.Path ramArc, cpuArc, healthArc;
        TextBlock ramPct, cpuPct, ramTxt, cpuTxt, boostBig, boostMsg, txtStartupHead, txtScore, txtHealthStatus;
        Button bBoost, bTrim, bLower, bEnd, bHealth, navBoost, navHealth;
        DataGrid gp;
        ItemsControl itemsStartup, itemsHealth;
        System.Collections.ObjectModel.ObservableCollection<ProcVm> procs = new System.Collections.ObjectModel.ObservableCollection<ProcVm>();
        System.Windows.Data.ListCollectionView procView;
        Dictionary<int, ProcVm> procMap = new Dictionary<int, ProcVm>();
        Dictionary<int, TimeSpan> prevCpuT = new Dictionary<int, TimeSpan>();
        DateTime prevSampleAt = DateTime.UtcNow;
        DispatcherTimer boostTm;
        bool boostInit, sampling, boosting, checkingHealth, procTick;
        List<StartupVm> startup = new List<StartupVm>();

        void WireBoost()
        {
            vBoost = F<FrameworkElement>("vBoost"); vHealth = F<FrameworkElement>("vHealth"); fxBoost = F<Canvas>("fxBoost");
            ramArc = F<System.Windows.Shapes.Path>("ramArc"); cpuArc = F<System.Windows.Shapes.Path>("cpuArc"); healthArc = F<System.Windows.Shapes.Path>("healthArc");
            ramPct = F<TextBlock>("ramPct"); cpuPct = F<TextBlock>("cpuPct"); ramTxt = F<TextBlock>("ramTxt"); cpuTxt = F<TextBlock>("cpuTxt");
            boostBig = F<TextBlock>("boostBig"); boostMsg = F<TextBlock>("boostMsg"); txtStartupHead = F<TextBlock>("txtStartupHead");
            txtScore = F<TextBlock>("txtScore"); txtHealthStatus = F<TextBlock>("txtHealthStatus");
            bBoost = F<Button>("bBoost"); bTrim = F<Button>("bTrim"); bLower = F<Button>("bLower"); bEnd = F<Button>("bEnd"); bHealth = F<Button>("bHealth");
            navBoost = F<Button>("navBoost"); navHealth = F<Button>("navHealth");
            gp = F<DataGrid>("gp"); itemsStartup = F<ItemsControl>("itemsStartup"); itemsHealth = F<ItemsControl>("itemsHealth");

            healthArc.Data = DriveVm.Arc(0);
            navBoost.Click += (_, _) => Go(vBoost);
            navHealth.Click += (_, _) => Go(vHealth);
            bBoost.Click += (_, _) => DoBoost();
            bTrim.Click += (_, _) => TrimSel();
            bLower.Click += (_, _) => LowerSel();
            bEnd.Click += (_, _) => EndSel();
            bHealth.Click += (_, _) => DoHealth();
            itemsStartup.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => StartupHead()));
            boostTm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            boostTm.Tick += (_, _) => BoostTick();
        }

        void StartupHead() { txtStartupHead.Text = "STARTUP APPS  ·  " + startup.Count(x => x.Enabled) + " of " + startup.Count + " enabled"; }

        async void EnterBoost()
        {
            if (!boostInit)
            {
                boostInit = true;
                procView = new System.Windows.Data.ListCollectionView(procs);
                procView.IsLiveSorting = true;
                procView.SortDescriptions.Add(new System.ComponentModel.SortDescription("MemBytes", System.ComponentModel.ListSortDirection.Descending));
                procView.LiveSortingProperties.Add("MemBytes");
                gp.ItemsSource = procView;
                Mem.Cpu();
                try { startup = await Task.Run(() => Startup.List()); } catch { startup = new List<StartupVm>(); }
                itemsStartup.ItemsSource = startup;
                StartupHead();
            }
            BoostTick();
            boostTm.Start();
        }

        void SetGauge(System.Windows.Shapes.Path arc, TextBlock pct, double v)
        {
            arc.Data = DriveVm.Arc(v);
            arc.Stroke = Br(v > 90 ? "#FF6B8A" : v > 75 ? "#FFB84D" : "#3DD6F5");
            pct.Text = ((int)Math.Round(v)) + "%";
        }

        async void BoostTick()
        {
            long t, a;
            Mem.Status(out t, out a);
            SetGauge(ramArc, ramPct, 100.0 * (t - a) / Math.Max(1, t));
            ramTxt.Text = Fmt(t - a) + " of " + Fmt(t) + " in use";
            SetGauge(cpuArc, cpuPct, Mem.Cpu());
            cpuTxt.Text = Environment.ProcessorCount + " logical processors";
            procTick = !procTick;                                  // the process list refreshes every 2 seconds
            if (!procTick || sampling) return;
            sampling = true;
            try { var snap = await Task.Run(() => Procs.Snap()); ApplySnap(snap); }
            catch { }
            finally { sampling = false; }
        }

        void ApplySnap(List<ProcSnap> snap)
        {
            var now = DateTime.UtcNow;
            double dt = Math.Max(200, (now - prevSampleAt).TotalMilliseconds);
            prevSampleAt = now;
            var seen = new HashSet<int>(); var next = new Dictionary<int, TimeSpan>();
            foreach (var s in snap)
            {
                seen.Add(s.Pid); next[s.Pid] = s.Cpu;
                double cpu = 0; TimeSpan pv;
                if (prevCpuT.TryGetValue(s.Pid, out pv)) cpu = Math.Max(0, (s.Cpu - pv).TotalMilliseconds / dt / Environment.ProcessorCount * 100.0);
                ProcVm vm;
                if (!procMap.TryGetValue(s.Pid, out vm))
                {
                    vm = new ProcVm { Pid = s.Pid, Name = s.Name };
                    vm.MemBytes = s.Mem; vm.Cpu = cpu;
                    procMap[s.Pid] = vm; procs.Add(vm);
                }
                else { vm.MemBytes = s.Mem; vm.Cpu = cpu; }
            }
            prevCpuT = next;
            foreach (var pid in procMap.Keys.Where(k => !seen.Contains(k)).ToList()) { procs.Remove(procMap[pid]); procMap.Remove(pid); }
        }

        ProcVm SelProc() { return gp.SelectedItem as ProcVm; }

        async void TrimSel()
        {
            var p = SelProc();
            if (p == null) { await Msg("SELECT A PROCESS", "Pick a process from the list first.", "info"); return; }
            int pid = p.Pid;
            bool ok = await Task.Run(() => Mem.Trim(pid));
            txtStatus.Text = ok ? "TRIMMED " + p.Name.ToUpper() : "COULD NOT TRIM " + p.Name.ToUpper() + " (ACCESS DENIED)";
        }

        async void LowerSel()
        {
            var p = SelProc();
            if (p == null) { await Msg("SELECT A PROCESS", "Pick a process from the list first.", "info"); return; }
            try { using (var pr = Process.GetProcessById(p.Pid)) pr.PriorityClass = ProcessPriorityClass.BelowNormal; txtStatus.Text = p.Name.ToUpper() + " NOW RUNS AT LOWER PRIORITY"; }
            catch (Exception ex) { await Msg("COULD NOT CHANGE PRIORITY", ex.Message, "warn"); }
        }

        async void EndSel()
        {
            var p = SelProc();
            if (p == null) { await Msg("SELECT A PROCESS", "Pick a process from the list first.", "info"); return; }
            if (Procs.Protected.Contains(p.Name) || p.Pid <= 4 || p.Pid == Process.GetCurrentProcess().Id)
            { await Msg("WINDOWS NEEDS THIS", p.Name + " is a core Windows process (or this app). Ending it could crash the PC or log you off, so it is protected.", "warn"); return; }
            if (!(await Confirm("END TASK", "End  " + p.Name + "  (PID " + p.Pid + ")?\n\nUnsaved work in that program will be lost.", "END TASK", "CANCEL", "warn"))) return;
            try
            {
                int pid = p.Pid;
                await Task.Run(() => { using (var pr = Process.GetProcessById(pid)) { pr.Kill(); pr.WaitForExit(3000); } });
                txtStatus.Text = "ENDED " + p.Name.ToUpper();
            }
            catch (Exception ex) { await Msg("COULD NOT END THE TASK", ex.Message, "danger"); }
        }

        async void DoBoost()
        {
            if (boosting) return;
            boosting = true; bBoost.IsEnabled = false;
            boostBig.Text = "Boosting…"; boostMsg.Text = "Releasing idle memory held by background programs…";
            var centre = bBoost.TranslatePoint(new Point(bBoost.ActualWidth / 2, bBoost.ActualHeight / 2), fxBoost);
            BurstOn(fxBoost, centre, 16, false);
            long t0, a0; Mem.Status(out t0, out a0);
            var r = await Task.Run(() => { int c; long f = Mem.TrimAll(out c); return Tuple.Create(f, c); });
            long t1, a1; Mem.Status(out t1, out a1);
            long freed = Math.Max(r.Item1, a1 - a0);
            boostBig.Text = freed > 0 ? Fmt(freed) + " freed" : "Already optimized";
            boostMsg.Text = "Trimmed " + r.Item2 + " processes  ·  " + Fmt(a1) + " of " + Fmt(t1) + " is now available. Windows refills memory as programs need it, so run BOOST whenever the PC feels slow.";
            BurstOn(fxBoost, centre, 30, false);
            BurstOn(fxBoost, new Point(centre.X, centre.Y - 60), 30, true);
            txtStatus.Text = "BOOST COMPLETE · " + (freed > 0 ? Fmt(freed) + " FREED" : "MEMORY ALREADY OPTIMIZED");
            boosting = false; bBoost.IsEnabled = true;
            BoostTick();
        }
    }
}
