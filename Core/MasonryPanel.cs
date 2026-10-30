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
    /// <summary>
    /// Lays cards out in columns and always puts the next card under the shortest column,
    /// so a short card never leaves an empty gap next to a tall one.
    /// </summary>
    sealed class MasonryPanel : Panel
    {
        int cols = 1; double colW;

        static int Shortest(double[] h) { int k = 0; for (int i = 1; i < h.Length; i++) if (h[i] < h[k] - 0.5) k = i; return k; }

        protected override Size MeasureOverride(Size avail)
        {
            double itemW = 0;
            foreach (UIElement c in InternalChildren)
            {
                c.Measure(new Size(double.IsInfinity(avail.Width) ? double.PositiveInfinity : avail.Width, double.PositiveInfinity));
                itemW = Math.Max(itemW, c.DesiredSize.Width);
            }
            colW = itemW;
            cols = itemW <= 0 || double.IsInfinity(avail.Width) ? 1 : Math.Max(1, (int)Math.Floor((avail.Width + 1) / itemW));
            var h = new double[cols];
            foreach (UIElement c in InternalChildren) { int k = Shortest(h); h[k] += c.DesiredSize.Height; }
            return new Size(cols * itemW, h.Max());
        }

        protected override Size ArrangeOverride(Size final)
        {
            var h = new double[cols];
            foreach (UIElement c in InternalChildren)
            {
                int k = Shortest(h);
                c.Arrange(new Rect(k * colW, h[k], colW, c.DesiredSize.Height));
                h[k] += c.DesiredSize.Height;
            }
            return new Size(final.Width, h.Max());
        }
    }
}
