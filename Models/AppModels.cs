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
    sealed class AppItem : Obs
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public long SizeBytes { get; set; }
        public string SizeText { get; set; }
        public string Location { get; set; }
        public string IconPath, Uninstall, Package;
        // install date + where the entry lives (used by the details panel)
        public DateTime? InstallDate;
        public string DateSource = "", RegSub = "", PackageFamily = "", PackageArch = "";
        public RegistryHive Hive; public RegistryView View;
        public string InstallText { get { return InstallDate.HasValue ? InstallDate.Value.ToString("yyyy-MM-dd") : "—"; } }
        public long InstallSort { get { return InstallDate.HasValue ? InstallDate.Value.Ticks : 0; } }
        // measured on demand when the app is selected (-1 = not measured yet)
        public long MeasuredBytes = -1; public int MeasuredFiles; public string RunText; public bool Measuring;
        ImageSource icon;
        public ImageSource Icon { get => icon; set => Set(ref icon, value); }
    }
}
