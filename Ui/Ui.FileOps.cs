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
    sealed partial class Ui
    {
        // ── fast file operations in the folder browser ──
        Grid opHost;
        ProgressBar opBar;
        TextBlock opTitle, opCurrent, opStats, opPct;
        Button opCancel, bCopy, bCut, bPaste, bDelete, bNewFolder;
        List<string> clipPaths; bool clipCut;
        OpCtx curOp; DispatcherTimer opTm; bool opBusy; int opGen; long opLastBytes; double opSpeed; DateTime opLastAt;

        static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                var t = d as T;
                if (t != null) return t;
                d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return null;
        }

        void WireFiles()
        {
            opHost = F<Grid>("opHost"); opBar = F<ProgressBar>("opBar");
            opTitle = F<TextBlock>("opTitle"); opCurrent = F<TextBlock>("opCurrent"); opStats = F<TextBlock>("opStats"); opPct = F<TextBlock>("opPct");
            opCancel = F<Button>("opCancel"); bCopy = F<Button>("bCopy"); bCut = F<Button>("bCut"); bPaste = F<Button>("bPaste"); bDelete = F<Button>("bDelete"); bNewFolder = F<Button>("bNewFolder");
            bCopy.Click += (_, _) => DoCopy(false);
            bCut.Click += (_, _) => DoCopy(true);
            bPaste.Click += (_, _) => DoPaste();
            bDelete.Click += (_, _) => DoDelete(false);
            bNewFolder.Click += (_, _) => NewFolder();
            opCancel.Click += (_, _) => { if (curOp != null) { curOp.Cancelled = true; opCancel.IsEnabled = false; opTitle.Text = "Cancelling…"; } };
            opTm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            opTm.Tick += (_, _) => OpTick();
            foreach (var n in new[] { "miOpen", "miExplorer", "miCopy", "miCut", "miPaste", "miNew", "miDelete", "miDeletePerm" })
            {
                var mi = F<MenuItem>(n);
                if (mi != null) mi.Click += (s, e) => BrowseAction(Convert.ToString(((MenuItem)s).Tag));
            }
            gs.PreviewMouseRightButtonDown += (s, e) =>
            {
                var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
                if (row != null && !row.IsSelected) { gs.SelectedItems.Clear(); row.IsSelected = true; }
            };
            gs.PreviewKeyDown += (s, e) =>
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                if (ctrl && e.Key == Key.C) { DoCopy(false); e.Handled = true; }
                else if (ctrl && e.Key == Key.X) { DoCopy(true); e.Handled = true; }
                else if (ctrl && e.Key == Key.V) { DoPaste(); e.Handled = true; }
                else if (e.Key == Key.Delete) { DoDelete(shift); e.Handled = true; }
                else if (e.Key == Key.F5) { RefreshHere(); e.Handled = true; }
            };
            StartShimmer(opBar);
        }

        bool OpKey(KeyEventArgs e)
        {
            if (!opBusy) return false;
            if (e.Key == Key.Escape && curOp != null) { curOp.Cancelled = true; opCancel.IsEnabled = false; }
            return e.Key != Key.F11;                           // modal while an operation runs
        }

        void BrowseAction(string a)
        {
            switch (a)
            {
                case "open": EnterSelected(); break;
                case "explorer": OpenExplorer(); break;
                case "copy": DoCopy(false); break;
                case "cut": DoCopy(true); break;
                case "paste": DoPaste(); break;
                case "new": NewFolder(); break;
                case "delete": DoDelete(false); break;
                case "delperm": DoDelete(true); break;
            }
        }

        List<Row> SelRows() { return gs.SelectedItems.Cast<Row>().Where(r => r != null && r.Path != null).ToList(); }

        // ── clipboard (shared with Windows Explorer) ──
        void SetClip(List<string> paths, bool cut)
        {
            try
            {
                var sc = new System.Collections.Specialized.StringCollection();
                sc.AddRange(paths.ToArray());
                var d = new DataObject();
                d.SetFileDropList(sc);
                d.SetData("Preferred DropEffect", new System.IO.MemoryStream(BitConverter.GetBytes(cut ? 2 : 5)));
                Clipboard.SetDataObject(d, true);
            }
            catch { }
        }

        List<string> GetClip(out bool cut)
        {
            cut = false;
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    var l = Clipboard.GetFileDropList().Cast<string>().ToList();
                    var d = Clipboard.GetDataObject();
                    var ms = d == null ? null : d.GetData("Preferred DropEffect") as System.IO.MemoryStream;
                    if (ms != null) { var b = ms.ToArray(); if (b.Length >= 4) cut = (BitConverter.ToInt32(b, 0) & 2) != 0; }
                    if (l.Count > 0) return l;
                }
            }
            catch { }
            cut = clipCut;
            return clipPaths ?? new List<string>();
        }

        async void DoCopy(bool cut)
        {
            var rows = SelRows();
            if (rows.Count == 0) { await Msg("NOTHING SELECTED", "Select one or more files or folders first (Ctrl or Shift + click selects several).", "info"); return; }
            if (cut)
                foreach (var r in rows) { var why = FastIO.Protect(r.Path); if (why != null) { await Msg("CANNOT MOVE THIS", r.Name + " is " + why + ".", "warn"); return; } }
            clipPaths = rows.Select(r => r.Path).ToList(); clipCut = cut;
            SetClip(clipPaths, cut);
            txtStatus.Text = (cut ? "CUT " : "COPIED ") + rows.Count + " ITEM(S) · open any folder and press PASTE";
        }

        async void DoPaste()
        {
            if (cur == null || opBusy) return;
            bool cut;
            var src = GetClip(out cut).Where(p => File.Exists(p) || Directory.Exists(p)).Select(p => p.TrimEnd('\\')).ToList();
            if (src.Count == 0) { await Msg("NOTHING TO PASTE", "Copy or cut some files first, then paste them into a folder.", "info"); return; }
            string dest = cur.Path;
            foreach (var p in src)
            {
                if (Directory.Exists(p) && FastIO.IsInside(dest, p)) { await Msg("CANNOT PASTE HERE", "A folder cannot be copied into itself:\n" + p, "warn"); return; }
                if (cut) { var why = FastIO.Protect(p); if (why != null) { await Msg("CANNOT MOVE THIS", Path.GetFileName(p) + " is " + why + ".", "warn"); return; } }
            }
            if (cut && src.All(p => string.Equals(Path.GetDirectoryName(p), dest.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) { txtStatus.Text = "THOSE ITEMS ARE ALREADY IN THIS FOLDER"; return; }

            var conflicts = src.Where(p => { string t = Path.Combine(dest, Path.GetFileName(p)); return File.Exists(t) || Directory.Exists(t); }).ToList();
            bool overwrite = false;
            if (conflicts.Count > 0)
            {
                int ch = await Ask("ITEMS ALREADY EXIST", conflicts.Count + " item(s) with the same name already exist in this folder:\n\n" +
                    string.Join("\n", conflicts.Take(5).Select(c => "•  " + Path.GetFileName(c))) + (conflicts.Count > 5 ? "\n•  …" : ""), "warn", "KEEP BOTH", "REPLACE", "CANCEL");
                if (ch == 2) return;
                overwrite = ch == 1;
            }
            var pairs = new List<Tuple<string, string>>();
            foreach (var p in src)
            {
                string name = Path.GetFileName(p), target = Path.Combine(dest, name);
                if ((File.Exists(target) || Directory.Exists(target)) && !overwrite) target = FastIO.UniqueName(dest, name, Directory.Exists(p));
                pairs.Add(Tuple.Create(p, target));
            }
            var touched = pairs.Where(x => Directory.Exists(x.Item1)).Select(x => x.Item2).ToList();
            await RunOp((cut ? "Moving " : "Copying ") + src.Count + " item(s)", ctx => FastPaste(pairs, cut, overwrite, ctx));
            await RefreshAfterOp(touched);
            if (cut) { clipPaths = null; try { Clipboard.Clear(); } catch { } }
        }

        static void FastPaste(List<Tuple<string, string>> pairs, bool cut, bool overwrite, OpCtx ctx)
        {
            Interlocked.Add(ref ctx.TotalFiles, pairs.Count);
            var rest = new List<Tuple<string, string>>();
            foreach (var pr in pairs)
            {
                if (cut && FastIO.SameVolume(pr.Item1, pr.Item2) && !(Directory.Exists(pr.Item1) && Directory.Exists(pr.Item2)))
                {
                    ctx.Current = pr.Item1;
                    if (FastIO.MoveRename(pr.Item1, pr.Item2, overwrite)) { Interlocked.Increment(ref ctx.DoneFiles); continue; }      // same drive: instant
                }
                rest.Add(pr);
            }
            if (rest.Count == 0) return;
            var plan = FastIO.Plan(rest);
            Interlocked.Add(ref ctx.TotalBytes, plan.Bytes);
            Interlocked.Add(ref ctx.TotalFiles, plan.Files.Count - rest.Count);
            FastIO.Copy(plan, overwrite, ctx);
            if (cut && !ctx.Cancelled && Interlocked.Read(ref ctx.Failed) == 0)
                FastIO.Delete(rest.Select(x => x.Item1), new OpCtx());       // the originals are only removed after a fully successful copy
        }

        async void DoDelete(bool permanentDirect)
        {
            if (opBusy) return;
            var rows = SelRows();
            if (rows.Count == 0) { await Msg("NOTHING SELECTED", "Select one or more files or folders first.", "info"); return; }
            foreach (var r in rows)
            {
                var why = FastIO.Protect(r.Path);
                if (why != null) { await Msg("PROTECTED", r.Name + " is " + why + " and cannot be deleted here.", "warn"); return; }
            }
            string what = rows.Count == 1 ? "\"" + rows[0].Name + "\"" : rows.Count + " items";
            bool perm;
            if (permanentDirect)
            {
                if (!(await Confirm("DELETE PERMANENTLY", "Permanently delete " + what + "?\n\nThis skips the Recycle Bin and cannot be undone.", "DELETE", "CANCEL", "danger"))) return;
                perm = true;
            }
            else
            {
                int ch = await Ask("DELETE", "Delete " + what + "?\n\nRecycle Bin keeps a copy you can restore. Permanent delete is much faster and cannot be undone.", "danger", "RECYCLE BIN", "DELETE PERMANENTLY", "CANCEL");
                if (ch == 2) return;
                perm = ch == 1;
            }
            var paths = rows.Select(r => r.Path).ToList();
            if (perm) await RunOp("Deleting " + what, ctx => FastIO.Delete(paths, ctx));
            else
            {
                txtStatus.Text = "MOVING TO THE RECYCLE BIN…";
                bool ok = await Task.Run(() => FastIO.Recycle(paths));
                if (!ok) await Msg("NOT FULLY DELETED", "Windows could not move everything to the Recycle Bin (items may be in use).", "warn");
            }
            await RefreshAfterOp(null);
        }

        async void NewFolder()
        {
            if (cur == null || opBusy) return;
            string name = FastIO.UniqueName(cur.Path, "New folder", true);
            try { Directory.CreateDirectory(FastIO.P(name)); }
            catch (Exception ex) { await Msg("COULD NOT CREATE THE FOLDER", ex.Message, "danger"); return; }
            await RefreshAfterOp(new List<string> { name });
            txtStatus.Text = "CREATED " + Path.GetFileName(name).ToUpper();
        }

        async void RefreshHere() { if (!opBusy) await RefreshAfterOp(null); }

        // ── progress overlay ──
        void SetOpBar(double p, int ms)
        {
            opBar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(Math.Max(0, Math.Min(100, p)), TimeSpan.FromMilliseconds(Math.Max(1, ms))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        async Task RunOp(string title, Action<OpCtx> work)
        {
            opBusy = true;
            var ctx = new OpCtx(); curOp = ctx; opLastBytes = 0; opLastAt = DateTime.UtcNow; opSpeed = 0;
            opTitle.Text = title; opCurrent.Text = "Preparing…"; opPct.Text = "0%"; opStats.Text = " "; opCancel.IsEnabled = true;
            SetOpBar(0, 1);
            int gen = ++opGen;
            opHost.BeginAnimation(UIElement.OpacityProperty, null); opHost.Opacity = 0; opHost.Visibility = Visibility.Visible;
            opHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            opTm.Start();
            try { await Task.Run(() => work(ctx)); }
            catch (Exception ex) { ctx.Errors.Add(ex.Message); Interlocked.Increment(ref ctx.Failed); }
            opTm.Stop();
            OpTick();
            double secs = Math.Max(0.05, ctx.Sw.Elapsed.TotalSeconds);
            string summary = (ctx.Cancelled ? "Cancelled" : "Done") + "  ·  " + Interlocked.Read(ref ctx.DoneFiles).ToString("N0") + " item(s)  ·  " + Fmt(Interlocked.Read(ref ctx.DoneBytes)) + " in " + secs.ToString("0.0") + " s"
                           + (ctx.DoneBytes > 0 ? "  ·  " + Fmt((long)(ctx.DoneBytes / secs)) + "/s" : "");
            opTitle.Text = ctx.Cancelled ? "Cancelled" : ctx.Failed > 0 ? "Finished with problems" : "Done ✓";
            opStats.Text = summary;
            if (!ctx.Cancelled) { SetOpBar(100, 180); opPct.Text = "100%"; }
            txtStatus.Text = summary.Replace("  ·  ", " · ").ToUpper();
            await Task.Delay(ctx.Failed > 0 ? 350 : 950);
            opHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150)));
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(190) };
            t.Tick += (s, e) => { t.Stop(); if (gen != opGen) return; opHost.BeginAnimation(UIElement.OpacityProperty, null); opHost.Opacity = 1; opHost.Visibility = Visibility.Collapsed; };
            t.Start();
            curOp = null; opBusy = false;
            if (ctx.Failed > 0) await Msg("SOME ITEMS FAILED", ctx.Failed + " item(s) could not be processed:\n\n" + string.Join("\n", ctx.Errors.Take(6)), "warn");
        }

        void OpTick()
        {
            var c = curOp;
            if (c == null) return;
            long done = Interlocked.Read(ref c.DoneBytes), tb = Interlocked.Read(ref c.TotalBytes), df = Interlocked.Read(ref c.DoneFiles), tf = Interlocked.Read(ref c.TotalFiles);
            double pct = tb > 0 ? 100.0 * done / tb : tf > 0 ? 100.0 * df / tf : 0;
            pct = Math.Min(100, pct);
            var now = DateTime.UtcNow; double dt = (now - opLastAt).TotalSeconds;
            if (dt >= 0.4) { double inst = (done - opLastBytes) / dt; opSpeed = opSpeed <= 0 ? inst : opSpeed * 0.6 + inst * 0.4; opLastBytes = done; opLastAt = now; }
            SetOpBar(pct, 110);
            opPct.Text = ((int)pct) + "%";
            opCurrent.Text = string.IsNullOrEmpty(c.Current) ? "Preparing…" : c.Current;
            string eta = "";
            if (opSpeed > 1 && tb > done) { double s = (tb - done) / opSpeed; eta = "  ·  " + (s < 60 ? s.ToString("0") + " s left" : (s / 60).ToString("0.0") + " min left"); }
            opStats.Text = df.ToString("N0") + " of " + tf.ToString("N0") + " items  ·  " + Fmt(done) + " of " + Fmt(tb) + (opSpeed > 1 ? "  ·  " + Fmt((long)opSpeed) + "/s" : "") + eta;
        }

        // ── keep the scanned size tree correct after changes (no full rescan) ──
        async Task RefreshAfterOp(List<string> touched)
        {
            var n = cur;
            if (n == null) return;
            await Task.Run(() => UpdateNode(n, touched));
            if (cur == n) Show(n);
        }

        static void UpdateNode(Node n, List<string> touched)
        {
            long oS = n.Size, oF = n.Files, oD = n.Dirs;
            string np = n.Path.TrimEnd('\\');
            Func<string, bool> isTouched = p => touched != null && touched.Any(t => string.Equals(t.TrimEnd('\\'), p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
            var keep = new List<Node>();
            foreach (var k in n.Kids.ToList())
            {
                if (!Directory.Exists(k.Path)) continue;          // deleted or moved away
                if (isTouched(k.Path)) continue;                  // re-read from disk below
                keep.Add(k);
            }
            if (touched != null)
                foreach (var t in touched)
                {
                    string tp = t.TrimEnd('\\');
                    if (!Directory.Exists(tp) || !string.Equals(Path.GetDirectoryName(tp), np, StringComparison.OrdinalIgnoreCase)) continue;
                    var nn = new Node { Name = Path.GetFileName(tp), Path = tp, Parent = n };
                    new Scanner().Run(nn, CancellationToken.None);
                    keep.Add(nn);
                }
            n.Kids = keep;
            var files = Fs.Files(n.Path);
            n.Own = files.Sum(f => f.Size);
            n.Size = n.Own + keep.Sum(k => k.Size);
            n.Files = files.Count + keep.Sum(k => k.Files);
            n.Dirs = keep.Count + keep.Sum(k => k.Dirs);
            long dS = n.Size - oS, dF = n.Files - oF, dD = n.Dirs - oD;
            for (var p = n.Parent; p != null; p = p.Parent) { p.Size += dS; p.Files += dF; p.Dirs += dD; }
        }
    }
}
