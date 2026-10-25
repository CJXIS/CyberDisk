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
        Window w;
        FrameworkElement vDrives, vBrowse, vApps, pvBody, apBody;
        ItemsControl itemsDrives;
        DataGrid gs, ga;
        TextBlock txtCrumb, txtInfo, txtStatus, txtCount, pvName, pvKind, pvBig, pvListHdr, pvTypesHdr, pvEmpty, apName, apPub, apEmpty;
        Image pvIcon, pvImg, apIcon;
        ItemsControl pvList;
        Button bBack, bUp, bRescan, bOpen, bRefDrives, navDrives, navApps, bUn, bRef, bStopAll, fAll, fApp, fGame, fStore;
        TextBox txtSearch;
        FrameworkElement vClean, mascot, cleanList;
        Canvas fx;
        ItemsControl itemsClean;
        TextBlock txtReclaim, txtCleanStatus, txtCleanPct, txtFreed;
        ProgressBar cleanBar;
        Button bAnalyze, bClean, navClean, bFull, bMax;
        System.Windows.Shapes.Path icoFull;
        ScaleTransform mScale, eyeScale; TranslateTransform mMove; RotateTransform lidRot;
        List<CleanItem> clean = new List<CleanItem>();
        bool cleaning, analyzing, analyzed, cleanInit, full;
        long doneBytes, cleanTotal, cuFrom, realFreed;      // doneBytes = progress estimate (bar); realFreed = bytes REALLY deleted so far (text)
        double shown;
        Win.RECT savedRect;
        Random rnd = new Random();
        DispatcherTimer cleanTm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        DispatcherTimer blinkTm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
        DispatcherTimer cuTm;
        FrameworkElement bodyGrid, svJunk, svBrowsers;
        Panel navPanel;
        Canvas wOrbs;
        Grid welcome;
        ProgressBar wBar;
        TextBlock wStatus, wHint, txtZoom;
        ColumnDefinition colBrowsePane, colCleanPane, colAppsPane;
        Button bTabJunk, bTabBrowsers, bZoomIn, bZoomOut, bResetLayout;
        CheckBox chkLeft;
        ItemsControl itemsBrowsers;
        ScaleTransform zoomT = new ScaleTransform(1, 1);
        double zoom = 1.0;
        Dictionary<string, string> cfg = Cfg.Load();
        List<BrowserVm> browsers = new List<BrowserVm>();
        List<CleanItem> allItems = new List<CleanItem>();
        System.Windows.Data.ListCollectionView junkView;
        bool welcomeDone;
        object dragging;
        long creepBudget, creepGiven;

        List<DriveVm> drives = new List<DriveVm>();
        List<DriveVm> scanning = new List<DriveVm>();
        DriveVm curDrive, openAfter;
        Node cur;
        int navId, pvId, iconGen;
        List<AppItem> apps = new List<AppItem>();
        string kindFilter;
        bool appsLoaded;
        DispatcherTimer tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };

        public static string Fmt(long b)
        {
            if (b <= 0) return "—";
            string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < 5) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : v >= 100 ? "0" : "0.#") + " " + u[i];
        }
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Color> colCache = new System.Collections.Concurrent.ConcurrentDictionary<string, Color>();
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SolidColorBrush> brCache = new System.Collections.Concurrent.ConcurrentDictionary<string, SolidColorBrush>();
        /// <summary>Colour from "#RRGGBB" text - parsed once, then served from a cache (this is called thousands of times).</summary>
        public static Color Col(string c) => colCache.GetOrAdd(c, k => (Color)ColorConverter.ConvertFromString(k));
        /// <summary>Shared frozen brush: frozen brushes are cheaper to render and safe to use from any thread.</summary>
        static SolidColorBrush Br(string c) => brCache.GetOrAdd(c, k => { var b = new SolidColorBrush(Col(k)); b.Freeze(); return b; });
        static double Pct(long a, long b) => b > 0 ? 100.0 * a / b : 0;
        T F<T>(string n) where T : class => (T)w.FindName(n);

        static void Anim(UIElement e)
        {
            if (!Settings.Animations) return;
            e.Opacity = 0;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.CacheMode = new BitmapCache();                                         // draw the page once, then only move the picture: much smoother
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
            fade.Completed += (_, _) => { e.CacheMode = null; };                      // back to sharp live text when the motion has ended
            e.BeginAnimation(UIElement.OpacityProperty, fade);
            var tt = new TranslateTransform(0, 14);
            e.RenderTransform = tt;
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        }

        public Window Build()
        {
            Settings.Load(cfg); Health.LoadReviewed(); Health.LoadIgnored(); AppErr.LoadDismissed(); Loc.Ar = Settings.Lang == "ar";
            w = (Window)XamlReader.Parse(Xaml);
            vDrives = F<FrameworkElement>("vDrives"); vBrowse = F<FrameworkElement>("vBrowse"); vApps = F<FrameworkElement>("vApps");
            pvBody = F<FrameworkElement>("pvBody"); apBody = F<FrameworkElement>("apBody");
            itemsDrives = F<ItemsControl>("itemsDrives"); gs = F<DataGrid>("gs"); ga = F<DataGrid>("ga");
            txtCrumb = F<TextBlock>("txtCrumb"); txtInfo = F<TextBlock>("txtInfo"); txtStatus = F<TextBlock>("txtStatus"); txtCount = F<TextBlock>("txtCount");
            pvName = F<TextBlock>("pvName"); pvKind = F<TextBlock>("pvKind"); pvBig = F<TextBlock>("pvBig"); pvTypesHdr = F<TextBlock>("pvTypesHdr");
            pvListHdr = F<TextBlock>("pvListHdr"); pvEmpty = F<TextBlock>("pvEmpty");
            apName = F<TextBlock>("apName"); apPub = F<TextBlock>("apPub"); apEmpty = F<TextBlock>("apEmpty");
            pvIcon = F<Image>("pvIcon"); pvImg = F<Image>("pvImg"); apIcon = F<Image>("apIcon");
            pvList = F<ItemsControl>("pvList"); txtSearch = F<TextBox>("txtSearch");
            bBack = F<Button>("bBack"); bUp = F<Button>("bUp"); bRescan = F<Button>("bRescan"); bOpen = F<Button>("bOpen"); bRefDrives = F<Button>("bRefDrives");
            navDrives = F<Button>("navDrives"); navApps = F<Button>("navApps"); bUn = F<Button>("bUn"); bRef = F<Button>("bRef"); bStopAll = F<Button>("bStopAll");
            fAll = F<Button>("fAll"); fApp = F<Button>("fApp"); fGame = F<Button>("fGame"); fStore = F<Button>("fStore");

            WindowChrome.SetWindowChrome(w, new WindowChrome
            {
                CaptionHeight = 44, ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false
            });
            var bMin = F<Button>("bMin"); var bClose = F<Button>("bClose");
            WindowChrome.SetIsHitTestVisibleInChrome(bMin, true);
            WindowChrome.SetIsHitTestVisibleInChrome(bClose, true);
            bMin.Click += (_, _) => w.WindowState = WindowState.Minimized;
            bClose.Click += (_, _) => w.Close();
            w.StateChanged += (_, _) => F<Grid>("rootGrid").Margin = w.WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
            w.Closing += (_, e) => { if (!exiting && trayOnClose && tray != null) return; foreach (var d in scanning) d.Cts?.Cancel(); };

            navDrives.Click += (_, _) => Go(vDrives);
            navApps.Click += (_, _) => { Go(vApps); if (!appsLoaded) LoadApps(); };
            itemsDrives.AddHandler(Button.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                var src = e.OriginalSource as FrameworkElement;
                if (src?.DataContext is DriveVm d)
                {
                    if (src is Button bt && (bt.Tag as string) == "disk") { e.Handled = true; ShowDisks(d.Letter); }   // the small DISK HEALTH button
                    else OnDrive(d);
                }
            }));
            bRefDrives.Click += (_, _) => LoadDrives();
            bStopAll.Click += (_, _) => { foreach (var d in scanning.ToList()) d.Cts.Cancel(); };

            bBack.Click += (_, _) => Go(vDrives);
            bUp.Click += (_, _) => Up();
            bRescan.Click += (_, _) => { if (curDrive != null) { curDrive.Scanned = false; Go(vDrives); StartScan(curDrive); } };
            bOpen.Click += (_, _) => OpenExplorer();
            gs.MouseDoubleClick += (_, _) => EnterSelected();
            gs.KeyDown += (_, e) => { if (e.Key == Key.Enter) EnterSelected(); else if (e.Key == Key.Back) Up(); };
            gs.SelectionChanged += (_, _) => Preview();

            bRef.Click += (_, _) => LoadApps();
            bUn.Click += (_, _) => DoUninstall();
            txtSearch.TextChanged += (_, _) => ApplyFilter();
            fAll.Click += (_, _) => Chip(null); fApp.Click += (_, _) => Chip("APP");
            fGame.Click += (_, _) => Chip("GAME"); fStore.Click += (_, _) => Chip("STORE");
            ga.SelectionChanged += (_, _) => ShowApp();

            tm.Tick += (_, _) =>
            {
                foreach (var d in scanning.ToList())
                    d.Hint = "SCANNING  " + Interlocked.Read(ref d.Sc.Files).ToString("N0") + " files · " + Fmt(Interlocked.Read(ref d.Sc.Bytes));
            };

            Chip(null);
            w.ContentRendered += (_, _) =>
            {
                LoadDrives(); NavStart(); Guard("background loading", StartPrewarm);
                if (wireErrors.Count > 0) Fail("Some features could not start and were switched off:\n" + string.Join("\n", wireErrors) + "\n\nDetails: " + Path.Combine(Path.GetTempPath(), "CyberDisk-uninstall.log"));
            };   // drives are only LISTED here - nothing is scanned
            WireExtra();
            ApplyLang(Loc.Ar, false);
            if (StartHidden && tray != null) { w.ShowInTaskbar = false; w.WindowState = WindowState.Minimized; }
            return w;
        }
    }
}
