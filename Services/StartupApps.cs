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
                }
                else vm.Icon = ShellIcons.Get(".exe", false).Icon;
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
