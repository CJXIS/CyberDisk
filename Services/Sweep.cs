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
    static class Sweep
    {
        const string VC = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern int SHLoadIndirectString(string src, StringBuilder buf, int cch, IntPtr res);

        static CleanItem Item(string group, string name, string desc, string glyph, string accent, string risk, bool on,
            Func<IEnumerable<string>> roots, Func<IEnumerable<string>> files = null, Func<string, bool> filter = null, bool rec = true)
        {
            var c = new CleanItem { Name = name, Desc = desc, Glyph = glyph, Group = group, Accent = new SolidColorBrush(Ui.Col(accent)), Checked = on, Filter = filter, Recursive = rec };
            if (roots != null) c.Roots = roots;
            if (files != null) c.SingleFiles = files;
            c.SetRisk(risk);
            return c;
        }

        public static List<CleanItem> Make()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string tmp = Path.GetTempPath();
            const string Q = "QUICK CLEAN", S = "SYSTEM FILES", A = "APP CACHES", P = "PRIVACY TRACES";
            var L = new List<CleanItem>();
            L.Add(Item(Q, "User temporary files", "Leftovers from apps and installers in your Temp folder", "▣", "#3DD6F5", "SAFE", true, () => new[] { tmp }));
            L.Add(Item(Q, "Windows temporary files", "System-wide temp folder used by Windows and installers", "▤", "#8B7CFF", "SAFE", true, () => new[] { Path.Combine(win, "Temp") }));
            var rb = Item(Q, "Recycle Bin", "Deleted files still waiting on every drive", "♻", "#3DDC97", "SAFE", true, null); rb.Kind = 1; L.Add(rb);
            L.Add(Item(Q, "Thumbnail & icon cache", "Explorer preview cache, rebuilt automatically", "▦", "#FFB84D", "SAFE", true,
                () => new[] { Path.Combine(local, @"Microsoft\Windows\Explorer") }, null,
                n => (n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase)) && n.EndsWith(".db", StringComparison.OrdinalIgnoreCase), false));
            L.Add(Item(Q, "GPU shader caches", "DirectX, NVIDIA and AMD shader caches (games rebuild them)", "◆", "#5B9BFF", "SAFE", true,
                () => new[] { Path.Combine(local, "D3DSCache"), Path.Combine(local, @"NVIDIA\DXCache"), Path.Combine(local, @"NVIDIA\GLCache"), Path.Combine(local, @"AMD\DxCache"), Path.Combine(local, @"AMD\GLCache"), Path.Combine(pd, @"NVIDIA Corporation\NV_Cache") }));

            L.Add(Item(S, "Windows Update downloads", "Already-installed update packages", "⬇", "#FF9F6B", "SAFE", true, () => new[] { Path.Combine(win, @"SoftwareDistribution\Download") }));
            L.Add(Item(S, "Delivery Optimization cache", "Peer-to-peer update cache kept by Windows", "⇵", "#FF6B8A", "SAFE", true,
                () => new[] { Path.Combine(win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") }));
            L.Add(Item(S, "Crash dumps & error reports", "Memory dumps (incl. MEMORY.DMP) and Windows Error Reporting leftovers", "☠", "#FF7A7A", "SAFE", true,
                () => new[] { Path.Combine(local, "CrashDumps"), Path.Combine(win, "Minidump"), Path.Combine(pd, @"Microsoft\Windows\WER\ReportArchive"), Path.Combine(pd, @"Microsoft\Windows\WER\ReportQueue") },
                () => new[] { Path.Combine(win, "MEMORY.DMP") }));
            L.Add(Item(S, "Windows & setup logs", "CBS, DISM and Panther setup logs", "☰", "#8B7CFF", "SAFE", true,
                () => new[] { Path.Combine(win, @"Logs\CBS"), Path.Combine(win, @"Logs\DISM"), Path.Combine(win, "Panther") }));
            L.Add(Item(S, "Defender scan history", "Old Microsoft Defender scan history files", "⛨", "#3DD6F5", "SAFE", true,
                () => new[] { Path.Combine(pd, @"Microsoft\Windows Defender\Scans\History") }));
            L.Add(Item(S, "Prefetch files", "Launch-speed hints; Windows rebuilds them but first launches are slower", "⚡", "#FFB84D", "ADVANCED", false,
                () => new[] { Path.Combine(win, "Prefetch") }, null, n => n.EndsWith(".pf", StringComparison.OrdinalIgnoreCase), false));
            var dism = Item(S, "Component store cleanup (DISM)", "Removes superseded Windows update files from WinSxS - takes a few minutes", "⚙", "#5B9BFF", "ADVANCED", false, null);
            dism.Kind = 3; dism.Cmd = "@dism"; L.Add(dism);

            L.Add(Item(A, "Discord cache", "Chat media and GPU cache", "◍", "#8B7CFF", "SAFE", true,
                () => new[] { Path.Combine(roam, @"discord\Cache"), Path.Combine(roam, @"discord\Code Cache"), Path.Combine(roam, @"discord\GPUCache") }));
            L.Add(Item(A, "Spotify cache", "Offline song cache and temporary data", "♫", "#3DDC97", "SAFE", true,
                () => new[] { Path.Combine(local, @"Spotify\Storage"), Path.Combine(local, @"Spotify\Data") }));
            L.Add(Item(A, "VS Code cache", "Cached data, GPU cache and logs", "⌨", "#5B9BFF", "SAFE", true,
                () => new[] { Path.Combine(roam, @"Code\Cache"), Path.Combine(roam, @"Code\CachedData"), Path.Combine(roam, @"Code\Code Cache"), Path.Combine(roam, @"Code\GPUCache"), Path.Combine(roam, @"Code\logs") }));
            L.Add(Item(A, "Steam web cache", "Steam client browser cache", "◐", "#3DD6F5", "SAFE", true, () => new[] { Path.Combine(local, @"Steam\htmlcache") }));
            L.Add(Item(A, "Teams & Slack cache", "Chat app caches", "✉", "#FF9F6B", "SAFE", true,
                () => new[] { Path.Combine(roam, @"Microsoft\Teams\Cache"), Path.Combine(roam, @"Microsoft\Teams\Code Cache"), Path.Combine(roam, @"Microsoft\Teams\GPUCache"), Path.Combine(roam, @"Slack\Cache"), Path.Combine(roam, @"Slack\Code Cache"), Path.Combine(roam, @"Slack\GPUCache") }));

            L.Add(Item(P, "Recent files & jump lists", "Recently opened documents shown in Explorer and the taskbar", "◷", "#8B7CFF", "PRIVACY", false,
                () => new[] { Path.Combine(roam, @"Microsoft\Windows\Recent") }));
            var dns = Item(P, "DNS resolver cache", "Flushes the DNS cache (ipconfig /flushdns)", "⌁", "#3DD6F5", "SAFE", true, null);
            dns.Kind = 3; dns.Cmd = "@dns"; L.Add(dns);

            L.AddRange(Handlers());
            return L;
        }

        // everything Windows' own Disk Cleanup (cleanmgr) offers, discovered from the registry
        static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "DownloadsFolder", "Recycle Bin", "Thumbnail Cache", "Temporary Files", "Delivery Optimization Files", "Windows Error Reporting Files",
          "System error memory dump files", "System error minidump files", "D3D Shader Cache" };
        static readonly HashSet<string> On = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Active Setup Temp Folders", "BranchCache", "Content Indexer Cleaner", "Diagnostic Data Viewer database files", "Downloaded Program Files", "Internet Cache Files",
          "Offline Pages Files", "Old ChkDsk Files", "RetailDemo Offline Content", "Service Pack Cleanup", "Setup Log Files", "Temporary Setup Files", "Update Cleanup",
          "Upgrade Discarded Files", "Windows Defender", "Windows Upgrade Log Files" };
        static readonly HashSet<string> Adv = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Previous Installations", "Windows ESD installation files", "Language Pack", "Device Driver Packages", "User file versions" };

        static string Ind(string s)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '@') return s;
            var sb = new StringBuilder(512);
            return SHLoadIndirectString(s, sb, sb.Capacity, IntPtr.Zero) == 0 ? sb.ToString() : null;
        }

        public static List<CleanItem> Handlers()
        {
            var res = new List<CleanItem>();
            string sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            try
            {
                using (var hk = Registry.LocalMachine.OpenSubKey(VC))
                {
                    if (hk == null) return res;
                    foreach (var name in hk.GetSubKeyNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    {
                        if (Skip.Contains(name)) continue;
                        using (var k = hk.OpenSubKey(name))
                        {
                            if (k == null) continue;
                            string disp = Ind(k.GetValue("Display") as string);
                            if (string.IsNullOrWhiteSpace(disp)) disp = name;
                            string desc = Ind(k.GetValue("Description") as string);
                            if (string.IsNullOrWhiteSpace(desc)) desc = "Cleaned by Windows Disk Cleanup";
                            desc = desc.Replace("\r", " ").Replace("\n", " ");
                            if (desc.Length > 150) desc = desc.Substring(0, 147) + "…";
                            var c = new CleanItem { Name = disp, Desc = desc, Glyph = "⚙", Group = "WINDOWS DISK CLEANUP", Accent = new SolidColorBrush(Ui.Col("#8B7CFF")), Kind = 2, HandlerKey = name, Checked = On.Contains(name) };
                            c.SetRisk(Adv.Contains(name) ? "ADVANCED" : "SAFE");
                            if (name == "Previous Installations") c.Roots = () => new[] { sys + "Windows.old", sys + "$WINDOWS.~BT", sys + "$WINDOWS.~WS" };
                            res.Add(c);
                        }
                    }
                }
            }
            catch { }
            return res;
        }

        public static bool Safe(string p)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) return false;
                var full = Path.GetFullPath(p).TrimEnd('\\');
                return full.Length > 12 && Path.GetPathRoot(full).TrimEnd('\\') != full;
            }
            catch { return false; }
        }

        public static long Measure(CleanItem it, out long files)
        {
            long total = 0, n = 0;
            files = 0;
            try
            {
                if (it.Kind == 1)
                {
                    var i = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
                    if (SHQueryRecycleBin(null, ref i) == 0) { files = i.i64NumItems; return i.i64Size; }
                    return 0;
                }
                foreach (var d in it.Roots())
                    if (Safe(d)) Each(d, it.Recursive, it.Filter, (p, bytes) => { total += bytes; n++; }, null);
                foreach (var f in it.SingleFiles())
                {
                    try { var fi = new FileInfo(f); if (fi.Exists) { total += fi.Length; n++; } } catch { }
                }
            }
            catch { }
            files = n;
            return total;
        }

        public static long Clean(CleanItem it, Action<long> report)
        {
            long freed = 0;
            if (it.Kind == 1)
            {
                long est = it.Size;
                int rc = -1;
                try { rc = SHEmptyRecycleBin(IntPtr.Zero, null, 7); } catch { }
                if (rc == 0 || est == 0) { report(est); return est; }
                return 0;
            }
            Action<string> rm = s => { try { Directory.Delete(s, false); } catch { } };
            foreach (var d in it.Roots())
                if (Safe(d)) Each(d, it.Recursive, it.Filter, (p, bytes) => { if (Del(p)) { freed += bytes; report(bytes); } }, it.Recursive ? rm : null);
            foreach (var f in it.SingleFiles())
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.Exists) { long len = fi.Length; if (Del(f)) { freed += len; report(len); } }
                }
                catch { }
            }
            return freed;
        }

        static long FreeTotal()
        {
            long t = 0;
            foreach (var d in DriveInfo.GetDrives()) { try { if (d.IsReady && d.DriveType == DriveType.Fixed) t += d.TotalFreeSpace; } catch { } }
            return t;
        }

        // runs Windows' own Disk Cleanup for the chosen handlers (cleanmgr /sagerun) and reports the free-space gain
        public static long RunHandlers(List<string> keys)
        {
            long before = FreeTotal();
            try
            {
                using (var hk = Registry.LocalMachine.OpenSubKey(VC, true))
                {
                    if (hk == null) return 0;
                    foreach (var name in hk.GetSubKeyNames())
                        using (var k = hk.OpenSubKey(name, true))
                        {
                            if (k == null) continue;
                            if (keys.Contains(name)) k.SetValue("StateFlags0099", 2, RegistryValueKind.DWord);
                            else k.DeleteValue("StateFlags0099", false);
                        }
                }
                var psi = new ProcessStartInfo("cleanmgr.exe", "/sagerun:99") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized };
                using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(40 * 60 * 1000); }
            }
            catch { }
            finally
            {
                try
                {
                    using (var hk = Registry.LocalMachine.OpenSubKey(VC, true))
                        if (hk != null)
                            foreach (var name in hk.GetSubKeyNames())
                                using (var k = hk.OpenSubKey(name, true)) { if (k != null) k.DeleteValue("StateFlags0099", false); }
                }
                catch { }
            }
            return Math.Max(0, FreeTotal() - before);
        }

        static void Exec(string exe, string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(psi)) { if (p != null && !p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } } }
        }

        static string ExecOut(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd(); p.WaitForExit(20000); return o; }
        }

        public static long RunCommand(CleanItem it)
        {
            try
            {
                if (it.Cmd == "@dns") { try { Mem.DnsFlushResolverCache(); } catch { } return 0; }
                if (it.Cmd == "@dism")
                {
                    long b = FreeTotal();
                    Exec("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", 40 * 60 * 1000);
                    return Math.Max(0, FreeTotal() - b);
                }
            }
            catch { }
            return 0;
        }

        public static long DeleteTree(string root)
        {
            long freed = 0;
            try
            {
                Each(root, true, null, (p, bytes) => { if (Del(p)) freed += bytes; }, s => { try { Directory.Delete(s, false); } catch { } });
                try { Directory.Delete(root, false); } catch { }
            }
            catch { }
            return freed;
        }

        public static long DirSize(string root)
        {
            long t = 0;
            try { Each(root, true, null, (p, bytes) => { t += bytes; }, null); } catch { }
            return t;
        }

        public static bool Del(string p)
        {
            try
            {
                string q = p.Length >= 240 ? @"\\?\" + p : p;
                var a = File.GetAttributes(q);
                if ((a & FileAttributes.ReadOnly) != 0) File.SetAttributes(q, FileAttributes.Normal);
                File.Delete(q);
                return true;
            }
            catch { return false; }
        }

        static void Each(string dir, bool rec, Func<string, bool> filter, Action<string, long> onFile, Action<string> afterDir)
        {
            string b = dir.TrimEnd('\\');
            var subs = new List<string>();
            var files = new List<KeyValuePair<string, long>>();
            IntPtr h = Native.FindFirstFileEx(@"\\?\" + b + "\\*", 1, out var d, 0, IntPtr.Zero, 2);
            if (h == new IntPtr(-1)) return;
            try
            {
                do
                {
                    uint a = d.dwFileAttributes; string nm = d.cFileName;
                    if ((a & 0x10) != 0)
                    {
                        if (nm == "." || nm == ".." || (a & 0x400) != 0) continue;   // never follow junctions / symlinks
                        subs.Add(b + "\\" + nm);
                    }
                    else if (filter == null || filter(nm))
                        files.Add(new KeyValuePair<string, long>(b + "\\" + nm, ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow));
                } while (Native.FindNextFile(h, out d));
            }
            finally { Native.FindClose(h); }
            foreach (var f in files) onFile(f.Key, f.Value);
            if (rec) foreach (var sd in subs) { Each(sd, rec, filter, onFile, afterDir); if (afterDir != null) afterDir(sd); }
        }
    }
}
