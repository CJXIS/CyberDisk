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
    // ───────────────────────── Installed software ─────────────────────────
    static class Inventory
    {
        public static bool IncludeStore;

        public static List<AppItem> Load()
        {
            var d = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
            var targets = new[] {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Registry64) };
            foreach (var (hive, view) in targets)
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(hive, view))
                    using (var u = bk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (u == null) continue;
                        foreach (var sn in u.GetSubKeyNames())
                        {
                            try
                            {
                                using (var k = u.OpenSubKey(sn))
                                {
                                    if (k == null) continue;
                                    var name = (k.GetValue("DisplayName") as string)?.Trim();
                                    if (string.IsNullOrEmpty(name)) continue;
                                    if (Convert.ToInt32(k.GetValue("SystemComponent") ?? 0) == 1) continue;
                                    if (k.GetValue("ParentKeyName") != null) continue;
                                    var rt = k.GetValue("ReleaseType") as string;
                                    if (rt == "Update" || rt == "Security Update" || rt == "Hotfix" || rt == "Update Rollup") continue;

                                    string us = (k.GetValue("UninstallString") as string) ?? "";
                                    if (us.Length == 0 && Guid.TryParse(sn, out _)) us = "msiexec.exe /x " + sn;
                                    string loc = ((k.GetValue("InstallLocation") as string) ?? "").Trim().Trim('"');
                                    string pub = (k.GetValue("Publisher") as string) ?? "";
                                    string ver = (k.GetValue("DisplayVersion") as string) ?? "";
                                    long size = Convert.ToInt64(k.GetValue("EstimatedSize") ?? 0) * 1024;

                                    bool game = us.Has("steam://") || loc.Has(@"\steamapps\") || loc.Has(@"\Epic Games\")
                                        || loc.Has(@"\GOG") || pub.Has("Epic Games") || pub.Has("Valve") || pub.Has("Riot Games");

                                    DateTime? idt = null; string isrc = "";
                                    string ids = Convert.ToString(k.GetValue("InstallDate"));
                                    DateTime pd;
                                    if (!string.IsNullOrEmpty(ids) && ids.Length == 8 && DateTime.TryParseExact(ids, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out pd) && pd.Year > 1990 && pd <= DateTime.Now.AddDays(1)) { idt = pd; isrc = "Installer record"; }
                                    if (!idt.HasValue && loc.Length > 0) { try { if (Directory.Exists(loc)) { var ct = Directory.GetCreationTime(loc); if (ct.Year > 1990) { idt = ct; isrc = "Install folder"; } } } catch { } }
                                    if (!idt.HasValue) { var kt = Native.KeyWriteTime(k); if (kt.HasValue && kt.Value.Year > 1990) { idt = kt; isrc = "Registry entry"; } }

                                    var key = name + "|" + ver;
                                    if (d.ContainsKey(key)) continue;
                                    d[key] = new AppItem
                                    {
                                        Name = name, Kind = game ? "GAME" : "APP", Publisher = pub, Version = ver, Uninstall = us,
                                        IconPath = k.GetValue("DisplayIcon") as string, Location = loc,
                                        SizeBytes = size, SizeText = Ui.Fmt(size), InstallDate = idt, DateSource = isrc, RegSub = sn, Hive = hive, View = view
                                    };
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            var list = d.Values.ToList();
            if (IncludeStore) list.AddRange(LoadStore());
            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static List<AppItem> LoadStore()
        {
            var res = new List<AppItem>();
            try
            {
                string cmd = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; Get-AppxPackage | Where-Object { -not $_.NonRemovable -and -not $_.IsFramework -and $_.SignatureKind -eq 'Store' } | ForEach-Object { $_.Name + '|' + $_.Version + '|' + $_.Publisher + '|' + $_.PackageFullName + '|' + $_.InstallLocation + '|' + $_.Architecture + '|' + $_.PackageFamilyName }";
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + cmd + "\"")
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    foreach (var line in outp.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var f = line.Trim().Split('|');
                        if (f.Length < 5) continue;
                        var m = Regex.Match(f[2], "CN=([^,]+)");
                        DateTime? idt = null;
                        try { if (f[4].Length > 0 && Directory.Exists(f[4])) { var ct = Directory.GetCreationTime(f[4]); if (ct.Year > 1990) idt = ct; } } catch { }
                        res.Add(new AppItem
                        {
                            Name = f[0], Kind = "STORE", Version = f[1], Package = f[3], Location = f[4], SizeText = "—",
                            Publisher = m.Success ? m.Groups[1].Value.Trim('"') : f[2],
                            InstallDate = idt, DateSource = idt.HasValue ? "Package folder" : "",
                            PackageArch = f.Length > 5 ? f[5].Trim() : "", PackageFamily = f.Length > 6 ? f[6].Trim() : ""
                        });
                    }
                }
            }
            catch { }
            return res;
        }

        // ── details for the preview panel ──
        const string UNINST = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        static void Add(List<HwRow> l, string k, string v) { if (!string.IsNullOrWhiteSpace(v)) l.Add(new HwRow { K = k, V = v.Trim() }); }

        /// <summary>Everything Windows knows about an app, as label / value rows (empty values are skipped).</summary>
        public static List<HwRow> Details(AppItem a)
        {
            var r = new List<HwRow>();
            Add(r, "Install date source", a.DateSource);
            if (a.Package != null)
            {
                Add(r, "Package name", a.Package); Add(r, "Package family", a.PackageFamily); Add(r, "Architecture", a.PackageArch);
                Add(r, "Install scope", "Microsoft Store app");
            }
            else
            {
                bool user = a.Hive == RegistryHive.CurrentUser;
                Add(r, "Install scope", user ? "Current user only" : "All users");
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(a.Hive, a.View))
                    using (var k = bk.OpenSubKey(UNINST + "\\" + a.RegSub))
                    {
                        bool x86 = (a.Hive == RegistryHive.LocalMachine && a.View == RegistryView.Registry32) || (a.Location ?? "").Has("(x86)");
                        Add(r, "Architecture", x86 ? "32-bit (x86)" : "64-bit");
                        bool guid = Guid.TryParse(a.RegSub, out _);
                        bool msi = k != null && Convert.ToInt32(k.GetValue("WindowsInstaller") ?? 0) == 1;
                        Add(r, "Installer type", msi ? "Windows Installer (MSI)" : guid ? "Windows Installer package" : "Setup program (EXE)");
                        Add(r, "Product code / key", a.RegSub);
                        if (k != null)
                        {
                            Func<string, string> S = n => Convert.ToString(k.GetValue(n)) ?? "";
                            Add(r, "Install source", S("InstallSource"));
                            if (a.SizeBytes > 0) Add(r, "Size (registry)", Ui.Fmt(a.SizeBytes));
                            bool noMod = Convert.ToInt32(k.GetValue("NoModify") ?? 0) == 1, noRep = Convert.ToInt32(k.GetValue("NoRepair") ?? 0) == 1;
                            Add(r, "Modify / repair", (noMod ? "Modify: no" : "Modify: yes") + "  ·  " + (noRep ? "Repair: no" : "Repair: yes"));
                            string lang = S("Language"); int lcid;
                            if (int.TryParse(lang, out lcid) && lcid > 0) { try { lang = System.Globalization.CultureInfo.GetCultureInfo(lcid).DisplayName; } catch { } }
                            Add(r, "Language", lang);
                            Add(r, "Uninstall command", S("UninstallString")); Add(r, "Silent uninstall", S("QuietUninstallString")); Add(r, "Modify command", S("ModifyPath"));
                            Add(r, "Display icon", S("DisplayIcon"));
                            Add(r, "Help link", S("HelpLink")); Add(r, "Website", S("URLInfoAbout")); Add(r, "Update info", S("URLUpdateInfo"));
                            Add(r, "Support phone", S("HelpTelephone")); Add(r, "Contact", S("Contact")); Add(r, "Comments", S("Comments"));
                            var wt = Native.KeyWriteTime(k); if (wt.HasValue) Add(r, "Registry last changed", wt.Value.ToString("yyyy-MM-dd HH:mm"));
                            Add(r, "Registry key", (a.Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM") + "\\" + UNINST + "\\" + a.RegSub);
                        }
                    }
                }
                catch { }
            }
            if (!string.IsNullOrEmpty(a.Location))
            {
                string fs = a.MeasuredBytes >= 0 ? Ui.Fmt(a.MeasuredBytes) + "  ·  " + a.MeasuredFiles.ToString("N0") + " files" : a.Measuring ? "calculating…" : "";
                Add(r, "Folder size (measured)", fs);
                Add(r, "Running now", a.RunText ?? (a.Measuring ? "checking…" : ""));
            }
            return r;
        }

        /// <summary>Measures the install folder and finds running processes (slow, so it runs in the background).</summary>
        public static void Measure(AppItem a)
        {
            long total = 0; int files = 0; string run = "Not running";
            string root = (a.Location ?? "").TrimEnd('\\');
            try
            {
                if (root.Length > 3 && Directory.Exists(root))
                {
                    var sw = Stopwatch.StartNew(); var st = new Stack<string>(); st.Push(root);
                    while (st.Count > 0 && sw.Elapsed.TotalSeconds < 12)
                    {
                        string dir = st.Pop();
                        try { foreach (var f in Directory.EnumerateFiles(dir)) { try { total += new FileInfo(f).Length; files++; } catch { } } } catch { }
                        try { foreach (var sd in Directory.EnumerateDirectories(dir)) { try { if ((File.GetAttributes(sd) & FileAttributes.ReparsePoint) == 0) st.Push(sd); } catch { } } } catch { }
                    }
                    int n = 0; long ws = 0;
                    foreach (var p in Process.GetProcesses())
                    {
                        try { string path = p.MainModule.FileName; if (path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) { n++; ws += p.WorkingSet64; } } catch { } finally { p.Dispose(); }
                    }
                    if (n > 0) run = n + " process" + (n == 1 ? "" : "es") + "  ·  " + Ui.Fmt(ws) + " memory";
                }
            }
            catch { }
            a.MeasuredFiles = files; a.RunText = run; a.MeasuredBytes = total;
        }

        // true while the app's uninstall entry is still registered
        public static bool StillInstalled(AppItem a)
        {
            try
            {
                foreach (var t in new[] { Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64), Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32), Tuple.Create(RegistryHive.CurrentUser, RegistryView.Registry64) })
                    using (var bk = RegistryKey.OpenBaseKey(t.Item1, t.Item2))
                    using (var u = bk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (u == null) continue;
                        foreach (var sn in u.GetSubKeyNames())
                            using (var k = u.OpenSubKey(sn))
                                if (k != null && string.Equals(k.GetValue("DisplayName") as string, a.Name, StringComparison.OrdinalIgnoreCase)) return true;
                    }
            }
            catch { return true; }
            return false;
        }

        static void SplitCommand(string cmd, out string exe, out string args)
        {
            cmd = Environment.ExpandEnvironmentVariables(cmd.Trim());
            if (cmd.StartsWith("\""))
            {
                int q = cmd.IndexOf('"', 1);
                if (q > 0) { exe = cmd.Substring(1, q - 1); args = cmd.Substring(q + 1).Trim(); return; }
            }
            if (cmd.Contains("://")) { exe = cmd; args = ""; return; }                // protocol link such as steam://uninstall/..
            int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (i > 0) { exe = cmd.Substring(0, i + 4); args = cmd.Substring(i + 4).Trim(); return; }
            int sp = cmd.IndexOf(' ');
            if (sp > 0) { exe = cmd.Substring(0, sp); args = cmd.Substring(sp + 1); } else { exe = cmd; args = ""; }
        }

        public static void Remove(AppItem a)
        {
            ProcessStartInfo psi;
            if (a.Package != null)
                psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"Remove-AppxPackage -Package '" + a.Package + "'\"") { UseShellExecute = false, CreateNoWindow = true };
            else
            {
                string cmd = a.Uninstall.Trim();
                if (cmd.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase)) cmd = Regex.Replace(cmd, @"/I\s*(?=\{)", "/X", RegexOptions.IgnoreCase);
                string exe, args;
                SplitCommand(cmd, out exe, out args);
                psi = new ProcessStartInfo(exe, args) { UseShellExecute = true };      // exactly how Windows itself starts an uninstaller
            }
            using (var p = Process.Start(psi))
            {
                var sw = Stopwatch.StartNew();
                if (p != null)
                    while (!p.WaitForExit(2000))
                    {
                        if (a.Package == null && !StillInstalled(a)) return;           // finished, even if the uninstaller process lingers
                        if (sw.Elapsed.TotalMinutes > 25) return;
                    }
                // launcher-style uninstallers (Steam, Epic ...) return at once and finish later: keep watching for up to 4 minutes
                if (a.Package == null)
                    for (int i = 0; i < 120 && StillInstalled(a); i++) Thread.Sleep(2000);
            }
        }
    }
}
