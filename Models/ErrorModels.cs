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
    // ───────────────────────── App error detection ─────────────────────────
    sealed class CrashEvt { public DateTime Time; public string Kind, App, Version, Module, ModulePath, Code, ExePath; }

    sealed class ErrApp : Obs
    {
        public string Name { get; set; }
        public string ExePath { get; set; }
        public List<CrashEvt> Events = new List<CrashEvt>();
        public int Crashes, Hangs;
        public DateTime Last;
        public string Summary { get; set; }
        public string Cause { get; set; }
        public string Advice { get; set; }
        public string ActionCaption { get; set; }
        public string ActionUri { get; set; }
        public string Details { get; set; }
        public Brush Tone { get; set; }
        public string Glyph { get; set; }
        public string Severity { get; set; }
        bool open;
        public bool Open { get => open; set { Set(ref open, value); Raise("DetailsVis"); Raise("OpenCaption"); } }
        public Visibility DetailsVis { get { return open ? Visibility.Visible : Visibility.Collapsed; } }
        public string OpenCaption { get { return open ? "HIDE DETAILS" : "SHOW DETAILS"; } }
        public Visibility ActionVis { get { return string.IsNullOrEmpty(ActionCaption) ? Visibility.Collapsed : Visibility.Visible; } }
        public Visibility FolderVis { get { return !string.IsNullOrEmpty(ExePath) ? Visibility.Visible : Visibility.Collapsed; } }
    }

    sealed class HungVm { public int Pid; public string Name { get; set; } public string Title { get; set; } }
}
