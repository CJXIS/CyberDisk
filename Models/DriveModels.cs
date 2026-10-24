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
    // ───────────────────────── View models ─────────────────────────
    sealed class CatVm { public string Name { get; set; } public string SizeText { get; set; } public double Percent { get; set; } public Brush Color { get; set; } }

    sealed class DriveVm : Obs
    {
        public string Letter { get; set; }
        public string Title { get; set; }
        public string Meta { get; set; }
        public string UsedText { get; set; }
        public string FreeText { get; set; }
        public string PctText { get; set; }
        public Geometry Ring { get; set; }
        public Brush RingBrush { get; set; }
        public string Root;
        public Node Node; public Scanner Sc; public CancellationTokenSource Cts; public Stopwatch Sw;

        string hint = "⚡ CLICK TO SCAN", summary, heaviest;
        bool scanned, scanning;
        List<CatVm> cats;
        public string Hint { get => hint; set => Set(ref hint, value); }
        public string Summary { get => summary; set => Set(ref summary, value); }
        public string Heaviest { get => heaviest; set => Set(ref heaviest, value); }
        public bool Scanned { get => scanned; set => Set(ref scanned, value); }
        public bool Scanning { get => scanning; set => Set(ref scanning, value); }
        public List<CatVm> Cats { get => cats; set => Set(ref cats, value); }
        string diskBadge; Brush diskTone;
        public string DiskBadge { get => diskBadge; set => Set(ref diskBadge, value); }     // e.g. "● EXCELLENT · NVMe SSD · 38°C"
        public Brush DiskTone { get => diskTone; set => Set(ref diskTone, value); }

        public DriveVm(DriveInfo di)
        {
            Root = di.Name;
            Letter = di.Name.Substring(0, 2);
            Title = string.IsNullOrEmpty(di.VolumeLabel) ? "Local Disk" : di.VolumeLabel;
            Meta = di.DriveFormat + " · " + di.DriveType.ToString().ToUpper();
            long tot = di.TotalSize, free = di.TotalFreeSpace, used = tot - free;
            double pct = tot > 0 ? 100.0 * used / tot : 0;
            PctText = pct.ToString("0") + "%";
            UsedText = Ui.Fmt(used) + " / " + Ui.Fmt(tot);
            FreeText = Ui.Fmt(free) + " FREE";
            Ring = Arc(pct);
            string c1 = pct > 90 ? "#FF6B8A" : pct > 75 ? "#FFB84D" : "#3DD6F5";
            string c2 = pct > 90 ? "#FF9F6B" : pct > 75 ? "#FF6B8A" : "#8B7CFF";
            var b = new LinearGradientBrush(Ui.Col(c1), Ui.Col(c2), 45); b.Freeze();
            RingBrush = b;
        }

        public static Geometry Arc(double pct)
        {
            double a = Math.Min(Math.Max(pct, 0.6), 99.9) / 100 * 2 * Math.PI;
            const double R = 55, C = 60;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(C, C - R), false, false);
                c.ArcTo(new Point(C + R * Math.Sin(a), C - R * Math.Cos(a)), new Size(R, R), 0, a > Math.PI, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }

        public void Finish()
        {
            Summary = Sc.Files.ToString("N0") + " files · " + Sc.Dirs.ToString("N0") + " folders · " + Sw.Elapsed.TotalSeconds.ToString("0.0") + "s";
            var top = Node.Kids.OrderByDescending(k => k.Size).FirstOrDefault();
            Heaviest = top == null ? "" : "HEAVIEST ▸ " + top.Name + "  " + Ui.Fmt(top.Size);
            long tot = Sc.Cat.Sum();
            var cs = Sc;
            Cats = Enumerable.Range(0, 8).Where(i => cs.Cat[i] > 0).OrderByDescending(i => cs.Cat[i]).Select(i => new CatVm
            {
                Name = Cls.Names[i], SizeText = Ui.Fmt(cs.Cat[i]), Percent = tot > 0 ? 100.0 * cs.Cat[i] / tot : 0,
                Color = new SolidColorBrush(Ui.Col(Cls.Colors[i]))
            }).ToList();
            Scanned = true;
            Hint = "OPEN ▶  click to browse";
        }
    }
}
