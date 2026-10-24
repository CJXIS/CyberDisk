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
    static class FastIO
    {
        delegate uint CopyProgressRoutine(long total, long transferred, long streamSize, long streamTransferred, uint streamNo, uint reason, IntPtr hSrc, IntPtr hDst, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CopyFileEx(string src, string dst, CopyProgressRoutine cb, IntPtr data, ref int cancel, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool DeleteFile(string p);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool RemoveDirectory(string p);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetFileAttributes(string p, uint a);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool MoveFileEx(string src, string dst, uint flags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd; public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags; public bool fAnyOperationsAborted; public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        public static string P(string p)
        {
            if (p.StartsWith(@"\\?\")) return p;
            return p.StartsWith(@"\\") ? @"\\?\UNC\" + p.Substring(2) : @"\\?\" + p;
        }

        // why this path must not be touched (null = fine)
        public static string Protect(string p)
        {
            try
            {
                string full = Path.GetFullPath(p).TrimEnd('\\');
                if (Path.GetPathRoot(full).TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase)) return "a drive root";
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
                string prof = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
                var exact = new[] { win, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\'), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\'),
                                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd('\\'), prof, Path.GetDirectoryName(prof) };
                if (exact.Any(x => !string.IsNullOrEmpty(x) && x.Equals(full, StringComparison.OrdinalIgnoreCase))) return "a protected system folder";
                if (full.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase) && !full.StartsWith(win + "\\Temp\\", StringComparison.OrdinalIgnoreCase)) return "part of Windows";
                string leaf = Path.GetFileName(full);
                foreach (var n in new[] { "System Volume Information", "$Recycle.Bin", "pagefile.sys", "hiberfil.sys", "swapfile.sys" })
                    if (leaf.Equals(n, StringComparison.OrdinalIgnoreCase)) return "a protected system item";
            }
            catch { return "an invalid path"; }
            return null;
        }

        public static bool IsInside(string child, string parent)
        {
            string c = Path.GetFullPath(child).TrimEnd('\\') + "\\", p = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
            return c.StartsWith(p, StringComparison.OrdinalIgnoreCase);
        }

        public static string UniqueName(string dir, string name, bool isDir)
        {
            string target = Path.Combine(dir, name);
            if (!File.Exists(target) && !Directory.Exists(target)) return target;
            string stem = isDir ? name : Path.GetFileNameWithoutExtension(name), ext = isDir ? "" : Path.GetExtension(name);
            for (int i = 1; i < 10000; i++)
            {
                string cand = Path.Combine(dir, stem + " - Copy" + (i == 1 ? "" : " (" + i + ")") + ext);
                if (!File.Exists(cand) && !Directory.Exists(cand)) return cand;
            }
            return target;
        }

        // lists everything below a folder using the fastest Win32 enumeration (junctions/symlinks are not followed)
        static void Walk(string dir, List<string> dirs, List<Tuple<string, long>> files)
        {
            string b = dir.TrimEnd('\\');
            var subs = new List<string>();
            IntPtr h = Native.FindFirstFileEx(P(b) + "\\*", 1, out var d, 0, IntPtr.Zero, 2);
            if (h == new IntPtr(-1)) return;
            try
            {
                do
                {
                    uint a = d.dwFileAttributes; string nm = d.cFileName;
                    if ((a & 0x10) != 0)
                    {
                        if (nm == "." || nm == ".." || (a & 0x400) != 0) continue;
                        subs.Add(b + "\\" + nm);
                    }
                    else files.Add(Tuple.Create(b + "\\" + nm, ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow));
                } while (Native.FindNextFile(h, out d));
            }
            finally { Native.FindClose(h); }
            foreach (var s in subs) { dirs.Add(s); Walk(s, dirs, files); }
        }

        // ── copy ──
        // overwrite=false + a unique target name is decided by the caller (target paths are passed in)
        public static CopyPlan Plan(IEnumerable<Tuple<string, string>> pairs)
        {
            var plan = new CopyPlan();
            foreach (var pr in pairs)
            {
                string src = pr.Item1, dst = pr.Item2;
                if (Directory.Exists(src))
                {
                    plan.Dirs.Add(dst);
                    var ds = new List<string>(); var fs = new List<Tuple<string, long>>();
                    Walk(src, ds, fs);
                    string root = src.TrimEnd('\\');
                    foreach (var d in ds) plan.Dirs.Add(dst + d.Substring(root.Length));
                    foreach (var f in fs) { plan.Files.Add(Tuple.Create(f.Item1, dst + f.Item1.Substring(root.Length), f.Item2)); plan.Bytes += f.Item2; }
                }
                else
                {
                    long len = 0; try { len = new FileInfo(src).Length; } catch { }
                    plan.Files.Add(Tuple.Create(src, dst, len)); plan.Bytes += len;
                }
            }
            return plan;
        }

        static void CopyOne(string src, string dst, long size, bool overwrite, OpCtx ctx)
        {
            long last = 0;
            CopyProgressRoutine cb = (tot, xfer, ss, st, sn, reason, hs, hd, dat) =>
            {
                long delta = xfer - last; last = xfer;
                if (delta > 0) Interlocked.Add(ref ctx.DoneBytes, delta);
                return ctx.Cancelled ? 1u : 0u;                 // PROGRESS_CANCEL
            };
            int cancel = 0;
            uint flags = overwrite ? 0u : 1u;                    // COPY_FILE_FAIL_IF_EXISTS
            if (size >= 128L * 1024 * 1024) flags |= 0x1000;     // COPY_FILE_NO_BUFFERING: big files do not flush the system cache
            ctx.Current = src;
            bool ok = CopyFileEx(P(src), P(dst), cb, IntPtr.Zero, ref cancel, flags);
            if (!ok && overwrite && !ctx.Cancelled)
            {
                SetFileAttributes(P(dst), 0x80);                 // clear read-only on the target and retry once
                last = 0; ok = CopyFileEx(P(src), P(dst), cb, IntPtr.Zero, ref cancel, flags);
            }
            GC.KeepAlive(cb);
            if (ok) { long rest = size - last; if (rest > 0) Interlocked.Add(ref ctx.DoneBytes, rest); }
            else if (!ctx.Cancelled) { Interlocked.Increment(ref ctx.Failed); ctx.Errors.Add(Path.GetFileName(src) + "  (" + new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message + ")"); }
            Interlocked.Increment(ref ctx.DoneFiles);
        }

        public static void Copy(CopyPlan plan, bool overwrite, OpCtx ctx)
        {
            foreach (var d in plan.Dirs) { try { Directory.CreateDirectory(P(d)); } catch { } }
            double avg = plan.Files.Count == 0 ? 0 : (double)plan.Bytes / plan.Files.Count;
            int dop = avg > 256.0 * 1024 * 1024 ? 2 : avg > 16.0 * 1024 * 1024 ? 6 : Math.Min(16, Math.Max(4, Environment.ProcessorCount * 2));
            Parallel.ForEach(plan.Files, new ParallelOptions { MaxDegreeOfParallelism = dop }, f =>
            {
                if (ctx.Cancelled) return;
                CopyOne(f.Item1, f.Item2, f.Item3, overwrite, ctx);
            });
        }

        // ── move (same drive = instant rename, otherwise copy + delete) ──
        public static bool SameVolume(string a, string b)
        {
            return string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
        }

        public static bool MoveRename(string src, string dst, bool overwrite)
        {
            // MOVEFILE_REPLACE_EXISTING (1) only works for files; folders must not exist at the target
            return MoveFileEx(P(src), P(dst), overwrite && File.Exists(src) ? 1u : 0u);
        }

        // ── delete ──
        public static void Delete(IEnumerable<string> paths, OpCtx ctx)
        {
            var files = new List<Tuple<string, long>>(); var dirs = new List<string>();
            foreach (var p in paths)
            {
                if (Directory.Exists(p)) { dirs.Add(p.TrimEnd('\\')); Walk(p, dirs, files); }
                else { long len = 0; try { len = new FileInfo(p).Length; } catch { } files.Add(Tuple.Create(p, len)); }
            }
            ctx.TotalFiles = files.Count + dirs.Count; ctx.TotalBytes = files.Sum(f => f.Item2);
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(16, Math.Max(4, Environment.ProcessorCount * 2)) }, f =>
            {
                if (ctx.Cancelled) return;
                string q = P(f.Item1);
                bool ok = DeleteFile(q);
                if (!ok) { SetFileAttributes(q, 0x80); ok = DeleteFile(q); }
                if (ok) Interlocked.Add(ref ctx.DoneBytes, f.Item2);
                else { Interlocked.Increment(ref ctx.Failed); ctx.Errors.Add(Path.GetFileName(f.Item1) + "  (" + new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message + ")"); }
                ctx.Current = f.Item1;
                Interlocked.Increment(ref ctx.DoneFiles);
            });
            // folders: deepest first, one depth level at a time so every folder is empty when its turn comes
            foreach (var grp in dirs.GroupBy(d => d.Count(c => c == '\\')).OrderByDescending(g => g.Key))
            {
                if (ctx.Cancelled) break;
                Parallel.ForEach(grp, d =>
                {
                    string q = P(d);
                    if (!RemoveDirectory(q)) { SetFileAttributes(q, 0x80); RemoveDirectory(q); }
                    Interlocked.Increment(ref ctx.DoneFiles);
                });
            }
        }

        // delete into the Recycle Bin (can be undone, slower)
        public static bool Recycle(IEnumerable<string> paths)
        {
            var op = new SHFILEOPSTRUCT { wFunc = 3, pFrom = string.Join("\0", paths) + "\0\0", fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400 };   // ALLOWUNDO | NOCONFIRMATION | SILENT | NOERRORUI
            return SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
        }
    }
}
