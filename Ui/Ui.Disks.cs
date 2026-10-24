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
    // ───────────────────────── Ui: disk health & info page ─────────────────────────

        FrameworkElement vDisk, panelDiskAdv;
        Button bDiskBack, bDiskAdv, bDiskRefresh, bDiskExport;
        FrameworkElement diskContent; TextBlock txtDiskNoVols; ItemsControl itemsDiskParts;
        Panel diskTabs;
        ScrollViewer svDisk;
        TextBlock txtDiskLoading, txtDiskScore, txtDiskGrade, txtDiskName, txtDiskSub, txtDiskVerdict, txtDiskNoSmart;
        System.Windows.Shapes.Path diskArc;
        ItemsControl itemsDiskTiles, itemsDiskIssues, itemsDiskVols, itemsDiskId, itemsDiskSmart;
        List<PhysDisk> physDisks;
        PhysDisk curDisk;
        bool diskAdv, diskLoading;

        void WireDisks()
        {
            vDisk = F<FrameworkElement>("vDisk"); panelDiskAdv = F<FrameworkElement>("panelDiskAdv");
            bDiskBack = F<Button>("bDiskBack"); bDiskAdv = F<Button>("bDiskAdv"); bDiskRefresh = F<Button>("bDiskRefresh");
            diskTabs = F<Panel>("diskTabs"); svDisk = F<ScrollViewer>("svDisk");
            txtDiskLoading = F<TextBlock>("txtDiskLoading"); txtDiskScore = F<TextBlock>("txtDiskScore"); txtDiskGrade = F<TextBlock>("txtDiskGrade");
            txtDiskName = F<TextBlock>("txtDiskName"); txtDiskSub = F<TextBlock>("txtDiskSub"); txtDiskVerdict = F<TextBlock>("txtDiskVerdict"); txtDiskNoSmart = F<TextBlock>("txtDiskNoSmart");
            diskArc = F<System.Windows.Shapes.Path>("diskArc");
            itemsDiskTiles = F<ItemsControl>("itemsDiskTiles"); itemsDiskIssues = F<ItemsControl>("itemsDiskIssues"); itemsDiskVols = F<ItemsControl>("itemsDiskVols");
            itemsDiskId = F<ItemsControl>("itemsDiskId"); itemsDiskSmart = F<ItemsControl>("itemsDiskSmart");

            bDiskExport = F<Button>("bDiskExport"); diskContent = F<FrameworkElement>("diskContent"); txtDiskNoVols = F<TextBlock>("txtDiskNoVols"); itemsDiskParts = F<ItemsControl>("itemsDiskParts");
            svDisk.PreviewMouseWheel += (s, e) => { svDisk.ScrollToVerticalOffset(svDisk.VerticalOffset - e.Delta); e.Handled = true; };
            bDiskExport.Click += (_, _) => ShowExportMenu(bDiskExport, "Disk health report", () => curDisk == null ? "" : DiskSubtitle(curDisk), diskContent, DiskText, "CyberDisk-Disk" + (curDisk == null ? "" : curDisk.Number.ToString()));
            bDiskBack.Click += (_, _) => Go(vDrives);
            bDiskAdv.Click += (_, _) => { diskAdv = !diskAdv; ApplyDiskAdvanced(); };
            bDiskAdv.Content = IconLabel(Vec.Settings, "ADVANCED");
            bDiskRefresh.Click += async (_, _) => { await LoadDisks(true); BuildDiskTabs(curDisk == null ? -1 : curDisk.Number); };
        }

        /// <summary>Opens the disk page, preselecting the physical disk that holds <paramref name="letter"/> (or the first disk).</summary>
        async void ShowDisks(string letter)
        {
            Go(vDisk);
            if (physDisks == null) await LoadDisks(false);
            int no = string.IsNullOrEmpty(letter) ? -1 : Native.DiskNumberOfLetter(letter);          // ask Windows which disk holds this drive letter
            var pick = no >= 0 ? physDisks.FirstOrDefault(d => d.Number == no) : null;
            if (pick == null && !string.IsNullOrEmpty(letter)) pick = physDisks.FirstOrDefault(d => d.Volumes.Any(v => string.Equals(v.Letter, letter, StringComparison.OrdinalIgnoreCase)));
            BuildDiskTabs(pick != null ? pick.Number : -1);
        }

        async Task LoadDisks(bool force)
        {
            if (diskLoading) return;
            diskLoading = true;
            txtDiskLoading.Text = "Reading your drives…"; txtDiskLoading.Visibility = Visibility.Visible; svDisk.Visibility = Visibility.Collapsed;
            txtStatus.Text = "READING DRIVE HEALTH…";
            try { physDisks = await Task.Run(() => DiskInfo.Get(force ? 0 : 120)); }
            catch { physDisks = new List<PhysDisk>(); }
            diskLoading = false;
            txtStatus.Text = "READY";
        }

        void BuildDiskTabs(int selectNumber)
        {
            diskTabs.Children.Clear();
            if (physDisks == null || physDisks.Count == 0)
            {
                txtDiskLoading.Text = "No drive information could be read on this PC.";
                txtDiskLoading.Visibility = Visibility.Visible; svDisk.Visibility = Visibility.Collapsed;
                return;
            }
            var neon = (Style)w.FindResource("Neon");
            foreach (var d in physDisks)
            {
                var disk = d;
                string caption = "DISK " + d.Number + (d.Letters.Length > 0 ? "  ·  " + d.Letters : "") + "  ·  " + d.Kind;
                var b = new Button { Content = caption, Style = neon, Tag = d.Number, Padding = new Thickness(14, 5, 14, 5) };
                b.Click += (_, _) => SelectDisk(disk);
                diskTabs.Children.Add(b);
            }
            diskTabs.Visibility = physDisks.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            SelectDisk(physDisks.FirstOrDefault(d => d.Number == selectNumber) ?? physDisks[0]);
        }

        void SelectDisk(PhysDisk d)
        {
            curDisk = d;
            foreach (var b in diskTabs.Children.OfType<Button>()) b.Background = Br(b.Tag is int && (int)b.Tag == d.Number ? "#333DD6F5" : "#111E31");
            FillDisk(d);
        }

        static Brush LevelBrush(int level) { return Br(ScoreTheme.Level(level)); }

        void FillDisk(PhysDisk d)
        {
            txtDiskLoading.Visibility = Visibility.Collapsed; svDisk.Visibility = Visibility.Visible;
            ScoreTheme.StyleRing(diskArc, d.Score, d.Score);
            txtDiskScore.Text = d.Score.ToString();
            txtDiskGrade.Text = d.Grade; txtDiskGrade.Foreground = ScoreTheme.Solid(d.Score);
            txtDiskName.Text = string.IsNullOrWhiteSpace(d.Model) ? "Disk " + d.Number : Regex.Replace(d.Model, @"\s+", " ").Trim();
            txtDiskSub.Text = string.Join("  ·  ", new[] { d.Kind, Fmt(d.Size), d.Bus, d.Letters }.Where(x => !string.IsNullOrEmpty(x)));
            txtDiskVerdict.Text = d.Verdict; txtDiskVerdict.Foreground = ScoreTheme.Solid(d.Score);

            // key metrics
            var tiles = new List<TileVm>();
            int hl = string.Equals(d.Health, "Healthy", StringComparison.OrdinalIgnoreCase) ? 0 : string.Equals(d.Health, "Warning", StringComparison.OrdinalIgnoreCase) ? 1 : d.Health.Length == 0 ? 3 : 2;
            tiles.Add(new TileVm { Label = "WINDOWS STATUS", Value = d.Health.Length > 0 ? d.Health : "—", Sub = d.Operational.Length > 0 ? d.Operational : "not reported", Brush = hl == 3 ? Br("#E6EEFA") : LevelBrush(hl) });
            int tl = !d.TempC.HasValue ? 3 : d.TempC.Value >= 70 ? 2 : d.TempC.Value >= 55 ? 1 : 0;
            tiles.Add(new TileVm { Label = "TEMPERATURE", Value = d.TempC.HasValue ? d.TempC + " °C" : "—", Sub = d.TempMaxC.HasValue && d.TempMaxC.Value > 0 ? "max " + d.TempMaxC + " °C" : "not reported", Brush = tl == 3 ? Br("#E6EEFA") : LevelBrush(tl) });
            if (d.WearPct.HasValue) tiles.Add(new TileVm { Label = "LIFE LEFT", Value = (100 - d.WearPct.Value) + "%", Sub = "wear " + d.WearPct + "%", Brush = LevelBrush(d.WearPct.Value >= 90 ? 2 : d.WearPct.Value >= 70 ? 1 : 0) });
            else tiles.Add(new TileVm { Label = "LIFE LEFT", Value = "—", Sub = d.IsSsd ? "not reported" : "not applicable (HDD)", Brush = Br("#E6EEFA") });
            string age = "—", ageSub = "not reported";
            if (d.PowerOnHours.HasValue) { long h = d.PowerOnHours.Value; age = h.ToString("N0") + " h"; ageSub = h >= 8760 ? (h / 8760.0).ToString("0.0") + " years" : (h / 24) + " days"; }
            tiles.Add(new TileVm { Label = "POWER-ON TIME", Value = age, Sub = ageSub, Brush = Br("#E6EEFA") });
            long errs = (d.ReadErrors ?? 0) + (d.WriteErrors ?? 0);
            tiles.Add(new TileVm { Label = "READ / WRITE ERRORS", Value = errs.ToString("N0"), Sub = (d.ReadErrors ?? 0) + " read  ·  " + (d.WriteErrors ?? 0) + " write", Brush = LevelBrush(errs > 100 ? 2 : errs > 0 ? 1 : 0) });
            tiles.Add(new TileVm { Label = "SMART PREDICTION", Value = d.PredictFailure ? "FAILING" : d.Smart.Count > 0 ? "PASSED" : "—", Sub = d.Smart.Count > 0 ? d.Smart.Count + " attributes" : "no SMART table", Brush = d.PredictFailure ? LevelBrush(2) : d.Smart.Count > 0 ? LevelBrush(0) : Br("#E6EEFA") });
            itemsDiskTiles.ItemsSource = tiles;

            // what affects the score
            var issues = d.Issues.Select(i => new IssueVm { Glyph = i.Level == 2 ? "✖" : "!", Text = i.Text, Penalty = "−" + i.Penalty, Tone = LevelBrush(i.Level) }).ToList();
            if (issues.Count == 0) issues.Add(new IssueVm { Glyph = "✓", Text = "No problems found - all health indicators are normal.", Penalty = "", Tone = LevelBrush(0) });
            itemsDiskIssues.ItemsSource = issues;

            // volumes
            var vols = d.Volumes.Select(v =>
            {
                double used = v.Size > 0 ? 100.0 * (v.Size - v.Free) / v.Size : 0;
                return new VolVm
                {
                    Letter = v.Letter, Label = string.IsNullOrEmpty(v.Label) ? "Local Disk" : v.Label, Format = v.Format,
                    Info = v.Size > 0 ? Fmt(v.Free) + " free of " + Fmt(v.Size) : "Not ready",
                    Used = v.Size > 0 ? used.ToString("0") + "% used" : "", Percent = used,
                    Brush = ScoreTheme.Gradient((int)Math.Max(0, 100 - used))
                };
            }).ToList();
            txtDiskNoVols.Visibility = vols.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            itemsDiskVols.ItemsSource = vols;

            // advanced: identity table and SMART attributes
            var id = new List<HwRow>
            {
                new HwRow { K = "Model", V = d.Model }, new HwRow { K = "Serial number", V = d.Serial }, new HwRow { K = "Firmware", V = d.Firmware },
                new HwRow { K = "Disk number", V = d.Number.ToString() }, new HwRow { K = "Capacity", V = Fmt(d.Size) },
                new HwRow { K = "Bus / interface", V = d.Bus.Length > 0 ? d.Bus : d.Interface }, new HwRow { K = "Media type", V = d.Media.Length > 0 ? d.Media : d.Kind },
                new HwRow { K = "Partition style", V = d.PartStyle }, new HwRow { K = "Partitions", V = d.Partitions.ToString() },
                new HwRow { K = "Sector size (logical / physical)", V = (d.LogicalSector.HasValue ? d.LogicalSector + " B" : "—") + " / " + (d.PhysicalSector.HasValue ? d.PhysicalSector + " B" : "—") },
                new HwRow { K = "Spindle speed", V = d.Rpm.HasValue ? d.Rpm + " RPM" : d.IsSsd ? "Solid state (no moving parts)" : "—" },
                new HwRow { K = "Windows health status", V = (d.Health + "  " + d.Operational).Trim() },
                new HwRow { K = "Start / stop cycles", V = d.StartStops.HasValue ? d.StartStops.Value.ToString("N0") : "—" },
                new HwRow { K = "Load / unload cycles", V = d.LoadCycles.HasValue ? d.LoadCycles.Value.ToString("N0") : "—" },
            };
            itemsDiskId.ItemsSource = id.Select(r => new HwRow { K = r.K, V = string.IsNullOrWhiteSpace(r.V) ? "—" : r.V }).ToList();
            itemsDiskParts.ItemsSource = d.Parts.OrderBy(x => x.No).Select(x => new HwRow { K = "#" + x.No, V = x.Type + (x.Boot ? "  ·  boot" : ""), W = Fmt(x.Size) }).ToList();
            var important = new HashSet<int> { 5, 187, 197, 198, 199 };
            itemsDiskSmart.ItemsSource = d.Smart.OrderBy(a => a.Id).Select(a => new SmartVm
            {
                Id = a.Id, Name = a.Name, Current = a.Current, Worst = a.Worst, Raw = a.Raw,
                Tone = important.Contains(a.Id) && (a.Raw & 0xFFFFFFFFL) > 0 ? LevelBrush(1) : Br("#E6EEFA")
            }).ToList();
            txtDiskNoSmart.Visibility = d.Smart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ApplyDiskAdvanced();
        }

        void ApplyDiskAdvanced()
        {
            panelDiskAdv.Visibility = diskAdv ? Visibility.Visible : Visibility.Collapsed;
            bDiskAdv.Content = IconLabel(diskAdv ? Vec.Back : Vec.Settings, diskAdv ? "BASIC" : "ADVANCED");
        }

        string DiskSubtitle(PhysDisk d)
        {
            return (string.IsNullOrWhiteSpace(d.Model) ? "Disk " + d.Number : Regex.Replace(d.Model, @"\s+", " ").Trim()) + "  ·  " + d.Kind + "  ·  " + Fmt(d.Size) + "  ·  health " + d.Score + "/100 (" + d.Grade + ")";
        }

        /// <summary>Plain-text version of the disk report (for TXT export and "copy all").</summary>
        string DiskText()
        {
            var d = curDisk; if (d == null) return "";
            var sb = new StringBuilder();
            sb.AppendLine("CyberDisk - disk health report"); sb.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "   PC: " + Environment.MachineName); sb.AppendLine(new string('=', 70));
            sb.AppendLine(DiskSubtitle(d)); sb.AppendLine(d.Verdict); sb.AppendLine();
            sb.AppendLine("Windows status: " + d.Health + " " + d.Operational);
            sb.AppendLine("Temperature:    " + (d.TempC.HasValue ? d.TempC + " °C" : "—"));
            sb.AppendLine("Life left:      " + (d.WearPct.HasValue ? (100 - d.WearPct.Value) + "%" : "—"));
            sb.AppendLine("Power-on hours: " + (d.PowerOnHours.HasValue ? d.PowerOnHours.Value.ToString("N0") : "—"));
            sb.AppendLine("R/W errors:     " + (d.ReadErrors ?? 0) + " read, " + (d.WriteErrors ?? 0) + " write");
            sb.AppendLine(); sb.AppendLine("What affects the score:");
            if (d.Issues.Count == 0) sb.AppendLine("  No problems found."); else foreach (var i in d.Issues) sb.AppendLine("  -" + i.Penalty + "  " + i.Text);
            sb.AppendLine(); sb.AppendLine("Volumes:");
            if (d.Volumes.Count == 0) sb.AppendLine("  (none)"); foreach (var v in d.Volumes) sb.AppendLine("  " + v.Letter + "  " + v.Label + "  " + Fmt(v.Free) + " free of " + Fmt(v.Size) + "  " + v.Format);
            sb.AppendLine(); sb.AppendLine("Details:"); foreach (var r in (itemsDiskId.ItemsSource as IEnumerable<HwRow>) ?? new HwRow[0]) sb.AppendLine("  " + r.K.PadRight(34) + r.V);
            if (d.Smart.Count > 0) { sb.AppendLine(); sb.AppendLine("SMART attributes (id, name, value, worst, raw):"); foreach (var a in d.Smart.OrderBy(x => x.Id)) sb.AppendLine("  " + a.Id.ToString().PadRight(4) + a.Name.PadRight(34) + a.Current.ToString().PadRight(6) + a.Worst.ToString().PadRight(6) + a.Raw); }
            return sb.ToString();
        }

        /// <summary>Adds the "● EXCELLENT · NVMe SSD · 38°C" line to every drive card once the disk data is ready.</summary>
        async void ApplyDiskBadges()
        {
            try
            {
                var list = await Task.Run(() => DiskInfo.Get(300));
                physDisks = list;
                foreach (var dv in drives)
                {
                    var pd = list.FirstOrDefault(p => p.Volumes.Any(v => string.Equals(v.Letter, dv.Letter, StringComparison.OrdinalIgnoreCase)));
                    if (pd == null) continue;
                    dv.DiskBadge = "● " + pd.Grade + "  ·  " + pd.Kind + (pd.TempC.HasValue ? "  ·  " + pd.TempC + "°C" : "");
                    dv.DiskTone = ScoreTheme.Solid(pd.Score);
                }
            }
            catch { }
        }
    }
}
