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
    // ───────────────────────── Language: English <-> Arabic (text + right-to-left layout) ─────────────────────────
    static partial class Loc
    {
        public static bool Ar;                                              // Arabic is active
        static readonly Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);
        sealed class Pat { public Regex Rx; public string Tpl; public int Weight; }
        static readonly List<Pat> pats = new List<Pat>();
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        static bool ready;

        static void A(string en, string ar) { exact[en] = ar; }
        // template with {0} {1} ... wildcards (in increasing order in the English text)
        static void P(string en, string ar)
        {
            var sb = new StringBuilder("^");
            int last = 0, lit = 0;
            foreach (Match m in Regex.Matches(en, @"\{n?\d\}"))
            {
                string part = en.Substring(last, m.Index - last);
                sb.Append(Regex.Escape(part)); lit += part.Length;
                sb.Append(m.Value[1] == 'n' ? @"(—|\d[\d,\.]*(?:\s?[KMGTP]?B)?)" : "([^·]+?)");   // {n0} = a number or a size such as 3.2 GB
                last = m.Index + m.Length;
            }
            string tail = en.Substring(last);
            sb.Append(Regex.Escape(tail)); lit += tail.Length; sb.Append("$");
            pats.Add(new Pat { Rx = new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), Tpl = ar, Weight = lit });
        }

        static void Ready()
        {
            if (ready) return;
            ready = true;
            Dict1(); Dict2(); Dict3(); Dict4();
            pats.Sort((x, y) => y.Weight.CompareTo(x.Weight));            // the most specific template wins
        }

        public static string T(string s)
        {
            if (!Ar || string.IsNullOrEmpty(s)) return s;
            string r;
            if (cache.TryGetValue(s, out r)) return r;
            Ready();
            r = Tr(s, 0);
            if (cache.Count > 6000) cache.Clear();
            cache[s] = r;
            return r;
        }

        static readonly Regex Seg = new Regex(@"(\s*[·|]\s*)", RegexOptions.CultureInvariant);
        static readonly Regex Pre = new Regex(@"^([^\p{L}\p{N}\(\[]+)(.*\S.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        static string Tr(string s, int depth)
        {
            if (string.IsNullOrWhiteSpace(s) || depth > 6) return s;
            string r;
            if (exact.TryGetValue(s, out r)) return r;

            if (s.IndexOf('\n') >= 0)                                       // multi-line text: every line on its own
            {
                var lines = s.Split('\n'); bool any = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string raw = lines[i].TrimEnd('\r'), t = Tr(raw, depth + 1);
                    if (t != raw) { any = true; lines[i] = t; }
                }
                return any ? string.Join("\n", lines) : s;
            }

            foreach (var p in pats)
            {
                var m = p.Rx.Match(s);
                if (!m.Success) continue;
                string res = p.Tpl;
                for (int g = m.Groups.Count - 1; g >= 1; g--)
                    res = res.Replace("{" + (g - 1) + "}", Tr(m.Groups[g].Value, depth + 1));
                return res;
            }

            var pm = Pre.Match(s);                                          // leading symbols such as "⟳ " or "• "
            if (pm.Success)
            {
                string core = pm.Groups[2].Value, t = Tr(core, depth + 1);
                if (t != core) return Flip(pm.Groups[1].Value) + t;
            }

            if (s.IndexOf('·') >= 0 || s.IndexOf('|') >= 0)                 // "a · b | c": translate each part
            {
                var parts = Seg.Split(s); bool any = false;
                for (int i = 0; i < parts.Length; i += 2)
                {
                    string t = Tr(parts[i], depth + 1);
                    if (t != parts[i]) { any = true; parts[i] = t; }
                }
                if (any) return string.Concat(parts);
            }

            string trimmed = s.Trim();                                      // same text with stray spaces around it
            if (trimmed.Length != s.Length && trimmed.Length > 0)
            {
                string t = Tr(trimmed, depth + 1);
                if (t != trimmed) return s.Substring(0, s.IndexOf(trimmed[0])) + t + s.Substring(s.LastIndexOf(trimmed[trimmed.Length - 1]) + 1);
            }
            return s;
        }

        static string Flip(string prefix) { return prefix.Replace('◀', '\u0001').Replace('▶', '◀').Replace('\u0001', '▶').Replace('◂', '\u0001').Replace('▸', '◂').Replace('\u0001', '▸'); }

        // ── applying the language to the live window ──
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]> done = new System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]>();
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]> doneTip = new System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]>();

        // one pass over the visible tree. Arabic: English text -> Arabic. English: restore what this pass translated.
        public static void Walk(DependencyObject o, bool all)
        {
            if (o == null) return;
            var fe = o as FrameworkElement;
            if (fe != null && !all && fe.Visibility != Visibility.Visible) return;
            if (Ar && fe != null && (fe is Image || fe is Canvas || fe is System.Windows.Shapes.Shape) && fe.FlowDirection != FlowDirection.LeftToRight)
                fe.SetCurrentValue(FrameworkElement.FlowDirectionProperty, FlowDirection.LeftToRight);   // icons, drawings and absolute-positioned canvases are never mirrored

            var tb = o as TextBlock;
            if (tb != null) FixText(tb);
            var rb = o as TextBox;
            if (rb != null && rb.IsReadOnly) FixBox(rb);
            if (fe != null && !(fe is TextBox)) FixTip(fe);

            int n = VisualTreeHelper.GetChildrenCount(o);
            for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(o, i), all);
        }

        static void FixText(TextBlock tb)
        {
            string cur = tb.Text; string[] rec;
            if (done.TryGetValue(tb, out rec))
            {
                if (Ar) { if (cur == rec[1]) return; }
                else { if (cur == rec[1]) tb.SetCurrentValue(TextBlock.TextProperty, rec[0]); done.Remove(tb); return; }
            }
            if (!Ar || string.IsNullOrEmpty(cur)) return;
            string t = T(cur);
            if (t == cur || InNameColumn(tb)) return;                       // file / app / process names are data, never translated
            tb.SetCurrentValue(TextBlock.TextProperty, t);                 // SetCurrentValue keeps data bindings alive
            done.Remove(tb); done.Add(tb, new[] { cur, t });
        }

        static void FixBox(TextBox tb)
        {
            string cur = tb.Text; string[] rec;
            if (done.TryGetValue(tb, out rec))
            {
                if (Ar) { if (cur == rec[1]) return; }
                else { if (cur == rec[1]) tb.SetCurrentValue(TextBox.TextProperty, rec[0]); done.Remove(tb); return; }
            }
            if (!Ar || string.IsNullOrEmpty(cur)) return;
            string t = T(cur);
            if (t == cur) return;
            tb.SetCurrentValue(TextBox.TextProperty, t);
            done.Remove(tb); done.Add(tb, new[] { cur, t });
        }

        static bool InNameColumn(DependencyObject o)
        {
            for (int i = 0; i < 8 && o != null; i++)
            {
                var cell = o as DataGridCell;
                if (cell != null) return cell.Column != null && cell.Column.DisplayIndex == 0;
                o = VisualTreeHelper.GetParent(o);
            }
            return false;
        }

        static void FixTip(FrameworkElement fe)
        {
            string cur = fe.ToolTip as string; string[] rec;
            if (doneTip.TryGetValue(fe, out rec))
            {
                if (Ar) { if (cur == rec[1]) return; }
                else { if (cur == rec[1]) fe.SetCurrentValue(FrameworkElement.ToolTipProperty, rec[0]); doneTip.Remove(fe); return; }
            }
            if (!Ar || string.IsNullOrEmpty(cur)) return;
            string t = T(cur);
            if (t == cur) return;
            fe.SetCurrentValue(FrameworkElement.ToolTipProperty, t);
            doneTip.Remove(fe); doneTip.Add(fe, new[] { cur, t });
        }

        // context menus live in their own popup, so they are translated each time they open
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]> doneMenu = new System.Runtime.CompilerServices.ConditionalWeakTable<DependencyObject, string[]>();
        public static void Menu(ItemsControl menu)
        {
            foreach (var it in menu.Items)
            {
                var mi = it as MenuItem;
                if (mi == null) continue;
                string cur = mi.Header as string; string[] rec;
                if (doneMenu.TryGetValue(mi, out rec) && cur == rec[1] && !Ar) { mi.Header = rec[0]; doneMenu.Remove(mi); continue; }
                if (!Ar || cur == null || (rec != null && cur == rec[1])) continue;
                string t = T(cur);
                if (t == cur) continue;
                mi.Header = t; doneMenu.Remove(mi); doneMenu.Add(mi, new[] { cur, t });
            }
        }
    }
}
