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
    // ───────────────────────── Small view models for the new pages ─────────────────────────
    /// <summary>A big number with a caption, e.g. TEMPERATURE 38 °C.</summary>
    sealed class TileVm { public string Label { get; set; } public string Value { get; set; } public string Sub { get; set; } public Brush Brush { get; set; } }

    /// <summary>One line in "What affects the score".</summary>
    sealed class IssueVm { public string Glyph { get; set; } public string Text { get; set; } public string Penalty { get; set; } public Brush Tone { get; set; } }

    /// <summary>A volume row with a usage bar.</summary>
    sealed class VolVm { public string Letter { get; set; } public string Label { get; set; } public string Info { get; set; } public string Used { get; set; } public string Format { get; set; } public Visibility FormatVis { get { return string.IsNullOrEmpty(Format) ? Visibility.Collapsed : Visibility.Visible; } } public double Percent { get; set; } public Brush Brush { get; set; } }

    /// <summary>A SMART attribute row.</summary>
    sealed class SmartVm { public int Id { get; set; } public string Name { get; set; } public int Current { get; set; } public int Worst { get; set; } public long Raw { get; set; } public Brush Tone { get; set; } }

    /// <summary>One bar of the activity chart on the App Errors page.</summary>
    sealed class ErrBar { public double Height { get; set; } public Brush Brush { get; set; } public string Label { get; set; } public string Tip { get; set; } }
}
