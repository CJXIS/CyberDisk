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
        // ── in-app dialogs: themed overlays instead of Windows message boxes ──
        Grid dlgHost, reviewHost;
        Border dlgCard, reviewCard, reviewHolder, dlgIconBg;
        TextBlock dlgGlyph, dlgTitleT, dlgMsg;

        Canvas dragLayer;
        Action<int> dlgPick; int dlgLast; Action reviewEsc;
        readonly SemaphoreSlim dlgGate = new SemaphoreSlim(1, 1);
        DateTime lastUnEnd = DateTime.MinValue; int dlgGen; string lastFail; DateTime lastFailAt; bool uninstalling; Style cardStyle;
        DispatcherTimer dlgHideTimer;                    // the ONE pending 'collapse the dialog overlay' timer


        // diagnostics: %TEMP%\CyberDisk-uninstall.log  (only the build that contains this method writes it)
        public static void Dbg(string m)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "CyberDisk-uninstall.log"), DateTime.Now.ToString("HH:mm:ss.fff") + "  " + m + Environment.NewLine); } catch { }
        }

        // ShellExecute (UseShellExecute=true) must be called from an STA thread, not from a thread-pool (MTA) thread
        static Task RunSta(Action act)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var th = new Thread(() => { try { act(); tcs.SetResult(true); } catch (Exception ex) { tcs.SetException(ex); } });
            th.SetApartmentState(ApartmentState.STA); th.IsBackground = true; th.Start();
            return tcs.Task;
        }

        public void Fail(string message)
        {
            Dbg("Fail: " + message);
            if (message == lastFail && (DateTime.UtcNow - lastFailAt).TotalSeconds < 6) return;       // never spam the same error
            lastFail = message; lastFailAt = DateTime.UtcNow;
            var t = Msg("SOMETHING WENT WRONG", message, "danger");
        }

        Task Msg(string title, string msg, string kind = "info") { return Ask(title, msg, kind, "OK"); }

        async Task<bool> Confirm(string title, string msg, string yes, string no, string kind = "question")
        {
            return (await Ask(title, msg, kind, yes, no)) == 0;
        }

        // buttons[0] is the primary action (Enter), the last button is the cancel action (Esc); returns the clicked index
        bool DlgKey(KeyEventArgs e)
        {
            if (dlgPick == null && dlgHost != null && dlgHost.Visibility == Visibility.Visible && e.Key == Key.Escape) { dlgHost.Visibility = Visibility.Collapsed; return true; }
            if (dlgPick != null)
            {
                if (e.Key == Key.Escape) { dlgPick(dlgLast); return true; }
                if (e.Key == Key.Enter) { dlgPick(0); return true; }
                return e.Key != Key.F11;
            }
            if (reviewEsc != null && e.Key == Key.Escape) { reviewEsc(); return true; }
            return false;
        }

        void ShowReview()
        {
            reviewHost.Visibility = Visibility.Visible;
            reviewHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            var st = new ScaleTransform(0.94, 0.94); reviewCard.RenderTransform = st;
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });
        }

        void HideReview()
        {
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
            reviewHost.BeginAnimation(UIElement.OpacityProperty, fade);
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            t.Tick += (s, e) => { t.Stop(); reviewHost.BeginAnimation(UIElement.OpacityProperty, null); reviewHost.Opacity = 1; reviewHost.Visibility = Visibility.Collapsed; reviewHolder.Child = null; };
            t.Start();
        }

        async Task ReviewLeftovers(AppItem a, HashSet<string> stems)
        {
            txtStatus.Text = "SCANNING FOR LEFTOVERS…";
            List<Leftover> found;
            try { found = await Task.Run(() => Leftovers.Find(a, stems)); } catch { found = new List<Leftover>(); }
            if (found.Count == 0) { txtStatus.Text = "NO LEFTOVERS · " + a.Name + " is completely gone"; return; }
            txtStatus.Text = "READY";

            var root = (FrameworkElement)XamlReader.Parse(DlgXaml);
            var list = (ItemsControl)root.FindName("dlgList"); var sub = (TextBlock)root.FindName("dlgSub"); var sum = (TextBlock)root.FindName("dlgSum");
            var bScan = (Button)root.FindName("dlgScan"); var bSkip = (Button)root.FindName("dlgSkip"); var bDel = (Button)root.FindName("dlgDelete");
            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action close = () => { reviewEsc = null; HideReview(); done.TrySetResult(true); };

            Action total = () =>
            {
                var ch = found.Where(x => x.Checked).ToList();
                sum.Text = ch.Count + " selected  ·  " + Fmt(ch.Sum(x => x.Size));
            };
            Action fill = () =>
            {
                sub.Text = a.Name + "  ·  " + found.Count + " leftover item(s) - untick anything you want to keep";
                list.ItemsSource = null; list.ItemsSource = found;
                total();
            };
            fill();
            list.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((s, e) => total()));
            bSkip.Click += (s, e) => close();
            bScan.Click += async (s, e) =>
            {
                bScan.IsEnabled = false; sum.Text = "Scanning…";
                try { found = await Task.Run(() => Leftovers.Find(a, stems)); } catch { found = new List<Leftover>(); }
                bScan.IsEnabled = true;
                if (found.Count == 0) { close(); return; }
                fill();
            };
            bDel.Click += async (s, e) =>
            {
                var chosen = found.Where(x => x.Checked).ToList();
                if (chosen.Count == 0) { close(); return; }
                bDel.IsEnabled = false; bSkip.IsEnabled = false; bScan.IsEnabled = false; sum.Text = "Deleting…";
                long freed = chosen.Sum(x => x.Size);
                int fail = 0;
                await Task.Run(() => { foreach (var l in chosen) { try { l.Remove(); } catch { fail++; } } });
                txtStatus.Text = "LEFTOVERS REMOVED · " + chosen.Count + " item(s) · " + Fmt(freed) + " freed" + (fail > 0 ? " · " + fail + " could not be removed" : "");
                close();
                LoadDrives();
            };
            reviewHolder.Child = root;
            reviewEsc = close;
            ShowReview();
            await done.Task;
        }

        const string DlgXaml = """
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Grid Margin="22">
  <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
  <StackPanel x:Name="dlgHead" Background="Transparent" Margin="0,0,0,14">
   <TextBlock Text="LEFTOVERS FOUND" FontSize="20" FontWeight="Bold" Foreground="#3DDC97"/>
   <TextBlock x:Name="dlgSub" Foreground="#8195B0" Margin="0,2,0,0" TextWrapping="Wrap"/>
  </StackPanel>
  <Border Grid.Row="1" Style="{DynamicResource Panel}" Padding="10">
   <ScrollViewer VerticalScrollBarVisibility="Auto">
    <ItemsControl x:Name="dlgList"><ItemsControl.ItemTemplate><DataTemplate>
     <Border CornerRadius="10" Background="#0B1322" BorderBrush="#21304A" BorderThickness="1" Padding="12,9" Margin="0,0,6,6">
      <Grid>
       <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
       <CheckBox IsChecked="{Binding Checked, Mode=TwoWay}" VerticalAlignment="Center" Margin="0,0,12,0"/>
       <Border Grid.Column="1" CornerRadius="6" Padding="7,2" Background="{Binding KindBrush}" VerticalAlignment="Center" Margin="0,0,12,0">
        <TextBlock Text="{Binding Kind}" FontSize="10" FontWeight="Bold" Foreground="#06111D"/></Border>
       <TextBlock Grid.Column="2" Text="{Binding Shown}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" ToolTip="{Binding Shown}"/>
       <TextBlock Grid.Column="3" Text="{Binding SizeText}" FontWeight="SemiBold" Margin="12,0,0,0" VerticalAlignment="Center"/>
      </Grid>
     </Border>
    </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
   </ScrollViewer>
  </Border>
  <DockPanel Grid.Row="2" Margin="0,14,0,0">
   <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
    <Button x:Name="dlgScan" Content="↻ RESCAN" Style="{DynamicResource Neon}" Padding="14,9"/>
    <Button x:Name="dlgSkip" Content="SKIP" Style="{DynamicResource Neon}" Padding="16,9"/>
    <Button x:Name="dlgDelete" Content="DELETE SELECTED" Style="{DynamicResource Primary}" Padding="18,9" FontSize="13"/>
   </StackPanel>
   <TextBlock x:Name="dlgSum" Foreground="#C7D6EE" VerticalAlignment="Center"/>
  </DockPanel>
 </Grid>
</Grid>
""";

        // ── dialogs: every button exists in XAML, nothing is created at run time ──
        Border dlgGlow; FrameworkElement dlgWrap; Button dlgBtnA, dlgBtnB, dlgBtnC;

        void WireDialogs()
        {
            dlgHost = F<Grid>("dlgHost"); reviewHost = F<Grid>("reviewHost");
            dlgCard = F<Border>("dlgCard"); dlgGlow = F<Border>("dlgGlow"); dlgWrap = F<FrameworkElement>("dlgWrap");
            reviewCard = F<Border>("reviewCard"); reviewHolder = F<Border>("reviewHolder"); dlgIconBg = F<Border>("dlgIconBg");
            dlgGlyph = F<TextBlock>("dlgGlyph"); dlgTitleT = F<TextBlock>("dlgTitleT"); dlgMsg = F<TextBlock>("dlgMsg");
            dragLayer = F<Canvas>("dragLayer");
            dlgBtnA = F<Button>("dlgBtnA"); dlgBtnB = F<Button>("dlgBtnB"); dlgBtnC = F<Button>("dlgBtnC");
            foreach (var b0 in new[] { dlgBtnA, dlgBtnB, dlgBtnC })
            {
                var b = b0;
                b.Click += (s, e) => { var p = dlgPick; if (p != null) p(Convert.ToInt32(b.Tag)); };
            }
            dlgHost.MouseLeftButtonDown += (s, e) => { if (dlgPick == null && e.OriginalSource == dlgHost) dlgHost.Visibility = Visibility.Collapsed; };
        }

        // buttons[0] is the primary action (right-most, Enter), the last button is the cancel action (Esc); returns the clicked index
        async Task<int> Ask(string title, string msg, string kind, params string[] buttons)
        {
            await dlgGate.WaitAsync();                       // dialogs are shown strictly one after another
            try
            {
                if (buttons == null || buttons.Length == 0) buttons = new[] { "OK" };
                var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);   // never resume the caller inside the Click handler
                string acc = kind == "warn" ? "#FFB84D" : kind == "danger" ? "#FF6B8A" : kind == "ok" ? "#3DDC97" : kind == "question" ? "#8B7CFF" : "#3DD6F5";
                dlgGlyph.Text = kind == "warn" ? "⚠" : kind == "danger" ? "✖" : kind == "ok" ? "✓" : kind == "question" ? "?" : "ℹ";
                dlgGlyph.Foreground = Br(acc);
                dlgIconBg.Background = Br("#26" + acc.Substring(1));
                dlgCard.BorderBrush = Br(acc);
                dlgGlow.Effect = new DropShadowEffect { Color = Col(acc), BlurRadius = 40, ShadowDepth = 0, Opacity = 0.4 };
                dlgTitleT.Text = title; dlgMsg.Text = msg;

                Action<int> pick = null;
                pick = idx => { if (dlgPick != pick) return; Dbg("dialog '" + title + "' answered: " + idx); dlgPick = null; HideDlg(); tcs.TrySetResult(idx); };

                int n = Math.Min(3, buttons.Length);
                var slots = new[] { dlgBtnC, dlgBtnB, dlgBtnA };         // index 0 -> right-most
                dlgBtnA.Visibility = dlgBtnB.Visibility = dlgBtnC.Visibility = Visibility.Collapsed;
                for (int i = 0; i < n; i++) { slots[i].Content = buttons[i]; slots[i].Tag = i; slots[i].Visibility = Visibility.Visible; slots[i].IsEnabled = true; }
                dlgLast = n - 1;
                dlgPick = pick;
                Dbg("dialog '" + title + "' shown, buttons=" + n + " [" + string.Join(" | ", buttons.Take(n)) + "]");
                ShowDlg();
                return await tcs.Task;
            }
            finally { dlgGate.Release(); }
        }

        void ShowDlg()
        {
            dlgGen++;
            if (dlgHideTimer != null) { dlgHideTimer.Stop(); dlgHideTimer = null; }     // a late 'hide' must never close the NEW dialog
            dlgHost.BeginAnimation(UIElement.OpacityProperty, null);
            dlgHost.Opacity = 0;
            dlgHost.Visibility = Visibility.Visible;
            dlgHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
            var st = new ScaleTransform(0.92, 0.92); dlgWrap.RenderTransform = st;
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        }

        void HideDlg()
        {
            if (dlgHideTimer != null) { dlgHideTimer.Stop(); dlgHideTimer = null; }
            CollapseDlg();                                   // immediate: the overlay is gone the moment a button is clicked
        }

        // hard reset of the overlay - after this nothing can be left covering the window
        void CollapseDlg()
        {
            dlgHost.BeginAnimation(UIElement.OpacityProperty, null);
            dlgHost.Opacity = 1;
            dlgHost.Visibility = Visibility.Collapsed;
        }
    }
}
