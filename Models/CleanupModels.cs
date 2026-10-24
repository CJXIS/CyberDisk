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
    // ───────────────────────── Cleanup ─────────────────────────
    sealed class CleanItem : Obs
    {
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Glyph { get; set; }
        public string Group { get; set; }
        public Brush Accent { get; set; }
        public string Badge { get; private set; } = "";
        public Brush BadgeBrush { get; private set; } = Brushes.Transparent;
        public int Kind;                              // 0 = delete paths, 1 = Recycle Bin, 2 = Windows Disk Cleanup handler, 3 = command
        public string HandlerKey, Cmd, Browser;
        public Func<IEnumerable<string>> Roots = () => new string[0];
        public Func<IEnumerable<string>> SingleFiles = () => new string[0];
        public Func<string, bool> Filter;
        public bool Recursive = true;
        public long Files;
        public Action OnChanged;
        public bool DefaultOn;
        bool chk; long size; double pct;
        string sizeText = "—", status = "not analyzed yet";
        Brush statusBrush = new SolidColorBrush(Ui.Col("#8195B0"));
        public bool Checked { get => chk; set { Set(ref chk, value); if (OnChanged != null) OnChanged(); } }
        public long Size { get => size; set { size = value; SizeText = value > 0 ? Ui.Fmt(value) : "—"; } }
        public string SizeText { get => sizeText; set => Set(ref sizeText, value); }
        public string Status { get => status; set => Set(ref status, value); }
        public double Percent { get => pct; set => Set(ref pct, value); }
        public Brush StatusBrush { get => statusBrush; set => Set(ref statusBrush, value); }
        public long Weight => Size > 0 ? Size : 160L * 1024 * 1024;     // progress weight for items whose size Windows computes itself
        public void SetRisk(string risk)
        {
            Badge = risk ?? "";
            string c = risk == "SAFE" ? "#3DDC97" : risk == "ADVANCED" ? "#FF6B8A" : risk == "SIGN-OUT" ? "#FFB84D" : "#8B7CFF";
            BadgeBrush = new SolidColorBrush(Ui.Col(c));
        }
    }

    sealed class SiteVm { public string Host { get; set; } public string SizeText { get; set; } public double Percent { get; set; } }

    sealed class BrowserVm : Obs
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public bool IsFirefox;
        public string[] Procs;
        public List<string> ProfileDirs = new List<string>();
        public List<CleanItem> Types { get; set; } = new List<CleanItem>();
        public ImageSource Icon { get; set; }
        List<SiteVm> sites = new List<SiteVm>();
        bool hasSites; string total = "—", running = "";
        public List<SiteVm> Sites { get => sites; set => Set(ref sites, value); }
        public bool HasSites { get => hasSites; set => Set(ref hasSites, value); }
        public string TotalText { get => total; set => Set(ref total, value); }
        public string RunningText { get => running; set => Set(ref running, value); }
        public string DisplayName => Name;
        public bool Selected
        {
            get { return Types.Any(x => x.Checked); }
            set { foreach (var t in Types) t.Checked = value && t.DefaultOn; Raise("Selected"); }
        }
        public void NotifySelected() { Raise("Selected"); }
        public bool IsRunning() { try { return Procs.Any(p => Process.GetProcessesByName(p).Length > 0); } catch { return false; } }
        public void Close() { foreach (var n in Procs) foreach (var p in Process.GetProcessesByName(n)) { try { p.CloseMainWindow(); } catch { } } }
        public void Refresh()
        {
            long t = Types.Sum(x => x.Size);
            TotalText = t > 0 ? Ui.Fmt(t) : "—";
            RunningText = IsRunning() ? "● running - close it for best results" : "";
        }
    }
}
