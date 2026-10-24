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
    // ───────────────────────── Uninstall leftovers ─────────────────────────
    sealed class Leftover : Obs
    {
        public string Kind { get; set; }          // Folder / File / Registry / Startup
        public string Shown { get; set; }
        public string Target;
        public string SizeText { get; set; } = "—";
        public long Size;
        public Action Remove;
        public Brush KindBrush { get; set; }
        bool chk = true;
        public bool Checked { get => chk; set => Set(ref chk, value); }
    }
}
