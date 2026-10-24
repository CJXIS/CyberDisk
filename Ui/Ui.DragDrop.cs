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
        // ── drag & drop reordering: floating ghost with glow + move icon, dimmed source, popping drop target ──
        UIElement hot;
        FrameworkElement ghost, dragSrc;
        Point grabOff;

        void SetHot(FrameworkElement c)
        {
            if (c == hot) return;
            ClearHot();
            if (c == null) return;
            hot = c;
            c.Effect = new DropShadowEffect { Color = Col("#3DD6F5"), BlurRadius = 26, ShadowDepth = 0, Opacity = 0.95 };
            c.RenderTransformOrigin = new Point(0.5, 0.5);
            var st = new ScaleTransform(1, 1); c.RenderTransform = st;
            var a = new DoubleAnimation(1.04, TimeSpan.FromMilliseconds(130)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a); st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        void ClearHot()
        {
            if (hot == null) return;
            var h = hot; hot = null;
            h.Effect = null; h.RenderTransform = Transform.Identity;
        }

        static FrameworkElement ContainerOf(ItemsControl ic, object src)
        {
            var d = src as DependencyObject;
            return d == null ? null : ItemsControl.ContainerFromElement(ic, d) as FrameworkElement;
        }

        void StartGhost(FrameworkElement src, Point grab)
        {
            EndGhost();
            if (src == null || src.ActualWidth < 4 || src.ActualHeight < 4 || dragLayer == null) return;
            dragSrc = src;
            double gw = src.ActualWidth * zoom, gh = src.ActualHeight * zoom;
            grabOff = new Point(grab.X * zoom, grab.Y * zoom);

            // snapshot of the item (taken before it is dimmed)
            ImageSource snap = null;
            try
            {
                var ps = PresentationSource.FromVisual(w);
                double k = zoom * (ps != null ? ps.CompositionTarget.TransformToDevice.M11 : 1.0);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen()) dc.DrawRectangle(new VisualBrush(src), null, new Rect(0, 0, src.ActualWidth, src.ActualHeight));
                var rtb = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(src.ActualWidth * k)), Math.Max(1, (int)Math.Ceiling(src.ActualHeight * k)), 96 * k, 96 * k, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.Freeze();
                snap = rtb;
            }
            catch { }

            var root = new Grid { Width = gw, Height = gh, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
            var sc = new ScaleTransform(0.94, 0.94);
            root.RenderTransform = sc;
            var body = new Border { CornerRadius = new CornerRadius(14), ClipToBounds = true };
            if (snap != null) body.Child = new Image { Source = snap, Stretch = Stretch.Fill };
            else body.Background = new VisualBrush(src) { Stretch = Stretch.Fill };
            root.Children.Add(body);
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2),
                BorderBrush = new LinearGradientBrush(Col("#3DD6F5"), Col("#8B7CFF"), 45)
            });
            root.Children.Add(new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Br("#3DD6F5"),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -13, -13, 0),
                Child = new TextBlock { Text = "✥", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 16, Foreground = Br("#06111D"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            });
            root.Effect = new DropShadowEffect { Color = Col("#3DD6F5"), BlurRadius = 36, ShadowDepth = 0, Opacity = 0.8 };
            ghost = root;
            dragLayer.Children.Add(root);
            MoveGhost();

            root.Opacity = 0;
            root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.96, TimeSpan.FromMilliseconds(130)));
            var pop = new DoubleAnimation(0.94, 1.04, TimeSpan.FromMilliseconds(190)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, pop); sc.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            src.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.28, TimeSpan.FromMilliseconds(150)));     // the original stays behind, dimmed
        }

        void MoveGhost()
        {
            if (ghost == null || dragLayer == null) return;
            Win.POINT p;
            if (!Win.GetCursorPos(out p)) return;
            var pt = dragLayer.PointFromScreen(new Point(p.X, p.Y));
            Canvas.SetLeft(ghost, pt.X - grabOff.X);
            Canvas.SetTop(ghost, pt.Y - grabOff.Y);
        }

        void EndGhost()
        {
            if (dragSrc != null)
            {
                var s = dragSrc; dragSrc = null;
                var back = new DoubleAnimation(0.28, 1, TimeSpan.FromMilliseconds(220));
                back.Completed += (x, y) => { s.BeginAnimation(UIElement.OpacityProperty, null); s.Opacity = 1; };
                s.BeginAnimation(UIElement.OpacityProperty, back);
            }
            if (ghost != null)
            {
                var g = ghost; ghost = null;
                var fo = new DoubleAnimation(0.96, 0, TimeSpan.FromMilliseconds(150));
                fo.Completed += (x, y) => dragLayer.Children.Remove(g);
                g.BeginAnimation(UIElement.OpacityProperty, fo);
            }
        }

        void EnableReorder(ItemsControl ic, Func<System.Collections.IList> getList, Func<object, object, bool> allow, Action moved)
        {
            object cand = null; FrameworkElement candEl = null; Point start = new Point(), grab = new Point();
            ic.AllowDrop = true;
            ic.PreviewMouseLeftButtonDown += (s, e) =>
            {
                candEl = ContainerOf(ic, e.OriginalSource);
                cand = candEl == null ? null : candEl.DataContext;
                start = e.GetPosition(null);
                grab = candEl == null ? new Point() : e.GetPosition(candEl);
            };
            ic.PreviewMouseLeftButtonUp += (s, e) => { cand = null; };
            ic.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || cand == null) return;
                var p = e.GetPosition(null);
                if (Math.Abs(p.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                if (!getList().Contains(cand)) { cand = null; return; }          // group headers etc. are not draggable
                dragging = cand; var el = candEl; var g0 = grab; cand = null;
                StartGhost(el, g0);
                try { DragDrop.DoDragDrop(ic, "cd-drag", DragDropEffects.Move); }
                finally { dragging = null; ClearHot(); EndGhost(); }
            };
            ic.GiveFeedback += (s, e) => { MoveGhost(); e.UseDefaultCursors = false; Mouse.SetCursor(Cursors.SizeAll); e.Handled = true; };
            ic.DragOver += (s, e) =>
            {
                MoveGhost();
                var c = ContainerOf(ic, e.OriginalSource); var tgt = c == null ? null : c.DataContext; var l = getList();
                bool ok = dragging != null && tgt != null && tgt != dragging && l.Contains(tgt) && l.Contains(dragging) && (allow == null || allow(dragging, tgt));
                e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
                SetHot(ok ? c : null);
                e.Handled = true;
            };
            ic.DragLeave += (s, e) => ClearHot();
            ic.Drop += (s, e) =>
            {
                var c = ContainerOf(ic, e.OriginalSource); var tgt = c == null ? null : c.DataContext; var l = getList(); var src = dragging;
                ClearHot();
                if (src == null || tgt == null || src == tgt || !l.Contains(src) || !l.Contains(tgt) || (allow != null && !allow(src, tgt))) return;
                int ti = l.IndexOf(tgt); l.Remove(src); l.Insert(ti, src);
                moved();
                e.Handled = true;
            };
        }

        void EnableReorderPanel(Panel p, Action moved)
        {
            FrameworkElement cand = null; Point start = new Point(), grab = new Point();
            Func<object, FrameworkElement> childOf = o =>
            {
                var d = o as DependencyObject;
                while (d != null)
                {
                    var fe = d as FrameworkElement;
                    if (fe != null && fe.Parent == p) return fe;
                    d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
                }
                return null;
            };
            p.AllowDrop = true;
            p.PreviewMouseLeftButtonDown += (s, e) => { cand = childOf(e.OriginalSource); start = e.GetPosition(null); grab = cand == null ? new Point() : e.GetPosition(cand); };
            p.PreviewMouseLeftButtonUp += (s, e) => { cand = null; };
            p.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || cand == null) return;
                var pt = e.GetPosition(null);
                if (Math.Abs(pt.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(pt.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                dragging = cand; var el = cand; var g0 = grab; cand = null;
                StartGhost(el, g0);
                try { DragDrop.DoDragDrop(p, "cd-drag", DragDropEffects.Move); }
                finally { dragging = null; ClearHot(); EndGhost(); }
            };
            p.GiveFeedback += (s, e) => { MoveGhost(); e.UseDefaultCursors = false; Mouse.SetCursor(Cursors.SizeAll); e.Handled = true; };
            p.DragOver += (s, e) =>
            {
                MoveGhost();
                var tgt = childOf(e.OriginalSource);
                bool ok = dragging is FrameworkElement && tgt != null && tgt != dragging;
                e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
                SetHot(ok ? tgt : null);
                e.Handled = true;
            };
            p.DragLeave += (s, e) => ClearHot();
            p.Drop += (s, e) =>
            {
                var tgt = childOf(e.OriginalSource); var src = dragging as FrameworkElement;
                ClearHot();
                if (src == null || tgt == null || src == tgt) return;
                int ti = p.Children.IndexOf(tgt);
                p.Children.Remove(src); p.Children.Insert(ti, src);
                moved();
                e.Handled = true;
            };
        }

        void ToggleFull()
        {
            var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            if (!full)
            {
                if (w.WindowState != WindowState.Normal) w.WindowState = WindowState.Normal;
                Win.GetWindowRect(h, out savedRect);
                var mi = new Win.MONITORINFO { cbSize = Marshal.SizeOf(typeof(Win.MONITORINFO)) };
                Win.GetMonitorInfo(Win.MonitorFromWindow(h, 2), ref mi);
                Win.SetWindowPos(h, new IntPtr(-1), mi.rcMonitor.L, mi.rcMonitor.T, mi.rcMonitor.R - mi.rcMonitor.L, mi.rcMonitor.B - mi.rcMonitor.T, 0x0040);
                full = true;
            }
            else
            {
                Win.SetWindowPos(h, new IntPtr(-2), savedRect.L, savedRect.T, savedRect.R - savedRect.L, savedRect.B - savedRect.T, 0x0040);
                full = false;
            }
            icoFull.Data = Geometry.Parse(full
                ? "M 5,1 L 5,5 L 1,5 M 9,1 L 9,5 L 13,5 M 13,9 L 9,9 L 9,13 M 1,9 L 5,9 L 5,13"
                : "M 1,5 L 1,1 L 5,1 M 9,1 L 13,1 L 13,5 M 13,9 L 13,13 L 9,13 M 5,13 L 1,13 L 1,9");
            bFull.ToolTip = full ? "Exit full screen (F11 / Esc)" : "Full screen (F11)";
        }
    }
}
