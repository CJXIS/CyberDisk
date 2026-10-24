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
    // ───────────────────────── PC hardware information ─────────────────────────
    /// <summary>A label / value line inside a hardware card.</summary>
    sealed class HwRow { public string K { get; set; } public string V { get; set; } public string W { get; set; } }

    /// <summary>One drive line in the Storage card.</summary>
    sealed class HwDisk
    {
        public string Name { get; set; }       // Hitachi HDS721010DLE630
        public string Meta { get; set; }       // 932 GB · HDD · C:, D:
        public string Kind { get; set; }       // HDD / NVMe SSD
        public string Health { get; set; }     // 27
        public string Grade { get; set; }      // POOR
        public Brush Tone { get; set; }
        public double Percent { get; set; }
        public string Plain { get; set; }      // plain text for copy / export
    }

    /// <summary>One card on the "My PC" page: processor, memory, graphics, ...</summary>
    sealed class HwCard : Obs
    {
        public string Title { get; set; }              // PROCESSOR
        public string Glyph { get; set; }              // kept for older code; the icon is drawn from IconData
        public string IconData { get; set; }           // vector path from Vec
        public Geometry Icon { get { return string.IsNullOrEmpty(IconData) ? null : Vec.Geo(IconData); } }
        public List<HwDisk> Disks { get; set; } = new List<HwDisk>();
        public Visibility DisksVis { get { return Disks.Count > 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility RowsVis { get { return Rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed; } }
        public Brush AccentBrush { get; set; }
        public string Headline { get; set; }           // the model name
        public string Sub { get; set; }                // one-line summary
        public List<HwRow> Rows { get; set; } = new List<HwRow>();
        public List<HwRow> AdvRows { get; set; } = new List<HwRow>();
        // optional live gauge (CPU load, memory in use, battery level)
        public double GaugeValue { get; set; }
        public string GaugeLabel { get; set; }
        public string GaugeText { get; set; }
        public Brush GaugeBrush { get; set; }
        public bool HasGauge;
        public Visibility GaugeVis { get { return HasGauge ? Visibility.Visible : Visibility.Collapsed; } }

        double cardWidth = 430;
        public double CardWidth { get { return cardWidth; } set { cardWidth = value; Raise("CardWidth"); } }

        bool showAdv;
        public bool ShowAdv { get { return showAdv; } set { showAdv = value; Raise("AdvVis"); } }
        public Visibility AdvVis { get { return showAdv && AdvRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed; } }
    }

    /// <summary>The whole hardware report.</summary>
    sealed class HwReport
    {
        public string PcName = "", Model = "", Os = "", Summary = "", Uptime = "";
        public List<HwCard> Cards = new List<HwCard>();
    }
}
