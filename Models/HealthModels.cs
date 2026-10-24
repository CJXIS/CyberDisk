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
    // ───────────────────────── Health scoring ─────────────────────────
    /// <summary>One category bar in the health report (Storage, Security, ...).</summary>
    sealed class CatScore
    {
        public string Name { get; set; }
        public int Score { get; set; }
        public string ScoreText { get; set; }
        public double Percent { get; set; }
        public Brush Brush { get; set; }
    }

    /// <summary>The complete, explainable result of a health check.</summary>
    sealed class HealthReport
    {
        public int Score;
        public string Grade;
        public int Problems, Warnings, Passed;
        public List<CatScore> Categories = new List<CatScore>();
    }
}
