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
        // ── apps ──

        async void LoadApps()
        {
            appsLoaded = true; bRef.IsEnabled = false;
            txtStatus.Text = "ENUMERATING INSTALLED SOFTWARE…";
            List<AppItem> list;
            try { list = await Task.Run(() => Inventory.Load()); } catch { list = new List<AppItem>(); }
            apps = list; bRef.IsEnabled = true; txtStatus.Text = "READY";
            ApplyFilter();
            ShowApp();                                       // the removed app must not stay selected / actionable while icons load
            int gen = ++iconGen;
            var snap = apps.ToList();
            await Task.Run(() => Parallel.ForEach(snap, new ParallelOptions { MaxDegreeOfParallelism = 4 }, a => { if (gen == iconGen) a.Icon = IconLoader.For(a); }));
            ShowApp();
        }

        void ApplyFilter()
        {
            if (txtSearch == null) return;
            var q = txtSearch.Text.Trim();
            var l = apps.Where(a => (kindFilter == null || a.Kind == kindFilter)
                && (q.Length == 0 || a.Name.Has(q) || a.Publisher.Has(q))).ToList();
            ga.ItemsSource = l;
            txtCount.Text = l.Count + " ITEMS";
        }

        ItemsControl apRows, apAdv; Button bApFolder, bApCopy; ScrollViewer svApp;
        List<HwRow> apShown = new List<HwRow>();

        void WireAppDetails()
        {
            apRows = F<ItemsControl>("apRows"); apAdv = F<ItemsControl>("apAdv"); bApFolder = F<Button>("bApFolder"); bApCopy = F<Button>("bApCopy"); svApp = F<ScrollViewer>("svApp");
            svApp.PreviewMouseWheel += (s, e) => { svApp.ScrollToVerticalOffset(svApp.VerticalOffset - e.Delta); e.Handled = true; };
            bApFolder.Click += (_, _) =>
            {
                var a = ga.SelectedItem as AppItem; if (a == null) return;
                try { if (!string.IsNullOrEmpty(a.Location) && Directory.Exists(a.Location)) Process.Start("explorer.exe", "\"" + a.Location + "\""); else txtStatus.Text = "THIS APP HAS NO KNOWN INSTALL FOLDER"; } catch { }
            };
            bApCopy.Click += (_, _) =>
            {
                var a = ga.SelectedItem as AppItem; if (a == null) return;
                var sb = new StringBuilder(); sb.AppendLine(a.Name); if (!string.IsNullOrEmpty(a.Publisher)) sb.AppendLine(a.Publisher); sb.AppendLine();
                foreach (var r in apShown) sb.AppendLine(r.K.PadRight(26) + r.V);
                try { Clipboard.SetText(sb.ToString()); txtStatus.Text = "APP DETAILS COPIED"; } catch { txtStatus.Text = "COULD NOT COPY"; }
            };
        }

        void ShowApp()
        {
            var a = ga.SelectedItem as AppItem;
            apEmpty.Visibility = a == null ? Visibility.Visible : Visibility.Collapsed;
            apBody.Visibility = a == null ? Visibility.Collapsed : Visibility.Visible;
            if (a == null) return;
            apIcon.Source = a.Icon; apName.Text = a.Name;
            apPub.Text = string.IsNullOrEmpty(a.Publisher) ? "Unknown publisher" : a.Publisher;
            string ver = string.IsNullOrEmpty(a.Version) ? "—" : a.Version;
            string size = a.SizeBytes > 0 ? a.SizeText : a.MeasuredBytes > 0 ? Fmt(a.MeasuredBytes) : a.SizeText;
            var basic = new List<HwRow> { new HwRow { K = "TYPE", V = a.Kind }, new HwRow { K = "VERSION", V = ver }, new HwRow { K = "SIZE", V = size }, new HwRow { K = "INSTALLED", V = a.InstallText } };
            if (!string.IsNullOrEmpty(a.Location)) basic.Add(new HwRow { K = "LOCATION", V = a.Location });
            var adv = Inventory.Details(a);
            apShown = basic.Concat(adv).ToList();
            apRows.ItemsSource = basic; apAdv.ItemsSource = adv;
            if (!string.IsNullOrEmpty(a.Location) && a.MeasuredBytes < 0 && !a.Measuring)                 // measure the folder in the background, then refresh this panel
            {
                a.Measuring = true;
                Task.Run(() => Inventory.Measure(a)).ContinueWith(_ => w.Dispatcher.BeginInvoke(new Action(() => { a.Measuring = false; if (ga.SelectedItem == a) ShowApp(); })));
            }
        }

        async void DoUninstall()
        {
            if (uninstalling) { Dbg("DoUninstall ignored (already running)"); return; }
            if ((DateTime.UtcNow - lastUnEnd).TotalMilliseconds < 1500) { Dbg("DoUninstall ignored (repeat click right after the previous run)"); return; }
            uninstalling = true;
            Dbg("DoUninstall start");
            bUn.IsEnabled = false;                           // a disabled button cannot be re-triggered by a stray click or key
            try
            {
                var a = ga.SelectedItem as AppItem;
                if (a != null && !apps.Contains(a)) { Dbg("DoUninstall ignored: '" + a.Name + "' is no longer in the list"); a = null; ShowApp(); return; }
                if (a == null) { await Msg("SELECT AN APP", "Pick an app or game from the list first.", "info"); return; }
                if (a.Package == null && string.IsNullOrWhiteSpace(a.Uninstall)) { await Msg("CANNOT UNINSTALL", "Windows has no uninstaller registered for this item.", "warn"); return; }
                bool deep = chkLeft.IsChecked == true && a.Package == null;
                string q = "Uninstall  " + a.Name + "  ?" + (deep ? "\n\nAfterwards you will get a list of leftover files, registry entries, startup items and crash dumps to review - nothing extra is deleted without your approval." : "");
                if (!(await Confirm("CONFIRM UNINSTALL", q, "UNINSTALL", "CANCEL", "warn"))) { txtStatus.Text = "READY"; return; }

                Dbg("confirmed: " + a.Name);
                var stems = deep ? Leftovers.Stems(a) : null;      // executable names are read BEFORE the app disappears
                txtStatus.Text = "UNINSTALLING " + a.Name.ToUpper() + "…  (the app stays usable meanwhile)";
                bool failed = false;
                try { await RunSta(() => Inventory.Remove(a)); }
                catch (Exception ex) { failed = true; await Msg("UNINSTALL ERROR", ex.Message, "danger"); }

                bool gone = a.Package != null || !Inventory.StillInstalled(a);
                Dbg("Remove returned, failed=" + failed + " gone=" + gone);
                LoadApps();                                      // refresh the list (it re-enables the Refresh button itself)
                if (failed) txtStatus.Text = "UNINSTALL FAILED · " + a.Name.ToUpper();
                else if (!gone) txtStatus.Text = "UNINSTALL NOT COMPLETED · " + a.Name.ToUpper() + " IS STILL INSTALLED";
                else
                {
                    txtStatus.Text = "UNINSTALL FINISHED · refreshing the list…";
                    if (deep)
                    {
                        try { await ReviewLeftovers(a, stems); }   // leftovers are only offered once the app is really gone
                        catch (Exception ex)
                        {
                            Dbg("ReviewLeftovers failed: " + ex);
                            reviewEsc = null; HideReview();
                            txtStatus.Text = "UNINSTALLED · leftover review could not be shown";
                        }
                    }
                }
            }
            catch (Exception ex) { try { await Msg("UNINSTALL ERROR", ex.Message, "danger"); } catch { } }
            finally
            {
                uninstalling = false; bUn.IsEnabled = true; lastUnEnd = DateTime.UtcNow;
                Dbg("DoUninstall end");
                if (dlgPick == null && dlgHost.Visibility == Visibility.Visible) CollapseDlg();   // safety net: no dead overlay may survive an uninstall
            }
        }

        // ── apps: Store apps (PowerShell) are only read when you ask for them ──
        bool storeLoaded;
        void Chip(string k)
        {
            kindFilter = k;
            foreach (var (b, key) in new[] { (fAll, (string)null), (fApp, "APP"), (fGame, "GAME"), (fStore, "STORE") })
                b.Background = key == k ? Br("#333DD6F5") : Br("#111E31");
            if (k == "STORE" && !storeLoaded && appsLoaded) { storeLoaded = true; Inventory.IncludeStore = true; LoadApps(); }
            else ApplyFilter();
        }
    }
}
