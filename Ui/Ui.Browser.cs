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
        // ── folder browser ──
        void OpenBrowser(DriveVm d)
        {
            curDrive = d;
            Go(vBrowse);
            Show(d.Node);
        }

        Row FolderRow(Node k, Node parent) => new Row
        {
            Name = k.Name, IsDir = true, Node = k, Path = k.Path, Kind = "Folder", Meta = k.Files.ToString("N0") + " files",
            SizeBytes = k.Size, SizeText = Fmt(k.Size), Percent = Pct(k.Size, parent.Size), PctText = Pct(k.Size, parent.Size).ToString("0.0") + "%",
            PctBrush = ShareBrush(Pct(k.Size, parent.Size)), Icon = ShellIcons.Get("", true).Icon
        };

        Row FileRow(FE f, Node parent)
        {
            var ic = ShellIcons.Get(Ext.ExtOf(f.Name), false);
            return new Row
            {
                Name = f.Name, IsDir = false, Path = parent.Path.TrimEnd('\\') + "\\" + f.Name, Kind = ic.Type,
                Meta = f.Mod == DateTime.MinValue ? "" : f.Mod.ToString("yyyy-MM-dd HH:mm"), Modified = f.Mod,
                SizeBytes = f.Size, SizeText = Fmt(f.Size), Percent = Pct(f.Size, parent.Size), PctText = Pct(f.Size, parent.Size).ToString("0.0") + "%",
                PctBrush = ShareBrush(Pct(f.Size, parent.Size)), Icon = ic.Icon
            };
        }

        async void Show(Node n)
        {
            cur = n; int id = ++navId;
            txtCrumb.Text = "◈ " + n.Path;
            var folders = n.Kids.OrderByDescending(k => k.Size).Select(k => FolderRow(k, n)).ToList();
            gs.ItemsSource = folders;
            txtInfo.Text = n.Kids.Count.ToString("N0") + " folders · loading files…";
            List<FE> files;
            try { files = await Task.Run(() => Fs.Files(n.Path)); } catch { files = new List<FE>(); }
            if (id != navId) return;

            const int cap = 1500;
            var all = new List<Row>(folders);
            foreach (var f in files.Take(cap)) all.Add(FileRow(f, n));
            if (files.Count > cap)
            {
                long rest = files.Skip(cap).Sum(f => f.Size);
                all.Add(new Row { Name = "… " + (files.Count - cap).ToString("N0") + " more small files", Kind = "—", Meta = "", SizeBytes = rest, SizeText = Fmt(rest), Percent = Pct(rest, n.Size), PctText = Pct(rest, n.Size).ToString("0.0") + "%" });
            }
            gs.ItemsSource = all.OrderByDescending(r => r.SizeBytes).ToList();
            txtInfo.Text = n.Kids.Count.ToString("N0") + " folders · " + files.Count.ToString("N0") + " files here  |  " + Fmt(n.Size) + " total · "
                         + n.Files.ToString("N0") + " files · " + n.Dirs.ToString("N0") + " folders inside";
        }

        void Up() { if (cur == null) return; if (cur.Parent != null) Show(cur.Parent); else Go(vDrives); }
        void EnterSelected() { if (gs.SelectedItem is Row r && r.IsDir && r.Node != null) Show(r.Node); }
        void OpenExplorer()
        {
            var p = (gs.SelectedItem as Row)?.Path ?? cur?.Path;
            if (!string.IsNullOrEmpty(p)) Process.Start("explorer.exe", "\"" + p + "\"");
        }

        // ── preview pane ──
        TextBox pvPath, pvText; Border pvTextBox; ItemsControl pvRows, pvTypes; Button bPvOpen, bPvExplorer, bPvCopy; ScrollViewer svPv;
        static readonly Dictionary<int, Brush> shareBrushes = new Dictionary<int, Brush>();

        /// <summary>Share bars run from cool (small) to hot (a large part of the folder).</summary>
        static Brush ShareBrush(double pct)
        {
            int k = pct >= 50 ? 3 : pct >= 20 ? 2 : pct >= 5 ? 1 : 0;
            lock (shareBrushes)
            {
                Brush b;
                if (!shareBrushes.TryGetValue(k, out b))
                {
                    var br = new SolidColorBrush(Col(k == 3 ? "#FF6B8A" : k == 2 ? "#FFB84D" : k == 1 ? "#3DD6F5" : "#3DDC97")); br.Freeze(); b = br; shareBrushes[k] = b;
                }
                return b;
            }
        }

        void WireBrowserExtra()
        {
            pvPath = F<TextBox>("pvPath"); pvText = F<TextBox>("pvText"); pvTextBox = F<Border>("pvTextBox"); pvRows = F<ItemsControl>("pvRows"); pvTypes = F<ItemsControl>("pvTypes");
            bPvOpen = F<Button>("bPvOpen"); bPvExplorer = F<Button>("bPvExplorer"); bPvCopy = F<Button>("bPvCopy"); svPv = F<ScrollViewer>("svPv");
            svPv.PreviewMouseWheel += (s, e) => { svPv.ScrollToVerticalOffset(svPv.VerticalOffset - e.Delta); e.Handled = true; };
            bPvOpen.Click += async (_, _) =>
            {
                var r = gs.SelectedItem as Row; if (r == null || r.Path == null) return;
                if (r.IsDir) { EnterSelected(); return; }
                string ext = Ext.ExtOf(r.Name).ToLowerInvariant();
                if (new[] { ".exe", ".bat", ".cmd", ".msi", ".ps1", ".vbs", ".js", ".scr", ".com", ".jar" }.Contains(ext) && !(await Confirm("RUN THIS FILE?", r.Name + " is a program or script. Opening it will run it.", "RUN", "CANCEL", "warn"))) return;
                try { Process.Start(new ProcessStartInfo(r.Path) { UseShellExecute = true }); } catch (Exception ex) { await Msg("COULD NOT OPEN IT", ex.Message, "warn"); }
            };
            bPvExplorer.Click += (_, _) =>
            {
                var r = gs.SelectedItem as Row; if (r == null || r.Path == null) return;
                try { Process.Start("explorer.exe", r.IsDir ? "\"" + r.Path + "\"" : "/select,\"" + r.Path + "\""); } catch { }
            };
            bPvCopy.Click += (_, _) =>
            {
                var r = gs.SelectedItem as Row; if (r == null || r.Path == null) return;
                try { Clipboard.SetText(r.Path); txtStatus.Text = "PATH COPIED"; } catch { txtStatus.Text = "COULD NOT COPY"; }
            };
        }

        void ClearPreview() { pvId++; pvBody.Visibility = Visibility.Collapsed; pvEmpty.Visibility = Visibility.Visible; }

        static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".tif", ".tiff", ".webp" };
        static readonly string[] TextExt = { ".txt", ".log", ".md", ".json", ".xml", ".ini", ".cfg", ".conf", ".csv", ".tsv", ".cs", ".py", ".js", ".ts", ".html", ".htm", ".css", ".bat", ".cmd", ".ps1", ".yml", ".yaml", ".sql", ".java", ".c", ".cpp", ".h", ".rs", ".go", ".php", ".sh", ".reg", ".inf", ".manifest", ".csproj", ".xaml", ".srt" };

        static ImageSource LoadImg(string p, out string dims)
        {
            dims = "";
            try
            {
                var bi = new BitmapImage();
                using (var s = File.OpenRead(p))
                {
                    bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.DecodePixelWidth = 360; bi.StreamSource = s; bi.EndInit();
                }
                bi.Freeze();
                try
                {
                    using (var s2 = File.OpenRead(p)) { var fr = BitmapFrame.Create(s2, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None); dims = fr.PixelWidth + " × " + fr.PixelHeight + " px"; }
                }
                catch { }
                return bi;
            }
            catch { return null; }
        }

        /// <summary>First lines of a text file (null when it is binary or too big to preview).</summary>
        static string LoadText(string p, long size)
        {
            try
            {
                if (size > 8L * 1024 * 1024) return null;
                var buf = new byte[16384]; int n;
                using (var fs = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) n = fs.Read(buf, 0, buf.Length);
                if (n == 0) return "(empty file)";
                for (int i = 0; i < Math.Min(n, 4096); i++) if (buf[i] == 0) return null;
                string t = (n >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF) ? Encoding.UTF8.GetString(buf, 3, n - 3) : Encoding.UTF8.GetString(buf, 0, n);
                var lines = t.Replace("\r", "").Split('\n').Take(22).Select(l => l.Length > 140 ? l.Substring(0, 140) + "…" : l.Replace("\t", "    "));
                return string.Join("\n", lines);
            }
            catch { return null; }
        }

        sealed class PvInfo { public DateTime Created, Modified, Accessed; public string Attrs = "", Dims = "", Owner = ""; public ImageSource Img; public string Text; public List<Row> Types = new List<Row>(); public List<Row> Items = new List<Row>(); public int FileCount; }

        async void Preview()
        {
            var r = gs.SelectedItem as Row;
            if (r == null || r.Path == null) { ClearPreview(); return; }
            int id = ++pvId;
            pvEmpty.Visibility = Visibility.Collapsed; pvBody.Visibility = Visibility.Visible;
            pvIcon.Source = r.Icon; pvName.Text = r.Name; pvKind.Text = r.Kind; pvBig.Text = r.SizeText; pvPath.Text = r.Path;
            pvImg.Source = null; pvImg.Visibility = Visibility.Collapsed; pvTextBox.Visibility = Visibility.Collapsed; pvText.Text = "";
            pvTypes.Visibility = pvTypesHdr.Visibility = pvListHdr.Visibility = pvList.Visibility = Visibility.Collapsed;
            pvTypes.ItemsSource = null; pvList.ItemsSource = null;
            string parent = cur == null ? "" : cur.Name;
            var rows = new List<HwRow> { new HwRow { K = "SHARE", V = r.PctText + (parent.Length > 0 ? " of " + parent : "") } };
            if (r.IsDir && r.Node != null) { rows.Add(new HwRow { K = "FILES", V = r.Node.Files.ToString("N0") }); rows.Add(new HwRow { K = "FOLDERS", V = r.Node.Dirs.ToString("N0") }); }
            else rows.Add(new HwRow { K = "EXACT SIZE", V = r.SizeBytes.ToString("N0") + " bytes" });
            pvRows.ItemsSource = rows;

            // the slow parts (disk reads) run in the background; the pane is already filled with what is known
            bool isDir = r.IsDir; var node = r.Node; string path = r.Path, name = r.Name; long size = r.SizeBytes; double folderSize = node == null ? 0 : node.Size;
            PvInfo pi;
            try
            {
                pi = await Task.Run(() =>
                {
                    var o = new PvInfo();
                    try
                    {
                        FileSystemInfo fi = isDir ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path);
                        o.Created = fi.CreationTime; o.Modified = fi.LastWriteTime; o.Accessed = fi.LastAccessTime;
                        var at = fi.Attributes; var l = new List<string>();
                        if ((at & FileAttributes.ReadOnly) != 0) l.Add("read-only"); if ((at & FileAttributes.Hidden) != 0) l.Add("hidden"); if ((at & FileAttributes.System) != 0) l.Add("system");
                        if ((at & FileAttributes.Compressed) != 0) l.Add("compressed"); if ((at & FileAttributes.Encrypted) != 0) l.Add("encrypted"); if ((at & FileAttributes.ReparsePoint) != 0) l.Add("link");
                        o.Attrs = l.Count == 0 ? "normal" : string.Join(", ", l);
                    }
                    catch { }
                    if (isDir && node != null)
                    {
                        List<FE> files; try { files = Fs.Files(node.Path); } catch { files = new List<FE>(); }
                        o.FileCount = files.Count;
                        var groups = files.GroupBy(f => Ext.ExtOf(f.Name).ToLowerInvariant()).Select(g => new { Ext = g.Key, Size = g.Sum(f => f.Size), N = g.Count() }).OrderByDescending(g => g.Size).Take(5).ToList();
                        long subs = node.Kids.Sum(k => k.Size);
                        double tot = Math.Max(1, folderSize);
                        if (subs > 0) o.Types.Add(new Row { Name = "Subfolders (" + node.Kids.Count + ")", SizeText = Fmt(subs), Percent = 100.0 * subs / tot, PctBrush = ShareBrush(100.0 * subs / tot) });
                        foreach (var g in groups)
                        {
                            var ic = ShellIcons.Get(g.Ext, false);
                            string nm = g.Ext.Length == 0 ? "No extension" : (ic.Type ?? g.Ext);
                            o.Types.Add(new Row { Name = nm + " (" + g.N + ")", SizeText = Fmt(g.Size), Percent = 100.0 * g.Size / tot, PctBrush = ShareBrush(100.0 * g.Size / tot) });
                        }
                        var kids = node.Kids.OrderByDescending(k => k.Size).Take(30).Select(k => FolderRow(k, node));
                        o.Items = kids.Concat(files.Take(60).Select(f => FileRow(f, node))).OrderByDescending(x => x.SizeBytes).Take(40).ToList();
                    }
                    else
                    {
                        string ext = Ext.ExtOf(name).ToLowerInvariant();
                        if (ImgExt.Contains(ext)) { string d; o.Img = LoadImg(path, out d); o.Dims = d; }
                        else if (TextExt.Contains(ext) || size < 512 * 1024) o.Text = LoadText(path, size);
                    }
                    return o;
                });
            }
            catch { return; }
            if (id != pvId) return;

            rows.Add(new HwRow { K = "MODIFIED", V = pi.Modified.Year > 1700 ? pi.Modified.ToString("yyyy-MM-dd HH:mm") : "—" });
            rows.Add(new HwRow { K = "CREATED", V = pi.Created.Year > 1700 ? pi.Created.ToString("yyyy-MM-dd HH:mm") : "—" });
            rows.Add(new HwRow { K = "OPENED", V = pi.Accessed.Year > 1700 ? pi.Accessed.ToString("yyyy-MM-dd HH:mm") : "—" });
            if (pi.Dims.Length > 0) rows.Add(new HwRow { K = "DIMENSIONS", V = pi.Dims });
            if (!isDir && Ext.ExtOf(name).Length > 0) rows.Add(new HwRow { K = "EXTENSION", V = Ext.ExtOf(name).ToLowerInvariant() });
            rows.Add(new HwRow { K = "ATTRIBUTES", V = pi.Attrs });
            pvRows.ItemsSource = null; pvRows.ItemsSource = rows;

            if (isDir)
            {
                if (pi.Types.Count > 0) { pvTypes.ItemsSource = pi.Types; pvTypes.Visibility = pvTypesHdr.Visibility = Visibility.Visible; }
                pvListHdr.Text = "LARGEST ITEMS INSIDE"; pvList.ItemsSource = pi.Items;
                pvListHdr.Visibility = pvList.Visibility = pi.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                if (pi.Img != null) { pvImg.Source = pi.Img; pvImg.Visibility = Visibility.Visible; }
                else if (!string.IsNullOrEmpty(pi.Text)) { pvText.Text = pi.Text; pvTextBox.Visibility = Visibility.Visible; }
            }
        }
    }
}
