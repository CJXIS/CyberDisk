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
    sealed class ProcSnap { public int Pid; public string Name; public long Mem, Priv; public TimeSpan Cpu; public int Threads, Handles; }

    sealed class ProcVm : Obs
    {
        public int Pid { get; set; }
        public string Name { get; set; }
        long mem; double cpu;
        public long MemBytes { get => mem; set { Set(ref mem, value); Raise("MemText"); } }
        public string MemText => Ui.Fmt(mem);
        public double Cpu { get => cpu; set { Set(ref cpu, value); Raise("CpuText"); } }
        public string CpuText => cpu < 0.05 ? "0%" : cpu.ToString("0.0") + "%";
        long priv; int threads, handles; string path = "", desc = "", company = "", version = ""; System.Windows.Media.ImageSource icon;
        public long PrivBytes { get => priv; set { Set(ref priv, value); Raise("PrivText"); } }
        public string PrivText => Ui.Fmt(priv);
        public int Threads { get => threads; set => Set(ref threads, value); }
        public int Handles { get => handles; set => Set(ref handles, value); }
        public string Path { get => path; set => Set(ref path, value ?? ""); }
        public string Description { get => desc; set => Set(ref desc, value ?? ""); }
        public string Company { get => company; set => Set(ref company, value ?? ""); }
        public string Version { get => version; set => Set(ref version, value ?? ""); }
        public System.Windows.Media.ImageSource Icon { get => icon; set => Set(ref icon, value); }
    }
}
