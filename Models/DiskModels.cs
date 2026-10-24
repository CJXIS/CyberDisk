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
    // ───────────────────────── Physical disk health & information ─────────────────────────
    /// <summary>One SMART attribute as reported by a SATA drive.</summary>
    sealed class SmartAttr { public int Id; public string Name; public int Current, Worst; public long Raw; }

    /// <summary>A volume (drive letter) that lives on a physical disk.</summary>
    sealed class DiskVolume { public string Letter, Label, Format; public long Size, Free; }

    /// <summary>A partition on a physical disk (with or without a drive letter).</summary>
    sealed class DiskPart { public int No; public string Type = "", Letter = ""; public long Size, Offset; public bool Boot; }

    /// <summary>One point that raised or lowered the health score, shown in "What affects the score".</summary>
    sealed class DiskIssue { public int Level; public int Penalty; public string Text; }

    /// <summary>Everything CyberDisk knows about one physical disk.</summary>
    sealed class PhysDisk
    {
        // identity
        public int Number;
        public string Model = "", Serial = "", Firmware = "", Bus = "", Media = "", PartStyle = "", Pnp = "", Interface = "", DeviceId = "";
        public long Size;
        public int Partitions;
        public int? Rpm, LogicalSector, PhysicalSector;
        // health (Windows storage stack)
        public string Health = "", Operational = "";
        public bool PredictFailure;
        // reliability counters
        public int? TempC, TempMaxC, WearPct;
        public long? PowerOnHours, ReadErrors, WriteErrors, StartStops, LoadCycles;
        // details
        public List<DiskVolume> Volumes = new List<DiskVolume>();
        public List<DiskPart> Parts = new List<DiskPart>();
        public List<SmartAttr> Smart = new List<SmartAttr>();
        // verdict
        public int Score = 100;
        public string Grade = "EXCELLENT", Verdict = "";
        public List<DiskIssue> Issues = new List<DiskIssue>();

        public bool IsSsd { get { return Media == "SSD" || Bus == "NVMe"; } }
        public string Letters { get { return string.Join(", ", Volumes.Select(v => v.Letter)); } }
        public string Kind { get { return Bus == "NVMe" ? "NVMe SSD" : Media == "SSD" ? "SSD" : Media == "HDD" ? "HDD" : Bus == "USB" ? "USB drive" : "Disk"; } }
    }
}
