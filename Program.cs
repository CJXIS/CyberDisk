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
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show("CYBERDISK must be run as Administrator.", "ACCESS DENIED", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }
            bool firstInstance;
            var instanceLock = new Mutex(true, "CyberDisk.Ahmad.SingleInstance", out firstInstance);
            if (!firstInstance) { try { EventWaitHandle.OpenExisting("CyberDisk.ShowWindow").Set(); } catch { } return; }
            ThreadPool.SetMinThreads(64, 64);
            try { Directory.CreateDirectory(Settings.Dir); System.Runtime.ProfileOptimization.SetProfileRoot(Settings.Dir); System.Runtime.ProfileOptimization.StartProfile("startup.prof"); } catch { }   // remembers what to pre-compile: the next start is faster
            var app = new Application();
            var ui = new Ui { StartHidden = args != null && args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)) };
            app.DispatcherUnhandledException += (s, e) => { e.Handled = true; Ui.Dbg("UNHANDLED: " + e.Exception); ui.Fail(e.Exception.Message); };
            TaskScheduler.UnobservedTaskException += (s, e) => { Ui.Dbg("UNOBSERVED: " + e.Exception); e.SetObserved(); };
            try { app.Run(ui.Build()); }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "CyberDisk-crash.log"), DateTime.Now + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine); } catch { }
                MessageBox.Show("CyberDisk could not start.\n\n" + ex.Message + (ex.InnerException != null ? "\n" + ex.InnerException.Message : "") + "\n\nFull details: " + Path.Combine(Path.GetTempPath(), "CyberDisk-crash.log"), "CYBERDISK", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            GC.KeepAlive(instanceLock);
        }
    }
}
