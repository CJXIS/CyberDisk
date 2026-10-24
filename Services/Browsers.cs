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
    static class Browsers
    {
        sealed class Def { public string Key, Name; public bool Firefox; public string[] Procs, Roots, Exes; }
        sealed class TypeDef { public string Name, Desc, Risk, Glyph, Accent; public bool On; public string[] Dirs, Files; }
        static readonly string[] None = new string[0];
        static TypeDef T(string name, string desc, string risk, bool on, string glyph, string accent, string[] dirs, string[] files)
            => new TypeDef { Name = name, Desc = desc, Risk = risk, On = on, Glyph = glyph, Accent = accent, Dirs = dirs, Files = files };

        static readonly TypeDef[] Chromium =
        {
            T("Web cache", "Saved copies of pages, images and scripts so sites load faster. Rebuilds itself.", "SAFE", true, "▣", "#3DD6F5",
                new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache", "GrShaderCache", "ShaderCache", "GraphiteDawnCache", "Media Cache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache" }, None),
            T("Cookies", "Keep you signed in. Deleting them signs you out of every website.", "SIGN-OUT", false, "●", "#FFB84D",
                None, new[] { @"Network\Cookies", @"Network\Cookies-journal", "Cookies", "Cookies-journal" }),
            T("Browsing history", "Visited pages, favicons, top sites and address-bar suggestions.", "PRIVACY", false, "◷", "#8B7CFF",
                None, new[] { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal", "Favicons", "Favicons-journal", "Network Action Predictor", "Shortcuts", "Shortcuts-journal" }),
            T("Site data", "Local storage, IndexedDB and offline data kept by websites (can sign you out of some sites).", "SIGN-OUT", false, "▥", "#FF9F6B",
                new[] { "Local Storage", "IndexedDB", "Session Storage", "File System", "WebStorage" }, None),
            T("Autofill & forms", "Saved addresses, cards and search engines (passwords are NOT included).", "PRIVACY", false, "✎", "#5B9BFF",
                None, new[] { "Web Data", "Web Data-journal" }),
            T("Open tabs & sessions", "The tabs that reopen when you restart the browser.", "PRIVACY", false, "▭", "#FF6B8A",
                new[] { "Sessions" }, new[] { "Current Session", "Current Tabs", "Last Session", "Last Tabs" }),
            T("Crash reports & metrics", "Diagnostic data collected by the browser.", "SAFE", true, "☠", "#FF7A7A",
                new[] { @"Crashpad\reports", "BrowserMetrics" }, new[] { "BrowserMetrics-spare.pma" }),
        };

        static readonly TypeDef[] Firefox =
        {
            T("Web cache", "Saved copies of pages and images. Rebuilds itself.", "SAFE", true, "▣", "#3DD6F5",
                new[] { "cache2", "startupCache", "thumbnails", "jumpListCache", "OfflineCache" }, None),
            T("Cookies", "Keep you signed in. Deleting them signs you out of every website.", "SIGN-OUT", false, "●", "#FFB84D",
                None, new[] { "cookies.sqlite", "cookies.sqlite-wal", "cookies.sqlite-shm" }),
            T("Form history", "Text you typed into website forms and the search bar.", "PRIVACY", false, "✎", "#5B9BFF",
                None, new[] { "formhistory.sqlite", "formhistory.sqlite-wal" }),
            T("Open tabs & sessions", "The tabs that reopen when you restart Firefox.", "PRIVACY", false, "▭", "#FF6B8A",
                new[] { "sessionstore-backups" }, new[] { "sessionstore.jsonlz4" }),
        };

        public static List<BrowserVm> Detect()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var defs = new[]
            {
                new Def { Key = "chrome", Name = "Google Chrome", Procs = new[] { "chrome" }, Roots = new[] { Path.Combine(local, @"Google\Chrome\User Data") },
                    Exes = new[] { Path.Combine(pf, @"Google\Chrome\Application\chrome.exe"), Path.Combine(pf86, @"Google\Chrome\Application\chrome.exe"), Path.Combine(local, @"Google\Chrome\Application\chrome.exe") } },
                new Def { Key = "edge", Name = "Microsoft Edge", Procs = new[] { "msedge" }, Roots = new[] { Path.Combine(local, @"Microsoft\Edge\User Data") },
                    Exes = new[] { Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe") } },
                new Def { Key = "brave", Name = "Brave", Procs = new[] { "brave" }, Roots = new[] { Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data") },
                    Exes = new[] { Path.Combine(pf, @"BraveSoftware\Brave-Browser\Application\brave.exe"), Path.Combine(pf86, @"BraveSoftware\Brave-Browser\Application\brave.exe") } },
                new Def { Key = "vivaldi", Name = "Vivaldi", Procs = new[] { "vivaldi" }, Roots = new[] { Path.Combine(local, @"Vivaldi\User Data") },
                    Exes = new[] { Path.Combine(local, @"Vivaldi\Application\vivaldi.exe"), Path.Combine(pf, @"Vivaldi\Application\vivaldi.exe") } },
                new Def { Key = "opera", Name = "Opera", Procs = new[] { "opera" }, Roots = new[] { Path.Combine(roam, @"Opera Software\Opera Stable"), Path.Combine(local, @"Opera Software\Opera Stable") },
                    Exes = new[] { Path.Combine(local, @"Programs\Opera\opera.exe"), Path.Combine(pf, @"Opera\opera.exe") } },
                new Def { Key = "operagx", Name = "Opera GX", Procs = new[] { "opera" }, Roots = new[] { Path.Combine(roam, @"Opera Software\Opera GX Stable"), Path.Combine(local, @"Opera Software\Opera GX Stable") },
                    Exes = new[] { Path.Combine(local, @"Programs\Opera GX\opera.exe") } },
                new Def { Key = "firefox", Name = "Mozilla Firefox", Firefox = true, Procs = new[] { "firefox" }, Roots = new[] { Path.Combine(roam, @"Mozilla\Firefox\Profiles"), Path.Combine(local, @"Mozilla\Firefox\Profiles") },
                    Exes = new[] { Path.Combine(pf, @"Mozilla Firefox\firefox.exe"), Path.Combine(pf86, @"Mozilla Firefox\firefox.exe") } },
            };
            var res = new List<BrowserVm>();
            foreach (var d in defs)
            {
                var profs = new List<string>();
                foreach (var r in d.Roots)
                {
                    if (!Directory.Exists(r)) continue;
                    try
                    {
                        if (d.Firefox) profs.AddRange(Directory.GetDirectories(r));
                        else
                        {
                            profs.Add(r);
                            profs.AddRange(Directory.GetDirectories(r).Where(x => { var n = Path.GetFileName(x); return n == "Default" || n.StartsWith("Profile "); }));
                        }
                    }
                    catch { }
                }
                if (profs.Count == 0) continue;
                var bv = new BrowserVm { Key = d.Key, Name = d.Name, IsFirefox = d.Firefox, Procs = d.Procs, ProfileDirs = profs };
                int np = d.Firefox ? d.Roots.Count(x => Directory.Exists(x)) > 0 ? profs.Count / Math.Max(1, d.Roots.Count(x => Directory.Exists(x))) : 1
                                   : profs.Count(x => { var n = Path.GetFileName(x); return n == "Default" || n.StartsWith("Profile "); });
                if (np < 1) np = 1;
                bv.Info = np + (np == 1 ? " profile" : " profiles") + "  ·  " + (d.Firefox ? "Gecko engine" : "Chromium engine");
                var exe = d.Exes.FirstOrDefault(File.Exists);
                bv.Icon = (exe != null ? ShellIcons.FromFile(exe, 0) : null) ?? ShellIcons.Get(".exe", false).Icon;
                foreach (var t in d.Firefox ? Firefox : Chromium)
                {
                    var dirs = t.Dirs; var files = t.Files; var plist = profs;
                    var it = new CleanItem
                    {
                        Name = t.Name, Desc = t.Desc, Glyph = t.Glyph, Group = d.Name, Browser = d.Key,
                        Accent = new SolidColorBrush(Ui.Col(t.Accent)), Checked = t.On,
                        Roots = () => plist.SelectMany(p => dirs.Select(r => Path.Combine(p, r))).Where(x => Directory.Exists(x)).ToList(),
                        SingleFiles = () => plist.SelectMany(p => files.Select(r => Path.Combine(p, r))).Where(x => File.Exists(x)).ToList()
                    };
                    it.SetRisk(t.Risk);
                    it.DefaultOn = t.On;
                    it.OnChanged = bv.NotifySelected;
                    bv.Types.Add(it);
                }
                res.Add(bv);
            }
            return res;
        }

        // reads the cache index of Chromium browsers to find out WHICH websites the cache belongs to
        public static List<SiteVm> Inspect(BrowserVm b)
        {
            var bytes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            long other = 0;
            foreach (var prof in b.ProfileDirs)
            {
                foreach (var rel in new[] { @"Cache\Cache_Data", "Cache" })
                {
                    var dir = Path.Combine(prof, rel);
                    if (!Directory.Exists(dir)) continue;
                    var sizes = new Dictionary<string, long>();
                    var zero = new List<string>();
                    try
                    {
                        foreach (var fi in new DirectoryInfo(dir).EnumerateFiles())
                        {
                            string n = fi.Name;
                            if (n.Length == 18 && n[16] == '_')
                            {
                                string h = n.Substring(0, 16);
                                long c0; sizes.TryGetValue(h, out c0); sizes[h] = c0 + fi.Length;
                                if (n[17] == '0') zero.Add(fi.FullName);
                            }
                            else other += fi.Length;
                        }
                    }
                    catch { }
                    var hostOf = new ConcurrentDictionary<string, string>();
                    Parallel.ForEach(zero.Take(80000), new ParallelOptions { MaxDegreeOfParallelism = 8 }, f =>
                    {
                        var hs = ReadHost(f);
                        if (hs != null) hostOf[Path.GetFileName(f).Substring(0, 16)] = hs;
                    });
                    foreach (var kv in sizes)
                    {
                        string hst;
                        if (hostOf.TryGetValue(kv.Key, out hst)) { long c1; bytes.TryGetValue(hst, out c1); bytes[hst] = c1 + kv.Value; }
                        else other += kv.Value;
                    }
                }
            }
            long total = bytes.Values.Sum() + other;
            var top = bytes.OrderByDescending(k => k.Value).Take(8).ToList();
            var list = top.Select(k => new SiteVm { Host = k.Key, SizeText = Ui.Fmt(k.Value), Percent = total > 0 ? 100.0 * k.Value / total : 0 }).ToList();
            long rest = total - top.Sum(k => k.Value);
            if (list.Count > 0 && rest > 0) list.Add(new SiteVm { Host = "everything else", SizeText = Ui.Fmt(rest), Percent = 100.0 * rest / total });
            return list;
        }

        static string ReadHost(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var buf = new byte[4096];
                    int n = fs.Read(buf, 0, buf.Length);
                    if (n < 32) return null;
                    if (BitConverter.ToUInt64(buf, 0) != 0xfcfb6d1ba7725c30UL) return null;        // simple-cache magic number
                    int kl = (int)BitConverter.ToUInt32(buf, 12);
                    if (kl <= 0 || 24 + kl > n) return null;
                    string key = Encoding.UTF8.GetString(buf, 24, kl);
                    string url = null;
                    foreach (var tok in key.Split(' '))
                    {
                        int i = tok.IndexOf("http", StringComparison.Ordinal);
                        if (i >= 0 && tok.IndexOf("://", i, StringComparison.Ordinal) > 0) url = tok.Substring(i);
                    }
                    if (url == null) return null;
                    Uri u;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return null;
                    string host = u.Host.ToLowerInvariant();
                    if (host.StartsWith("www.")) host = host.Substring(4);
                    var lab = host.Split('.');
                    if (lab.Length > 2)
                    {
                        int take = (lab[lab.Length - 1].Length == 2 && lab[lab.Length - 2].Length <= 3) ? 3 : 2;
                        host = string.Join(".", lab.Skip(lab.Length - take));
                    }
                    return host;
                }
            }
            catch { return null; }
        }
    }
}
