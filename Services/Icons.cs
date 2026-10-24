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
    // ───────────────────────── Shell icons ─────────────────────────
    sealed class Ico { public ImageSource Icon; public string Type; }

    static class ShellIcons
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon; public int iIcon; public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO fi, uint cb, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint ExtractIconEx(string file, int idx, IntPtr[] large, IntPtr[] small, uint n);
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);

        static readonly ConcurrentDictionary<string, Ico> cache = new ConcurrentDictionary<string, Ico>();

        static ImageSource FromHIcon(IntPtr h)
        {
            try
            {
                var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(h, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally { DestroyIcon(h); }
        }

        public static Ico Get(string ext, bool dir)
        {
            string key = dir ? "\0dir" : ext.ToLowerInvariant();
            return cache.GetOrAdd(key, _ =>
            {
                var fi = new SHFILEINFO();
                SHGetFileInfo(dir ? "folder" : "file" + ext, dir ? 0x10u : 0x80u, ref fi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), 0x100 | 0x1 | 0x10 | 0x400);
                ImageSource ic = null;
                try { if (fi.hIcon != IntPtr.Zero) ic = FromHIcon(fi.hIcon); } catch { }
                string type = !string.IsNullOrEmpty(fi.szTypeName) ? fi.szTypeName : (dir ? "Folder" : (ext.TrimStart('.').ToUpper() + " File").Trim());
                return new Ico { Icon = ic, Type = type };
            });
        }

        public static ImageSource FromFile(string path, int idx)
        {
            try
            {
                var l = new IntPtr[1];
                uint n = ExtractIconEx(path, idx, l, null, 1);
                if (n == 0 || l[0] == IntPtr.Zero) return null;
                return FromHIcon(l[0]);
            }
            catch { return null; }
        }
    }

    static class IconLoader
    {
        public static ImageSource For(AppItem a)
        {
            try
            {
                if (a.Package != null) return Store(a.Location);
                var ic = FromDisplayIcon(a.IconPath);
                if (ic != null) return ic;
                string exe = FindExe(a);
                if (exe != null) { ic = ShellIcons.FromFile(exe, 0); if (ic != null) return ic; }
                var ue = ExeFrom(a.Uninstall);
                if (ue != null && File.Exists(ue) && !Path.GetFileName(ue).Has("msiexec")) { ic = ShellIcons.FromFile(ue, 0); if (ic != null) return ic; }
            }
            catch { }
            return ShellIcons.Get(".exe", false).Icon;
        }

        static ImageSource FromDisplayIcon(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var m = Regex.Match(raw.Trim(), "^\"?(?<f>[^\",]+)\"?(,\\s*(?<i>-?\\d+))?");
            if (!m.Success) return null;
            string p = Environment.ExpandEnvironmentVariables(m.Groups["f"].Value.Trim());
            int idx = m.Groups["i"].Success ? int.Parse(m.Groups["i"].Value) : 0;
            return File.Exists(p) ? ShellIcons.FromFile(p, idx) : null;
        }

        static string ExeFrom(string us)
        {
            if (string.IsNullOrWhiteSpace(us)) return null;
            var m = Regex.Match(us, "^\\s*\"([^\"]+)\"|^\\s*(\\S+?\\.exe)", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return Environment.ExpandEnvironmentVariables(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        }

        static string FindExe(AppItem a)
        {
            var loc = a.Location;
            if (string.IsNullOrWhiteSpace(loc) || !Directory.Exists(loc)) return null;
            var exes = Directory.GetFiles(loc, "*.exe").Where(f => { var n = Path.GetFileName(f); return !n.Has("unins") && !n.Has("setup"); }).ToList();
            if (exes.Count == 0) return null;
            string key = new string(a.Name.Where(char.IsLetterOrDigit).ToArray());
            if (key.Length > 4) key = key.Substring(0, 4);
            return exes.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Has(key)) ?? exes[0];
        }

        static ImageSource Store(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return null;
            string mf = Path.Combine(loc, "AppxManifest.xml");
            if (!File.Exists(mf)) return null;
            string txt = File.ReadAllText(mf);
            var m = Regex.Match(txt, "Square44x44Logo=\"([^\"]+)\"");
            if (!m.Success) m = Regex.Match(txt, "Square150x150Logo=\"([^\"]+)\"");
            if (!m.Success) m = Regex.Match(txt, "<Logo>([^<]+)</Logo>");
            if (!m.Success) return null;
            string full = Path.Combine(loc, m.Groups[1].Value.Replace('/', '\\'));
            string dir = Path.GetDirectoryName(full), stem = Path.GetFileNameWithoutExtension(full), ext = Path.GetExtension(full);
            if (!Directory.Exists(dir)) return null;
            var f = Directory.GetFiles(dir, stem + "*" + ext).Where(x => !x.Has("contrast")).OrderByDescending(x => new FileInfo(x).Length).FirstOrDefault();
            if (f == null) return null;
            var bi = new BitmapImage();
            using (var s = File.OpenRead(f))
            {
                bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.DecodePixelWidth = 48; bi.StreamSource = s; bi.EndInit();
            }
            bi.Freeze();
            return bi;
        }
    }
}
