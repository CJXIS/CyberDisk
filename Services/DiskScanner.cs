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
    sealed class Node
    {
        public string Name, Path;
        public long Size, Own, Files, Dirs;
        public Node Parent;
        public List<Node> Kids = new List<Node>();
    }

    sealed class Scanner
    {
        public long Files, Bytes, Dirs;
        public long[] Cat = new long[8];
        CancellationToken ct;

        public void Run(Node n, CancellationToken t) { ct = t; Walk(n, 0); }

        void Walk(Node n, int depth)
        {
            if (ct.IsCancellationRequested) return;
            long own = 0, cnt = 0;
            var lc = new long[8];
            var dirs = new List<Node>();
            string bp = n.Path.TrimEnd('\\') + "\\";
            IntPtr h = Native.FindFirstFileEx(@"\\?\" + bp + "*", 1, out var d, 0, IntPtr.Zero, 2);   // basic info + large fetch
            if (h != new IntPtr(-1))
            {
                try
                {
                    do
                    {
                        uint a = d.dwFileAttributes;
                        if ((a & 0x10) != 0)
                        {
                            // skip junctions + symlinks only (cloud folders are real folders)
                            if ((a & 0x400) != 0 && (d.dwReserved0 == 0xA0000003 || d.dwReserved0 == 0xA000000C)) continue;
                            string nm = d.cFileName;
                            if (nm == "." || nm == "..") continue;
                            dirs.Add(new Node { Name = nm, Path = bp + nm, Parent = n });
                        }
                        else if ((a & Native.CLOUD) == 0)
                        {
                            long sz = ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow;
                            own += sz; cnt++;
                            lc[Cls.Of(d.cFileName)] += sz;
                        }
                    } while (!ct.IsCancellationRequested && Native.FindNextFile(h, out d));
                }
                finally { Native.FindClose(h); }
            }
            Interlocked.Add(ref Files, cnt); Interlocked.Add(ref Bytes, own); Interlocked.Add(ref Dirs, dirs.Count);
            for (int i = 0; i < 8; i++) if (lc[i] != 0) Interlocked.Add(ref Cat[i], lc[i]);

            if (depth < 3 && dirs.Count > 1)
                Parallel.ForEach(dirs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, k => Walk(k, depth + 1));
            else
                foreach (var k in dirs) Walk(k, depth + 1);

            n.Own = own; n.Kids = dirs;
            n.Size = own + dirs.Sum(k => k.Size);
            n.Files = cnt + dirs.Sum(k => k.Files);
            n.Dirs = dirs.Count + dirs.Sum(k => k.Dirs);
        }
    }

    sealed class FE { public string Name; public long Size; public DateTime Mod; }

    static class Fs
    {
        // lists the files of ONE folder on demand (used by the browser and the preview pane)
        public static List<FE> Files(string dir)
        {
            var l = new List<FE>();
            IntPtr h = Native.FindFirstFileEx(@"\\?\" + dir.TrimEnd('\\') + "\\*", 1, out var d, 0, IntPtr.Zero, 2);
            if (h == new IntPtr(-1)) return l;
            try
            {
                do
                {
                    uint a = d.dwFileAttributes;
                    if ((a & 0x10) != 0 || (a & Native.CLOUD) != 0) continue;
                    l.Add(new FE { Name = d.cFileName, Size = ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow, Mod = ToDt(d.ftLastWriteTime) });
                } while (Native.FindNextFile(h, out d));
            }
            finally { Native.FindClose(h); }
            l.Sort((x, y) => y.Size.CompareTo(x.Size));
            return l;
        }
        static DateTime ToDt(System.Runtime.InteropServices.ComTypes.FILETIME ft)
        {
            try { return DateTime.FromFileTime(((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime); }
            catch { return DateTime.MinValue; }
        }
    }
}
