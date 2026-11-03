using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
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
    // ───────────────────────── What a running process really is: program, publisher, icon, parent, services ─────────────────────────
    static class ProcInfo
    {
        public sealed class Info { public int Pid; public string Path = "", Description = "", Company = "", Version = ""; public ImageSource Icon; }
        sealed class FileDesc { public string Description = "", Company = "", Version = ""; public ImageSource Icon; }

        static readonly ConcurrentDictionary<string, FileDesc> cache = new ConcurrentDictionary<string, FileDesc>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, string> Builtin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "system", "NT Kernel & System" }, { "idle", "System Idle Process (free CPU time)" }, { "registry", "Windows Registry" }, { "memory compression", "Compressed memory store" },
            { "secure system", "Virtualization-based security" }
        };

        static FileDesc Describe(string path)
        {
            return cache.GetOrAdd(path, p =>
            {
                var d = new FileDesc();
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(p);
                    d.Description = (vi.FileDescription ?? "").Trim(); d.Company = (vi.CompanyName ?? "").Trim();
                    d.Version = !string.IsNullOrWhiteSpace(vi.ProductVersion) ? vi.ProductVersion.Trim() : (vi.FileVersion ?? "").Trim();
                }
                catch { }
                try { d.Icon = ShellIcons.FromFile(p, 0); } catch { }
                if (d.Icon == null) { try { d.Icon = ShellIcons.Get(".exe", false).Icon; } catch { } }
                return d;
            });
        }

        /// <summary>Program, publisher and icon for new processes (runs in the background; every file is looked at only once).</summary>
        public static List<Info> Resolve(List<Tuple<int, string>> procs)
        {
            var res = new List<Info>();
            foreach (var t in procs)
            {
                var i = new Info { Pid = t.Item1 };
                try
                {
                    i.Path = Native.ProcessPath(t.Item1);
                    if (i.Path.Length > 0) { var d = Describe(i.Path); i.Description = d.Description; i.Company = d.Company; i.Version = d.Version; i.Icon = d.Icon; }
                    else
                    {
                        string b; i.Description = Builtin.TryGetValue(t.Item2, out b) ? b : "";
                        if (i.Description.Length > 0) i.Company = "Microsoft Windows";
                        try { i.Icon = ShellIcons.Get(".exe", false).Icon; } catch { }
                    }
                }
                catch { }
                res.Add(i);
            }
            return res;
        }

        static string Span(TimeSpan t)
        {
            if (t.TotalDays >= 1) return (int)t.TotalDays + " d " + t.Hours + " h";
            if (t.TotalHours >= 1) return (int)t.TotalHours + " h " + t.Minutes + " min";
            if (t.TotalMinutes >= 1) return (int)t.TotalMinutes + " min " + t.Seconds + " s";
            return t.TotalSeconds.ToString("0.0") + " s";
        }

        /// <summary>The slower facts about one process, read only when it is selected.</summary>
        public static List<KeyValuePair<string, string>> Detail(int pid)
        {
            var rows = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (k, v) => { if (!string.IsNullOrWhiteSpace(v)) rows.Add(new KeyValuePair<string, string>(k, v.Trim())); };
            try
            {
                using (var pr = Process.GetProcessById(pid))
                {
                    try { var st = pr.StartTime; add("STARTED", st.ToString("yyyy-MM-dd HH:mm") + "  ·  running " + Span(DateTime.Now - st)); } catch { }
                    try { add("CPU TIME", Span(pr.TotalProcessorTime)); } catch { }
                    try { add("PRIORITY", pr.PriorityClass.ToString()); } catch { }
                    try { add("PEAK MEMORY", Ui.Fmt(pr.PeakWorkingSet64)); } catch { }
                    try { if (pr.MainWindowHandle != IntPtr.Zero) { add("WINDOW", pr.MainWindowTitle); add("RESPONDING", pr.Responding ? "Yes" : "No - not responding"); } } catch { }
                }
            }
            catch { }
            try { var is32 = Native.ProcessIs32Bit(pid); if (is32.HasValue) add("ARCHITECTURE", is32.Value ? "32-bit" : "64-bit"); } catch { }
            try
            {
                var w = Wmi.Query("SELECT CommandLine, ParentProcessId, SessionId FROM Win32_Process WHERE ProcessId = " + pid).FirstOrDefault();
                if (w != null)
                {
                    long ppid = Wmi.Long(w, "ParentProcessId"); string pn = "";
                    try { using (var pp = Process.GetProcessById((int)ppid)) pn = pp.ProcessName; } catch { }
                    add("PARENT", pn.Length > 0 ? pn + "  (PID " + ppid + ")" : "PID " + ppid + "  (no longer running)");
                    add("SESSION", Wmi.Str(w, "SessionId"));
                    try
                    {
                        var mo = w as ManagementObject;
                        if (mo != null)
                        {
                            var o = mo.InvokeMethod("GetOwner", null, null);
                            string u = o == null ? "" : Convert.ToString(o["User"]), dom = o == null ? "" : Convert.ToString(o["Domain"]);
                            if (!string.IsNullOrEmpty(u)) add("USER", string.IsNullOrEmpty(dom) ? u : dom + "\\" + u);
                        }
                    }
                    catch { }
                    add("COMMAND LINE", Wmi.Str(w, "CommandLine"));
                }
            }
            catch { }
            try
            {
                var sv = Wmi.Query("SELECT DisplayName FROM Win32_Service WHERE ProcessId = " + pid).Select(s => Wmi.Str(s, "DisplayName")).Where(n => n.Length > 0).ToList();
                if (sv.Count > 0) add("SERVICES", string.Join(", ", sv.Take(8)) + (sv.Count > 8 ? "  +" + (sv.Count - 8) + " more" : ""));
            }
            catch { }
            return rows;
        }
    }
}
