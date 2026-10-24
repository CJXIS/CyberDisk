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
    // ───────────────────────── Startup apps (same switch Task Manager uses) ─────────────────────────
    sealed class StartupVm : Obs
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Source { get; set; }
        public RegistryHive Hive; public string ApprovedKey;
        bool en;
        public bool Enabled
        {
            get => en;
            set { if (en == value) return; if (Startup.SetEnabled(this, value)) Set(ref en, value); else Raise("Enabled"); }
        }
        public void Init(bool v) { en = v; }
    }
}
