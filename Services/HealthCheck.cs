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
    static class Health
    {
        static string cat = "System";               // category of the check that is running (set before each step)

        static Finding F(int level, string title, string detail)
        {
            var f = new Finding { Title = title, Detail = detail, Category = cat };
            f.SetLevel(level);
            return f;
        }

        // events the user fixed or reviewed: only events NEWER than this moment are reported again
        public static readonly Dictionary<string, DateTime> Reviewed = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        static string RP { get { return Path.Combine(Settings.Dir, "reviewed.ini"); } }
        public static void LoadReviewed()
        {
            try
            {
                foreach (var l in File.ReadAllLines(RP))
                {
                    int i = l.LastIndexOf('='); long t;
                    if (i > 0 && long.TryParse(l.Substring(i + 1), out t)) Reviewed[l.Substring(0, i)] = new DateTime(t, DateTimeKind.Utc);
                }
            }
            catch { }
        }
        public static void MarkReviewed(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            Reviewed[key] = DateTime.UtcNow;
            try { Directory.CreateDirectory(Settings.Dir); File.WriteAllLines(RP, Reviewed.Select(kv => kv.Key + "=" + kv.Value.Ticks)); } catch { }
        }

        static readonly Dictionary<string, string> Known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Microsoft-Windows-Kernel-Power|41", "The PC restarted without a clean shutdown (crash, power loss or forced reset)." },
            { "disk|7", "Windows hit a bad block on a disk." },
            { "disk|11", "A disk controller reported an error." },
            { "disk|51", "A disk reported an error during a paging operation." },
            { "disk|153", "A disk I/O operation had to be retried." },
            { "Ntfs|55", "The file system structure on a drive is corrupt - run a disk check." },
            { "Ntfs|98", "NTFS found a problem on a volume." },
            { "Ntfs|137", "NTFS could not write to a volume's log." },
            { "Microsoft-Windows-WHEA-Logger|18", "A hardware error was reported by the CPU or memory (WHEA)." },
            { "Microsoft-Windows-WHEA-Logger|1", "A hardware error was reported (WHEA)." },
            { "volmgr|161", "Windows could not create a memory dump file." },
            { "Service Control Manager|7000", "A service failed to start." },
            { "Service Control Manager|7001", "A service failed because of a dependency." },
            { "Service Control Manager|7023", "A service terminated with an error." },
            { "Service Control Manager|7031", "A service stopped unexpectedly." },
            { "Service Control Manager|7034", "A service crashed unexpectedly." },
            { "Application Error|1000", "An application crashed." },
            { "Application Hang|1002", "An application stopped responding." },
        };
        static readonly HashSet<string> Severe = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Microsoft-Windows-Kernel-Power|41", "disk|7", "disk|11", "disk|51", "Ntfs|55", "Ntfs|98", "Ntfs|137", "Microsoft-Windows-WHEA-Logger|18", "Microsoft-Windows-WHEA-Logger|1" };

        public static readonly string[] CoreServices = { "EventLog", "WinDefend", "mpssvc", "Dnscache", "Dhcp", "RpcSs", "LanmanWorkstation" };

        // ── "what the fix does" texts (one bullet per line) ──
        public const string InfoSvcCore = "• Starts every stopped core service listed above.\n• A service that was disabled is set back to Automatic start first.\n• Waits up to 20 seconds for each service and checks that it is really running.";
        public const string InfoSvcEvt = "• Starts the failed service(s) again.\n• Services you disabled on purpose are left untouched.\n• Checks afterwards that each service is really running.";
        public const string InfoDirty = "• Runs Windows chkdsk /f on each volume marked dirty to repair file system errors.\n• The system drive cannot be checked while Windows runs, so its check is scheduled for the next restart.\n• Other drives are repaired now when no program uses them, otherwise they are scheduled for the next restart.";
        public const string InfoDisk = "• Runs a read-only online scan (chkdsk /scan) on every fixed NTFS drive.\n• If a drive has errors, a repair is queued and finishes during the next restart.\n• Nothing is deleted - chkdsk only repairs file system structures.";
        public const string InfoRtp = "• Turns Microsoft Defender real-time protection back on.\n• Then checks that Windows really reports the protection as active.\n• If Tamper Protection or another security product blocks it, you are told how to switch it on by hand.";
        public const string InfoDefs = "• Downloads the latest virus definitions for Microsoft Defender.\n• Needs an internet connection and can take a minute or two.";
        public const string InfoMem = "• Asks Windows to move idle memory pages of background programs out of RAM.\n• No program is closed - pages are loaded back automatically when needed.";
        public const string InfoDump = "• Turns on automatic pagefile management so Windows can write crash dumps.\n• Sets the crash dump type to Automatic memory dump.\n• Takes effect after the next restart.";
        public const string InfoSfc = "• Runs System File Checker (sfc /scannow) to verify every protected Windows file.\n• Damaged files are replaced from the Windows component store automatically.\n• If some files cannot be repaired, DISM /RestoreHealth runs and the check is repeated.\n• Takes 10-30 minutes - you can keep using the PC.";
        public const string InfoMemDiag = "• Opens Windows Memory Diagnostic and lets you restart now or later to test your RAM.\n• The test runs before Windows starts and the result appears after you sign in.";
        public const string InfoReboot = "• Restarts Windows after a 30-second warning so pending updates and changes can finish.\n• Save your work first. You can cancel the restart from the message that appears.";
        public const string InfoDiskPage = "• Opens Disk Health & Info with the full report for this drive.\n• Shows temperature, wear, SMART data and exactly what lowers the score.";
        public const string InfoSpace = "• Opens Cleanup Lab and runs ANALYZE for you.\n• Items marked SAFE are ticked, so one click frees the space.";
        public const string InfoStartup = "• Opens the BOOST tab where you can untick apps that start with Windows.\n• Nothing is disabled automatically - you choose which apps to keep.";
        public const string InfoErrors = "• Opens App Errors, which lists the crashing programs with the cause and a recommended action.";
        public const string InfoAck = "• Marks this event as reviewed so future checks hide it.\n• It comes back only if the same error happens again.\n• The Windows event log itself is not changed.";

        public static List<Finding> Run(Action<string> step)
        {
            var res = new List<Finding>();
            string sysDrive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');

            cat = "Storage"; step("Checking file systems…");
            try
            {
                var dirty = new List<string>();
                foreach (var v in Wmi.Query(@"root\cimv2", "SELECT DriveLetter, DirtyBitSet FROM Win32_Volume WHERE DriveType = 3"))
                    if (v["DirtyBitSet"] is bool && (bool)v["DirtyBitSet"]) dirty.Add((v["DriveLetter"] as string) ?? "?");
                dirty.RemoveAll(x => x == "?");
                if (dirty.Count > 0) res.Add(F(2, "File system needs a check", "Volume " + string.Join(", ", dirty) + " is marked dirty. Windows will offer a disk check on restart.").WithFix("dirty", "REPAIR DRIVES", InfoDirty, string.Join("|", dirty), true));
                else res.Add(F(0, "File systems are consistent", "No volume is marked dirty."));
            }
            catch { }

            cat = "Storage"; step("Checking free space…");
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                    double free = 100.0 * d.TotalFreeSpace / d.TotalSize;
                    string nm = d.Name.TrimEnd('\\');
                    if (free < 5) res.Add(F(2, nm + " is almost full", Ui.Fmt(d.TotalFreeSpace) + " free (" + free.ToString("0") + "%). Windows slows down badly below 10%.").WithFix("go:clean", "OPEN CLEANUP", InfoSpace));
                    else if (free < 10) res.Add(F(1, nm + " is running low on space", Ui.Fmt(d.TotalFreeSpace) + " free (" + free.ToString("0") + "%). Use the Cleanup tab.").WithFix("go:clean", "OPEN CLEANUP", InfoSpace));
                }
                catch { }
            }

            cat = "Reliability"; step("Reading recent system errors…");
            try
            {
                var groups = new Dictionary<string, int>(); var sample = new Dictionary<string, string>(); var svcNames = new Dictionary<string, HashSet<string>>();
                foreach (var log in new[] { "System", "Application" })
                {
                    var q = new System.Diagnostics.Eventing.Reader.EventLogQuery(log, System.Diagnostics.Eventing.Reader.PathType.LogName,
                        "*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 86400000]]]");
                    using (var r = new System.Diagnostics.Eventing.Reader.EventLogReader(q))
                    {
                        System.Diagnostics.Eventing.Reader.EventRecord ev; int cap = 0;
                        while ((ev = r.ReadEvent()) != null && cap++ < 3000)
                        {
                            using (ev)
                            {
                                string key = ev.ProviderName + "|" + ev.Id;
                                DateTime seen;
                                if (Reviewed.TryGetValue(key, out seen) && ev.TimeCreated.HasValue && ev.TimeCreated.Value.ToUniversalTime() <= seen) continue;   // already fixed / reviewed
                                int c; groups.TryGetValue(key, out c); groups[key] = c + 1;
                                if (!sample.ContainsKey(key)) { try { var m = ev.FormatDescription(); sample[key] = m == null ? "" : m.Split('\n')[0].Trim(); } catch { sample[key] = ""; } }
                                if (ev.ProviderName == "Service Control Manager" && ev.Properties != null && ev.Properties.Count > 0)
                                {
                                    string sn = Convert.ToString(ev.Properties[0].Value);
                                    if (!string.IsNullOrWhiteSpace(sn)) { HashSet<string> hs; if (!svcNames.TryGetValue(key, out hs)) svcNames[key] = hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase); hs.Add(sn.Trim()); }
                                }
                            }
                        }
                    }
                }
                if (groups.Count == 0) res.Add(F(0, "No critical errors in the last 24 hours", "The System and Application logs are clean."));
                else
                {
                    foreach (var g in groups.OrderByDescending(x => Severe.Contains(x.Key) ? 1 : 0).ThenByDescending(x => x.Value).Take(6))
                    {
                        string prov = g.Key.Split('|')[0], id = g.Key.Split('|')[1], note;
                        if (!Known.TryGetValue(g.Key, out note)) note = sample.ContainsKey(g.Key) ? sample[g.Key] : "";
                        if (note.Length > 160) note = note.Substring(0, 157) + "…";
                        var f = F(Severe.Contains(g.Key) ? 2 : 1, prov + "  (event " + id + ")  ×" + g.Value, note.Length > 0 ? note : "Reported as an error in the last 24 hours.").WithKey(g.Key);
                        HashSet<string> names; svcNames.TryGetValue(g.Key, out names);
                        EventFix(f, g.Key, names);
                        res.Add(f);
                    }
                }
            }
            catch { res.Add(F(3, "Event log not readable", "Could not read the Windows event logs.")); }

            cat = "System"; step("Checking pending restarts…");
            try
            {
                string why = PendingRestartReason();
                res.Add(why.Length > 0
                    ? F(1, "A restart is pending", "Windows is waiting for a restart to finish " + why + ".").WithFix("reboot", "RESTART NOW", InfoReboot)
                    : F(0, "No restart pending", "Nothing is waiting for a reboot."));
            }
            catch { }

            cat = "System"; step("Checking core services…");
            try
            {
                var stopped = new List<string>(); var names = new List<string>();
                foreach (var n in CoreServices)
                {
                    try { using (var sc = new System.ServiceProcess.ServiceController(n)) if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running) { stopped.Add(sc.DisplayName); if (n != "WinDefend") names.Add(n); } } catch { }
                }
                if (stopped.Count > 0)
                {
                    var f = F(1, "Some core services are not running", string.Join(", ", stopped) + ". Normal if another security product replaced Defender or the firewall.");
                    if (names.Count > 0) f.WithFix("svc-core", "START SERVICES", InfoSvcCore, string.Join("|", names), true);
                    else f.WithAdvice("No automatic fix: Microsoft Defender is switched off by another security product, which is normal.");
                    res.Add(f);
                }
                else res.Add(F(0, "Core services are running", "Event Log, Firewall, DNS, DHCP and RPC are all up."));
            }
            catch { }

            cat = "Security"; step("Checking Windows security…");
            try
            {
                foreach (var m in Wmi.Query(@"root\Microsoft\Windows\Defender", "SELECT AntivirusEnabled, RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus"))
                {
                    bool av = m["AntivirusEnabled"] is bool && (bool)m["AntivirusEnabled"], rt = m["RealTimeProtectionEnabled"] is bool && (bool)m["RealTimeProtectionEnabled"];
                    uint age = m["AntivirusSignatureAge"] is uint ? (uint)m["AntivirusSignatureAge"] : 0;
                    if (!av || !rt) res.Add(F(2, "Real-time protection is off", "Microsoft Defender is not protecting this PC right now.").WithFix("rtp", "TURN ON PROTECTION", InfoRtp, null, true));
                    else if (age > 7) res.Add(F(1, "Virus definitions are " + age + " days old", "Update Microsoft Defender from Windows Security.").WithFix("defs", "UPDATE DEFINITIONS", InfoDefs, null, true));
                    else res.Add(F(0, "Microsoft Defender is active", "Real-time protection is on, definitions are current."));
                    break;
                }
            }
            catch { }

            cat = "Performance"; step("Checking performance…");
            try
            {
                long t, a; Mem.Status(out t, out a);
                double used = 100.0 * (t - a) / Math.Max(1, t);
                if (used > 90) res.Add(F(1, "Memory is almost full", used.ToString("0") + "% of RAM is in use. Try BOOST or close heavy apps.").WithFix("mem", "FREE MEMORY", InfoMem, null, true));
                else res.Add(F(0, "Memory usage is fine", used.ToString("0") + "% of " + Ui.Fmt(t) + " in use."));
                double days = TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue).TotalDays;
                if (days > 14) res.Add(F(1, "The PC has not restarted for " + days.ToString("0") + " days", "A restart clears memory leaks and applies pending updates.").WithFix("reboot", "RESTART NOW", InfoReboot));
                int su = Startup.List().Count(x => x.Enabled);
                if (su > 15) res.Add(F(1, su + " apps start with Windows", "Disable some in the BOOST tab for a faster startup.").WithFix("go:boost", "OPEN BOOST", InfoStartup));
            }
            catch { }

            cat = "System";
            res.Add(F(3, "System file integrity not scanned", "A full check (sfc /scannow) takes a long time, so it is not part of this quick check.").WithFix("sfc", "RUN SCAN", InfoSfc));
            return res;
        }

        /// <summary>
        /// Returns what Windows is really waiting to finish ("" when no restart is needed).
        /// Only the flags Windows itself clears on every boot are used. The old PendingFileRenameOperations value is NOT used:
        /// antivirus, browsers and installers leave it set permanently, which made "restart pending" appear forever.
        /// </summary>
        static string PendingRestartReason()
        {
            DateTime boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue);
            Func<string, bool> flagged = path =>
            {
                using (var k = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (k == null) return false;
                    var t = Native.KeyWriteTime(k);
                    return !t.HasValue || t.Value > boot.AddMinutes(1);          // a flag written before the last boot is a leftover
                }
            };
            if (flagged(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")) return "installing Windows updates";
            if (flagged(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")) return "installing a Windows component";
            try
            {
                using (var a = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ActiveComputerName"))
                using (var b = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName"))
                    if (a != null && b != null && !string.Equals(Convert.ToString(a.GetValue("ComputerName")), Convert.ToString(b.GetValue("ComputerName")), StringComparison.OrdinalIgnoreCase)) return "applying the new computer name";
            }
            catch { }
            return "";
        }

        static void EventFix(Finding f, string key, HashSet<string> svc)
        {
            switch (key)
            {
                case "disk|7": case "disk|11": case "disk|51": case "disk|153": case "Ntfs|55": case "Ntfs|98": case "Ntfs|137":
                    f.WithFix("disk", "REPAIR DRIVES", InfoDisk, null, true); break;
                case "Service Control Manager|7000": case "Service Control Manager|7001": case "Service Control Manager|7023": case "Service Control Manager|7031": case "Service Control Manager|7034":
                    if (svc != null && svc.Count > 0) f.WithFix("svc-evt", "START SERVICES", InfoSvcEvt, string.Join("|", svc), true);
                    else f.WithFix("ack", "MARK AS REVIEWED", InfoAck);
                    break;
                case "volmgr|161":
                    f.WithFix("dump", "REPAIR SETTINGS", InfoDump, null, true); break;
                case "Microsoft-Windows-WHEA-Logger|18": case "Microsoft-Windows-WHEA-Logger|1":
                    f.WithFix("memdiag", "TEST MEMORY", InfoMemDiag).WithAdvice("Hardware errors can be caused by failing memory or an overheating CPU."); break;
                case "Application Error|1000": case "Application Hang|1002":
                    f.WithFix("go:errors", "OPEN APP ERRORS", InfoErrors); break;
                case "Microsoft-Windows-Kernel-Power|41":
                    f.WithFix("ack", "MARK AS REVIEWED", InfoAck).WithAdvice("If this keeps happening, check the power supply, cables and temperatures, or test the memory."); break;
                default:
                    f.WithFix("ack", "MARK AS REVIEWED", InfoAck); break;
            }
        }

        public static int Score(IEnumerable<Finding> l)
        {
            return HealthScorer.Evaluate(l).Score;
        }
    }
}
