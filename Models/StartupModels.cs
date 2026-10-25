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
        public static Action<string> OnFail;                       // told when Windows refused a change (for example without administrator rights)
        public ImageSource Icon { get; set; }
        public string Publisher { get; set; } = "";
        public string ExePath { get; set; } = "";
        public bool Missing { get; set; }
        public string Badge { get { return ApprovedKey == "StartupFolder" ? "STARTUP FOLDER" : Source.IndexOf("32-bit", StringComparison.OrdinalIgnoreCase) >= 0 ? "32-BIT" : Hive == RegistryHive.CurrentUser ? "THIS USER" : "ALL USERS"; } }
        public string ShortCommand { get { return string.IsNullOrEmpty(ExePath) ? Command : ExePath; } }
        public string StateText { get { return Missing ? "File not found - this entry does nothing" : en ? "Starts with Windows" : "Turned off"; } }
        public Brush StateBrush { get { return new SolidColorBrush(Ui.Col(Missing ? "#FFB84D" : en ? "#3DDC97" : "#6B7E99")); } }
        public Visibility FolderVis { get { return !Missing && ExePath.Length > 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public double Dim { get { return en ? 1.0 : 0.72; } }
        bool en;
        public bool Enabled
        {
            get => en;
            set
            {
                if (en == value) return;
                if (Startup.SetEnabled(this, value)) { Set(ref en, value); Raise("StateText"); Raise("StateBrush"); Raise("Dim"); }
                else { Raise("Enabled"); if (OnFail != null) OnFail(Name); }
            }
        }
        public void Init(bool v) { en = v; }
    }
}
