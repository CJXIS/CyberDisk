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
    sealed class Row
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Meta { get; set; }
        public long SizeBytes { get; set; }
        public string SizeText { get; set; }
        public double Percent { get; set; }
        public string PctText { get; set; }
        public ImageSource Icon { get; set; }
        public bool IsDir;
        public string Path;
        public Node Node;
        public DateTime Modified;
        public Brush PctBrush { get; set; }
        public string ModText { get { return Modified == DateTime.MinValue ? "" : Modified.ToString("yyyy-MM-dd HH:mm"); } }
        public long ModSort { get { return Modified.Ticks; } }
    }
}
