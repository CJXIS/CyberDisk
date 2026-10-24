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
            var app = new Application();
            var ui = new Ui { StartHidden = args != null && args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase)) };
            app.DispatcherUnhandledException += (s, e) => { e.Handled = true; Ui.Dbg("UNHANDLED: " + e.Exception); ui.Fail(e.Exception.Message); };
            TaskScheduler.UnobservedTaskException += (s, e) => { Ui.Dbg("UNOBSERVED: " + e.Exception); e.SetObserved(); };
            app.Run(ui.Build());
            GC.KeepAlive(instanceLock);
        }
    }
}
