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
    // ───────────────────────── Health check ─────────────────────────
    sealed class Finding : Obs
    {
        public int Level;                       // 0 ok, 1 warning, 2 problem, 3 info
        public string Key;                      // event findings: provider|id, used to remember "reviewed"
        public string Category = "System";      // Storage, Reliability, Security, System or Performance (drives the score breakdown)
        public string FixId, FixData;           // what the fix is and its argument
        public bool AutoSafe;                   // part of FIX ALL
        string caption, title, detail, glyph, info, advice, result;
        Brush tone, resBrush; bool busy, fixed_;
        public string Title { get => title; set => Set(ref title, value); }
        public string Detail { get => detail; set => Set(ref detail, value); }
        public string Glyph { get => glyph; set => Set(ref glyph, value); }
        public Brush Tone { get => tone; set => Set(ref tone, value); }
        public string FixInfo { get => info; set { Set(ref info, value); Refresh(); } }
        public string Advice { get => advice; set { Set(ref advice, value); Refresh(); } }
        public string Result { get => result; set { Set(ref result, value); Refresh(); } }
        public Brush ResultBrush { get => resBrush; set => Set(ref resBrush, value); }
        public string FixCaption { get => busy ? "FIXING…" : caption; }
        public bool Busy { get => busy; set { busy = value; Refresh(); } }
        public bool IsFixed { get => fixed_; set { fixed_ = value; Refresh(); } }
        public bool CanRun { get { return FixId != null && !busy && !fixed_; } }
        public Visibility FixVis { get { return FixId != null && !fixed_ ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility InfoVis { get { return FixId != null && !fixed_ && !string.IsNullOrEmpty(info) ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility AdviceVis { get { return FixId == null && !string.IsNullOrEmpty(advice) ? Visibility.Visible : Visibility.Collapsed; } }
        public Visibility ResultVis { get { return string.IsNullOrEmpty(result) ? Visibility.Collapsed : Visibility.Visible; } }
        public void Refresh() { foreach (var n in new[] { "FixCaption", "CanRun", "FixVis", "InfoVis", "AdviceVis", "ResultVis" }) Raise(n); }

        public Finding WithFix(string id, string cap, string infoText, string data = null, bool auto = false)
        { FixId = id; caption = cap; info = infoText; FixData = data; AutoSafe = auto; return this; }
        public Finding WithAdvice(string text) { advice = text; return this; }
        public Finding WithKey(string k) { Key = k; return this; }
        public Finding In(string category) { Category = category; return this; }

        public void SetLevel(int level)
        {
            Level = level;
            string c = level == 0 ? "#3DDC97" : level == 1 ? "#FFB84D" : level == 2 ? "#FF6B8A" : "#5B9BFF";
            var b = new SolidColorBrush(Ui.Col(c)); b.Freeze();
            Tone = b; Glyph = level == 0 ? "✓" : level == 1 ? "!" : level == 2 ? "✖" : "i";
        }
    }
}
