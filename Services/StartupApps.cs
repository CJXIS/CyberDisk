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
    static class Startup
    {
        const string RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string APPROVED = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        static bool IsOn(RegistryHive hive, string key, string name)
        {
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var k = bk.OpenSubKey(APPROVED + key))
                {
                    var v = k == null ? null : k.GetValue(name) as byte[];
                    return v == null || v.Length == 0 || (v[0] & 1) == 0;
                }
            }
            catch { return true; }
        }

        public static bool SetEnabled(StartupVm s, bool on)
        {
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(s.Hive, RegistryView.Registry64))
                using (var k = bk.CreateSubKey(APPROVED + s.ApprovedKey))
                {
                    var b = new byte[12];
                    if (on) b[0] = 2; else { b[0] = 3; BitConverter.GetBytes(DateTime.Now.ToFileTime()).CopyTo(b, 4); }
                    k.SetValue(s.Name, b, RegistryValueKind.Binary);
                }
                return true;
            }
            catch { return false; }
        }

        // ───────── removal (with a saved copy, so it can be restored) ─────────
        sealed class Removed { public string Kind, Hive, View, Key, Name, Command, Original, Backup; }

        static string RemovedFile { get { return Path.Combine(Settings.Dir, "removed-startup.ini"); } }
        static string RemovedDir { get { return Path.Combine(Settings.Dir, "removed-startup"); } }
        static string Clean(string v) { return (v ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '); }

        static List<Removed> LoadRemoved()
        {
            var l = new List<Removed>();
            try
            {
                foreach (var line in File.ReadAllLines(RemovedFile))
                {
                    var f = line.Split('\t');
                    if (f.Length >= 8) l.Add(new Removed { Kind = f[0], Hive = f[1], View = f[2], Key = f[3], Name = f[4], Command = f[5], Original = f[6], Backup = f[7] });
                }
            }
            catch { }
            return l;
        }

        static void SaveRemoved(List<Removed> l)
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.WriteAllLines(RemovedFile, l.Select(r => string.Join("\t", new[] { r.Kind, r.Hive, r.View, r.Key, Clean(r.Name), Clean(r.Command), Clean(r.Original), Clean(r.Backup) })));
            }
            catch { }
        }

        public static int RemovedCount() { return LoadRemoved().Count; }
        public static List<string> RemovedNames() { return LoadRemoved().Select(r => r.Name).ToList(); }

        // every place Windows (and Task Manager) reads startup programs from
        const string POLICY = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run";

        internal sealed class Src { public RegistryHive Hive; public RegistryView View; public string Path, Approved, Label; }
        static readonly Src[] RunSources =
        {
            new Src { Hive = RegistryHive.CurrentUser,  View = RegistryView.Registry64, Path = RUN,    Approved = "Run",   Label = "Run list · this user" },
            new Src { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64, Path = RUN,    Approved = "Run",   Label = "Run list · all users" },
            new Src { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry32, Path = RUN,    Approved = "Run32", Label = "Run list · all users (32-bit)" },
            new Src { Hive = RegistryHive.CurrentUser,  View = RegistryView.Registry64, Path = POLICY, Approved = null,    Label = "Policy list · this user" },
            new Src { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64, Path = POLICY, Approved = null,    Label = "Policy list · all users" },
            new Src { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry32, Path = POLICY, Approved = null,    Label = "Policy list · all users (32-bit)" },
        };

        /// <summary>One thing that starts the program: a registry value or a file in a startup folder.</summary>
        public sealed class Match { internal Src Source; public string Name, Command, FilePath, Label; public RegistryHive FolderHive; }

        public sealed class RemoveResult { public List<string> Places = new List<string>(), Failed = new List<string>(); public bool Running; public string ProgramName = ""; }

        /// <summary>Everything that starts the same program as this entry (same name or same executable) - this is what will be removed.</summary>
        public static List<Match> Matches(StartupVm s)
        {
            var res = new List<Match>();
            string exe = ExeOf(s.Command).ToLowerInvariant();
            bool useExe = exe.Length > 3;
            foreach (var src in RunSources)
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(src.Hive, src.View))
                    using (var rk = bk.OpenSubKey(src.Path))
                    {
                        if (rk == null) continue;
                        foreach (var n in rk.GetValueNames())
                        {
                            string v = rk.GetValue(n) as string; if (string.IsNullOrEmpty(n) || v == null) continue;
                            bool byName = src.Approved != null && string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase);
                            bool byExe = useExe && ExeOf(v).ToLowerInvariant() == exe;
                            if (byName || byExe) res.Add(new Match { Source = src, Name = n, Command = v, Label = src.Label });
                        }
                    }
                }
                catch { }
            }
            var folders = new[] { Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Startup folder · this user"),
                                  Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Startup folder · all users") };
            foreach (var f in folders)
            {
                try
                {
                    if (!Directory.Exists(f.Item1)) continue;
                    foreach (var file in Directory.GetFiles(f.Item1))
                    {
                        string n = Path.GetFileName(file);
                        if (n.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        bool same = string.Equals(file, s.Command, StringComparison.OrdinalIgnoreCase) || (s.ApprovedKey == "StartupFolder" && string.Equals(n, s.Name, StringComparison.OrdinalIgnoreCase));
                        if (same) res.Add(new Match { FilePath = file, Name = n, Command = file, Label = f.Item3, FolderHive = f.Item2 });
                    }
                }
                catch { }
            }
            return res;
        }

        static void DeleteApproved(RegistryHive hive, string key, string name)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var k = bk.OpenSubKey(APPROVED + key, true)) { if (k != null) k.DeleteValue(name, false); }
            }
            catch { }
        }

        /// <summary>
        /// Deletes the startup entries for good (registry values and startup-folder files), checks that each one is really gone,
        /// and keeps a copy of everything so RestoreAll can put it back.
        /// </summary>
        public static RemoveResult Remove(List<Match> matches, string programName, string exePath)
        {
            var result = new RemoveResult { ProgramName = programName };
            var records = LoadRemoved();
            foreach (var m in matches)
            {
                try
                {
                    if (m.Source == null)                                    // a file in a startup folder
                    {
                        if (File.Exists(m.FilePath))
                        {
                            Directory.CreateDirectory(RemovedDir);
                            string copy = Path.Combine(RemovedDir, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(m.FilePath));
                            File.Copy(m.FilePath, copy, true);
                            File.SetAttributes(m.FilePath, FileAttributes.Normal);
                            File.Delete(m.FilePath);
                            if (File.Exists(m.FilePath)) { result.Failed.Add(m.Label + " · " + m.Name); continue; }
                            records.Add(new Removed { Kind = "file", Hive = m.FolderHive.ToString(), View = "", Key = "StartupFolder", Name = m.Name, Command = m.Command, Original = m.FilePath, Backup = copy });
                        }
                        DeleteApproved(m.FolderHive, "StartupFolder", m.Name);
                        result.Places.Add(m.Label + " · " + m.Name);
                        continue;
                    }
                    var src = m.Source; bool gone = false;
                    using (var bk = RegistryKey.OpenBaseKey(src.Hive, src.View))
                    using (var rk = bk.OpenSubKey(src.Path, true))
                    {
                        if (rk != null)
                        {
                            rk.DeleteValue(m.Name, false);
                            gone = rk.GetValue(m.Name) == null;
                        }
                        else gone = true;
                    }
                    if (!gone)                                                // second try with the built-in registry tool
                    {
                        string root = src.Hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM", o;
                        Fixer.Exec("reg.exe", "delete \"" + root + "\\" + src.Path + "\" /v \"" + m.Name + "\" /f /reg:" + (src.View == RegistryView.Registry32 ? "32" : "64"), 8000, out o);
                        using (var bk = RegistryKey.OpenBaseKey(src.Hive, src.View))
                        using (var rk = bk.OpenSubKey(src.Path)) gone = rk == null || rk.GetValue(m.Name) == null;
                    }
                    if (!gone) { result.Failed.Add(m.Label + " · " + m.Name); continue; }
                    DeleteApproved(src.Hive, src.Approved, m.Name);
                    records.Add(new Removed { Kind = "reg", Hive = src.Hive.ToString(), View = src.View.ToString(), Key = src.Approved ?? "", Name = m.Name, Command = m.Command, Original = src.Path == RUN ? "" : src.Path, Backup = "" });
                    result.Places.Add(m.Label + " · " + m.Name);
                }
                catch (Exception ex) { result.Failed.Add(m.Label + " · " + m.Name + " (" + ex.Message + ")"); }
            }
            SaveRemoved(records);
            try
            {
                string pn = Path.GetFileNameWithoutExtension(exePath ?? "");
                result.Running = pn.Length > 0 && Process.GetProcessesByName(pn).Length > 0;
            }
            catch { }
            return result;
        }

        /// <summary>Puts every removed entry back. Returns how many could not be restored.</summary>
        public static int RestoreAll()
        {
            int failed = 0; var keep = new List<Removed>();
            foreach (var r in LoadRemoved())
            {
                try
                {
                    var hive = (RegistryHive)Enum.Parse(typeof(RegistryHive), r.Hive);
                    if (r.Kind == "file")
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(r.Original));
                        File.Copy(r.Backup, r.Original, true);
                        try { File.Delete(r.Backup); } catch { }
                    }
                    else
                    {
                        var view = (RegistryView)Enum.Parse(typeof(RegistryView), r.View);
                        using (var bk = RegistryKey.OpenBaseKey(hive, view))
                        using (var rk = bk.CreateSubKey(string.IsNullOrEmpty(r.Original) ? RUN : r.Original)) rk.SetValue(r.Name, r.Command, RegistryValueKind.String);
                    }
                    DeleteApproved(hive, r.Key, r.Name);                                                                           // back to "on"
                }
                catch { failed++; keep.Add(r); }
            }
            SaveRemoved(keep);
            return failed;
        }

        /// <summary>True when an icon has no visible pixels at all.</summary>
        static bool IsBlank(ImageSource src)
        {
            var bs = src as BitmapSource; if (bs == null) return src == null;
            try
            {
                int w = bs.PixelWidth, h = bs.PixelHeight;
                var px = new byte[w * h * 4];
                new FormatConvertedBitmap(bs, PixelFormats.Bgra32, null, 0).CopyPixels(px, w * 4, 0);
                for (int i = 3; i < px.Length; i += 4) if (px[i] > 24) return false;
                return true;
            }
            catch { return false; }
        }

        static string ExeOf(string cmd)
        {
            cmd = Environment.ExpandEnvironmentVariables((cmd ?? "").Trim());
            if (cmd.StartsWith("\"")) { int e = cmd.IndexOf('"', 1); if (e > 1) return cmd.Substring(1, e - 1); }
            var m = System.Text.RegularExpressions.Regex.Match(cmd, @"^(.*?\.(exe|bat|cmd|lnk|com|msi|vbs))(\s|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
            int sp = cmd.IndexOf(' ');
            return sp > 0 ? cmd.Substring(0, sp) : cmd;
        }

        /// <summary>Finds the program behind an entry: its icon, publisher and whether the file still exists.</summary>
        static void Resolve(StartupVm vm)
        {
            try
            {
                string exe = ExeOf(vm.Command);
                vm.ExePath = exe;
                bool ok = exe.Length > 0 && (File.Exists(exe) || Directory.Exists(exe));
                vm.Missing = !ok;
                if (ok)
                {
                    try { var vi = FileVersionInfo.GetVersionInfo(exe); vm.Publisher = !string.IsNullOrWhiteSpace(vi.CompanyName) ? vi.CompanyName.Trim() : (vi.ProductName ?? "").Trim(); } catch { }
                    string ext = Path.GetExtension(exe).ToLowerInvariant();
                    if (ext == ".exe") vm.Icon = ShellIcons.FromFile(exe, 0);
                    if (vm.Icon == null) vm.Icon = ShellIcons.Get(ext, false).Icon;
                    if (IsBlank(vm.Icon)) vm.Icon = null;                       // some programs return an empty picture: show the letter tile instead
                }
                else vm.Icon = null;
            }
            catch { }
        }

        public static List<StartupVm> List()
        {
            var res = new List<StartupVm>();
            var runs = new[]
            {
                Tuple.Create(RegistryHive.CurrentUser, RegistryView.Registry64, "Run", "Registry (user)"),
                Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64, "Run", "Registry (all users)"),
                Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32, "Run32", "Registry (all users, 32-bit)"),
            };
            foreach (var t in runs)
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(t.Item1, t.Item2))
                    using (var rk = bk.OpenSubKey(RUN))
                    {
                        if (rk == null) continue;
                        foreach (var n in rk.GetValueNames())
                        {
                            var v = rk.GetValue(n) as string;
                            if (string.IsNullOrEmpty(n) || v == null) continue;
                            var vm = new StartupVm { Name = n, Command = v, Source = t.Item4, Hive = t.Item1, ApprovedKey = t.Item3 };
                            vm.Init(IsOn(t.Item1, t.Item3, n)); Resolve(vm);
                            res.Add(vm);
                        }
                    }
                }
                catch { }
            }
            var folders = new[]
            {
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, "Startup folder (user)"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, "Startup folder (all users)"),
            };
            foreach (var f in folders)
            {
                try
                {
                    if (!Directory.Exists(f.Item1)) continue;
                    foreach (var file in Directory.GetFiles(f.Item1))
                    {
                        var n = Path.GetFileName(file);
                        if (n.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        var vm = new StartupVm { Name = n, Command = file, Source = f.Item3, Hive = f.Item2, ApprovedKey = "StartupFolder" };
                        vm.Init(IsOn(f.Item2, "StartupFolder", n)); Resolve(vm);
                        res.Add(vm);
                    }
                }
                catch { }
            }
            return res.OrderByDescending(x => x.Enabled).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
