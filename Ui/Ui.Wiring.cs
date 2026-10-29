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
        public bool StartHidden;
        FrameworkElement vErrors, vSettings;
        Button navErrors, navSettings, bLang, bFixAll, bErrScan, bErrExport, bErr1, bErr7, bErr30, bLangEn, bLangAr, bSetZoomIn, bSetZoomOut, bOpenData, bOpenLog, bResetSettings, bSetResetLayout;
        ItemsControl itemsErrors, itemsHung;
        TextBlock txtErrHead, txtErrEmpty, langTxt, errBadgeText, txtSetZoom, txtAbout;
        Border errBadge, hungPanel;
        CheckBox chkTray, chkWelcome, chkAnim, chkAlerts, chkAutoStart, chkStartTray;
        Panel startPages;
        const string DevEmail = "ahmadrqash@gmail.com", DevLinkedIn = "https://linkedin.com/in/ahmadrq";
        Button bMail, bLinkedIn;
        System.Windows.Shapes.Path errArc;
        TextBlock txtErrScore, txtErrGrade, txtErrVerdict;
        ItemsControl itemsErrTiles, itemsErrBars;
        DateTime lastHealth;
        TextBlock txtGrade, txtHealthCounts, txtHealthMeta;     // health panel: grade label, counters, "last check" line
        ItemsControl itemsCats;                                 // health panel: category bars
        List<Finding> healthList = new List<Finding>();
        List<ErrApp> errApps;
        int errDays = 7, newErrors;
        bool errScanning, settingsSync, walking, lastBalloonErr;
        DispatcherTimer hungTm;
        System.Diagnostics.Eventing.Reader.EventLogWatcher crashWatcher;
        System.Windows.Forms.ContextMenuStrip trayMenu;

        readonly List<string> wireErrors = new List<string>();

        /// <summary>Runs one setup step; if it fails the error is written to the log and shown once, and the rest of the app still starts.</summary>
        void Guard(string what, Action step)
        {
            try { step(); }
            catch (Exception ex) { Dbg("SETUP FAILED - " + what + ": " + ex); wireErrors.Add(what + ": " + ex.Message); }
        }

        void WireNew()
        {
            vErrors = F<FrameworkElement>("vErrors"); vSettings = F<FrameworkElement>("vSettings");
            navErrors = F<Button>("navErrors"); navSettings = F<Button>("navSettings"); bLang = F<Button>("bLang"); bFixAll = F<Button>("bFixAll");
            itemsErrors = F<ItemsControl>("itemsErrors"); itemsHung = F<ItemsControl>("itemsHung");
            txtErrHead = F<TextBlock>("txtErrHead"); txtErrEmpty = F<TextBlock>("txtErrEmpty"); langTxt = F<TextBlock>("langTxt");
            errBadge = F<Border>("errBadge"); errBadgeText = F<TextBlock>("errBadgeText"); hungPanel = F<Border>("hungPanel");
            bErrScan = F<Button>("bErrScan"); bErrExport = F<Button>("bErrExport"); bErr1 = F<Button>("bErr1"); bErr7 = F<Button>("bErr7"); bErr30 = F<Button>("bErr30");

            errArc = F<System.Windows.Shapes.Path>("errArc"); txtErrScore = F<TextBlock>("txtErrScore"); txtErrGrade = F<TextBlock>("txtErrGrade"); txtErrVerdict = F<TextBlock>("txtErrVerdict");
            itemsErrTiles = F<ItemsControl>("itemsErrTiles"); itemsErrBars = F<ItemsControl>("itemsErrBars");
            bMail = F<Button>("bMail"); bLinkedIn = F<Button>("bLinkedIn");
            bMail.Click += async (_, _) =>
            {
                int pick = await Ask("CONTACT THE DEVELOPER", DevEmail + "\n\nWhat would you like to do with this address?", "info", "SEND EMAIL", "COPY ADDRESS", "CANCEL");
                if (pick == 0) OpenUrl("mailto:" + DevEmail + "?subject=CyberDisk");
                else if (pick == 1) { try { Clipboard.SetText(DevEmail); txtStatus.Text = "EMAIL ADDRESS COPIED"; } catch { txtStatus.Text = "COULD NOT COPY"; } }
            };
            bLinkedIn.Click += (_, _) => OpenUrl(DevLinkedIn);
            txtGrade = F<TextBlock>("txtGrade"); txtHealthCounts = F<TextBlock>("txtHealthCounts"); txtHealthMeta = F<TextBlock>("txtHealthMeta"); itemsCats = F<ItemsControl>("itemsCats");
            Guard("disk health", WireDisks); Guard("my pc", WirePc); Guard("app details", WireAppDetails); Guard("health extras", WireHealthExtra);
            Guard("app errors", WireErrorsExtra); Guard("file preview", WireBrowserExtra); Guard("startup apps", WireStartupUx); Guard("screenshots", WireShots);
            WindowChrome.SetIsHitTestVisibleInChrome(bLang, true);
            bLang.Click += (_, _) => ApplyLang(!Loc.Ar, true);

            // navigation
            navErrors.Click += (_, _) => { Go(vErrors); EnterErrors(); };
            navSettings.Click += (_, _) => { Go(vSettings); SyncSettings(); };

            // health: FIX buttons
            bFixAll.Click += (_, _) => FixAll();
            itemsHealth.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                var b = e.OriginalSource as Button; var f = b == null ? null : b.DataContext as Finding;
                if (b == null || f == null) return;
                string tag = b.Tag as string;
                if (tag == "fix") RunFix(f); else if (tag == "ignore") ToggleIgnore(f);
            }));

            // app errors
            bErrScan.Click += (_, _) => ScanErrors();
            bErrExport.Click += (_, _) => ExportErrors();
            bErr1.Click += (_, _) => { errDays = 1; ScanErrors(); };
            bErr7.Click += (_, _) => { errDays = 7; ScanErrors(); };
            bErr30.Click += (_, _) => { errDays = 30; ScanErrors(); };
            itemsErrors.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                var b = e.OriginalSource as Button; var a = b == null ? null : b.DataContext as ErrApp;
                if (b != null && a != null) ErrClick(a, b.Tag as string);
            }));
            itemsHung.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) =>
            {
                var b = e.OriginalSource as Button; var h = b == null ? null : b.DataContext as HungVm;
                if (b != null && h != null) EndHung(h);
            }));
            hungTm = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            hungTm.Tick += (_, _) => RefreshHung();

            // settings
            bLangEn = F<Button>("bLangEn"); bLangAr = F<Button>("bLangAr");
            chkTray = F<CheckBox>("chkTray"); chkWelcome = F<CheckBox>("chkWelcome"); chkAnim = F<CheckBox>("chkAnim"); chkAlerts = F<CheckBox>("chkAlerts");
            chkAutoStart = F<CheckBox>("chkAutoStart"); chkStartTray = F<CheckBox>("chkStartTray");
            startPages = F<Panel>("startPages"); txtSetZoom = F<TextBlock>("txtSetZoom"); txtAbout = F<TextBlock>("txtAbout");
            bSetZoomIn = F<Button>("bSetZoomIn"); bSetZoomOut = F<Button>("bSetZoomOut"); bOpenData = F<Button>("bOpenData"); bOpenLog = F<Button>("bOpenLog");
            bResetSettings = F<Button>("bResetSettings"); bSetResetLayout = F<Button>("bSetResetLayout");

            bLangEn.Click += (_, _) => ApplyLang(false, true);
            bLangAr.Click += (_, _) => ApplyLang(true, true);
            bSetZoomIn.Click += (_, _) => { SetZoom(zoom + 0.1); SyncSettings(); };
            bSetZoomOut.Click += (_, _) => { SetZoom(zoom - 0.1); SyncSettings(); };
            chkTray.Click += (_, _) => { if (!settingsSync) SetTrayOnClose(chkTray.IsChecked == true); };
            chkWelcome.Click += (_, _) => { if (!settingsSync) Settings.ShowWelcome = chkWelcome.IsChecked == true; };
            chkAnim.Click += (_, _) => { if (!settingsSync) Settings.Animations = chkAnim.IsChecked == true; };
            chkAlerts.Click += (_, _) => { if (!settingsSync) Settings.CrashAlerts = chkAlerts.IsChecked == true; };
            chkStartTray.Click += (_, _) => { if (!settingsSync) { Settings.StartTray = chkStartTray.IsChecked == true; if (chkAutoStart.IsChecked == true) ToggleAutoStart(true); } };
            chkAutoStart.Click += (_, _) => { if (!settingsSync) ToggleAutoStart(chkAutoStart.IsChecked == true); };
            foreach (var b in startPages.Children.OfType<Button>())
            {
                var bb = b;
                bb.Click += (_, _) => { Settings.StartPage = (string)bb.Tag; SyncSettings(); };
            }
            bOpenData.Click += (_, _) => { try { Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", "\"" + Settings.Dir + "\""); } catch { } };
            bOpenLog.Click += (_, _) => { try { if (File.Exists(Fixer.LogPath)) Process.Start("explorer.exe", "/select,\"" + Fixer.LogPath + "\""); else Msg("NO LOG YET", "The fix log is created the first time a health fix runs.", "info"); } catch { } };
            bSetResetLayout.Click += (_, _) => { ResetLayout(); SyncSettings(); };
            bResetSettings.Click += (_, _) => ResetSettings();

            // translate everything that appears later (new rows, dialogs, status text)
            w.LayoutUpdated += (_, _) =>
            {
                if (!Loc.Ar || walking) return;
                walking = true;
                try { Loc.Walk(w, false); } finally { walking = false; }
            };
            EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((s, e) => Loc.Menu((ContextMenu)s)));

            // crash watcher: knows the moment any program crashes or freezes
            crashWatcher = AppErr.Watch(c => w.Dispatcher.BeginInvoke(new Action(() => OnCrash(c))));
            if (tray != null) tray.BalloonTipClicked += (s, e) => { if (lastBalloonErr) { ShowFromTray(); Go(vErrors); EnterErrors(); } };
            w.Closed += (_, _) => { try { if (crashWatcher != null) { crashWatcher.Enabled = false; crashWatcher.Dispose(); } } catch { } };
        }
    }
}
