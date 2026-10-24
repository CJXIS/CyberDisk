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
    static class Procs
    {
        public static readonly HashSet<string> Protected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso", "svchost", "dwm", "fontdrvhost",
          "memory compression", "securityhealthservice", "msmpeng", "sihost", "ctfmon", "audiodg", "wudfhost" };

        public static List<ProcSnap> Snap()
        {
            var l = new List<ProcSnap>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    TimeSpan c;
                    try { c = p.TotalProcessorTime; } catch { c = TimeSpan.Zero; }
                    l.Add(new ProcSnap { Pid = p.Id, Name = p.ProcessName, Mem = p.WorkingSet64, Cpu = c });
                }
                catch { }
                finally { p.Dispose(); }
            }
            return l;
        }
    }
}
