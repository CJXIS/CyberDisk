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
    /// <summary>Reads disk identity, health and SMART data, and turns it into an explainable 0-100 health score.</summary>
    static class DiskInfo
    {
        static readonly object gate = new object();
        static List<PhysDisk> cache; static DateTime cacheAt = DateTime.MinValue;

        /// <summary>Returns the last result when it is younger than <paramref name="maxAgeSec"/> seconds, otherwise reads the disks again.</summary>
        public static List<PhysDisk> Get(int maxAgeSec)
        {
            lock (gate)
            {
                if (cache != null && (DateTime.Now - cacheAt).TotalSeconds < maxAgeSec) return cache;
                cache = Query(); cacheAt = DateTime.Now;
                return cache;
            }
        }

        public static void Invalidate() { lock (gate) { cache = null; } }

        static readonly Dictionary<int, string> SmartNames = new Dictionary<int, string>
        {
            { 1, "Read error rate" }, { 3, "Spin-up time" }, { 4, "Start / stop count" }, { 5, "Reallocated sectors" }, { 7, "Seek error rate" }, { 9, "Power-on hours" },
            { 10, "Spin retry count" }, { 12, "Power cycle count" }, { 177, "Wear leveling count" }, { 179, "Used reserved blocks" }, { 181, "Program fail count" },
            { 182, "Erase fail count" }, { 187, "Reported uncorrectable errors" }, { 188, "Command timeout" }, { 190, "Airflow temperature" }, { 192, "Unsafe shutdown count" },
            { 193, "Load cycle count" }, { 194, "Temperature" }, { 195, "Hardware ECC recovered" }, { 196, "Reallocation events" }, { 197, "Current pending sectors" },
            { 198, "Offline uncorrectable sectors" }, { 199, "Interface CRC errors" }, { 231, "SSD life left" }, { 233, "Media wearout indicator" }, { 241, "Total host writes" }, { 242, "Total host reads" },
        };

        // PowerShell reads what the classic WMI classes cannot: SSD/HDD type, bus type, wear, temperature and error counters.
        const string StorageScript = @"
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$tab = [string][char]9
Get-PhysicalDisk | ForEach-Object {
  $d = $_
  $r = $d | Get-StorageReliabilityCounter
  $g = Get-Disk -Number $d.DeviceId
  $f = @($d.DeviceId, $d.FriendlyName, $d.SerialNumber, $d.FirmwareVersion, $d.BusType, $d.MediaType, $d.HealthStatus, ($d.OperationalStatus -join '/'), $g.PartitionStyle, $d.SpindleSpeed, $d.LogicalSectorSize, $d.PhysicalSectorSize, $r.Temperature, $r.TemperatureMax, $r.Wear, $r.PowerOnHours, $r.ReadErrorsTotal, $r.WriteErrorsTotal, $r.StartStopCycleCount, $r.LoadUnloadCycleCount)
  'DISK' + $tab + (($f | ForEach-Object { '' + $_ }) -join $tab)
}
Get-Partition | ForEach-Object {
  $l = ''; if ($_.DriveLetter -and [int][char]$_.DriveLetter -gt 32) { $l = [string]$_.DriveLetter }
  'PART' + $tab + $_.DiskNumber + $tab + $l
}";

        /// <summary>Reads every physical disk. Slow (a few seconds) - always call from a background thread.</summary>
        public static List<PhysDisk> Query()
        {
            var list = new List<PhysDisk>();
            ReadBasics(list);
            ReadVolumes(list);
            ReadStorageStack(list);
            foreach (var d in list) d.Volumes = d.Volumes.OrderBy(v => v.Letter).ToList();
            ReadSmart(list);
            foreach (var d in list) Assess(d);
            return list.OrderBy(d => d.Number).ToList();
        }

        static void ReadBasics(List<PhysDisk> list)
        {
            foreach (var m in Wmi.Query("SELECT Index, Model, SerialNumber, FirmwareRevision, InterfaceType, Size, Partitions, PNPDeviceID, DeviceID, BytesPerSector, MediaType FROM Win32_DiskDrive"))
            {
                var d = new PhysDisk
                {
                    Number = (int)Wmi.Long(m, "Index"), Model = Wmi.Str(m, "Model"), Serial = Wmi.Str(m, "SerialNumber"), Firmware = Wmi.Str(m, "FirmwareRevision"),
                    Interface = Wmi.Str(m, "InterfaceType"), Size = Wmi.Long(m, "Size"), Partitions = (int)Wmi.Long(m, "Partitions"), Pnp = Wmi.Str(m, "PNPDeviceID"),
                    Bus = Wmi.Str(m, "InterfaceType"), Health = "", Operational = ""
                };
                long bps = Wmi.Long(m, "BytesPerSector"); if (bps > 0) d.LogicalSector = (int)bps;
                string media = Wmi.Str(m, "MediaType");
                d.Media = media.IndexOf("Removable", StringComparison.OrdinalIgnoreCase) >= 0 ? "Removable" : "";
                d.DeviceId = Wmi.Str(m, "DeviceID");
                list.Add(d);
            }
        }

        static readonly Regex RxDiskNo = new Regex(@"Disk #(\d+)", RegexOptions.IgnoreCase);
        static readonly Regex RxId = new Regex("DeviceID=\"([^\"]+)\"");
        static string RefId(string reference) { var m = RxId.Match(reference ?? ""); return m.Success ? m.Groups[1].Value : ""; }

        /// <summary>Adds a drive letter to a disk once, filling label, file system and space from Windows.</summary>
        static void AddVolume(PhysDisk d, string letter)
        {
            letter = (letter ?? "").Trim().TrimEnd('\\');
            if (letter.Length == 1) letter += ":";
            if (letter.Length != 2 || letter[1] != ':' || !char.IsLetter(letter[0])) return;
            letter = letter.ToUpperInvariant();
            if (d.Volumes.Any(v => v.Letter == letter)) return;
            var vol = new DiskVolume { Letter = letter, Label = "", Format = "" };
            try { var di = new DriveInfo(letter); if (di.IsReady) { vol.Label = di.VolumeLabel; vol.Format = di.DriveFormat; vol.Size = di.TotalSize; vol.Free = di.TotalFreeSpace; } } catch { }
            d.Volumes.Add(vol);
        }

        /// <summary>
        /// Finds the drive letters of every disk. Three independent sources are combined, because any single one can come back empty:
        /// 1) the storage driver (IOCTL) for every letter, 2) the WMI partition links, 3) the PowerShell partition list (see ReadStorageStack).
        /// </summary>
        static void ReadVolumes(List<PhysDisk> list)
        {
            try
            {
                foreach (var di in DriveInfo.GetDrives())
                {
                    if (di.DriveType != DriveType.Fixed && di.DriveType != DriveType.Removable) continue;
                    int no = Native.DiskNumberOfLetter(di.Name);
                    var d = no < 0 ? null : list.FirstOrDefault(x => x.Number == no);
                    if (d != null) AddVolume(d, di.Name);
                }
            }
            catch { }
            try
            {
                foreach (var m in Wmi.Query("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition"))
                {
                    var no = RxDiskNo.Match(RefId(Wmi.Str(m, "Antecedent")));          // the partition id reads "Disk #3, Partition #1"
                    string letter = RefId(Wmi.Str(m, "Dependent"));
                    if (!no.Success || letter.Length == 0) continue;
                    int n = int.Parse(no.Groups[1].Value);
                    var d = list.FirstOrDefault(x => x.Number == n);
                    if (d != null) AddVolume(d, letter);
                }
            }
            catch { }
            try
            {
                foreach (var p in Wmi.Query("SELECT DiskIndex, Index, Size, Type, BootPartition, StartingOffset FROM Win32_DiskPartition"))
                {
                    var d = list.FirstOrDefault(x => x.Number == (int)Wmi.Long(p, "DiskIndex"));
                    if (d == null) continue;
                    d.Parts.Add(new DiskPart { No = (int)Wmi.Long(p, "Index") + 1, Type = PartTypeName(Wmi.Str(p, "Type")), Size = Wmi.Long(p, "Size"), Offset = Wmi.Long(p, "StartingOffset"), Boot = Wmi.Bool(p, "BootPartition") });
                }
            }
            catch { }
        }

        static string PartTypeName(string t)
        {
            if (string.IsNullOrEmpty(t)) return "Partition";
            if (t.IndexOf("System", StringComparison.OrdinalIgnoreCase) >= 0 && t.StartsWith("GPT", StringComparison.OrdinalIgnoreCase)) return "EFI system partition";
            if (t.IndexOf("Recovery", StringComparison.OrdinalIgnoreCase) >= 0) return "Recovery";
            if (t.IndexOf("Reserved", StringComparison.OrdinalIgnoreCase) >= 0) return "Microsoft reserved";
            if (t.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("Installable", StringComparison.OrdinalIgnoreCase) >= 0) return "Data (NTFS / exFAT)";
            return t.Replace("GPT: ", "");
        }

        static int? I(string[] f, int i) { int v; return i < f.Length && int.TryParse(f[i].Trim(), out v) ? v : (int?)null; }
        static long? L(string[] f, int i) { long v; return i < f.Length && long.TryParse(f[i].Trim(), out v) ? v : (long?)null; }

        static void ReadStorageStack(List<PhysDisk> list)
        {
            try
            {
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(StorageScript));
                string output;
                Fixer.Exec("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, 60000, out output, Encoding.UTF8);
                foreach (var raw in (output ?? "").Split('\n'))
                {
                    string line = raw.Trim('\r', '\uFEFF', ' ');
                    if (line.StartsWith("PART\t"))                                  // a partition with a drive letter: make sure the letter is listed
                    {
                        var pf = line.Split('\t'); int pn;
                        if (pf.Length >= 3 && int.TryParse(pf[1].Trim(), out pn) && pf[2].Trim().Length > 0) { var pd = list.FirstOrDefault(x => x.Number == pn); if (pd != null) AddVolume(pd, pf[2].Trim()); }
                        continue;
                    }
                    if (!line.StartsWith("DISK\t")) continue;
                    var f = line.Split('\t');                       // f[0] = "DISK", then the fields in the order of the script
                    var g = f.Skip(1).ToArray();
                    int num = I(g, 0) ?? -1;
                    var d = list.FirstOrDefault(x => x.Number == num);
                    if (d == null) continue;
                    if (g.Length > 1 && g[1].Length > 0) d.Model = g[1];
                    if (g.Length > 2 && g[2].Trim().Length > 0) d.Serial = g[2].Trim();
                    if (g.Length > 3 && g[3].Trim().Length > 0) d.Firmware = g[3].Trim();
                    if (g.Length > 4 && g[4].Length > 0) d.Bus = g[4];
                    if (g.Length > 5 && g[5].Length > 0) d.Media = g[5] == "Unspecified" ? (d.Bus == "NVMe" ? "SSD" : d.Media) : g[5];
                    if (g.Length > 6) d.Health = g[6];
                    if (g.Length > 7) d.Operational = g[7];
                    if (g.Length > 8) d.PartStyle = g[8];
                    var rpm = L(g, 9); d.Rpm = rpm.HasValue && rpm.Value > 0 && rpm.Value < 100000 ? (int?)rpm.Value : null;
                    d.LogicalSector = I(g, 10) ?? d.LogicalSector; d.PhysicalSector = I(g, 11);
                    d.TempC = I(g, 12); d.TempMaxC = I(g, 13); d.WearPct = I(g, 14);
                    d.PowerOnHours = L(g, 15); d.ReadErrors = L(g, 16); d.WriteErrors = L(g, 17); d.StartStops = L(g, 18); d.LoadCycles = L(g, 19);
                }
            }
            catch { }
        }

        static void ReadSmart(List<PhysDisk> list)
        {
            try
            {
                foreach (var m in Wmi.Query(@"root\wmi", "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_FailurePredictData"))
                {
                    string inst = Wmi.Str(m, "InstanceName");
                    var d = list.FirstOrDefault(x => x.Pnp.Length > 0 && inst.StartsWith(x.Pnp, StringComparison.OrdinalIgnoreCase));
                    var v = m["VendorSpecific"] as byte[];
                    if (d == null || v == null) continue;
                    for (int i = 2; i + 12 <= v.Length && i < 362; i += 12)          // 12 bytes per attribute: id, flags(2), current, worst, raw(6), reserved
                    {
                        int id = v[i]; if (id == 0) continue;
                        long raw = 0; for (int k = 5; k >= 0; k--) raw = (raw << 8) | v[i + 5 + k];
                        string name; if (!SmartNames.TryGetValue(id, out name)) name = "Vendor attribute " + id;
                        d.Smart.Add(new SmartAttr { Id = id, Name = name, Current = v[i + 3], Worst = v[i + 4], Raw = raw });
                    }
                }
                foreach (var m in Wmi.Query(@"root\wmi", "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus"))
                {
                    string inst = Wmi.Str(m, "InstanceName");
                    var d = list.FirstOrDefault(x => x.Pnp.Length > 0 && inst.StartsWith(x.Pnp, StringComparison.OrdinalIgnoreCase));
                    if (d != null && Wmi.Bool(m, "PredictFailure")) d.PredictFailure = true;
                }
            }
            catch { }
            foreach (var d in list)                                                  // fill gaps from SMART when the storage stack had no counters
            {
                var t = d.Smart.FirstOrDefault(a => a.Id == 194) ?? d.Smart.FirstOrDefault(a => a.Id == 190);
                if (d.TempC == null && t != null && (t.Raw & 0xFF) > 0 && (t.Raw & 0xFF) < 120) d.TempC = (int)(t.Raw & 0xFF);
                var h = d.Smart.FirstOrDefault(a => a.Id == 9);
                if (d.PowerOnHours == null && h != null) d.PowerOnHours = h.Raw & 0xFFFFFF;
                var life = d.Smart.FirstOrDefault(a => a.Id == 231) ?? d.Smart.FirstOrDefault(a => a.Id == 233);
                if (d.WearPct == null && life != null && life.Current > 0 && life.Current <= 100) d.WearPct = 100 - life.Current;
            }
        }

        static long Raw(PhysDisk d, int id) { var a = d.Smart.FirstOrDefault(x => x.Id == id); return a == null ? 0 : a.Raw & 0xFFFFFFFFL; }

        /// <summary>Builds the 0-100 score and the list of reasons behind it.</summary>
        public static void Assess(PhysDisk d)
        {
            var issues = new List<DiskIssue>(); int score = 100;
            Action<int, int, string> add = (level, penalty, text) => { issues.Add(new DiskIssue { Level = level, Penalty = penalty, Text = text }); score -= penalty; };

            if (string.Equals(d.Health, "Unhealthy", StringComparison.OrdinalIgnoreCase)) add(2, 60, "Windows reports this drive as unhealthy.");
            else if (string.Equals(d.Health, "Warning", StringComparison.OrdinalIgnoreCase)) add(1, 25, "Windows reports a warning for this drive.");
            if (d.PredictFailure) add(2, 70, "SMART predicts that this drive will fail soon.");

            if (d.TempC.HasValue)
            {
                int t = d.TempC.Value;
                if (t >= 70) add(2, 25, "Temperature is " + t + "°C - too hot, check airflow and dust.");
                else if (t >= 55) add(1, 8, "Temperature is " + t + "°C - running warm.");
            }
            if (d.WearPct.HasValue)
            {
                int w = d.WearPct.Value;
                if (w >= 90) add(2, 50, w + "% of the SSD's life is used - replace it soon.");
                else if (w >= 70) add(1, 25, w + "% of the SSD's life is used - plan a replacement.");
                else if (w >= 50) add(1, 8, w + "% of the SSD's life is used.");
            }
            long realloc = Raw(d, 5), pending = Raw(d, 197), uncorr = Raw(d, 198), crc = Raw(d, 199);
            if (realloc > 0) add(realloc >= 50 ? 2 : 1, realloc >= 50 ? 35 : 10, realloc + " reallocated sector(s) - the surface is starting to fail.");
            if (pending > 0) add(2, 25, pending + " sector(s) are waiting to be reallocated.");
            if (uncorr > 0) add(2, 25, uncorr + " sector(s) could not be read (offline uncorrectable).");
            if (crc > 0) add(1, crc >= 100 ? 15 : 5, crc + " interface CRC error(s) - check the data cable.");
            long errs = (d.ReadErrors ?? 0) + (d.WriteErrors ?? 0);
            if (errs > 0) add(errs > 100 ? 2 : 1, errs > 100 ? 25 : 10, "Windows logged " + (d.ReadErrors ?? 0) + " read and " + (d.WriteErrors ?? 0) + " write error(s).");
            if (d.Media == "HDD" && d.PowerOnHours.HasValue && d.PowerOnHours.Value > 35000)
                add(1, 8, "The drive has run for " + d.PowerOnHours.Value + " hours (about " + (d.PowerOnHours.Value / 8760) + " years).");

            d.Issues = issues;
            d.Score = Math.Max(0, Math.Min(100, score));
            d.Grade = ScoreTheme.Grade(d.Score);
            d.Verdict = d.Score >= 90 ? "Excellent - no sign of wear or errors."
                      : d.Score >= 75 ? "Good - a few points are worth watching."
                      : d.Score >= 55 ? "Fair - keep an eye on this drive and back up regularly."
                      : d.Score >= 35 ? "Poor - back up your data now."
                      : "Critical - back up immediately and replace this drive.";
        }
    }
}
