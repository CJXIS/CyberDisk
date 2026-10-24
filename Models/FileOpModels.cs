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
    // ───────────────────────── Fast file operations ─────────────────────────
    sealed class OpCtx
    {
        public long TotalBytes, DoneBytes, TotalFiles, DoneFiles, Failed;
        public volatile bool Cancelled;
        public string Current = "";
        public ConcurrentBag<string> Errors = new ConcurrentBag<string>();
        public Stopwatch Sw = Stopwatch.StartNew();
    }

    sealed class CopyPlan
    {
        public List<string> Dirs = new List<string>();
        public List<Tuple<string, string, long>> Files = new List<Tuple<string, string, long>>();
        public long Bytes;
    }
}
