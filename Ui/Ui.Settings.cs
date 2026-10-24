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
        // ───────── settings ─────────
        void SetTrayOnClose(bool on)
        {
            trayOnClose = on; Settings.TrayOnClose = on;
            if (trayChk != null && trayChk.Checked != on) trayChk.Checked = on;
            SyncSettings();
        }

        void SyncSettings()
        {
            if (chkTray == null) return;
            settingsSync = true;
            try
            {
                chkTray.IsChecked = Settings.TrayOnClose; chkWelcome.IsChecked = Settings.ShowWelcome; chkAnim.IsChecked = Settings.Animations;
                chkAlerts.IsChecked = Settings.CrashAlerts; chkStartTray.IsChecked = Settings.StartTray;
                bLangEn.Background = Br(!Loc.Ar ? "#333DD6F5" : "#111E31"); bLangAr.Background = Br(Loc.Ar ? "#333DD6F5" : "#111E31");
                foreach (var b in startPages.Children.OfType<Button>()) b.Background = Br((string)b.Tag == Settings.StartPage ? "#333DD6F5" : "#111E31");
                txtSetZoom.Text = ((int)Math.Round(zoom * 100)) + "%";
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                txtAbout.Text = "Version " + (ver == null ? "" : ver.ToString(3)) + "\nDisk analyzer, cleaner, app uninstaller and PC booster\nSettings folder: " + Settings.Dir;
            }
            finally { settingsSync = false; }
            RefreshAutoStart();
        }

        async void RefreshAutoStart()
        {
            try
            {
                bool on = await Task.Run(() => { string o; return Fixer.Exec("schtasks.exe", "/Query /TN \"CyberDisk\"", 15000, out o) == 0; });
                settingsSync = true; chkAutoStart.IsChecked = on; settingsSync = false;
            }
            catch { settingsSync = false; }
        }

        async void ToggleAutoStart(bool on)
        {
            chkAutoStart.IsEnabled = false;
            bool tray_ = Settings.StartTray;
            string err = await Task.Run(() =>
            {
                string o; int c;
                if (!on) { c = Fixer.Exec("schtasks.exe", "/Delete /TN \"CyberDisk\" /F", 20000, out o); return c == 0 ? null : o; }
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                string tr = "\\\"" + exe + "\\\"" + (tray_ ? " --tray" : "");
                c = Fixer.Exec("schtasks.exe", "/Create /TN \"CyberDisk\" /TR \"" + tr + "\" /SC ONLOGON /RL HIGHEST /DELAY 0000:15 /F", 20000, out o);
                return c == 0 ? null : o;
            });
            chkAutoStart.IsEnabled = true;
            if (err != null) { await Msg("COULD NOT CHANGE STARTUP", err.Trim(), "danger"); RefreshAutoStart(); return; }
            txtStatus.Text = on ? "CYBERDISK WILL START WITH WINDOWS" : "CYBERDISK NO LONGER STARTS WITH WINDOWS";
        }

        async void ResetSettings()
        {
            if (!(await Confirm("RESET ALL SETTINGS", "Language, startup options and notifications go back to their defaults, and the 'reviewed' health events are forgotten.\n\nThe start-with-Windows task is removed.", "RESET", "CANCEL", "warn"))) return;
            Settings.Reset();
            try { File.Delete(Path.Combine(Settings.Dir, "reviewed.ini")); } catch { }
            Health.Reviewed.Clear();
            trayOnClose = true;
            await Task.Run(() => { string o; Fixer.Exec("schtasks.exe", "/Delete /TN \"CyberDisk\" /F", 20000, out o); });
            ApplyLang(false, false);
            SyncSettings();
            txtStatus.Text = "SETTINGS RESET";
        }
    }
}
