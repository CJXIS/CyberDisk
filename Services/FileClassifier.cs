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
    // ───────────────────────── Scanning ─────────────────────────
    static class Cls
    {
        public static readonly string[] Names = { "DOCUMENTS", "IMAGES", "VIDEO", "AUDIO", "ARCHIVES", "CODE", "SYSTEM / EXEC", "OTHER" };
        public static readonly string[] Colors = { "#3DD6F5", "#FF6B8A", "#8B7CFF", "#3DDC97", "#FFB84D", "#5B9BFF", "#FF7A7A", "#8195B0" };
        static readonly Dictionary<string, int> M = Build();

        static Dictionary<string, int> Build()
        {
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            void A(int c, string list) { foreach (var e in list.Split(' ')) d["." + e] = c; }
            A(0, "doc docx pdf txt rtf odt xls xlsx ppt pptx csv md epub");
            A(1, "jpg jpeg png gif bmp webp tif tiff svg ico heic raw psd");
            A(2, "mp4 mkv avi mov wmv flv webm m4v mpg mpeg");
            A(3, "mp3 wav flac aac ogg m4a wma");
            A(4, "zip rar 7z tar gz bz2 xz iso cab wim vhd vhdx");
            A(5, "cs js ts py java c cpp h hpp html css json xml php go rs sql sh bat ps1");
            A(6, "exe dll sys msi msix drv ocx dat mui cat");
            return d;
        }
        public static int Of(string name)
        {
            int i = name.LastIndexOf('.');
            if (i < 0 || name.Length - i > 6) return 7;
            return M.TryGetValue(name.Substring(i), out var c) ? c : 7;
        }
    }
}
