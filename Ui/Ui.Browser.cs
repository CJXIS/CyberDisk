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
            Icon = ShellIcons.Get("", true).Icon
        };

        Row FileRow(FE f, Node parent)
        {
            var ic = ShellIcons.Get(Ext.ExtOf(f.Name), false);
            return new Row
            {
                Name = f.Name, IsDir = false, Path = parent.Path.TrimEnd('\\') + "\\" + f.Name, Kind = ic.Type,
                Meta = f.Mod == DateTime.MinValue ? "" : f.Mod.ToString("yyyy-MM-dd HH:mm"), Modified = f.Mod,
                SizeBytes = f.Size, SizeText = Fmt(f.Size), Percent = Pct(f.Size, parent.Size), PctText = Pct(f.Size, parent.Size).ToString("0.0") + "%",
                Icon = ic.Icon
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
        void ClearPreview() { pvId++; pvBody.Visibility = Visibility.Collapsed; pvEmpty.Visibility = Visibility.Visible; }

        static readonly string[] ImgExt = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".tif", ".tiff" };
        static ImageSource LoadImg(string p)
        {
            try
            {
                var bi = new BitmapImage();
                using (var s = File.OpenRead(p))
                {
                    bi.BeginInit(); bi.CacheOption = BitmapCacheOption.OnLoad; bi.DecodePixelWidth = 320; bi.StreamSource = s; bi.EndInit();
                }
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        async void Preview()
        {
            var r = gs.SelectedItem as Row;
            if (r == null || r.Path == null) { ClearPreview(); return; }
            int id = ++pvId;
            pvEmpty.Visibility = Visibility.Collapsed; pvBody.Visibility = Visibility.Visible;
            pvIcon.Source = r.Icon; pvName.Text = r.Name; pvKind.Text = r.Kind; pvPath.Text = r.Path;
            pvImg.Source = null; pvImg.Visibility = Visibility.Collapsed;
            if (r.IsDir)
            {
                var n = r.Node;
                pvInfoL.Text = "SIZE\nSHARE\nFILES\nFOLDERS"; pvInfo.Text = r.SizeText + "\n" + r.PctText + "\n" + n.Files.ToString("N0") + "\n" + n.Dirs.ToString("N0");
                pvListHdr.Text = "INSIDE ▸ largest items";
                pvListHdr.Visibility = Visibility.Visible; pvList.Visibility = Visibility.Visible;
                var kids = n.Kids.OrderByDescending(k => k.Size).Take(30).Select(k => FolderRow(k, n)).ToList();
                pvList.ItemsSource = kids;
                List<FE> files;
                try { files = await Task.Run(() => Fs.Files(n.Path)); } catch { files = new List<FE>(); }
                if (id != pvId) return;
                pvList.ItemsSource = kids.Concat(files.Take(30).Select(f => FileRow(f, n))).OrderByDescending(x => x.SizeBytes).Take(40).ToList();
            }
            else
            {
                string mod = r.Modified == DateTime.MinValue ? "—" : r.Modified.ToString("yyyy-MM-dd HH:mm");
                pvInfoL.Text = "SIZE\nSHARE\nMODIFIED"; pvInfo.Text = r.SizeText + "\n" + r.PctText + "\n" + mod;
                pvListHdr.Visibility = Visibility.Collapsed; pvList.Visibility = Visibility.Collapsed;
                if (ImgExt.Contains(Ext.ExtOf(r.Name).ToLowerInvariant()))
                {
                    string p = r.Path;
                    var src = await Task.Run(() => LoadImg(p));
                    if (id == pvId && src != null) { pvImg.Source = src; pvImg.Visibility = Visibility.Visible; }
                }
            }
        }
    }
}
