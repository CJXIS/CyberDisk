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
    // ───────────────────────── Score theme (shared by drives, health, disk health and stability rings) ─────────────────────────
    /// <summary>One colour language for every score ring: the better the score, the cooler the gradient.</summary>
    static class ScoreTheme
    {
        public static string Grade(int score) { return score >= 90 ? "EXCELLENT" : score >= 75 ? "GOOD" : score >= 55 ? "FAIR" : score >= 35 ? "POOR" : "CRITICAL"; }

        /// <summary>Gradient start / end colours for a score.</summary>
        public static string[] Colors(int score)
        {
            if (score >= 90) return new[] { "#3DDC97", "#3DD6F5" };
            if (score >= 75) return new[] { "#3DD6F5", "#8B7CFF" };
            if (score >= 55) return new[] { "#FFB84D", "#FF9F6B" };
            if (score >= 35) return new[] { "#FF9F6B", "#FF6B8A" };
            return new[] { "#FF6B8A", "#FF3D5A" };
        }

        public static Brush Gradient(int score) { var c = Colors(score); var b = new LinearGradientBrush(Ui.Col(c[0]), Ui.Col(c[1]), 45); b.Freeze(); return b; }
        public static Brush Solid(int score) { var b = new SolidColorBrush(Ui.Col(Colors(score)[0])); b.Freeze(); return b; }

        /// <summary>Colour for a severity level: 0 ok, 1 warning, 2 problem, 3 info.</summary>
        public static string Level(int level) { return level == 0 ? "#3DDC97" : level == 1 ? "#FFB84D" : level == 2 ? "#FF6B8A" : "#5B9BFF"; }

        /// <summary>Draws a score ring exactly like the drive cards: gradient stroke, round caps and a soft glow.</summary>
        public static void StyleRing(System.Windows.Shapes.Path arc, double percent, int score)
        {
            arc.Data = DriveVm.Arc(percent);
            arc.Stroke = Gradient(score);
            var glow = new DropShadowEffect { Color = Ui.Col(Colors(score)[0]), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.8 };
            glow.Freeze();
            arc.Effect = glow;
        }
    }
}
