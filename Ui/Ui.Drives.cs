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
        // ── drives ──
        void LoadDrives()
        {
            var list = new List<DriveVm>();
            foreach (var di in DriveInfo.GetDrives())
            {
                try
                {
                    if (!di.IsReady || (di.DriveType != DriveType.Fixed && di.DriveType != DriveType.Removable)) continue;
                    var old = drives.FirstOrDefault(x => x.Root == di.Name);
                    list.Add(old != null && (old.Scanned || old.Scanning) ? old : new DriveVm(di));
                }
                catch { }
            }
            drives = OrderDrives(list);
            itemsDrives.ItemsSource = null;
            itemsDrives.ItemsSource = drives;
            ApplyDiskBadges();
        }

        void OnDrive(DriveVm d)
        {
            if (d.Scanning) return;
            if (d.Scanned) OpenBrowser(d); else StartScan(d);
        }

        async void StartScan(DriveVm d)
        {
            if (d.Scanning) return;
            d.Scanning = true; d.Hint = "SCANNING…";
            d.Sc = new Scanner(); d.Cts = new CancellationTokenSource(); d.Sw = Stopwatch.StartNew();
            d.Node = new Node { Name = d.Letter, Path = d.Root };
            scanning.Add(d); tm.Start(); bStopAll.Visibility = Visibility.Visible;
            openAfter = d;
            txtStatus.Text = "SCANNING " + d.Letter + " · parallel enumeration active";
            var sc = d.Sc; var node = d.Node; var t = d.Cts.Token;
            try { await Task.Run(() => sc.Run(node, t)); }
            catch (Exception ex) { await Msg("SCAN ERROR", ex.Message, "danger"); }
            d.Sw.Stop(); d.Scanning = false; scanning.Remove(d);
            if (scanning.Count == 0) { tm.Stop(); bStopAll.Visibility = Visibility.Collapsed; }
            if (t.IsCancellationRequested) { d.Hint = "⚡ CLICK TO SCAN"; txtStatus.Text = "SCAN STOPPED"; return; }
            d.Finish();
            txtStatus.Text = "READY";
            if (openAfter == d && vDrives.Visibility == Visibility.Visible) OpenBrowser(d);
        }
    }
}
