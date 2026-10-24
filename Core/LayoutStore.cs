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
    // ───────────────────────── Layout settings (zoom, order, pane sizes) ─────────────────────────
    static class Cfg
    {
        static string P { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CyberDisk", "layout.ini"); } }
        public static Dictionary<string, string> Load()
        {
            var d = new Dictionary<string, string>();
            try { foreach (var l in File.ReadAllLines(P)) { int i = l.IndexOf('='); if (i > 0) d[l.Substring(0, i)] = l.Substring(i + 1); } } catch { }
            return d;
        }
        public static void Save(Dictionary<string, string> d)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(P)); File.WriteAllLines(P, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { }
        }
        public static void Reset() { try { File.Delete(P); } catch { } }
    }
}
