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
    static class AppErr
    {
        [DllImport("user32.dll")] static extern bool IsHungAppWindow(IntPtr hWnd);

        static string P(System.Diagnostics.Eventing.Reader.EventRecord ev, int i)
        {
            try { return i < ev.Properties.Count ? Convert.ToString(ev.Properties[i].Value) ?? "" : ""; } catch { return ""; }
        }

        // turns one Application-log record into a crash / hang entry (null = not one of ours)
        public static CrashEvt Parse(System.Diagnostics.Eventing.Reader.EventRecord ev)
        {
            string prov = ev.ProviderName ?? "";
            var e = new CrashEvt { Time = ev.TimeCreated ?? DateTime.Now };
            if (ev.Id == 1000 && prov == "Application Error")
            {
                e.Kind = "Crash"; e.App = P(ev, 0); e.Version = P(ev, 1); e.Module = P(ev, 3);
                e.Code = P(ev, 6).Replace("0x", "").Trim().ToLowerInvariant(); e.ExePath = P(ev, 10); e.ModulePath = P(ev, 11);
            }
            else if (ev.Id == 1002 && prov == "Application Hang")
            {
                e.Kind = "Hang"; e.App = P(ev, 0); e.Version = P(ev, 1); e.ExePath = P(ev, 5);
            }
            else if (ev.Id == 1026 && prov == ".NET Runtime")
            {
                string t = P(ev, 0);
                var m = Regex.Match(t, @"Application:\s*(\S+)");
                if (!m.Success) return null;
                e.Kind = "Crash"; e.App = m.Groups[1].Value; e.Module = ".NET"; e.Code = "e0434352";
            }
            else return null;
            if (string.IsNullOrWhiteSpace(e.App)) return null;
            e.App = e.App.Trim();
            return e;
        }

        public static List<ErrApp> Scan(int days, out int totalEvents)
        {
            var all = new List<CrashEvt>();
            try
            {
                string xp = "*[System[(EventID=1000 or EventID=1002 or EventID=1026) and TimeCreated[timediff(@SystemTime) <= " + (days * 86400000L) + "]]]";
                var q = new System.Diagnostics.Eventing.Reader.EventLogQuery("Application", System.Diagnostics.Eventing.Reader.PathType.LogName, xp) { ReverseDirection = true };
                using (var r = new System.Diagnostics.Eventing.Reader.EventLogReader(q))
                {
                    System.Diagnostics.Eventing.Reader.EventRecord ev; int cap = 0;
                    while ((ev = r.ReadEvent()) != null && cap++ < 5000)
                        using (ev) { var c = Parse(ev); if (c != null) all.Add(c); }
                }
            }
            catch { }
            totalEvents = all.Count;
            var list = new List<ErrApp>();
            foreach (var g in all.GroupBy(x => x.App, StringComparer.OrdinalIgnoreCase))
            {
                var a = new ErrApp { Name = g.Key, Events = g.OrderByDescending(x => x.Time).ToList() };
                a.Crashes = a.Events.Count(x => x.Kind == "Crash"); a.Hangs = a.Events.Count(x => x.Kind == "Hang");
                a.Last = a.Events[0].Time;
                a.ExePath = a.Events.Select(x => x.ExePath).FirstOrDefault(x => !string.IsNullOrEmpty(x) && File.Exists(x));
                Diagnose(a);
                list.Add(a);
            }
            return list.OrderByDescending(x => x.Crashes * 2 + x.Hangs).ThenByDescending(x => x.Last).ToList();
        }

        /// <summary>0-100 stability index: every crash costs 5 points and every hang 2, softened for longer periods.</summary>
        public static int Stability(List<ErrApp> apps, int days)
        {
            int crashes = apps.Sum(a => a.Crashes), hangs = apps.Sum(a => a.Hangs);
            double norm = days <= 1 ? 1 : days <= 7 ? 2.5 : 5;
            return (int)Math.Max(0, Math.Round(100 - (crashes * 5 + hangs * 2) / norm));
        }

        public static string StabilityVerdict(int score)
        {
            return score >= 90 ? "Your apps are running smoothly - no significant crashes."
                 : score >= 75 ? "Mostly stable - a few programs failed now and then."
                 : score >= 50 ? "Unstable - several programs keep failing. Follow the advice below."
                 : "Very unstable - repeated crashes. Start with the red items below.";
        }

        /// <summary>Activity chart: hourly bars for 24 hours, daily bars (up to 14) otherwise.</summary>
        public static List<ErrBar> Bars(List<ErrApp> apps, int days)
        {
            var events = apps.SelectMany(a => a.Events).ToList();
            bool hourly = days <= 1;
            int n = hourly ? 24 : Math.Min(days, 14);
            var counts = new int[n];
            DateTime now = DateTime.Now;
            foreach (var e in events)
            {
                int idx = hourly ? (int)(now - e.Time).TotalHours : (int)(now.Date - e.Time.Date).TotalDays;
                if (idx >= 0 && idx < n) counts[n - 1 - idx]++;
            }
            int max = Math.Max(1, counts.Max());
            var hot = new LinearGradientBrush(Ui.Col("#FFB84D"), Ui.Col("#FF6B8A"), 90); hot.Freeze();
            var cold = new SolidColorBrush(Ui.Col("#21304A")); cold.Freeze();
            var list = new List<ErrBar>();
            for (int i = 0; i < n; i++)
            {
                DateTime t = hourly ? now.AddHours(-(n - 1 - i)) : now.Date.AddDays(-(n - 1 - i));
                list.Add(new ErrBar
                {
                    Height = counts[i] == 0 ? 3 : 6 + 42.0 * counts[i] / max, Brush = counts[i] == 0 ? (Brush)cold : hot,
                    Label = hourly ? (i % 3 == 0 ? t.ToString("HH") : "") : t.ToString("dd"),
                    Tip = (hourly ? t.ToString("yyyy-MM-dd HH:00") : t.ToString("yyyy-MM-dd")) + "  ·  " + counts[i]
                });
            }
            return list;
        }

        public static string Ago(DateTime t)
        {
            var s = DateTime.Now - t;
            if (s.TotalMinutes < 2) return "just now";
            if (s.TotalHours < 1) return ((int)s.TotalMinutes) + " min ago";
            if (s.TotalDays < 1) return ((int)s.TotalHours) + " h ago";
            return ((int)s.TotalDays) + " d ago";
        }

        static readonly Dictionary<string, string> CodeCause = new Dictionary<string, string>
        {
            { "c0000005", "Access violation: the program touched memory it was not allowed to use. Usual causes are a bug in the program, a faulty add-on or driver, or damaged files." },
            { "c0000409", "Stack buffer overrun: the program detected corrupted memory and stopped itself on purpose." },
            { "e0434352", "Unhandled .NET exception: the program hit an error in its managed code that it did not handle." },
            { "c0000374", "Heap corruption: the program's memory structures were damaged, often by a faulty add-on, driver or bad RAM." },
            { "c00000fd", "Stack overflow: the program called itself endlessly until it ran out of stack space." },
            { "c0000135", "A required DLL was not found: the program or a runtime it needs is missing or damaged." },
            { "c0000142", "A DLL failed to start: usually a missing or mismatched runtime." },
            { "c000007b", "Bad image format: a 32-bit / 64-bit mismatch or a damaged DLL." },
            { "c000001d", "Illegal instruction: the program uses a CPU feature this PC lacks, or its code is damaged." },
            { "c0000006", "In-page error: Windows could not read data the program needed - a failing disk, an unreliable network drive or a damaged file." },
            { "c0000094", "Arithmetic error inside the program (division by zero) - a bug in the program." },
            { "c0000095", "Arithmetic error inside the program (overflow) - a bug in the program." },
            { "80000003", "A breakpoint was hit: a debugging leftover or a deliberate stop inside the program." },
            { "40000015", "The program's runtime requested a fatal exit." },
        };

        static bool Mod(string m, params string[] starts) { foreach (var s in starts) if (m.StartsWith(s, StringComparison.OrdinalIgnoreCase)) return true; return false; }

        static void Diagnose(ErrApp a)
        {
            var advice = new List<string>();
            var crashes = a.Events.Where(x => x.Kind == "Crash").ToList();
            string cause = "";
            string action = null, uri = null;

            if (crashes.Count > 0)
            {
                string code = crashes.GroupBy(x => x.Code).OrderByDescending(g => g.Count()).First().Key ?? "";
                string mod = (crashes.Where(x => !string.IsNullOrEmpty(x.Module)).GroupBy(x => x.Module.ToLowerInvariant()).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault()) ?? "";
                string modPath = crashes.Select(x => x.ModulePath).FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? "";
                string c1;
                cause = CodeCause.TryGetValue(code, out c1) ? c1 : (code.Length > 0 ? "Windows reported error code 0x" + code + "." : "The program closed unexpectedly.");

                string appBase = Path.GetFileNameWithoutExtension(a.Name) ?? "";
                string modBase = Path.GetFileNameWithoutExtension(mod) ?? "";
                bool win = modPath.IndexOf("\\windows\\", StringComparison.OrdinalIgnoreCase) >= 0;

                if (mod.Length == 0 || mod.StartsWith("unknown", StringComparison.OrdinalIgnoreCase))
                    cause += "\nThe faulting module is unknown, which often means memory was corrupted.";
                else if (Mod(mod, "vcruntime", "msvcp", "msvcr", "concrt", "ucrtbase", "vccorlib"))
                {
                    cause += "\nThe crash happened inside the Microsoft Visual C++ runtime (" + mod + ").";
                    advice.Add("Repair or reinstall the Microsoft Visual C++ Redistributable (2015-2022), both x64 and x86.");
                    action = "GET VC++ RUNTIME";
                    uri = (a.ExePath != null && a.ExePath.IndexOf("(x86)", StringComparison.OrdinalIgnoreCase) >= 0) ? "https://aka.ms/vs/17/release/vc_redist.x86.exe" : "https://aka.ms/vs/17/release/vc_redist.x64.exe";
                }
                else if (Mod(mod, "ntdll", "kernelbase", "kernel32", "win32u", "user32", "combase", "rpcrt4", "ole32"))
                {
                    cause += "\nThe crash was reported by a core Windows library (" + mod + ") on behalf of the program, so the real cause is usually in the program or in damaged system files.";
                    advice.Add("Run the System File Checker from the Health tab.");
                    action = "SCAN SYSTEM FILES"; uri = "cd://health";
                }
                else if (Mod(mod, "clr", "coreclr", "clrjit", "mscor", "system.", "hostfxr", "mscordac"))
                {
                    cause += "\nThe crash happened inside the .NET runtime (" + mod + ").";
                    advice.Add("Install the latest .NET update, or repair the .NET runtime the program uses.");
                    action = "GET .NET"; uri = "https://dotnet.microsoft.com/download";
                }
                else if (Mod(mod, "nvwgf2um", "nvlddmkm", "nvoglv", "nvd3d", "nvcuda", "atidxx", "atio6a", "atiumd", "amdxx", "amdvlk", "igd", "ig9", "igc", "igxel", "d3d", "dxgi", "opengl32", "vulkan-1", "dxcore"))
                {
                    cause += "\nThe crash happened inside a graphics component (" + mod + ").";
                    advice.Add("Update or cleanly reinstall your graphics driver.");
                    advice.Add("Turn off game or screen overlays (Discord, Steam, GeForce, MSI Afterburner).");
                    advice.Add("Lower the graphics settings inside the program.");
                    action = "DRIVER UPDATES"; uri = "ms-settings:windowsupdate-optionalupdates";
                }
                else if (Mod(mod, "gameoverlay", "rtss", "discordhook", "overlay", "nahimic", "sonicstudio", "owclient", "reshade", "dxtory"))
                {
                    cause += "\nAn overlay or hook module (" + mod + ") was loaded into the program when it crashed.";
                    advice.Add("Turn that overlay off, or update it.");
                }
                else if (Mod(mod, "aswhook", "avghook", "mfehidk", "bdhook", "avp", "sbiedll", "mbae"))
                {
                    cause += "\nA security product's module (" + mod + ") was inside the program when it crashed.";
                    advice.Add("Update your security software, or add this program to its exclusions.");
                }
                else if (modBase.Length > 0 && string.Equals(modBase, appBase, StringComparison.OrdinalIgnoreCase))
                {
                    cause += "\nThe fault is inside the program's own code (" + mod + ").";
                    advice.Add("Update the program to its newest version, or reinstall it.");
                }
                else if (modPath.Length > 0 && !win)
                {
                    cause += "\nA third-party module (" + mod + ") caused the crash.";
                    advice.Add("Update or uninstall the software that installed this module.");
                }
                else cause += "\nThe crash happened inside " + mod + ".";

                if (code == "c0000006" && action == null) { advice.Add("Run a disk check from the Health tab."); action = "CHECK DISK"; uri = "cd://health"; }
                if ((code == "c0000135" || code == "c0000142" || code == "c000007b") && !advice.Any(x => x.StartsWith("Reinstall"))) advice.Add("Reinstall the program so its runtime files are restored.");
                if (code == "c0000005" || code == "c0000409" || code == "c0000374") advice.Add("Update the program to its newest version.");
            }
            if (a.Hangs > 0)
            {
                cause = (cause.Length > 0 ? cause + "\n" : "") + "The program stopped responding: it was busy for too long or waited for something that never came (disk, network or another program).";
                advice.Add("Close other heavy programs to free memory and CPU.");
                advice.Add("Keep the program's data off a slow or disconnected network drive.");
            }
            if (a.Crashes + a.Hangs >= 5) advice.Add("Repeated failures: if updating does not help, uninstall and reinstall the program from the APPS tab.");
            if (advice.Count == 0) advice.Add("Update the program, and reinstall it from the APPS tab if it keeps failing.");

            a.Cause = cause;
            a.Advice = string.Join("\n", advice.Distinct().Take(5).Select(x => "• " + x));
            a.ActionCaption = action; a.ActionUri = uri;

            var parts = new List<string>();
            if (a.Crashes > 0) parts.Add(a.Crashes + " crash(es)");
            if (a.Hangs > 0) parts.Add(a.Hangs + " hang(s)");
            parts.Add("last seen " + Ago(a.Last));
            a.Summary = string.Join("  ·  ", parts);

            int score = a.Crashes * 2 + a.Hangs;
            a.Severity = score >= 10 ? "CRITICAL" : score >= 6 ? "HIGH" : score >= 3 ? "MEDIUM" : "LOW";
            string tone = score >= 10 ? "#FF6B8A" : score >= 6 ? "#FF9F6B" : score >= 3 ? "#FFB84D" : "#5B9BFF";
            var b = new SolidColorBrush(Ui.Col(tone)); b.Freeze(); a.Tone = b;
            a.Glyph = score >= 6 ? "✖" : score >= 3 ? "!" : "i";

            a.Details = string.Join("\n", a.Events.Take(6).Select(x =>
                x.Time.ToString("yyyy-MM-dd HH:mm") + "  ·  " + x.Kind + (string.IsNullOrEmpty(x.Module) ? "" : "  ·  " + x.Module) + (string.IsNullOrEmpty(x.Code) ? "" : "  ·  0x" + x.Code)));
        }

        // programs that are frozen right now (Windows marks a window as hung after ~5 s without answering)
        public static List<HungVm> Hung()
        {
            var l = new List<HungVm>();
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == me || p.MainWindowHandle == IntPtr.Zero) continue;
                    if (IsHungAppWindow(p.MainWindowHandle))
                    {
                        string title = ""; try { title = p.MainWindowTitle; } catch { }
                        l.Add(new HungVm { Pid = p.Id, Name = p.ProcessName + ".exe", Title = title });
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
            return l;
        }

        // tells us the moment any program crashes or freezes
        public static System.Diagnostics.Eventing.Reader.EventLogWatcher Watch(Action<CrashEvt> onEvent)
        {
            try
            {
                var q = new System.Diagnostics.Eventing.Reader.EventLogQuery("Application", System.Diagnostics.Eventing.Reader.PathType.LogName, "*[System[(EventID=1000 or EventID=1002 or EventID=1026)]]");
                var w = new System.Diagnostics.Eventing.Reader.EventLogWatcher(q);
                w.EventRecordWritten += (s, e) =>
                {
                    try { using (var ev = e.EventRecord) { if (ev == null) return; var c = Parse(ev); if (c != null) onEvent(c); } } catch { }
                };
                w.Enabled = true;
                return w;
            }
            catch { return null; }
        }

        public static string Report(List<ErrApp> apps, int days)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CyberDisk - app error report");
            sb.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "   Period: last " + days + " day(s)   PC: " + Environment.MachineName);
            sb.AppendLine(new string('=', 70));
            if (apps.Count == 0) sb.AppendLine("No application crashes or hangs were found.");
            foreach (var a in apps)
            {
                sb.AppendLine();
                sb.AppendLine(a.Name + "   (" + a.Crashes + " crash(es), " + a.Hangs + " hang(s), last " + a.Last.ToString("yyyy-MM-dd HH:mm") + ")");
                if (!string.IsNullOrEmpty(a.ExePath)) sb.AppendLine("  Path:   " + a.ExePath);
                sb.AppendLine("  Cause:  " + (a.Cause ?? "").Replace("\n", "\n          "));
                sb.AppendLine("  Advice:");
                foreach (var l in (a.Advice ?? "").Split('\n')) sb.AppendLine("    " + l);
                sb.AppendLine("  Events:");
                foreach (var l in (a.Details ?? "").Split('\n')) sb.AppendLine("    " + l);
            }
            return sb.ToString();
        }
    }
}
