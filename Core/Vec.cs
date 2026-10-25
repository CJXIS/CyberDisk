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
    // ───────────────────────── Vector icons ─────────────────────────
    /// <summary>
    /// One consistent set of line icons (24 x 24 grid, drawn with a round 1.8 px stroke).
    /// Used by the sidebar, the My PC cards and the export reports, so every icon shares the same style.
    /// </summary>
    static class Vec
    {
        public const string Drives   = "M22 12H2 M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z M6 16h.01 M10 16h.01";
        public const string Apps     = "M4 4h6.5v6.5H4z M13.5 4H20v6.5h-6.5z M4 13.5h6.5V20H4z M13.5 13.5H20V20h-6.5z";
        public const string Clean    = "M12 3l1.9 5.1L19 10l-5.1 1.9L12 17l-1.9-5.1L5 10l5.1-1.9z M19 15l.7 1.8 1.8.7-1.8.7L19 20l-.7-1.8-1.8-.7 1.8-.7z M5 3l.5 1.3L6.8 4.8 5.5 5.3 5 6.6 4.5 5.3 3.2 4.8l1.3-.5z";
        public const string Boost    = "M13 2L4 14h7l-1 8 9-12h-7l1-8z";
        public const string Health   = "M3 12h4l2.5-6 4 12 2.5-6h5";
        public const string Errors   = "M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01";
        public const string Settings = "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6";
        public const string Cpu      = "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M9 9h6v6H9z M9 1v3 M15 1v3 M9 20v3 M15 20v3 M20 9h3 M20 14h3 M1 9h3 M1 14h3";
        public const string Memory   = "M2 6h20v9H2z M6 15v3 M10 15v3 M14 15v3 M18 15v3 M6 10h2 M11 10h2 M16 10h2";
        public const string Gpu      = "M2 6h17v10H2z M19 9h3v4h-3z M5 16v3 M9 16v3 M11 11m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0 M11 9.4v3.2 M9.4 11h3.2";
        public const string Board    = "M4 3h16a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1z M8 8h5v5H8z M16 7v4 M8 17h8 M16 15v2";
        public const string Monitor  = "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M8 21h8 M12 17v4";
        public const string Network  = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z M2 12h20 M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z";
        public const string Windows  = "M3 5.6l8-1.1v7H3z M12.5 4.3L21 3v8.5h-8.5z M3 12.5h8v7l-8-1.1z M12.5 12.5H21V21l-8.5-1.3z";
        public const string Battery  = "M3 7h15a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z M22 10.5v3 M6 10.5v3 M10 10.5v3";
        public const string Shield   = "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z M9 12l2 2 4-4";
        public const string Export   = "M12 3v12 M7 10l5 5 5-5 M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2";
        public const string Refresh  = "M21 12a9 9 0 1 1-2.64-6.36 M21 3v6h-6";
        public const string Back     = "M15 18l-6-6 6-6";
        public const string Copy     = "M9 9h11a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V10a1 1 0 0 1 1-1z M5 15H4a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1h11a1 1 0 0 1 1 1v1";
        public const string Heart    = "M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1-1.1a5.5 5.5 0 0 0-7.8 7.8l1 1.1L12 21.2l7.8-7.7 1-1.1a5.5 5.5 0 0 0 0-7.8z";

        public const string Up       = "M18 15l-6-6-6 6";
        public const string Folder   = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z";
        public const string FolderPlus = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M12 11v6 M9 14h6";
        public const string Cut      = "M6 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M20 4L8.12 15.88 M14.47 14.48L20 20 M8.12 8.12L12 12";
        public const string Paste    = "M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2 M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z";
        public const string Trash    = "M3 6h18 M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2 M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6 M10 11v6 M14 11v6";
        public const string Search   = "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z M21 21l-4.35-4.35";
        public const string Check    = "M20 6L9 17l-5-5";
        public const string Close    = "M18 6L6 18 M6 6l12 12";
        public const string Stop     = "M7 7h10v10H7z";
        public const string List     = "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01";
        public const string Mail     = "M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M22 6l-10 7L2 6";
        public const string Power    = "M18.36 6.64a9 9 0 1 1-12.73 0 M12 2v10";
        public const string Eye      = "M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z";

        static readonly Dictionary<string, Geometry> cache = new Dictionary<string, Geometry>();

        /// <summary>Frozen geometry for an icon path string (cached).</summary>
        public static Geometry Geo(string path)
        {
            lock (cache)
            {
                Geometry g;
                if (!cache.TryGetValue(path, out g)) { g = Geometry.Parse(path); g.Freeze(); cache[path] = g; }
                return g;
            }
        }
    }
}
