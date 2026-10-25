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
    // ───────────────────────── Fixer: really repairs what the health check found ─────────────────────────
    static class Fixer
    {
        public sealed class Result { public bool Ok, Partial, Restart; public string Text; }
        static Result Good(string t, bool restart = false) { return new Result { Ok = true, Text = t, Restart = restart }; }
        static Result Part(string t, bool restart = false) { return new Result { Partial = true, Text = t, Restart = restart }; }
        static Result Bad(string t) { return new Result { Text = t }; }

        public static void Log(string s)
        {
            try { Directory.CreateDirectory(Settings.Dir); File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + s + Environment.NewLine); } catch { }
        }
        public static string LogPath { get { return Path.Combine(Settings.Dir, "fixes.log"); } }

        // runs a program with no window; returns its exit code (-1 = could not start or timed out)
        /// <summary>Set by the STOP button: running programs are ended and no new fix starts.</summary>
        public static volatile bool CancelRequested;
        static readonly object runLock = new object();

        public static int Exec(string file, string args, int timeoutMs, out string output, Encoding enc = null)
        {
            output = "";
            try
            {
                var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WindowStyle = ProcessWindowStyle.Hidden };
                if (enc != null) psi.StandardOutputEncoding = enc;
                var sb = new StringBuilder();
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    var clock = Stopwatch.StartNew();
                    while (!p.WaitForExit(300))                                     // wait in small steps so STOP works at once
                    {
                        if (CancelRequested || clock.ElapsedMilliseconds > timeoutMs) { try { p.Kill(); } catch { } lock (sb) output = sb.ToString(); return -1; }
                    }
                    p.WaitForExit();
                    lock (sb) output = sb.ToString();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        static string LastLines(string s, int n)
        {
            var l = (s ?? "").Replace("\0", "").Split('\n').Select(x => x.Trim('\r', ' ', '\t')).Where(x => x.Length > 3 && x.Any(char.IsLetter)).ToList();
            return string.Join(" ", l.Skip(Math.Max(0, l.Count - n)));
        }

        static System.ServiceProcess.ServiceController FindService(string name)
        {
            try { var sc = new System.ServiceProcess.ServiceController(name); var s = sc.Status; return sc; } catch { }
            foreach (var sc in System.ServiceProcess.ServiceController.GetServices())
            {
                if (string.Equals(sc.DisplayName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(sc.ServiceName, name, StringComparison.OrdinalIgnoreCase)) return sc;
                sc.Dispose();
            }
            return null;
        }

        static bool DefenderState(out bool rt, out uint age)
        {
            rt = false; age = 0;
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(@"root\Microsoft\Windows\Defender", "SELECT RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus"))
                    foreach (System.Management.ManagementBaseObject m in s.Get())
                    {
                        rt = m["RealTimeProtectionEnabled"] is bool && (bool)m["RealTimeProtectionEnabled"];
                        age = m["AntivirusSignatureAge"] is uint ? (uint)m["AntivirusSignatureAge"] : 0;
                        return true;
                    }
            }
            catch { }
            return false;
        }

        static bool IsDirty(string letter)
        {
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(@"root\cimv2", "SELECT DirtyBitSet FROM Win32_Volume WHERE DriveLetter = '" + letter + "'"))
                    foreach (System.Management.ManagementBaseObject m in s.Get()) return m["DirtyBitSet"] is bool && (bool)m["DirtyBitSet"];
            }
            catch { }
            return false;
        }

        static bool IsSystemDrive(string letter)
        {
            return string.Equals(letter.TrimEnd('\\'), Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        // chkdsk with the prompt answers it needs: system drive = "schedule at restart? Y"; other drives = "force dismount? N", "schedule? Y"
        static int Chkdsk(string letter, string mode, out string output)
        {
            string answers = IsSystemDrive(letter) ? "echo Y" : "echo N& echo Y";
            return Exec("cmd.exe", "/c \"(" + answers + ")| chkdsk " + letter + " " + mode + "\"", 45 * 60 * 1000, out output);
        }

        // ── the fixes (all of them run on a worker thread) ──
        public static bool IsAdmin()
        {
            try { return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); } catch { return false; }
        }

        /// <summary>Fixes that can take many minutes (shown with a time warning and always run last).</summary>
        public static bool IsLong(string id) { return id == "sfc" || id == "disk" || id == "dirty" || id == "defs"; }

        public static Result Run(Finding f)
        {
            lock (runLock)                                                          // only one repair at a time: they change the same system parts
            {
                if (CancelRequested) return new Result { Text = "Stopped before it started." };
                return RunLocked(f);
            }
        }

        static Result RunLocked(Finding f)
        {
            Log("FIX " + f.FixId + " · " + f.Title);
            Result r;
            try
            {
                if (!IsAdmin() && f.FixId != "ack" && f.FixId != "memdiag" && f.FixId != "mem")
                { r = Bad("CyberDisk needs administrator rights for this repair. Close it and start it again with \"Run as administrator\"."); Log("  -> NO ADMIN"); return r; }
                switch (f.FixId)
                {
                    case "svc-core": r = Services(f.FixData, true); break;
                    case "svc-evt": r = Services(f.FixData, false); break;
                    case "dirty": r = Dirty(f.FixData); break;
                    case "disk": r = DiskScan(); break;
                    case "rtp": r = Realtime(); break;
                    case "defs": r = Definitions(); break;
                    case "mem": r = Memory(); break;
                    case "dump": r = DumpSettings(); break;
                    case "sfc": r = Sfc(); break;
                    case "memdiag": r = MemDiag(); break;
                    case "ack": r = Good("Marked as reviewed."); break;
                    default: r = Bad("This fix is not available."); break;
                }
                if (CancelRequested && !r.Ok) r = Bad("Stopped by you.");
                if (r.Ok && f.Key != null) Health.MarkReviewed(f.Key);
            }
            catch (Exception ex) { r = Bad("The fix failed: " + ex.Message); }
            Log("  -> " + (r.Ok ? "OK" : r.Partial ? "PARTIAL" : "FAILED") + " · " + r.Text);
            return r;
        }

        static Result Services(string data, bool core)
        {
            var ok = new List<string>(); var bad = new List<string>(); var kept = new List<string>();
            foreach (var name in (data ?? "").Split('|').Where(x => x.Length > 0))
            {
                try
                {
                    using (var sc = FindService(name))
                    {
                        if (sc == null) { bad.Add(name); continue; }
                        string disp = sc.DisplayName;
                        if (sc.StartType == System.ServiceProcess.ServiceStartMode.Disabled)
                        {
                            if (!core) { kept.Add(disp); continue; }
                            string o; Exec("sc.exe", "config \"" + sc.ServiceName + "\" start= auto", 15000, out o);
                        }
                        sc.Refresh();
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.StopPending) sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                        sc.Refresh();
                        for (int attempt = 0; attempt < 2 && sc.Status != System.ServiceProcess.ServiceControllerStatus.Running; attempt++)
                        {
                            try { sc.Start(); sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(20)); }
                            catch (Exception ex) { Log("  start " + disp + " attempt " + (attempt + 1) + ": " + ex.Message); Thread.Sleep(1500); }
                            sc.Refresh();
                        }
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running) ok.Add(disp); else bad.Add(disp);
                    }
                }
                catch { bad.Add(name); }
            }
            var lines = new List<string>();
            if (ok.Count > 0) lines.Add("Running again: " + string.Join(", ", ok) + ".");
            if (bad.Count > 0) lines.Add("Could not start: " + string.Join(", ", bad) + ".");
            if (kept.Count > 0) lines.Add("Left disabled (your choice): " + string.Join(", ", kept) + ".");
            if (lines.Count == 0) lines.Add("Nothing needed to be started.");
            string text = string.Join("\n", lines);
            if (bad.Count == 0) return Good(text);
            return ok.Count > 0 ? Part(text) : Bad(text);
        }

        static Result Dirty(string data)
        {
            var fixedNow = new List<string>(); var sched = new List<string>(); var bad = new List<string>();
            foreach (var l in (data ?? "").Split('|').Where(x => x.Length > 0))
            {
                string o; int code = Chkdsk(l, "/f", out o);
                Log("  chkdsk " + l + " exit " + code + " " + LastLines(o, 2));
                if (code == -1) { bad.Add(l); continue; }
                if (!IsDirty(l)) fixedNow.Add(l); else sched.Add(l);        // still dirty = Windows repairs it at the next restart
            }
            var parts = new List<string>();
            if (fixedNow.Count > 0) parts.Add("Repaired: " + string.Join(", ", fixedNow) + ".");
            if (sched.Count > 0) parts.Add("Repair scheduled for the next restart: " + string.Join(", ", sched) + ".");
            if (bad.Count > 0) parts.Add("Could not repair: " + string.Join(", ", bad) + ".");
            string text = string.Join("\n", parts);
            if (bad.Count == 0) return Good(text, sched.Count > 0);
            return fixedNow.Count + sched.Count > 0 ? Part(text, sched.Count > 0) : Bad(text);
        }

        static Result DiskScan()
        {
            var clean = new List<string>(); var queued = new List<string>(); var bad = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || d.DriveType != DriveType.Fixed || !string.Equals(d.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase)) continue;
                    string l = d.Name.TrimEnd('\\'), o;
                    int code = Exec("chkdsk.exe", l + " /scan", 40 * 60 * 1000, out o);
                    Log("  chkdsk /scan " + l + " exit " + code);
                    if (code == 0) { clean.Add(l); continue; }
                    if (code == -1) { bad.Add(l); continue; }
                    string o2; int c2 = Chkdsk(l, "/spotfix", out o2);      // errors found: queue the offline spot fix
                    if (c2 == -1) bad.Add(l); else queued.Add(l);
                }
                catch { }
            }
            var parts = new List<string>();
            if (clean.Count > 0) parts.Add("No errors found on: " + string.Join(", ", clean) + ".");
            if (queued.Count > 0) parts.Add("Repair queued for the next restart: " + string.Join(", ", queued) + ".");
            if (bad.Count > 0) parts.Add("Could not check: " + string.Join(", ", bad) + ".");
            if (parts.Count == 0) return Good("No NTFS drive needed a check.");
            string text = string.Join("\n", parts);
            if (bad.Count == 0) return Good(text, queued.Count > 0);
            return clean.Count + queued.Count > 0 ? Part(text, queued.Count > 0) : Bad(text);
        }

        static Result Realtime()
        {
            string o;
            Exec("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Set-MpPreference -DisableRealtimeMonitoring $false\"", 60000, out o);
            Thread.Sleep(2500);
            bool rt; uint age;
            if (DefenderState(out rt, out age) && rt) return Good("Real-time protection is on.");
            return Bad("Windows blocked the change (Tamper Protection or another security product). Open Windows Security > Virus & threat protection > Manage settings and switch it on.");
        }

        static Result Definitions()
        {
            string o;
            int code = Exec("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Update-MpSignature\"", 6 * 60 * 1000, out o);
            bool rt; uint age;
            if (DefenderState(out rt, out age) && age <= 2) return Good("Virus definitions are up to date.");
            return Bad(code == -1 ? "The update did not finish. Check your internet connection and try again." : "Could not update the definitions. Check your internet connection and try again.");
        }

        static Result Memory()
        {
            long t, b, a; Mem.Status(out t, out b);
            int count; long freed = Mem.TrimAll(out count);
            Mem.Status(out t, out a);
            double used = 100.0 * (t - a) / Math.Max(1, t);
            long got = Math.Max(freed, a - b);
            if (used <= 90) return Good(got > 0 ? "Released " + Ui.Fmt(got) + " of memory from " + count + " processes." : "Memory is already optimized.");
            return Part("Released " + Ui.Fmt(Math.Max(0, got)) + ", but memory is still high. Close heavy apps in the BOOST tab.");
        }

        static Result DumpSettings()
        {
            bool pf = false, dump = false;
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(@"root\cimv2", "SELECT * FROM Win32_ComputerSystem"))
                    foreach (System.Management.ManagementObject cs in s.Get())
                    {
                        cs.Scope.Options.EnablePrivileges = true;
                        cs["AutomaticManagedPagefile"] = true; cs.Put(); pf = true;
                    }
            }
            catch (Exception ex) { Log("  pagefile: " + ex.Message); }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CrashControl", true))
                    if (k != null) { k.SetValue("CrashDumpEnabled", 7, RegistryValueKind.DWord); k.SetValue("AutoReboot", 1, RegistryValueKind.DWord); dump = true; }
            }
            catch (Exception ex) { Log("  crashcontrol: " + ex.Message); }
            if (pf && dump) return Good("Crash dumps are enabled. Restart Windows to apply the change.", true);
            if (pf || dump) return Part("Part of the settings were changed. Restart Windows to apply them.", true);
            return Bad("Could not change the crash dump settings.");
        }

        static Result Sfc()
        {
            string o;
            int code = Exec("sfc.exe", "/scannow", 120 * 60 * 1000, out o, Encoding.Unicode);
            string said = LastLines(o, 2);
            Log("  sfc exit " + code + " " + said);
            if (code == -1 && said.Length == 0) return Bad("The scan could not run.");
            if (code != 0)
            {
                string o2; Exec("dism.exe", "/Online /Cleanup-Image /RestoreHealth", 90 * 60 * 1000, out o2);
                Log("  dism " + LastLines(o2, 2));
                code = Exec("sfc.exe", "/scannow", 120 * 60 * 1000, out o, Encoding.Unicode);
                said = LastLines(o, 2);
            }
            return code == 0 ? Good(said.Length > 0 ? "Windows reports: " + said : "The scan finished.") : Part(said.Length > 0 ? "Windows reports: " + said : "Some files could not be repaired. See C:\\Windows\\Logs\\CBS\\CBS.log.");
        }

        static Result MemDiag()
        {
            try { Process.Start(new ProcessStartInfo("mdsched.exe") { UseShellExecute = false }); return Good("Windows Memory Diagnostic is open - choose when to restart."); }
            catch (Exception ex) { return Bad("Could not open Windows Memory Diagnostic: " + ex.Message); }
        }

        public static string Restart()
        {
            string o;
            int code = Exec("shutdown.exe", "/r /t 30 /c \"CyberDisk: restarting to finish pending changes\"", 15000, out o);
            Log("RESTART scheduled (exit " + code + ")");
            return code == 0 ? null : "Windows refused to restart: " + LastLines(o, 1);
        }
        public static void CancelRestart() { string o; Exec("shutdown.exe", "/a", 15000, out o); Log("RESTART cancelled"); }
    }
}
