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
    // ───────────────────────── Settings (own file, survives "Reset layout") ─────────────────────────
    static class Settings
    {
        static string P { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CyberDisk", "settings.ini"); } }
        public static string Dir { get { return Path.GetDirectoryName(P); } }
        static Dictionary<string, string> d = new Dictionary<string, string>();

        public static void Load(Dictionary<string, string> legacy)
        {
            d = new Dictionary<string, string>();
            try { foreach (var l in File.ReadAllLines(P)) { int i = l.IndexOf('='); if (i > 0) d[l.Substring(0, i)] = l.Substring(i + 1); } } catch { }
            string v;
            if (!d.ContainsKey("tray") && legacy != null && legacy.TryGetValue("tray", out v)) d["tray"] = v;     // migrate the old tray option
        }
        public static void Save() { try { Directory.CreateDirectory(Dir); File.WriteAllLines(P, d.Select(kv => kv.Key + "=" + kv.Value)); } catch { } }
        public static void Reset() { d.Clear(); try { File.Delete(P); } catch { } }

        static bool B(string k, bool def) { string v; return d.TryGetValue(k, out v) ? v == "1" : def; }
        static void SetB(string k, bool v) { d[k] = v ? "1" : "0"; Save(); }

        public static string Lang { get { string v; return d.TryGetValue("lang", out v) && v == "ar" ? "ar" : "en"; } set { d["lang"] = value; Save(); } }
        public static bool TrayOnClose { get { return B("tray", true); } set { SetB("tray", value); } }
        public static bool ShowWelcome { get { return B("welcome", true); } set { SetB("welcome", value); } }
        public static bool Animations { get { return B("anim", true); } set { SetB("anim", value); } }
        public static bool CrashAlerts { get { return B("crashAlerts", true); } set { SetB("crashAlerts", value); } }
        public static bool StartTray { get { return B("startTray", false); } set { SetB("startTray", value); } }
        public static string StartPage { get { string v; return d.TryGetValue("startPage", out v) && !string.IsNullOrEmpty(v) ? v : "drives"; } set { d["startPage"] = value; Save(); } }
    }
}
