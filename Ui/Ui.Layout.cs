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
        // ── extra wiring: welcome page, layout, zoom, full screen, cleanup lab ──
        void WireExtra()
        {
            WireDialogs();
            vClean = F<FrameworkElement>("vClean"); mascot = F<FrameworkElement>("mascot"); cleanList = F<FrameworkElement>("cleanList");
            fx = F<Canvas>("fx"); itemsClean = F<ItemsControl>("itemsClean"); itemsBrowsers = F<ItemsControl>("itemsBrowsers");
            svJunk = F<FrameworkElement>("svJunk"); svBrowsers = F<FrameworkElement>("svBrowsers");
            txtReclaim = F<TextBlock>("txtReclaim"); txtCleanStatus = F<TextBlock>("txtCleanStatus"); txtCleanPct = F<TextBlock>("txtCleanPct"); txtFreed = F<TextBlock>("txtFreed");
            txtZoom = F<TextBlock>("txtZoom");
            cleanBar = F<ProgressBar>("cleanBar");
            bAnalyze = F<Button>("bAnalyze"); bClean = F<Button>("bClean"); navClean = F<Button>("navClean"); bFull = F<Button>("bFull"); bMax = F<Button>("bMax");
            bTabJunk = F<Button>("bTabJunk"); bTabBrowsers = F<Button>("bTabBrowsers");
            bZoomIn = F<Button>("bZoomIn"); bZoomOut = F<Button>("bZoomOut"); bResetLayout = F<Button>("bResetLayout");
            chkLeft = F<CheckBox>("chkLeft");
            icoFull = F<System.Windows.Shapes.Path>("icoFull");
            mScale = F<ScaleTransform>("mScale"); mMove = F<TranslateTransform>("mMove"); lidRot = F<RotateTransform>("lidRot"); eyeScale = F<ScaleTransform>("eyeScale");
            bodyGrid = F<FrameworkElement>("bodyGrid"); navPanel = F<Panel>("navPanel");
            colBrowsePane = F<ColumnDefinition>("colBrowsePane"); colCleanPane = F<ColumnDefinition>("colCleanPane"); colAppsPane = F<ColumnDefinition>("colAppsPane");
            welcome = F<Grid>("welcome"); wOrbs = F<Canvas>("wOrbs"); wBar = F<ProgressBar>("wBar"); wStatus = F<TextBlock>("wStatus"); wHint = F<TextBlock>("wHint");

            var ic = ShellIcons.FromFile(System.Reflection.Assembly.GetExecutingAssembly().Location, 0);   // icon embedded in the exe
            if (ic != null) { F<Image>("logo").Source = ic; w.Icon = ic; }

            WindowChrome.SetIsHitTestVisibleInChrome(bFull, true);
            WindowChrome.SetIsHitTestVisibleInChrome(bMax, true);
            bFull.Click += (_, _) => ToggleFull();
            bMax.Click += (_, _) =>
            {
                if (full) ToggleFull();
                w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            };

            // zoom: the whole body (menus, drives, panels) scales
            bodyGrid.LayoutTransform = zoomT; bodyGrid.ClipToBounds = true;
            bZoomIn.Click += (_, _) => SetZoom(zoom + 0.1);
            bZoomOut.Click += (_, _) => SetZoom(zoom - 0.1);
            bResetLayout.Click += (_, _) => ResetLayout();
            w.PreviewMouseWheel += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) { SetZoom(zoom + (e.Delta > 0 ? 0.1 : -0.1)); e.Handled = true; }
            };
            w.PreviewKeyDown += (_, e) =>
            {
                if (DlgKey(e) || OpKey(e)) { e.Handled = true; return; }
                if (!welcomeDone && e.Key != Key.F11) { DismissWelcome(); e.Handled = true; return; }
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
                if (e.Key == Key.F11) { ToggleFull(); e.Handled = true; }
                else if (e.Key == Key.Escape && full) { ToggleFull(); e.Handled = true; }
                else if (ctrl && (e.Key == Key.OemPlus || e.Key == Key.Add)) { SetZoom(zoom + 0.1); e.Handled = true; }
                else if (ctrl && (e.Key == Key.OemMinus || e.Key == Key.Subtract)) { SetZoom(zoom - 0.1); e.Handled = true; }
                else if (ctrl && (e.Key == Key.D0 || e.Key == Key.NumPad0)) { SetZoom(1.0); e.Handled = true; }
            };
            w.Closing += (_, _) => SaveLayout();

            // resizable panes
            foreach (var n in new[] { "spBrowse", "spClean", "spApps" })
                F<GridSplitter>(n).DragCompleted += (_, _) => SaveLayout();

            // drag & drop: drive cards, menu buttons, cleanup rows, browser cards
            EnableReorder(itemsDrives, () => drives, null, () => { itemsDrives.ItemsSource = null; itemsDrives.ItemsSource = drives; SaveLayout(); });
            EnableReorder(itemsBrowsers, () => browsers, null, () => { itemsBrowsers.ItemsSource = null; itemsBrowsers.ItemsSource = browsers; });
            EnableReorder(itemsClean, () => clean, (a, b) => ((CleanItem)a).Group == ((CleanItem)b).Group, () => { if (junkView != null) junkView.Refresh(); });
            EnableReorderPanel(navPanel, SaveLayout);

            // cleanup lab
            navClean.Click += (_, _) => { Go(vClean); EnterClean(); };
            bTabJunk.Click += (_, _) => CleanTab(false);
            bTabBrowsers.Click += (_, _) => CleanTab(true);
            bAnalyze.Click += (_, _) => Analyze();
            bClean.Click += (_, _) => DoClean();
            itemsClean.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => OnCleanClick(e.OriginalSource)));
            itemsBrowsers.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => UpdateReclaim()));
            cleanTm.Tick += (_, _) => CleanTick();
            blinkTm.Tick += (_, _) => eyeScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, 0.08, TimeSpan.FromMilliseconds(90)) { AutoReverse = true });

            WireBoost(); WireFiles(); WireMisc(); WireNew();
            cardStyle = (Style)w.FindResource("Card");
            StartShimmer(wBar); StartShimmer(cleanBar);
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent, new MouseEventHandler((s, e) => CardHover(s as Button, true)));
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseLeaveEvent, new MouseEventHandler((s, e) => CardHover(s as Button, false)));
            ApplyLayout();
            welcome.MouseLeftButtonDown += (_, _) => DismissWelcome();
            w.ContentRendered += (_, _) => AfterFirstRender();
        }

        // ── zoom + saved layout ──
        void SetZoom(double z, bool save = true)
        {
            z = Math.Max(0.6, Math.Min(2.0, Math.Round(z * 20) / 20));
            zoom = z;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            zoomT.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(z, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            zoomT.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(z, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            txtZoom.Text = ((int)Math.Round(z * 100)) + "%";
            if (save) SaveLayout();
        }

        void ReorderNav(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                var el = navPanel.Children.OfType<FrameworkElement>().FirstOrDefault(x => x.Name == id);
                if (el != null) { navPanel.Children.Remove(el); navPanel.Children.Add(el); }
            }
            var known = new HashSet<string>(ids);                                   // tabs added later (not in an old saved order) go to the end
            foreach (var el in navPanel.Children.OfType<FrameworkElement>().Where(x => !known.Contains(x.Name)).ToList()) { navPanel.Children.Remove(el); navPanel.Children.Add(el); }
        }

        void ApplyLayout()
        {
            if (colBrowsePane == null || colCleanPane == null || colAppsPane == null) return;
            string v;
            double z;
            if (cfg.TryGetValue("zoom", out v) && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z)) SetZoom(z, false);
            if (cfg.TryGetValue("nav", out v) && !string.IsNullOrEmpty(v)) ReorderNav(v.Split(','));
            Action<string, ColumnDefinition> pane = (key, col) =>
            {
                string s; double d;
                if (cfg.TryGetValue(key, out s) && double.TryParse(s, out d) && d >= col.MinWidth && d <= col.MaxWidth) col.Width = new GridLength(d);
            };
            pane("wBrowse", colBrowsePane); pane("wClean", colCleanPane); pane("wApps", colAppsPane);
        }

        void SaveLayout()
        {
            if (navPanel == null || colBrowsePane == null || colCleanPane == null || colAppsPane == null) return;
            cfg["zoom"] = zoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            cfg["nav"] = string.Join(",", navPanel.Children.OfType<FrameworkElement>().Select(x => x.Name));
            cfg["drives"] = string.Join(",", drives.Select(d => d.Letter));
            if (colBrowsePane.ActualWidth > 100) cfg["wBrowse"] = ((int)colBrowsePane.ActualWidth).ToString();
            if (colCleanPane.ActualWidth > 100) cfg["wClean"] = ((int)colCleanPane.ActualWidth).ToString();
            if (colAppsPane.ActualWidth > 100) cfg["wApps"] = ((int)colAppsPane.ActualWidth).ToString();
            Cfg.Save(cfg);
        }

        void ResetLayout()
        {
            Cfg.Reset(); cfg.Clear();
            SetZoom(1.0, false);
            ReorderNav(new[] { "navDrives", "navApps", "navClean", "navBoost", "navHealth", "navErrors", "navPc", "navSettings" });
            colBrowsePane.Width = new GridLength(350); colCleanPane.Width = new GridLength(370); colAppsPane.Width = new GridLength(320);
            drives = drives.OrderBy(d => d.Letter).ToList();
            itemsDrives.ItemsSource = null; itemsDrives.ItemsSource = drives;
            txtStatus.Text = "LAYOUT RESET";
        }

        List<DriveVm> OrderDrives(List<DriveVm> l)
        {
            string saved;
            if (!cfg.TryGetValue("drives", out saved) || string.IsNullOrEmpty(saved)) return l;
            var order = saved.Split(',').ToList();
            return l.OrderBy(d => { int i = order.IndexOf(d.Letter); return i < 0 ? 999 : i; }).ToList();
        }
    }
}
