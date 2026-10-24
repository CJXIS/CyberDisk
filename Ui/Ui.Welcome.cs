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
        // ── welcome page ──
        static void Reveal(FrameworkElement e, int delay, double dy)
        {
            var tt = new TranslateTransform(0, dy); e.RenderTransform = tt; e.Opacity = 0;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease });
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, TimeSpan.FromMilliseconds(750)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease });
        }

        async void StartWelcome()
        {
            var safety = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) };
            safety.Tick += (s, e) => { safety.Stop(); DismissWelcome(); };
            safety.Start();
            try { await StartWelcomeCore(); }
            catch { DismissWelcome(); }
        }

        async Task StartWelcomeCore()
        {
            string[] cols = { "#3DD6F5", "#8B7CFF", "#3DDC97", "#FF6B8A", "#FFB84D" };
            for (int i = 0; i < 14; i++)
            {
                double d = 120 + rnd.Next(260);
                var c = Col(cols[rnd.Next(cols.Length)]);
                var fill = new RadialGradientBrush(Color.FromArgb(120, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
                var o = new Border { Width = d, Height = d, CornerRadius = new CornerRadius(d / 2), Background = fill };
                double x0 = rnd.NextDouble() * Math.Max(300, welcome.ActualWidth) - d / 3, y0 = rnd.NextDouble() * Math.Max(300, welcome.ActualHeight) - d / 3;
                Canvas.SetLeft(o, x0); Canvas.SetTop(o, y0); wOrbs.Children.Add(o);
                var sine = new SineEase { EasingMode = EasingMode.EaseInOut };
                o.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(x0, x0 + rnd.Next(-280, 280), TimeSpan.FromSeconds(7 + rnd.Next(9))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = sine });
                o.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(y0, y0 + rnd.Next(-200, 200), TimeSpan.FromSeconds(6 + rnd.Next(9))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = sine });
            }
            // the logo pops in and its ring keeps spinning
            var logo = F<FrameworkElement>("wLogo"); var ls = F<ScaleTransform>("wLogoScale");
            var back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 };
            ls.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(950)) { EasingFunction = back });
            ls.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(950)) { EasingFunction = back });
            logo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)));
            F<RotateTransform>("ringSpin").BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-90, 270, TimeSpan.FromSeconds(7)) { RepeatBehavior = RepeatBehavior.Forever });
            // the multi-colour text gradients keep sliding
            F<TranslateTransform>("gradA").BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 0.5, TimeSpan.FromSeconds(2.4)) { RepeatBehavior = RepeatBehavior.Forever });
            F<TranslateTransform>("gradB").BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0.5, 0, TimeSpan.FromSeconds(3.2)) { RepeatBehavior = RepeatBehavior.Forever });
            F<TranslateTransform>("gradC").BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 0.5, TimeSpan.FromSeconds(3)) { RepeatBehavior = RepeatBehavior.Forever });
            Reveal(F<FrameworkElement>("wTitle"), 450, 28); Reveal(F<FrameworkElement>("wCredit"), 850, 24);
            Reveal(F<FrameworkElement>("wTag"), 1200, 14); Reveal(F<FrameworkElement>("wLoad"), 1400, 10);

            var bar = new DoubleAnimation(0, 100, TimeSpan.FromMilliseconds(3600)) { BeginTime = TimeSpan.FromMilliseconds(1500), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            bar.Completed += async (s, e) => { wHint.Visibility = Visibility.Visible; await Task.Delay(1100); DismissWelcome(); };
            wBar.BeginAnimation(ProgressBar.ValueProperty, bar);

            string[] steps = { "Initializing sentinel core…", "Mapping your drives…", "Calibrating the cleanup engine…", "Loading browser intelligence…", "Ready ✦" };
            await Task.Delay(1500);
            for (int i = 0; i < steps.Length && !welcomeDone; i++) { wStatus.Text = steps[i]; await Task.Delay(780); }
        }

        void DismissWelcome()
        {
            if (welcomeDone) return;
            welcomeDone = true;
            var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            a.Completed += (s, e) =>
            {
                welcome.Visibility = Visibility.Collapsed;
                wOrbs.Children.Clear();
                F<RotateTransform>("ringSpin").BeginAnimation(RotateTransform.AngleProperty, null);
                wBar.BeginAnimation(ProgressBar.ValueProperty, null);
            };
            welcome.BeginAnimation(UIElement.OpacityProperty, a);
        }

        // ───────── start page / welcome ─────────
        void NavStart()
        {
            switch (Settings.StartPage)
            {
                case "apps": Go(vApps); if (!appsLoaded) LoadApps(); break;
                case "clean": Go(vClean); EnterClean(); break;
                case "boost": Go(vBoost); break;
                case "health": Go(vHealth); break;
                case "errors": Go(vErrors); EnterErrors(); break;
                case "pc": Go(vPc); EnterPc(); break;
                default: Go(vDrives); break;
            }
        }

        void SkipWelcome()
        {
            welcomeDone = true;
            welcome.Visibility = Visibility.Collapsed;
            try { wOrbs.Children.Clear(); } catch { }
        }

        void AfterFirstRender()
        {
            if (StartHidden && tray != null) { SkipWelcome(); trayHinted = true; w.Hide(); w.ShowInTaskbar = true; w.WindowState = WindowState.Normal; }
            else if (Settings.ShowWelcome) StartWelcome();
            else SkipWelcome();
        }
    }
}
