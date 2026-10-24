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
        // ── navigation ──

        static void Nav(Button b, bool on, string hex)
        {
            b.BorderBrush = on ? Br("#" + hex) : Brushes.Transparent;
            b.Background = on ? Br("#1C" + hex) : Brushes.Transparent;
        }

        // ── navigation (now with BOOST and HEALTH) ──
        void Go(FrameworkElement v)
        {
            foreach (var x in new[] { vDrives, vBrowse, vApps, vClean, vBoost, vHealth, vErrors, vPc, vDisk, vSettings }) x.Visibility = x == v ? Visibility.Visible : Visibility.Collapsed;
            Anim(v);
            Nav(navDrives, v == vDrives || v == vBrowse || v == vDisk, "3DD6F5");
            Nav(navApps, v == vApps, "8B7CFF");
            Nav(navClean, v == vClean, "3DDC97");
            Nav(navBoost, v == vBoost, "FFB84D");
            Nav(navHealth, v == vHealth, "FF6B8A");
            Nav(navErrors, v == vErrors, "FF9F6B");
            Nav(navPc, v == vPc, "5B9BFF");
            Nav(navSettings, v == vSettings, "A9BAD3");
            if (v != vErrors && hungTm != null) hungTm.Stop();
            if (v == vBoost) EnterBoost(); else if (boostTm != null) boostTm.Stop();
            if (!cleaning && scanning.Count == 0) txtStatus.Text = "READY";
        }
    }
}
