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
    static class Leftovers
    {
        const string UNINST = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string RUN = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        static readonly HashSet<string> Generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "microsoft", "windows", "common files", "internet explorer", "windowsapps", "system32", "temp", "packages", "programs", "data", "cache", "logs", "apps", "app",
          "windows nt", "microsoft shared", "uninstall", "installer", "setup", "update", "updates", "drivers", "users", "public", "default", "google", "mozilla", "adobe",
          "intel", "amd", "nvidia", "oracle", "java", "python", "dotnet", "visual studio" };

        static string San(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c.ToString(), "");
            return s.Trim().TrimEnd('.');
        }

        static string StripVer(string n)
        {
            string prev;
            do
            {
                prev = n;
                n = Regex.Replace(n, @"[\s\-_,]*(\(.*?\)|v?\d+(\.\d+)*[a-z]?|x64|x86|64-bit|32-bit)\s*$", "", RegexOptions.IgnoreCase).Trim();
            } while (n != prev && n.Length > 0);
            return n;
        }

        // executable names of the app, collected BEFORE it is uninstalled (needed to match crash dumps etc.)
        public static HashSet<string> Stems(AppItem a)
        {
            var st = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!string.IsNullOrWhiteSpace(a.Location) && Directory.Exists(a.Location))
                    foreach (var f in Directory.GetFiles(a.Location, "*.exe"))
                    {
                        var n = Path.GetFileNameWithoutExtension(f);
                        if (n.Length >= 4 && !n.Has("unins")) st.Add(n);
                    }
                var m = Regex.Match(a.IconPath ?? "", "([^\\\\\"',]+)\\.exe", RegexOptions.IgnoreCase);
                if (m.Success && m.Groups[1].Value.Length >= 4) st.Add(m.Groups[1].Value);
            }
            catch { }
            return st;
        }

        public static List<Leftover> Find(AppItem a, HashSet<string> stems)
        {
            var res = new List<Leftover>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stems == null) stems = new HashSet<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string pub = San(a.Publisher ?? "");
            var folderBrush = new SolidColorBrush(Ui.Col("#3DD6F5")); folderBrush.Freeze();      // created on a worker thread: must be frozen or the UI thread throws when it renders the list
            var fileBrush = new SolidColorBrush(Ui.Col("#FFB84D")); fileBrush.Freeze();      // created on a worker thread: must be frozen or the UI thread throws when it renders the list
            var regBrush = new SolidColorBrush(Ui.Col("#8B7CFF")); regBrush.Freeze();      // created on a worker thread: must be frozen or the UI thread throws when it renders the list
            var runBrush = new SolidColorBrush(Ui.Col("#FF6B8A")); runBrush.Freeze();      // created on a worker thread: must be frozen or the UI thread throws when it renders the list

            var names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);   // name -> exact match?
            Action<string, bool> addName = (s, exact) =>
            {
                s = San(s);
                if (s.Length < 4 || Generic.Contains(s)) return;
                bool cur;
                if (!names.TryGetValue(s, out cur) || (exact && !cur)) names[s] = exact || cur;
            };
            addName(a.Name, true);
            addName(StripVer(a.Name ?? ""), false);
            string loc = (a.Location ?? "").TrimEnd('\\');
            if (loc.Length > 3) addName(Path.GetFileName(loc), true);
            foreach (var st in stems) addName(st, false);

            var bases = new[] { pf, pf86, pd, roam, local, Path.Combine(local, "Programs"),
                                Path.Combine(roam, @"Microsoft\Windows\Start Menu\Programs"), Path.Combine(pd, @"Microsoft\Windows\Start Menu\Programs") }
                        .Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Func<string, bool> underBase = p => bases.Any(b => p.StartsWith(b.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

            Action<string, bool> addDir = (p, on) =>
            {
                try
                {
                    if (!Directory.Exists(p) || !seen.Add(p) || !Sweep.Safe(p) || Generic.Contains(Path.GetFileName(p))) return;
                    string pp = p;
                    res.Add(new Leftover { Kind = "Folder", Shown = p, Target = p, KindBrush = folderBrush, Checked = on, Remove = () => Sweep.DeleteTree(pp) });
                }
                catch { }
            };
            Action<string> addFile = p =>
            {
                try
                {
                    if (!File.Exists(p) || !seen.Add(p)) return;
                    string pp = p; long len = new FileInfo(p).Length;
                    res.Add(new Leftover { Kind = "File", Shown = p, Target = p, Size = len, SizeText = Ui.Fmt(len), KindBrush = fileBrush, Remove = () => Sweep.Del(pp) });
                }
                catch { }
            };
            Action<RegistryHive, RegistryView, string, bool> addKey = (hive, view, sub, on) =>
            {
                try { using (var bk = RegistryKey.OpenBaseKey(hive, view)) using (var k = bk.OpenSubKey(sub)) { if (k == null) return; } }
                catch { return; }
                string disp = (hive == RegistryHive.LocalMachine ? "HKLM\\" : "HKCU\\") + sub + (view == RegistryView.Registry32 ? "   (32-bit view)" : "");
                if (!seen.Add(disp)) return;
                res.Add(new Leftover { Kind = "Registry", Shown = disp, KindBrush = regBrush, Checked = on, Remove = () => { using (var b2 = RegistryKey.OpenBaseKey(hive, view)) b2.DeleteSubKeyTree(sub, false); } });
            };

            string desktop = Path.Combine(up, "Desktop"), pubDesk = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            foreach (var kv in names)
            {
                string n = kv.Key; bool ex = kv.Value;
                foreach (var b in bases) { addDir(Path.Combine(b, n), ex); addFile(Path.Combine(b, n + ".lnk")); }
                if (pub.Length >= 3 && !Generic.Contains(pub))
                    foreach (var b in new[] { pf, pf86, pd, roam, local }) addDir(Path.Combine(b, pub, n), ex);
                addFile(Path.Combine(desktop, n + ".lnk")); addFile(Path.Combine(pubDesk, n + ".lnk"));
                addKey(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\" + n, ex);
                addKey(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\" + n, ex);
                addKey(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\" + n, ex);
                if (pub.Length >= 3 && !Generic.Contains(pub))
                {
                    addKey(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\" + pub + "\\" + n, ex);
                    addKey(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\" + pub + "\\" + n, ex);
                    addKey(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\" + pub + "\\" + n, ex);
                }
            }

            // the original install folder, if it is still there and clearly belongs to this app
            if (loc.Length > 3 && Directory.Exists(loc) && !loc.StartsWith(win, StringComparison.OrdinalIgnoreCase) && !Generic.Contains(Path.GetFileName(loc))
                && !bases.Any(b => string.Equals(b.TrimEnd('\\'), loc, StringComparison.OrdinalIgnoreCase))
                && (underBase(loc) || names.Keys.Any(n => loc.Has(n))))
                addDir(loc, true);

            // crash dumps, error-report folders and prefetch entries of this app's executables
            foreach (var st in stems)
            {
                try { var cd = Path.Combine(local, "CrashDumps"); if (Directory.Exists(cd)) foreach (var f in Directory.GetFiles(cd, st + ".exe.*.dmp")) addFile(f); } catch { }
                try { var pe = Path.Combine(win, "Prefetch"); if (Directory.Exists(pe)) foreach (var f in Directory.GetFiles(pe, st.ToUpperInvariant() + ".EXE-*.pf")) addFile(f); } catch { }
                try
                {
                    var wer = Path.Combine(pd, @"Microsoft\Windows\WER\ReportArchive");
                    if (Directory.Exists(wer))
                        foreach (var d in Directory.GetDirectories(wer))
                        {
                            var dn = Path.GetFileName(d);
                            if ((dn.StartsWith("AppCrash_") || dn.StartsWith("AppHang_")) && dn.Has(st)) addDir(d, true);
                        }
                }
                catch { }
            }

            // the uninstall entry itself, if the uninstaller left it behind
            foreach (var t in new[] { Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64), Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32), Tuple.Create(RegistryHive.CurrentUser, RegistryView.Registry64) })
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(t.Item1, t.Item2))
                    using (var u = bk.OpenSubKey(UNINST))
                    {
                        if (u == null) continue;
                        foreach (var sn in u.GetSubKeyNames())
                            using (var k = u.OpenSubKey(sn))
                            {
                                if (k != null && string.Equals(k.GetValue("DisplayName") as string, a.Name, StringComparison.OrdinalIgnoreCase))
                                    addKey(t.Item1, t.Item2, UNINST + "\\" + sn, true);
                            }
                    }
                }
                catch { }
            }

            // startup entries that still point at the app
            foreach (var t in new[] { Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64), Tuple.Create(RegistryHive.CurrentUser, RegistryView.Registry64) })
            {
                try
                {
                    using (var bk = RegistryKey.OpenBaseKey(t.Item1, t.Item2))
                    using (var rk = bk.OpenSubKey(RUN))
                    {
                        if (rk == null) continue;
                        foreach (var vn in rk.GetValueNames())
                        {
                            var v = rk.GetValue(vn) as string;
                            if (string.IsNullOrEmpty(v)) continue;
                            bool hit = (loc.Length > 6 && v.Has(loc)) || stems.Any(s => v.Has("\\" + s + ".exe"));
                            if (!hit) continue;
                            var hive = t.Item1; var view = t.Item2; string name = vn;
                            res.Add(new Leftover
                            {
                                Kind = "Startup", Shown = (hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU") + "\\...\\Run   →   " + vn, KindBrush = runBrush,
                                Remove = () => { using (var b2 = RegistryKey.OpenBaseKey(hive, view)) using (var r2 = b2.OpenSubKey(RUN, true)) { if (r2 != null) r2.DeleteValue(name, false); } }
                            });
                        }
                    }
                }
                catch { }
            }

            Parallel.ForEach(res.Where(r => r.Kind == "Folder"), new ParallelOptions { MaxDegreeOfParallelism = 4 }, l => { l.Size = Sweep.DirSize(l.Target); l.SizeText = Ui.Fmt(l.Size); });
            return res.OrderBy(r => r.Kind == "Folder" ? 0 : r.Kind == "File" ? 1 : 2).ThenByDescending(r => r.Size).ToList();
        }
    }
}
