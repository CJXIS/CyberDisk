using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
    // ───────────────────────── Temperatures: every sensor Windows lets a program read ─────────────────────────
    /// <summary>
    /// Reads temperatures without installing a driver: ACPI thermal zones, NVIDIA cards (nvidia-smi), drive sensors (SMART) and,
    /// when LibreHardwareMonitor or OpenHardwareMonitor is running, everything that program shows (CPU package, AMD / Intel graphics ...).
    /// </summary>
    static class Thermals
    {
        public sealed class Reading { public string Key = "", Label = "", Group = "other"; public double Value; public string Format = "{0:0} °C"; public bool IsTemp = true; }

        static readonly SolidColorBrush cool = Fr("#3DDC97"), warm = Fr("#FFB84D"), hot = Fr("#FF6B8A"), neutral = Fr("#E6EEFA");
        static SolidColorBrush Fr(string hex) { var b = new SolidColorBrush(Ui.Col(hex)); b.Freeze(); return b; }

        public static Brush ToneOf(Reading r) { return !r.IsTemp ? neutral : r.Value >= 85 ? hot : r.Value >= 70 ? warm : cool; }
        public static string TextOf(Reading r) { return string.Format(CultureInfo.InvariantCulture, r.Format, r.Value); }

        public static List<Reading> Read(bool includeDisks)
        {
            var res = new List<Reading>();
            try { AddZones(res); } catch { }
            try { AddMonitorTool(res, "LibreHardwareMonitor"); AddMonitorTool(res, "OpenHardwareMonitor"); } catch { }
            try { AddNvidia(res); } catch { }
            if (includeDisks) { try { AddDisks(res); } catch { } }
            return res.GroupBy(r => r.Key).Select(g => g.First()).ToList();                  // one row per sensor
        }

        static string NiceGpu(string n) { return (n ?? "").Replace("NVIDIA GeForce ", "").Replace("NVIDIA ", "").Trim(); }

        /// <summary>The part number is what tells two drives apart: "Hitachi HDS721010CLA332" becomes "HDS721010CLA332".</summary>
        static string NiceDrive(string model)
        {
            var tokens = Regex.Replace(model ?? "", @"\s+", " ").Trim().Split(' ');
            var part = tokens.Where(t => t.Any(char.IsDigit) && t.Any(char.IsLetter)).OrderByDescending(t => t.Length).FirstOrDefault();
            return part ?? string.Join(" ", tokens);
        }

        static string Short(string name)
        {
            int i = Math.Max(name.LastIndexOf('.'), Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('_')));
            return i >= 0 && i < name.Length - 1 ? name.Substring(i + 1) : name;
        }

        static void AddZones(List<Reading> res)
        {
            int n = 0;
            foreach (var z in Wmi.Query("SELECT Name, Temperature, HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation"))
            {
                double k = Wmi.Long(z, "HighPrecisionTemperature") > 0 ? Wmi.Long(z, "HighPrecisionTemperature") / 10.0 : Wmi.Long(z, "Temperature");
                double c = k - 273.15; n++;
                if (c < 5 || c > 125) continue;
                res.Add(new Reading { Key = "zone:" + Wmi.Str(z, "Name"), Label = "Motherboard (" + Short(Wmi.Str(z, "Name")) + ")", Group = "board", Value = c });
            }
            if (n > 0) return;
            foreach (var z in Wmi.Query(@"root\wmi", "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
            {
                double c = Wmi.Long(z, "CurrentTemperature") / 10.0 - 273.15;
                if (c < 5 || c > 125) continue;
                res.Add(new Reading { Key = "acpi:" + Wmi.Str(z, "InstanceName"), Label = "Motherboard (" + Short(Wmi.Str(z, "InstanceName")) + ")", Group = "board", Value = c });
            }
        }

        static void AddMonitorTool(List<Reading> res, string ns)
        {
            foreach (var s in Wmi.Query(@"root\" + ns, "SELECT Name, Value, Identifier, SensorType FROM Sensor"))
            {
                if (!string.Equals(Wmi.Str(s, "SensorType"), "Temperature", StringComparison.OrdinalIgnoreCase)) continue;
                double v = Wmi.Dbl(s, "Value"); if (v < 1 || v > 130) continue;
                string id = Wmi.Str(s, "Identifier").ToLowerInvariant(), name = Wmi.Str(s, "Name");
                string group = id.Contains("cpu") ? "cpu" : id.Contains("gpu") ? "gpu" : id.Contains("hdd") || id.Contains("nvme") || id.Contains("ssd") ? "drive" : id.Contains("lpc") || id.Contains("mainboard") ? "board" : "other";
                string prefix = group == "cpu" ? "CPU · " : group == "gpu" ? "GPU · " : group == "drive" ? "Drive · " : group == "board" ? "Board · " : "";
                res.Add(new Reading { Key = "tool:" + id, Label = prefix + name, Group = group, Value = v });
            }
        }

        static string NvidiaExe()
        {
            string exe = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
            if (File.Exists(exe)) return exe;
            exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
            return File.Exists(exe) ? exe : null;
        }

        static void AddNvidia(List<Reading> res)
        {
            string exe = NvidiaExe(); if (exe == null) return;
            string o;
            if (Fixer.Exec(exe, "--query-gpu=index,name,temperature.gpu,fan.speed,power.draw --format=csv,noheader,nounits", 5000, out o) != 0) return;
            foreach (var line in o.Split('\n'))
            {
                var f = line.Trim().Split(',').Select(x => x.Trim()).ToArray();
                if (f.Length < 5) continue;
                double v;
                if (double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) res.Add(new Reading { Key = "nv" + f[0] + ":t", Label = "GPU · " + NiceGpu(f[1]), Group = "gpu", Value = v });
                if (double.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) res.Add(new Reading { Key = "nv" + f[0] + ":fan", Label = "GPU fan", Group = "gpu", Value = v, Format = v < 1 ? "{0:0} %  ·  stopped (low load)" : "{0:0} %", IsTemp = false });
                if (double.TryParse(f[4], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) res.Add(new Reading { Key = "nv" + f[0] + ":w", Label = "GPU power draw", Group = "gpu", Value = v, Format = "{0:0.#} W", IsTemp = false });
            }
        }

        static void AddDisks(List<Reading> res)
        {
            foreach (var d in DiskInfo.Get(600))
                if (d.TempC.HasValue && d.TempC.Value > 0 && d.TempC.Value < 130)
                    res.Add(new Reading { Key = "disk:" + d.Number, Label = "Drive " + d.Number + " · " + NiceDrive(d.Model), Group = "drive", Value = d.TempC.Value });
        }

        static int Order(string g) { return g == "cpu" ? 0 : g == "board" ? 1 : g == "gpu" ? 2 : g == "drive" ? 3 : 4; }

        /// <summary>Creates the rows of the card (create = true) or refreshes the numbers; sensors that appear later get a row too.</summary>
        public static void Fill(HwCard card, List<Reading> rs, bool create)
        {
            var added = new List<Reading>();
            foreach (var r in rs) if (!card.LiveRows.ContainsKey(r.Key)) added.Add(r);
            if (added.Count > 0)
            {
                var list = new List<HwRow>(card.Rows);
                foreach (var r in added.OrderBy(x => Order(x.Group)).ThenBy(x => x.Label)) { var row = new HwRow { K = r.Label }; list.Add(row); card.LiveRows[r.Key] = row; }
                // keep rows grouped: board / CPU first, then graphics, then drives
                var order = new Dictionary<string, int>(StringComparer.Ordinal); foreach (var x in rs) order[x.Key] = Order(x.Group);
                var byRow = card.LiveRows.ToDictionary(kv => kv.Value, kv => kv.Key);
                card.Rows = list.OrderBy(h => { string k; int o; return byRow.TryGetValue(h, out k) && order.TryGetValue(k, out o) ? o : 3; }).ToList();
            }
            foreach (var r in rs)
            {
                HwRow row;
                if (!card.LiveRows.TryGetValue(r.Key, out row)) continue;
                row.V = TextOf(r); row.Tone = ToneOf(r);
            }
            var hottest = rs.Where(r => r.IsTemp).OrderByDescending(r => r.Value).FirstOrDefault();
            card.Headline = hottest == null ? "No sensor available" : "Hottest: " + TextOf(hottest) + "  ·  " + hottest.Label;
            card.Sub = "Live  ·  updated " + DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
