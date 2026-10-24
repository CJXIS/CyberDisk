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
    // ───────────────────────── Boost / startup / health ─────────────────────────
    static class Mem
    {
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
        [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr h);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
        [DllImport("dnsapi.dll")] public static extern bool DnsFlushResolverCache();

        public static void Status(out long total, out long avail)
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            GlobalMemoryStatusEx(ref m);
            total = (long)m.ullTotalPhys; avail = (long)m.ullAvailPhys;
        }

        // asks Windows to move a process' idle pages out of its working set (they are paged back in on demand)
        public static bool Trim(int pid)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(0x0100 | 0x0400, false, pid);       // PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION
                return h != IntPtr.Zero && EmptyWorkingSet(h);
            }
            catch { return false; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        public static long TrimAll(out int count)
        {
            long t, before, after;
            Status(out t, out before);
            count = 0;
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses())
            {
                try { if (p.Id != me && p.Id > 4 && Trim(p.Id)) count++; } catch { } finally { p.Dispose(); }
            }
            Thread.Sleep(500);
            Status(out t, out after);
            return Math.Max(0, after - before);
        }

        static long pI, pK, pU;
        public static double Cpu()
        {
            long i, k, u;
            GetSystemTimes(out i, out k, out u);
            double tot = (k - pK) + (u - pU), idle = i - pI;
            pI = i; pK = k; pU = u;
            return tot <= 0 ? 0 : Math.Max(0, Math.Min(100, 100.0 * (1 - idle / tot)));
        }
    }
}
