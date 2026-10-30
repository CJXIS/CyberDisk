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
    // ───────────────────────── Ui: "My PC" hardware page ─────────────────────────

        FrameworkElement vPc;
        Button navPc, bPcRefresh, bPcAdv, bPcExport;
        ScrollViewer svPc;
        ItemsControl itemsHw;
        TextBlock txtPcLoading; TextBox txtPcName, txtPcModel, txtPcOs, txtPcSummary;
        FrameworkElement pcContent;
        HwReport hw;
        DispatcherTimer tempTm; int tempTicks; bool tempBusy;

        /// <summary>While MY PC is open the temperature rows refresh every two seconds (drive sensors every minute).</summary>
        void StartTempTimer()
        {
            if (tempTm != null) return;
            tempTm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            tempTm.Tick += async (_, _) =>
            {
                if (tempBusy || hw == null || vPc.Visibility != Visibility.Visible || !w.IsActive) return;
                var card = hw.Cards.FirstOrDefault(c => c.LiveRows.Count > 0); if (card == null) return;
                tempBusy = true;
                try { tempTicks++; bool disks = tempTicks % 30 == 0; var rs = await Task.Run(() => Thermals.Read(disks)); Thermals.Fill(card, rs, false); }
                catch { }
                finally { tempBusy = false; }
            };
            tempTm.Start();
        }

        Task<HwReport> hwPre;                                   // read quietly in the background shortly after start, so MY PC opens at once

        /// <summary>A few seconds after start (when the PC is idle) the hardware and disk data are read once in the background.</summary>
        void StartPrewarm()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                if (hw == null && hwPre == null) hwPre = Task.Run(() => { try { return SystemInfo.Collect(); } catch { return (HwReport)null; } });
                Task.Run(() => { try { DiskInfo.Get(300); } catch { } });
            };
            t.Start();
        }
        bool pcAdv, pcLoading;

        void WirePc()
        {
            vPc = F<FrameworkElement>("vPc"); navPc = F<Button>("navPc"); svPc = F<ScrollViewer>("svPc"); itemsHw = F<ItemsControl>("itemsHw");
            bPcRefresh = F<Button>("bPcRefresh"); bPcAdv = F<Button>("bPcAdv"); bPcExport = F<Button>("bPcExport");
            txtPcLoading = F<TextBlock>("txtPcLoading"); txtPcName = F<TextBox>("txtPcName"); txtPcModel = F<TextBox>("txtPcModel"); pcContent = F<FrameworkElement>("pcContent");
            txtPcOs = F<TextBox>("txtPcOs"); txtPcSummary = F<TextBox>("txtPcSummary");

            navPc.Click += (_, _) => { Go(vPc); EnterPc(); };
            bPcRefresh.Click += async (_, _) => { hwPre = null; DiskInfo.Invalidate(); await LoadPc(); };
            bPcAdv.Click += (_, _) =>
            {
                pcAdv = !pcAdv;
                if (hw != null) foreach (var c in hw.Cards) c.ShowAdv = pcAdv;
                bPcAdv.Content = IconLabel(pcAdv ? Vec.Back : Vec.Settings, pcAdv ? "BASIC" : "ADVANCED");
            };
            bPcAdv.Content = IconLabel(Vec.Settings, "ADVANCED");
            svPc.PreviewMouseWheel += (s, e) => { svPc.ScrollToVerticalOffset(svPc.VerticalOffset - e.Delta); e.Handled = true; };
            itemsHw.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(MasonryPanel)));          // columns that fill up evenly
            svPc.SizeChanged += (_, _) => FitPcCards();
            bPcExport.Click += (_, _) =>
            {
                if (hw == null) { txtStatus.Text = "THE HARDWARE INFORMATION IS STILL LOADING"; return; }
                ShowExportMenu(bPcExport, "PC hardware report", () => hw.PcName + "  ·  " + hw.Model + "  ·  " + hw.Os, pcContent, () => SystemInfo.ToText(hw), "CyberDisk-PC");
            };
        }

        async void EnterPc() { if (hw == null) await LoadPc(); }

        async Task LoadPc()
        {
            if (pcLoading) return;
            pcLoading = true; bPcRefresh.IsEnabled = false;
            txtPcLoading.Text = "Reading your hardware…"; txtPcLoading.Visibility = Visibility.Visible; svPc.Visibility = Visibility.Collapsed;
            txtStatus.Text = "READING HARDWARE…";
            HwReport rep;
            try
            {
                var pre = hwPre; hwPre = null; rep = null;
                if (pre != null) rep = await pre;                                       // already read in the background: no waiting
                if (rep == null) rep = await Task.Run(() => SystemInfo.Collect());
            }
            catch (Exception ex) { rep = null; txtPcLoading.Text = "Could not read the hardware information: " + ex.Message; }
            pcLoading = false; bPcRefresh.IsEnabled = true;
            if (rep == null) return;
            hw = rep;
            foreach (var c in hw.Cards) c.ShowAdv = pcAdv;
            txtPcName.Text = hw.PcName;
            txtPcModel.Text = hw.Model.Length > 0 ? hw.Model : "Custom-built PC";
            txtPcOs.Text = hw.Os + (hw.Uptime.Length > 0 ? "  ·  up " + hw.Uptime : "");
            txtPcSummary.Text = hw.Summary;
            itemsHw.ItemsSource = null; itemsHw.ItemsSource = hw.Cards; FitPcCards(); StartTempTimer();
            txtPcLoading.Visibility = Visibility.Collapsed; svPc.Visibility = Visibility.Visible;
            txtStatus.Text = "HARDWARE INFORMATION READY";
        }

        /// <summary>Two cards per row on a wide window, one on a narrow one - the cards always fill the row evenly.</summary>
        void FitPcCards()
        {
            if (hw == null) return;
            double avail = svPc.ViewportWidth - 4; if (avail <= 0) return;
            int cols = avail >= 940 ? 2 : 1;
            double wCard = Math.Floor((avail - 16 * cols) / cols);
            foreach (var c in hw.Cards) c.CardWidth = Math.Max(300, wCard);
        }
    }
}
