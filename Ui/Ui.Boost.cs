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
        FrameworkElement vBoost, vHealth, vStartup;
        Canvas fxBoost;
        System.Windows.Shapes.Path ramArc, cpuArc, healthArc;
        TextBlock ramPct, cpuPct, ramTxt, cpuTxt, boostBig, boostMsg, txtStartupHead, txtScore, txtHealthStatus;
        Button bBoost, bTrim, bLower, bEnd, bHealth, navBoost, navHealth, navStartup;
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
            vBoost = F<FrameworkElement>("vBoost"); vHealth = F<FrameworkElement>("vHealth"); vStartup = F<FrameworkElement>("vStartup"); fxBoost = F<Canvas>("fxBoost");
            ramArc = F<System.Windows.Shapes.Path>("ramArc"); cpuArc = F<System.Windows.Shapes.Path>("cpuArc"); healthArc = F<System.Windows.Shapes.Path>("healthArc");
            ramPct = F<TextBlock>("ramPct"); cpuPct = F<TextBlock>("cpuPct"); ramTxt = F<TextBlock>("ramTxt"); cpuTxt = F<TextBlock>("cpuTxt");
            boostBig = F<TextBlock>("boostBig"); boostMsg = F<TextBlock>("boostMsg"); txtStartupHead = F<TextBlock>("txtStartupHead");
            txtScore = F<TextBlock>("txtScore"); txtHealthStatus = F<TextBlock>("txtHealthStatus");
            bBoost = F<Button>("bBoost"); bTrim = F<Button>("bTrim"); bLower = F<Button>("bLower"); bEnd = F<Button>("bEnd"); bHealth = F<Button>("bHealth");
            navBoost = F<Button>("navBoost"); navHealth = F<Button>("navHealth"); navStartup = F<Button>("navStartup");
            gp = F<DataGrid>("gp"); itemsStartup = F<ItemsControl>("itemsStartup"); itemsHealth = F<ItemsControl>("itemsHealth");

            healthArc.Data = DriveVm.Arc(0);
            navBoost.Click += (_, _) => Go(vBoost);
            navStartup.Click += (_, _) => Go(vStartup);
            navHealth.Click += (_, _) => Go(vHealth);
            bBoost.Click += (_, _) => DoBoost();
            bTrim.Click += (_, _) => TrimSel();
            bLower.Click += (_, _) => LowerSel();
            bEnd.Click += (_, _) => EndSel();
            bHealth.Click += (_, _) => DoHealth();
            itemsStartup.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => StartupClick(e)));
            boostTm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            boostTm.Tick += (_, _) => BoostTick();
        }

        // ── startup apps: search, filter, switches ──
        TextBox txtStartupSearch; TextBlock txtStartupCount, txtStartupEmpty; Button sfAll, sfOn, sfOff, bStartupOffAll, bStartupRestore, bStartupRefresh, bRemRestoreAll, bRemClose; Border panelRemoved; ItemsControl itemsRemoved;
        System.Windows.Data.ListCollectionView startupView; string startupFilter = "all";

        void StartupHead()
        {
            int on = startup.Count(x => x.Enabled);
            txtStartupCount.Text = on + " of " + startup.Count + " ON";
            txtStartupHead.Text = "STARTUP APPS";
            bStartupOffAll.IsEnabled = on > 0;
            if (bStartupRestore != null) { int rem = Startup.RemovedCount(); bStartupRestore.Visibility = rem > 0 ? Visibility.Visible : Visibility.Collapsed; bStartupRestore.Content = IconLabel(Vec.Undo, Loc.T("RESTORE (" + rem + ")"), "#8195B0"); }
            if (startupView != null) { startupView.Refresh(); txtStartupEmpty.Visibility = startupView.Count == 0 && startup.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
        }

        void WireStartupUx()
        {
            txtStartupSearch = F<TextBox>("txtStartupSearch"); txtStartupCount = F<TextBlock>("txtStartupCount"); txtStartupEmpty = F<TextBlock>("txtStartupEmpty");
            sfAll = F<Button>("sfAll"); sfOn = F<Button>("sfOn"); sfOff = F<Button>("sfOff"); bStartupOffAll = F<Button>("bStartupOffAll"); bStartupRestore = F<Button>("bStartupRestore"); bStartupRefresh = F<Button>("bStartupRefresh");
            bStartupRefresh.Click += async (_, _) => { await LoadStartup(); txtStatus.Text = "STARTUP LIST REFRESHED"; };
            panelRemoved = F<Border>("panelRemoved"); itemsRemoved = F<ItemsControl>("itemsRemoved"); bRemRestoreAll = F<Button>("bRemRestoreAll"); bRemClose = F<Button>("bRemClose");
            bStartupRestore.Click += (_, _) => ToggleRemovedPanel();
            bRemClose.Click += (_, _) => panelRemoved.Visibility = Visibility.Collapsed;
            bRemRestoreAll.Click += (_, _) => RestoreStartup();
            itemsRemoved.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => RemovedClick(e)));
            StartupVm.OnFail = n => w.Dispatcher.BeginInvoke(new Action(() => { txtStatus.Text = "COULD NOT CHANGE " + n.ToUpperInvariant() + " - WINDOWS REFUSED THE CHANGE"; StartupHead(); }));
            Action<string> setFilter = f =>
            {
                startupFilter = f;
                foreach (var kv in new[] { Tuple.Create(sfAll, "all"), Tuple.Create(sfOn, "on"), Tuple.Create(sfOff, "off") }) kv.Item1.Background = Br(kv.Item2 == f ? "#333DD6F5" : "#111E31");
                StartupHead();
            };
            sfAll.Click += (_, _) => setFilter("all"); sfOn.Click += (_, _) => setFilter("on"); sfOff.Click += (_, _) => setFilter("off");
            txtStartupSearch.TextChanged += (_, _) => StartupHead();
            bStartupOffAll.Click += async (_, _) =>
            {
                var on = startup.Where(x => x.Enabled && !x.Missing).ToList();
                if (on.Count == 0) return;
                if (!(await Confirm("TURN ALL OFF", "Turn off " + on.Count + " startup app(s)?\n\nWindows itself and drivers are not affected. You can turn any app back on later.", "TURN ALL OFF", "CANCEL", "warn"))) return;
                int fail = 0;
                foreach (var v in on) { v.Enabled = false; if (v.Enabled) fail++; }
                StartupHead();
                txtStatus.Text = fail == 0 ? "ALL STARTUP APPS TURNED OFF" : fail + " APP(S) COULD NOT BE CHANGED";
            };
            setFilter("all");
        }

        void StartupClick(RoutedEventArgs e)
        {
            var b = e.OriginalSource as Button; var vm = b == null ? null : b.DataContext as StartupVm;
            if (b != null && (b.Tag as string) == "remove" && vm != null) { RemoveStartup(vm); return; }
            if (b != null && (b.Tag as string) == "folder" && vm != null)
            {
                try
                {
                    string target = vm.ApprovedKey == "StartupFolder" ? vm.Command : vm.ExePath;
                    if (!string.IsNullOrEmpty(target) && File.Exists(target)) Process.Start("explorer.exe", "/select,\"" + target + "\"");
                    else txtStatus.Text = "THE FILE WAS NOT FOUND";
                }
                catch { }
                return;
            }
            StartupHead();
        }

        async void RemoveStartup(StartupVm vm)
        {
            var matches = await Task.Run(() => Startup.Matches(vm));
            if (matches.Count == 0)
            {
                try { startup = await Task.Run(() => Startup.List()); } catch { }
                startupView = new System.Windows.Data.ListCollectionView(startup) { Filter = StartupMatch };
                itemsStartup.ItemsSource = startupView; StartupHead();
                await Msg("ALREADY GONE", "\"" + vm.Name + "\" is no longer in the Windows startup lists. The list was refreshed.", "info");
                return;
            }
            string list = string.Join("\n", matches.Select(m => "• " + m.Name + "   (" + m.Label + ")"));
            if (!(await Confirm("REMOVE FROM STARTUP", "These entries start \"" + vm.Name + "\" with Windows and will be deleted from the system:\n\n" + list + "\n\nThe program stays installed. A copy is kept, so RESTORE REMOVED can bring them back.", "REMOVE", "CANCEL", "warn"))) return;

            var res = await Task.Run(() => Startup.Remove(matches, vm.Name, vm.ExePath));
            try { startup = await Task.Run(() => Startup.List()); } catch { }
            startupView = new System.Windows.Data.ListCollectionView(startup) { Filter = StartupMatch };
            itemsStartup.ItemsSource = startupView;
            StartupHead();

            var sb = new StringBuilder();
            if (res.Places.Count > 0) { sb.AppendLine("Deleted:"); foreach (var pl in res.Places) sb.AppendLine("  ✓ " + pl); }
            if (res.Failed.Count > 0) { sb.AppendLine(); sb.AppendLine("Windows did not allow:"); foreach (var f in res.Failed) sb.AppendLine("  ✖ " + f); }
            if (res.Failed.Count == 0) { sb.AppendLine(); sb.Append("Close Task Manager and open it again to see the change."); }
            if (res.Running) { sb.AppendLine(); sb.Append("\n" + vm.Name + " is running right now. Some programs write their startup entry again when they start or close - if it comes back, turn off \"start with Windows\" inside that program too."); }
            txtStatus.Text = res.Failed.Count == 0 ? "REMOVED " + vm.Name.ToUpperInvariant() + " FROM STARTUP" : "SOME ENTRIES COULD NOT BE REMOVED";
            await Msg(res.Failed.Count == 0 ? "REMOVED FROM STARTUP" : "REMOVED WITH PROBLEMS", sb.ToString().Trim(), res.Failed.Count == 0 ? "info" : "warn");
        }

        void BindRemoved()
        {
            var items = Startup.RemovedItems();
            itemsRemoved.ItemsSource = items;
            if (items.Count == 0) panelRemoved.Visibility = Visibility.Collapsed;
            StartupHead();
        }

        void ToggleRemovedPanel()
        {
            if (panelRemoved.Visibility == Visibility.Visible) { panelRemoved.Visibility = Visibility.Collapsed; return; }
            BindRemoved();
            if (Startup.RemovedCount() > 0) panelRemoved.Visibility = Visibility.Visible;
        }

        async void RemovedClick(RoutedEventArgs e)
        {
            var b = e.OriginalSource as Button; var it = b == null ? null : b.DataContext as Startup.RemovedItem;
            if (it == null) return;
            string tag = b.Tag as string;
            if (tag == "restore")
            {
                int failed = await Task.Run(() => Startup.Restore(new HashSet<int> { it.Index }));
                await LoadStartup(); BindRemoved();
                txtStatus.Text = failed == 0 ? "RESTORED " + it.Name.ToUpperInvariant() : "COULD NOT RESTORE " + it.Name.ToUpperInvariant();
            }
            else if (tag == "forget")
            {
                if (!(await Confirm("FORGET THIS ENTRY", "Delete the saved copy of \"" + it.Name + "\"?\n\nIt cannot be restored afterwards. The program itself is not touched.", "FORGET", "CANCEL", "warn"))) return;
                await Task.Run(() => Startup.Forget(new HashSet<int> { it.Index }));
                BindRemoved();
                txtStatus.Text = "SAVED COPY OF " + it.Name.ToUpperInvariant() + " DELETED";
            }
        }

        async void RestoreStartup()
        {
            var names = Startup.RemovedNames();
            if (names.Count == 0) { StartupHead(); return; }
            if (!(await Confirm("RESTORE REMOVED ENTRIES", "Put these back into Windows startup?\n\n" + string.Join("\n", names.Select(n => "• " + n)), "RESTORE", "CANCEL", "question"))) return;
            int failed = await Task.Run(() => Startup.RestoreAll());
            try { startup = await Task.Run(() => Startup.List()); } catch { startup = new List<StartupVm>(); }
            startupView = new System.Windows.Data.ListCollectionView(startup) { Filter = StartupMatch };
            itemsStartup.ItemsSource = startupView;
            StartupHead();
            BindRemoved();
            txtStatus.Text = failed == 0 ? "STARTUP ENTRIES RESTORED" : failed + " ENTRY(IES) COULD NOT BE RESTORED";
        }

        bool StartupMatch(object o)
        {
            var v = o as StartupVm; if (v == null) return false;
            if (startupFilter == "on" && !v.Enabled) return false;
            if (startupFilter == "off" && v.Enabled) return false;
            string q = txtStartupSearch == null ? "" : txtStartupSearch.Text.Trim();
            return q.Length == 0 || v.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || v.Publisher.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || v.Command.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void EnterBoost()
        {
            if (!boostInit)
            {
                boostInit = true;
                procView = new System.Windows.Data.ListCollectionView(procs);
                procView.IsLiveSorting = true;
                procView.Filter = ProcMatch;
                procView.SortDescriptions.Add(new System.ComponentModel.SortDescription("MemBytes", System.ComponentModel.ListSortDirection.Descending));
                procView.LiveSortingProperties.Add("MemBytes");
                gp.ItemsSource = procView;
                Mem.Cpu();
            }
            BoostTick();
            boostTm.Start();
        }

        bool startupLoaded;

        /// <summary>Opens the STARTUP page: the list is read from Windows the first time, then kept (REFRESH reads it again).</summary>
        async void EnterStartup()
        {
            if (!startupLoaded) await LoadStartup(); else StartupHead();
        }

        async Task LoadStartup()
        {
            startupLoaded = true;
            txtStartupCount.Text = "…";
            try { startup = await Task.Run(() => Startup.List()); } catch { startup = new List<StartupVm>(); }
            startupView = new System.Windows.Data.ListCollectionView(startup) { Filter = StartupMatch };
            itemsStartup.ItemsSource = startupView;
            StartupHead();
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
            var seen = new HashSet<int>(); var next = new Dictionary<int, TimeSpan>(); var fresh = new List<ProcVm>(); long thTotal = 0, hdTotal = 0;
            foreach (var s in snap)
            {
                seen.Add(s.Pid); next[s.Pid] = s.Cpu; thTotal += s.Threads; hdTotal += s.Handles;
                double cpu = 0; TimeSpan pv;
                if (prevCpuT.TryGetValue(s.Pid, out pv)) cpu = Math.Max(0, (s.Cpu - pv).TotalMilliseconds / dt / Environment.ProcessorCount * 100.0);
                ProcVm vm;
                if (!procMap.TryGetValue(s.Pid, out vm))
                {
                    vm = new ProcVm { Pid = s.Pid, Name = s.Name };
                    vm.MemBytes = s.Mem; vm.Cpu = cpu; vm.PrivBytes = s.Priv; vm.Threads = s.Threads; vm.Handles = s.Handles;
                    procMap[s.Pid] = vm; procs.Add(vm); fresh.Add(vm);
                }
                else { vm.MemBytes = s.Mem; vm.Cpu = cpu; vm.PrivBytes = s.Priv; vm.Threads = s.Threads; vm.Handles = s.Handles; }
            }
            prevCpuT = next;
            foreach (var pid in procMap.Keys.Where(k => !seen.Contains(k)).ToList()) { procs.Remove(procMap[pid]); procMap.Remove(pid); }
            txtProcSummary.Text = Loc.T(procs.Count + " processes  ·  " + thTotal.ToString("N0") + " threads  ·  " + hdTotal.ToString("N0") + " handles");
            if (fresh.Count > 0) ResolveProcs(fresh);
            var picked = SelProc(); if (picked != null) RefreshPdLive(picked);
        }

        ProcVm SelProc() { return gp.SelectedItem as ProcVm; }

        // ───────── process details ─────────
        TextBlock txtProcSummary, pdName, pdDesc, pdEmpty, pdNote, pdCpu, pdMem, pdThreads, pdHandles; Image pdIcon; ScrollViewer pdBody; ItemsControl pdRows;
        Button bPdFolder, bPdCopy; TextBox txtProcSearch; int pdId;

        void WireProcUx()
        {
            txtProcSummary = F<TextBlock>("txtProcSummary"); pdName = F<TextBlock>("pdName"); pdDesc = F<TextBlock>("pdDesc"); pdEmpty = F<TextBlock>("pdEmpty"); pdNote = F<TextBlock>("pdNote");
            pdCpu = F<TextBlock>("pdCpu"); pdMem = F<TextBlock>("pdMem"); pdThreads = F<TextBlock>("pdThreads"); pdHandles = F<TextBlock>("pdHandles");
            pdIcon = F<Image>("pdIcon"); pdBody = F<ScrollViewer>("pdBody"); pdRows = F<ItemsControl>("pdRows"); bPdFolder = F<Button>("bPdFolder"); bPdCopy = F<Button>("bPdCopy"); txtProcSearch = F<TextBox>("txtProcSearch");
            pdBody.PreviewMouseWheel += (s, e) => { pdBody.ScrollToVerticalOffset(pdBody.VerticalOffset - e.Delta); e.Handled = true; };
            txtProcSearch.TextChanged += (_, _) => { if (procView != null) procView.Refresh(); };
            gp.SelectionChanged += (_, _) => ShowProc();
            bPdFolder.Click += (_, _) =>
            {
                var p = SelProc(); if (p == null) return;
                try { if (p.Path.Length > 0 && File.Exists(p.Path)) Process.Start("explorer.exe", "/select,\"" + p.Path + "\""); else txtStatus.Text = "THIS PROCESS HAS NO FILE ON DISK"; } catch { }
            };
            bPdCopy.Click += (_, _) =>
            {
                var p = SelProc(); if (p == null || p.Path.Length == 0) { txtStatus.Text = "THIS PROCESS HAS NO FILE ON DISK"; return; }
                try { Clipboard.SetText(p.Path); txtStatus.Text = "PATH COPIED"; } catch { txtStatus.Text = "COULD NOT COPY"; }
            };
        }

        bool ProcMatch(object o)
        {
            var p = o as ProcVm; if (p == null) return false;
            string q = txtProcSearch == null ? "" : txtProcSearch.Text.Trim();
            if (q.Length == 0) return true;
            return p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || p.Description.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                || p.Company.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || p.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || p.Pid.ToString().StartsWith(q);
        }

        void RefreshPdLive(ProcVm p) { pdCpu.Text = p.CpuText; pdMem.Text = p.MemText; pdThreads.Text = p.Threads.ToString(); pdHandles.Text = p.Handles.ToString("N0"); }

        void PdHeader(ProcVm p)
        {
            pdIcon.Source = p.Icon; pdName.Text = p.Name;
            pdDesc.Text = (p.Description.Length > 0 ? p.Description : "No description") + (p.Company.Length > 0 ? "  ·  " + p.Company : "");
        }

        async void ShowProc()
        {
            var p = SelProc(); int id = ++pdId;
            if (p == null) { pdBody.Visibility = Visibility.Collapsed; pdEmpty.Visibility = Visibility.Visible; return; }
            pdEmpty.Visibility = Visibility.Collapsed; pdBody.Visibility = Visibility.Visible;
            PdHeader(p); RefreshPdLive(p);
            pdNote.Text = Procs.Protected.Contains(p.Name) || p.Pid <= 4 ? "Windows needs this process, so END TASK is blocked for it." : "";
            var rows = new List<HwRow>();
            if (p.Path.Length > 0) rows.Add(new HwRow { K = "FILE", V = p.Path });
            if (p.Version.Length > 0) rows.Add(new HwRow { K = "VERSION", V = p.Version });
            rows.Add(new HwRow { K = "PID", V = p.Pid.ToString() });
            rows.Add(new HwRow { K = "PRIVATE MEMORY", V = p.PrivText });
            pdRows.ItemsSource = rows;
            List<KeyValuePair<string, string>> d;
            try { d = await Task.Run(() => ProcInfo.Detail(p.Pid)); } catch { return; }
            if (id != pdId) return;                                                    // another process was selected meanwhile
            foreach (var kv in d) rows.Add(new HwRow { K = kv.Key, V = kv.Value });
            pdRows.ItemsSource = null; pdRows.ItemsSource = rows;
        }

        /// <summary>Looks up program, publisher and icon of processes that just appeared (in the background).</summary>
        async void ResolveProcs(List<ProcVm> list)
        {
            var input = list.Select(v => Tuple.Create(v.Pid, v.Name)).ToList();
            List<ProcInfo.Info> res;
            try { res = await Task.Run(() => ProcInfo.Resolve(input)); } catch { return; }
            var sel = SelProc();
            foreach (var r in res)
            {
                ProcVm vm;
                if (!procMap.TryGetValue(r.Pid, out vm)) continue;
                vm.Path = r.Path; vm.Description = r.Description; vm.Company = r.Company; vm.Version = r.Version; vm.Icon = r.Icon;
                if (sel != null && sel.Pid == r.Pid) PdHeader(vm);
            }
            if (procView != null && txtProcSearch.Text.Length > 0) procView.Refresh();
        }

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
