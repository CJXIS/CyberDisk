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
    // ───────────────────────── Monitors: real refresh rate, colour depth and HDR (Windows display configuration API) ─────────────────────────
    static class DisplayCfg
    {
        public sealed class Mon
        {
            public string GdiName = "", Name = "", Connection = "", DevicePath = "", ModelCode = "";
            public double Hz; public int BitsPerChannel; public bool HdrSupported, HdrEnabled, WideColor; public uint ColorEncoding;
        }

        [StructLayout(LayoutKind.Sequential)] struct LUID { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] struct RATIONAL { public uint Num, Den; }
        [StructLayout(LayoutKind.Sequential)]
        struct PATH_SOURCE { public LUID Adapter; public uint Id, ModeIdx, Status; }
        [StructLayout(LayoutKind.Sequential)]
        struct PATH_TARGET { public LUID Adapter; public uint Id, ModeIdx, OutputTech, Rotation, Scaling; public RATIONAL Refresh; public uint ScanOrder; public int Available; public uint Status; }
        [StructLayout(LayoutKind.Sequential)]
        struct PATH_INFO { public PATH_SOURCE Source; public PATH_TARGET Target; public uint Flags; }
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        struct MODE_INFO { public uint InfoType, Id; public LUID Adapter; }

        [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PATH_INFO[] pathArray, ref uint modes, [Out] MODE_INFO[] modeArray, IntPtr topology);
        [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(IntPtr packet);

        // requests are built as raw buffers: header = type, size, adapter LUID (low, high), id
        static IntPtr Packet(int type, int size, LUID adapter, uint id)
        {
            IntPtr p = Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i += 4) Marshal.WriteInt32(p, i, 0);
            Marshal.WriteInt32(p, 0, type); Marshal.WriteInt32(p, 4, size);
            Marshal.WriteInt32(p, 8, (int)adapter.Low); Marshal.WriteInt32(p, 12, adapter.High); Marshal.WriteInt32(p, 16, (int)id);
            return p;
        }

        static string Tech(uint t)
        {
            switch (t)
            {
                case 0: return "VGA"; case 1: return "S-Video"; case 2: return "Composite"; case 3: return "Component"; case 4: return "DVI"; case 5: return "HDMI";
                case 6: return "LVDS"; case 8: return "D-Jpn"; case 9: return "SDI"; case 10: return "DisplayPort"; case 11: return "DisplayPort (internal)";
                case 12: return "UDI"; case 13: return "UDI (internal)"; case 14: return "SDTV dongle"; case 15: return "Miracast (wireless)"; case 16: return "USB-C / indirect";
                case 0x80000000: return "Internal panel"; default: return "";
            }
        }

        public static List<Mon> Read()
        {
            var res = new List<Mon>();
            try
            {
                uint np, nm;
                if (GetDisplayConfigBufferSizes(2, out np, out nm) != 0 || np == 0) return res;           // 2 = active monitors only
                var paths = new PATH_INFO[np]; var modes = new MODE_INFO[nm];
                if (QueryDisplayConfig(2, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return res;
                for (int i = 0; i < np; i++)
                {
                    var p = paths[i];
                    var m = new Mon();
                    m.Hz = p.Target.Refresh.Den > 0 ? (double)p.Target.Refresh.Num / p.Target.Refresh.Den : 0;
                    m.Connection = Tech(p.Target.OutputTech);

                    IntPtr s = Packet(1, 84, p.Source.Adapter, p.Source.Id);                                  // source name: \\.\DISPLAY1
                    try { if (DisplayConfigGetDeviceInfo(s) == 0) m.GdiName = Marshal.PtrToStringUni(IntPtr.Add(s, 20), 32).TrimEnd('\0'); } finally { Marshal.FreeHGlobal(s); }

                    IntPtr t = Packet(2, 420, p.Target.Adapter, p.Target.Id);                                 // target name: monitor model + device path
                    try
                    {
                        if (DisplayConfigGetDeviceInfo(t) == 0)
                        {
                            m.Name = Marshal.PtrToStringUni(IntPtr.Add(t, 36), 64).TrimEnd('\0');
                            m.DevicePath = Marshal.PtrToStringUni(IntPtr.Add(t, 164), 128).TrimEnd('\0');
                            var mm = System.Text.RegularExpressions.Regex.Match(m.DevicePath, @"DISPLAY#([A-Za-z0-9]+)#", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (mm.Success) m.ModelCode = mm.Groups[1].Value.ToUpperInvariant();
                        }
                    }
                    finally { Marshal.FreeHGlobal(t); }

                    IntPtr c = Packet(9, 32, p.Target.Adapter, p.Target.Id);                                  // advanced colour: HDR + bits per channel
                    try
                    {
                        if (DisplayConfigGetDeviceInfo(c) == 0)
                        {
                            int bits = Marshal.ReadInt32(c, 20);
                            m.HdrSupported = (bits & 1) != 0; m.HdrEnabled = (bits & 2) != 0; m.WideColor = (bits & 4) != 0;
                            m.ColorEncoding = (uint)Marshal.ReadInt32(c, 24); m.BitsPerChannel = Marshal.ReadInt32(c, 28);
                        }
                    }
                    finally { Marshal.FreeHGlobal(c); }
                    res.Add(m);
                }
            }
            catch { }
            return res;
        }

        public static string Encoding(uint e) { switch (e) { case 0: return "RGB"; case 1: return "YCbCr 4:4:4"; case 2: return "YCbCr 4:2:2"; case 3: return "YCbCr 4:2:0"; case 4: return "Intensity"; default: return ""; } }
    }
}
