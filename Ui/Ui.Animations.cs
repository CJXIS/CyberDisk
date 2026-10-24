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
        // ── animations that must not live inside templates (code-driven, fail-safe) ──
        static T FindByName<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                var fe = c as FrameworkElement;
                if (fe != null && fe.Name == name && fe is T) return (T)fe;
                var r = FindByName<T>(c, name);
                if (r != null) return r;
            }
            return null;
        }

        void StartShimmer(ProgressBar b)
        {
            try
            {
                if (b == null || (b.Tag as string) == "shim") return;
                b.ApplyTemplate();
                var r = FindByName<FrameworkElement>(b, "shimRect");
                if (r == null) return;
                var tt = new TranslateTransform(-80, 0);
                r.RenderTransform = tt;
                tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-80, 520, TimeSpan.FromMilliseconds(1500)) { RepeatBehavior = RepeatBehavior.Forever });
                b.Tag = "shim";
            }
            catch { }
        }

        void CardHover(Button btn, bool on)
        {
            try
            {
                if (btn == null || cardStyle == null || btn.Style != cardStyle) return;
                var g = FindByName<Border>(btn, "glow");
                if (g == null) return;
                g.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(on ? 180 : 260)));
            }
            catch { }
        }

        // ── cute animations ──
        void Idle()
        {
            mMove.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -9, TimeSpan.FromMilliseconds(950))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        void Hop()
        {
            var a = new DoubleAnimation(0, -42, TimeSpan.FromMilliseconds(240))
            { AutoReverse = true, RepeatBehavior = new RepeatBehavior(3), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            a.Completed += (s, e) => Idle();
            mMove.BeginAnimation(TranslateTransform.YProperty, a);
        }

        void Lid(bool open)
        {
            lidRot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(open ? -34 : 0, TimeSpan.FromMilliseconds(280))
            { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } });
        }

        void Gulp()
        {
            var d = TimeSpan.FromMilliseconds(170);
            mScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.09, 1, d));
            mScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, d));
        }

        // a little file flies from the list into the bin's mouth
        void Fly()
        {
            if (fx == null || fx.ActualWidth < 10 || cleanList.ActualWidth < 10) return;
            var p0 = cleanList.TranslatePoint(new Point(rnd.NextDouble() * cleanList.ActualWidth, rnd.NextDouble() * Math.Min(cleanList.ActualHeight, 520)), fx);
            var p1 = mascot.TranslatePoint(new Point(mascot.ActualWidth / 2, 22), fx);
            var rot = new RotateTransform(); var sc = new ScaleTransform(1, 1);
            var tg = new TransformGroup(); tg.Children.Add(sc); tg.Children.Add(rot);
            double w0 = 10 + rnd.Next(10), h0 = 13 + rnd.Next(10);
            var p = new Border { Width = w0, Height = h0, CornerRadius = new CornerRadius(3), Background = Br(Cls.Colors[rnd.Next(7)]), Opacity = 0.95, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = tg };
            Canvas.SetLeft(p, p0.X); Canvas.SetTop(p, p0.Y);
            fx.Children.Add(p);
            var dur = TimeSpan.FromMilliseconds(620 + rnd.Next(380));
            var ax = new DoubleAnimation(p0.X, p1.X - w0 / 2, dur) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            var ay = new DoubleAnimation(p0.Y, p1.Y - h0 / 2, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            ay.Completed += (s, e) => { fx.Children.Remove(p); Gulp(); };
            p.BeginAnimation(Canvas.LeftProperty, ax);
            p.BeginAnimation(Canvas.TopProperty, ay);
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, rnd.Next(-400, 400), dur));
            var shrink = new DoubleAnimation(1, 0.25, dur);
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, shrink); sc.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        }

        // sparkles (or confetti) bursting from a point
        void Burst(Point o, int n, bool confetti) { BurstOn(fx, o, n, confetti); }

        void BurstOn(Canvas cv, Point o, int n, bool confetti)
        {
            if (cv == null) return;
            string[] glyphs = { "✦", "✧", "★", "●", "◆" };
            for (int i = 0; i < n; i++)
            {
                double size = 8 + rnd.Next(14);
                var col = Br(Cls.Colors[rnd.Next(7)]);
                FrameworkElement el;
                if (confetti) el = new Border { Width = size * 0.6, Height = size, Background = col, CornerRadius = new CornerRadius(2) };
                else el = new TextBlock { Text = glyphs[rnd.Next(glyphs.Length)], FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = size + 6, Foreground = col };
                var rot = new RotateTransform();
                el.RenderTransformOrigin = new Point(0.5, 0.5); el.RenderTransform = rot;
                Canvas.SetLeft(el, o.X); Canvas.SetTop(el, o.Y);
                cv.Children.Add(el);

                double ang = rnd.NextDouble() * Math.PI * 2, dist = 70 + rnd.NextDouble() * 160;
                double dx = Math.Cos(ang) * dist, dy = Math.Sin(ang) * dist;
                var dur = TimeSpan.FromMilliseconds(750 + rnd.Next(650));
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                el.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(o.X, o.X + dx, dur) { EasingFunction = ease });
                DoubleAnimation ya;
                if (confetti) ya = new DoubleAnimation(o.Y, o.Y + dy * 0.4 + 170, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                else ya = new DoubleAnimation(o.Y, o.Y + dy, dur) { EasingFunction = ease };
                var target = el;
                ya.Completed += (s, e) => cv.Children.Remove(target);
                el.BeginAnimation(Canvas.TopProperty, ya);
                el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(dur.TotalMilliseconds * 0.6)) { BeginTime = TimeSpan.FromMilliseconds(dur.TotalMilliseconds * 0.4) });
                rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, rnd.Next(-540, 540), dur));
            }
        }
    }
}
