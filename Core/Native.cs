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
    // ───────────────────────── Win32 ─────────────────────────
    static class Native
    {
        public const uint CLOUD = 0x1000 | 0x40000 | 0x400000;   // offline / cloud-only placeholders (not really on disk)

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime, ftLastAccessTime, ftLastWriteTime;
            public uint nFileSizeHigh, nFileSizeLow, dwReserved0, dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindFirstFileEx(string name, int infoLevel, out WIN32_FIND_DATA data, int searchOp, IntPtr filter, int flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FindNextFile(IntPtr h, out WIN32_FIND_DATA data);
        [DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr h);

        // ── which physical disk holds a drive letter (IOCTL_STORAGE_GET_DEVICE_NUMBER) ──
        [StructLayout(LayoutKind.Sequential)]
        struct STORAGE_DEVICE_NUMBER { public uint DeviceType, DeviceNumber, PartitionNumber; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr h, uint code, IntPtr inBuf, uint inSize, out STORAGE_DEVICE_NUMBER outBuf, uint outSize, out uint returned, IntPtr overlapped);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        /// <summary>Number of the physical disk that holds <paramref name="letter"/> ("C" or "C:"), or -1 when Windows cannot say (network drive, optical drive, spanned volume ...).</summary>
        public static int DiskNumberOfLetter(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return -1;
            IntPtr h = new IntPtr(-1);
            try
            {
                h = CreateFile(@"\\.\" + letter.Substring(0, 1) + ":", 0, 7, IntPtr.Zero, 3, 0, IntPtr.Zero);          // access 0: no admin rights needed to ask
                if (h == new IntPtr(-1)) return -1;
                STORAGE_DEVICE_NUMBER n; uint ret;
                return DeviceIoControl(h, 0x002D1080, IntPtr.Zero, 0, out n, (uint)Marshal.SizeOf(typeof(STORAGE_DEVICE_NUMBER)), out ret, IntPtr.Zero) && n.DeviceType == 7 ? (int)n.DeviceNumber : -1;   // 7 = FILE_DEVICE_DISK
            }
            catch { return -1; }
            finally { if (h != new IntPtr(-1) && h != IntPtr.Zero) CloseHandle(h); }
        }

        // ── when a registry key was last written (used to ignore stale "restart pending" flags) ──
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegQueryInfoKey(Microsoft.Win32.SafeHandles.SafeRegistryHandle hKey, IntPtr lpClass, IntPtr lpcbClass, IntPtr lpReserved, IntPtr lpcSubKeys, IntPtr lpcbMaxSubKeyLen,
            IntPtr lpcbMaxClassLen, IntPtr lpcValues, IntPtr lpcbMaxValueNameLen, IntPtr lpcbMaxValueLen, IntPtr lpcbSecurityDescriptor, out long lpftLastWriteTime);

        public static DateTime? KeyWriteTime(Microsoft.Win32.RegistryKey key)
        {
            try { long ft; return RegQueryInfoKey(key.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out ft) == 0 ? DateTime.FromFileTime(ft) : (DateTime?)null; }
            catch { return null; }
        }
    }
}
