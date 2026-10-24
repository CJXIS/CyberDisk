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
        // ───────── language ─────────
        void ApplyLang(bool ar, bool save)
        {
            Loc.Ar = ar;
            if (save) Settings.Lang = ar ? "ar" : "en";
            w.FlowDirection = ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (langTxt != null) langTxt.Text = ar ? "EN" : "ع";
            Loc.Walk(w, true);
            FillTray();
            SyncSettings();
            UpdateHealthSummary();
            if (txtStatus != null && save) txtStatus.Text = "READY";
        }
    }
}
