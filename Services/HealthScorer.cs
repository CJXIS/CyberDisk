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
    /// <summary>
    /// Turns findings into one score. Every category starts at 100 and loses 28 points per problem and 10 per warning;
    /// the overall score is the weighted average of the categories that were checked, and is capped when real problems exist.
    /// </summary>
    static class HealthScorer
    {
        public static readonly string[] Categories = { "Storage", "Reliability", "Security", "System", "Performance" };
        static readonly Dictionary<string, double> Weight = new Dictionary<string, double> { { "Storage", 0.30 }, { "Reliability", 0.25 }, { "Security", 0.20 }, { "System", 0.15 }, { "Performance", 0.10 } };

        public static HealthReport Evaluate(IEnumerable<Finding> findings)
        {
            var list = findings.Where(f => f.Level != 3).ToList();   // info items never change the score
            var r = new HealthReport
            {
                Problems = list.Count(f => f.Level == 2), Warnings = list.Count(f => f.Level == 1), Passed = list.Count(f => f.Level == 0)
            };
            double sum = 0, weights = 0;
            foreach (var cat in Categories)
            {
                var inCat = list.Where(f => f.Category == cat).ToList();
                if (inCat.Count == 0) continue;
                int s = 100 - inCat.Count(f => f.Level == 2) * 28 - inCat.Count(f => f.Level == 1) * 10;
                s = Math.Max(0, Math.Min(100, s));
                r.Categories.Add(new CatScore { Name = cat, Score = s, ScoreText = s.ToString(), Percent = s, Brush = ScoreTheme.Gradient(s) });
                sum += Weight[cat] * s; weights += Weight[cat];
            }
            int score = weights > 0 ? (int)Math.Round(sum / weights) : 100;
            if (r.Problems == 1) score = Math.Min(score, 84);
            else if (r.Problems == 2) score = Math.Min(score, 74);
            else if (r.Problems >= 3) score = Math.Min(score, 59);
            if (r.Warnings >= 5) score = Math.Min(score, 79);
            r.Score = Math.Max(0, Math.Min(100, score));
            r.Grade = ScoreTheme.Grade(r.Score);
            return r;
        }
    }
}
