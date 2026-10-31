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
    // ───────────────────────── Colour theme: dark (original) or light ─────────────────────────
    /// <summary>
    /// The whole app is drawn with one dark palette. For the light theme every colour is converted with the same rules in one place:
    /// dark surfaces become near-white, light text becomes near-black, borders get a little darker than the surface,
    /// and bright accent colours get deeper so they stay readable on white. The theme is read once at start; changing it restarts the app.
    /// </summary>
    static class Theme
    {
        static bool? light;
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly Regex RxColor = new Regex(@"#(?:[0-9A-Fa-f]{8}|[0-9A-Fa-f]{6})\b", RegexOptions.Compiled);

        /// <summary>Saved choice: "dark", "light" or "system". Read straight from settings.ini so it is known before anything is drawn.</summary>
        public static string Saved()
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CyberDisk", "settings.ini");
                foreach (var l in File.ReadAllLines(p)) if (l.StartsWith("theme=")) { string v = l.Substring(6).Trim(); if (v == "light" || v == "system") return v; }
            }
            catch { }
            return "dark";
        }

        public static bool Light
        {
            get
            {
                if (!light.HasValue) { string t = Saved(); light = t == "light" || (t == "system" && SystemIsLight()); }
                return light.Value;
            }
        }

        static bool SystemIsLight()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return k != null && System.Convert.ToInt32(k.GetValue("AppsUseLightTheme", 0)) == 1; }
            catch { return false; }
        }

        public static string MapXaml(string xaml)
        {
            if (!Light) return xaml;
            string s = RxColor.Replace(xaml, m => Map(m.Value));
            return s.Replace("=\"White\"", "=\"" + Map("#FFFFFF") + "\"");
        }

        /// <summary>"#RRGGBB" or "#AARRGGBB" in the dark palette -> the same colour for the active theme.</summary>
        public static string Map(string hex)
        {
            if (!Light || string.IsNullOrEmpty(hex) || hex[0] != '#' || (hex.Length != 7 && hex.Length != 9)) return hex;
            lock (cache)
            {
                string r;
                if (cache.TryGetValue(hex, out r)) return r;
                r = MapColor(hex); cache[hex] = r; return r;
            }
        }

        static string MapColor(string hex)
        {
            string h = hex.Substring(1), alpha = "";
            if (h.Length == 8) { alpha = h.Substring(0, 2); h = h.Substring(2); }
            int ri = System.Convert.ToInt32(h.Substring(0, 2), 16), gi = System.Convert.ToInt32(h.Substring(2, 2), 16), bi = System.Convert.ToInt32(h.Substring(4, 2), 16);
            if (ri == 0 && gi == 0 && bi == 0) return hex;                                  // shadows and dimming layers stay black
            double H, S, L; ToHsl(ri / 255.0, gi / 255.0, bi / 255.0, out H, out S, out L);
            double L2, S2;
            if (S >= 0.55 && L >= 0.42 && L <= 0.85)                                       // accent colours (cyan, green, pink ...): darkened until they read well on white
            {
                S2 = Math.Min(1, S * 0.85); L2 = L;
                for (int i = 0; i < 40 && L2 > 0.25; i++)
                {
                    double ar, ag, ab; FromHsl(H, S2, L2, out ar, out ag, out ab);
                    if ((0.99) / (Luminance(ar, ag, ab) + 0.05) >= 4.3) break;
                    L2 -= 0.01;
                }
            }
            else
            {
                if (L <= 0.14) L2 = Math.Min(0.99, 0.955 + (L - 0.05) * 0.5);              // surfaces: near white, cards a touch whiter than the page
                else if (L <= 0.45) L2 = 1.02 - L * 0.9;                                    // borders, tracks, selections: a little darker than the surface
                else L2 = 1 - L;                                                            // text: light becomes dark
                S2 = S * (L2 > 0.5 ? 0.8 : 0.6);
            }
            double r, g, b; FromHsl(H, S2, Math.Max(0, Math.Min(1, L2)), out r, out g, out b);
            return "#" + alpha + B(r) + B(g) + B(b);
        }

        static double Luminance(double r, double g, double b)
        {
            Func<double, double> f = c => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
        }

        static string B(double v) { return ((int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255)).ToString("X2"); }

        static void ToHsl(double r, double g, double b, out double h, out double s, out double l)
        {
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max == min) { h = s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0); else if (max == g) h = (b - r) / d + 2; else h = (r - g) / d + 4;
            h /= 6;
        }

        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        static void FromHsl(double h, double s, double l, out double r, out double g, out double b)
        {
            if (s == 0) { r = g = b = l; return; }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3); g = Hue(p, q, h); b = Hue(p, q, h - 1.0 / 3);
        }
    }
}
