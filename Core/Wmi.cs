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
    // ───────────────────────── WMI helpers ─────────────────────────
    /// <summary>Small, defensive wrappers around System.Management so callers never deal with nulls or exceptions.</summary>
    static class Wmi
    {
        /// <summary>Runs a WQL query and returns every result; an empty list if WMI fails.</summary>
        public static List<System.Management.ManagementBaseObject> Query(string scope, string wql)
        {
            var list = new List<System.Management.ManagementBaseObject>();
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher(scope, wql))
                    foreach (var o in s.Get()) list.Add(o);
            }
            catch { }
            return list;
        }

        public static List<System.Management.ManagementBaseObject> Query(string wql) { return Query(@"root\cimv2", wql); }

        /// <summary>Property as trimmed text ("" when missing).</summary>
        public static string Str(System.Management.ManagementBaseObject o, string name)
        {
            try { var v = o[name]; return v == null ? "" : Convert.ToString(v).Trim(); } catch { return ""; }
        }

        public static long Long(System.Management.ManagementBaseObject o, string name)
        {
            try { var v = o[name]; return v == null ? 0 : Convert.ToInt64(v); } catch { return 0; }
        }

        public static double Dbl(System.Management.ManagementBaseObject o, string name)
        {
            try { var v = o[name]; return v == null ? 0 : Convert.ToDouble(v); } catch { return 0; }
        }

        public static bool Bool(System.Management.ManagementBaseObject o, string name)
        {
            try { var v = o[name]; return v is bool && (bool)v; } catch { return false; }
        }

        /// <summary>WMI date strings (yyyymmddHHMMSS.ffffff+UUU) to DateTime; null when unreadable.</summary>
        public static DateTime? Date(System.Management.ManagementBaseObject o, string name)
        {
            try { var v = o[name] as string; return string.IsNullOrEmpty(v) ? (DateTime?)null : System.Management.ManagementDateTimeConverter.ToDateTime(v); } catch { return null; }
        }
    }
}
