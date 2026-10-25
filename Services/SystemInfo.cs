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
    /// <summary>Reads CPU, memory, graphics, board, storage, display, network, battery and security information.</summary>
    static class SystemInfo
    {
        [DllImport("kernel32.dll")] static extern bool GetFirmwareType(out int type);

        static HwRow R(string k, string v) { return new HwRow { K = k, V = string.IsNullOrWhiteSpace(v) ? "—" : v }; }
        static Brush Accent(string hex) { var b = new SolidColorBrush(Ui.Col(hex)); b.Freeze(); return b; }
        static string Squash(string s) { return Regex.Replace(s ?? "", @"\s+", " ").Trim(); }
        static string Gb(long bytes) { return bytes <= 0 ? "—" : Ui.Fmt(bytes); }
        static string Ghz(long mhz) { return mhz <= 0 ? "—" : (mhz / 1000.0).ToString("0.00") + " GHz"; }
        static string Kb(long kb) { return kb <= 0 ? "—" : kb >= 1024 ? (kb / 1024.0).ToString("0.#") + " MB" : kb + " KB"; }

        static readonly Dictionary<int, string> MemType = new Dictionary<int, string>
        { { 20, "DDR" }, { 21, "DDR2" }, { 22, "DDR2 FB-DIMM" }, { 24, "DDR3" }, { 26, "DDR4" }, { 27, "LPDDR" }, { 28, "LPDDR2" }, { 29, "LPDDR3" }, { 30, "LPDDR4" }, { 34, "DDR5" }, { 35, "LPDDR5" } };
        static readonly Dictionary<int, string> FormFactor = new Dictionary<int, string> { { 8, "DIMM" }, { 12, "SO-DIMM" }, { 9, "RIMM" }, { 13, "SRIMM" } };
        static readonly Dictionary<int, string> CpuArch = new Dictionary<int, string> { { 0, "x86" }, { 5, "ARM" }, { 9, "x64" }, { 12, "ARM64" } };

        /// <summary>Collects everything. Takes a few seconds - call it from a background thread.</summary>
        static T Safe<T>(Func<T> f) where T : class { try { return f(); } catch { return null; } }

        public static HwReport Collect()
        {
            var rep = new HwReport();
            var os = Wmi.Query("SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime, RegisteredUser, WindowsDirectory, MUILanguages, TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem").FirstOrDefault();
            var cs = Wmi.Query("SELECT Manufacturer, Model, TotalPhysicalMemory, UserName, Domain, SystemType, HypervisorPresent, Name FROM Win32_ComputerSystem").FirstOrDefault();

            rep.PcName = Environment.MachineName;
            if (cs != null) rep.Model = Squash(Wmi.Str(cs, "Manufacturer") + " " + Wmi.Str(cs, "Model"));
            if (os != null) rep.Os = Squash(Wmi.Str(os, "Caption"));
            var boot = os == null ? null : Wmi.Date(os, "LastBootUpTime");
            if (boot.HasValue) { var up = DateTime.Now - boot.Value; rep.Uptime = (up.Days > 0 ? up.Days + " d " : "") + up.Hours + " h " + up.Minutes + " min"; }

            var cpu = Wmi.Query("SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, CurrentClockSpeed, L2CacheSize, L3CacheSize, Architecture, SocketDesignation, VirtualizationFirmwareEnabled, ProcessorId, Family, Revision, LoadPercentage, Description, VoltageCaps FROM Win32_Processor");
            // every card is read at the same time: the slow ones (clock measuring, disks, Wi-Fi) no longer wait for each other
            var tCpu = Task.Run(() => Safe(() => CpuCard(cpu)));
            var tMem = Task.Run(() => Safe(() => MemoryCard(cs)));
            var tGpu = Task.Run(() => Safe(() => GpuCards().ToList()));
            var tBoard = Task.Run(() => Safe(() => BoardCard(cs)));
            var tStore = Task.Run(() => Safe(() => StorageCard()));
            var tDisp = Task.Run(() => Safe(() => DisplayCard()));
            var tNet = Task.Run(() => Safe(() => NetworkCard()));
            var tOs = Task.Run(() => Safe(() => OsCard(os, cs, boot)));
            var tBat = Task.Run(() => Safe(() => BatteryCard()));
            var tSec = Task.Run(() => Safe(() => SecurityCard(cpu)));
            Add(rep, tCpu.Result); Add(rep, tMem.Result);
            if (tGpu.Result != null) foreach (var g in tGpu.Result) Add(rep, g);
            Add(rep, tBoard.Result); Add(rep, tStore.Result); Add(rep, tDisp.Result); Add(rep, tNet.Result); Add(rep, tOs.Result); Add(rep, tBat.Result); Add(rep, tSec.Result);

            var c0 = cpu.FirstOrDefault(); long ram = cs == null ? 0 : Wmi.Long(cs, "TotalPhysicalMemory");
            rep.Summary = Squash((c0 == null ? "" : ShortCpu(Wmi.Str(c0, "Name"))) + "  ·  " + (ram > 0 ? Ui.Fmt(ram) + " RAM" : ""));
            return rep;
        }

        static void Add(HwReport rep, HwCard c) { if (c != null) rep.Cards.Add(c); }

        static string ShortCpu(string name)
        {
            name = Squash(name).Replace("(R)", "").Replace("(TM)", "").Replace("(tm)", "").Replace("CPU", "");
            int at = name.IndexOf(" @", StringComparison.Ordinal); if (at > 0) name = name.Substring(0, at);
            return Squash(name);
        }

        // ── processor ──
        static HwCard CpuCard(List<System.Management.ManagementBaseObject> cpu)
        {
            var c = cpu.FirstOrDefault(); if (c == null) return null;
            long cores = cpu.Sum(x => Wmi.Long(x, "NumberOfCores")), threads = cpu.Sum(x => Wmi.Long(x, "NumberOfLogicalProcessors"));
            long maxMhz = Wmi.Long(c, "MaxClockSpeed"), nowMhz = Wmi.Long(c, "CurrentClockSpeed"), load = Wmi.Long(c, "LoadPercentage");
            int arch = (int)Wmi.Long(c, "Architecture"); string archName; if (!CpuArch.TryGetValue(arch, out archName)) archName = "—";
            var card = new HwCard
            {
                Title = "PROCESSOR", IconData = Vec.Cpu, AccentBrush = Accent("#3DD6F5"), Headline = Squash(Wmi.Str(c, "Name")),
                Sub = cores + " cores  ·  " + threads + " threads  ·  base " + Ghz(maxMhz)
            };
            long liveMhz = LiveClockMhz(maxMhz);
            long boostMhz = BoostMhz(maxMhz);
            if (boostMhz < liveMhz) boostMhz = liveMhz;
            if (boostMhz > 0) card.Sub = cores + " cores  ·  " + threads + " threads  ·  " + Ghz(maxMhz) + " base  ·  up to " + Ghz(boostMhz);
            card.Rows.Add(R("Manufacturer", Wmi.Str(c, "Manufacturer")));
            card.Rows.Add(R("Architecture", archName));
            card.Rows.Add(R("Cores / threads", cores + " / " + threads));
            card.Rows.Add(R("Base clock", Ghz(maxMhz)));
            if (boostMhz > 0) card.Rows.Add(R("Max clock (boost)", Ghz(boostMhz) + (boostMhz > maxMhz * 1.02 ? "" : "  ·  boost not reached")));
            card.Rows.Add(R("Current clock", Ghz(liveMhz > 0 ? liveMhz : nowMhz)));
            if (cpu.Count > 1) card.Rows.Add(R("Sockets", cpu.Count.ToString()));
            card.AdvRows.Add(R("Socket", Wmi.Str(c, "SocketDesignation")));
            card.AdvRows.Add(R("L2 cache", Kb(Wmi.Long(c, "L2CacheSize"))));
            card.AdvRows.Add(R("L3 cache", Kb(Wmi.Long(c, "L3CacheSize"))));
            card.AdvRows.Add(R("Virtualization", Wmi.Bool(c, "VirtualizationFirmwareEnabled") ? "Enabled in firmware" : "Disabled or not supported"));
            card.AdvRows.Add(R("Family / revision", Wmi.Str(c, "Family") + " / " + Wmi.Str(c, "Revision")));
            card.AdvRows.Add(R("Processor ID", Wmi.Str(c, "ProcessorId")));
            card.AdvRows.Add(R("Description", Wmi.Str(c, "Description")));
            string temp = CpuTemperature();
            card.AdvRows.Add(R("Thermal zone", temp));
            card.Rows.Add(R("Hyper-Threading / SMT", threads > cores ? "On  ·  " + (threads / Math.Max(1, cores)) + " threads per core" : "Off"));
            card.Rows.Add(R("Virtualization", Wmi.Bool(c, "VirtualizationFirmwareEnabled") ? "Enabled" : "Disabled or not supported"));
            card.Rows.Add(R("Power plan", ActivePowerPlan()));
            card.AdvRows.Add(R("Instruction sets", CpuFeatures()));
            const string cpuKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
            card.AdvRows.Add(R("Vendor ID", HklmValue(cpuKey, "VendorIdentifier")));
            card.AdvRows.Add(R("Identifier", HklmValue(cpuKey, "Identifier")));
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(cpuKey))
                {
                    var u = k == null ? null : k.GetValue("Update Revision") as byte[];
                    if (u != null && u.Length >= 8) card.AdvRows.Add(R("Microcode revision", "0x" + BitConverter.ToUInt32(u, 4).ToString("X")));
                }
            }
            catch { }
            card.AdvRows.Add(R("Logical processors", Environment.ProcessorCount.ToString()));
            return card;
        }

        /// <summary>
        /// The speed the processor runs at right now. Win32_Processor.CurrentClockSpeed is only the rated speed on modern CPUs,
        /// so this multiplies the base speed with Windows' "% Processor Performance" counter, which follows turbo boost and power saving.
        /// </summary>
        static long LiveClockMhz(long baseMhz)
        {
            if (baseMhz <= 0) return 0;
            try
            {
                using (var pc = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true))
                {
                    pc.NextValue(); double best = 0, last = 0;
                    for (int i = 0; i < 4; i++) { Thread.Sleep(250); last = pc.NextValue(); if (last > best) best = last; }
                    if (last <= 0) return 0;
                    return (long)Math.Round(baseMhz * last / 100.0);
                }
            }
            catch { return 0; }
        }

        [DllImport("kernel32.dll")] static extern bool IsProcessorFeaturePresent(uint feature);

        static string CpuFeatures()
        {
            var l = new List<string>();
            Action<uint, string> t = (id, n) => { try { if (IsProcessorFeaturePresent(id)) l.Add(n); } catch { } };
            t(13, "SSE3"); t(36, "SSSE3"); t(37, "SSE4.1"); t(38, "SSE4.2"); t(39, "AVX"); t(40, "AVX2"); t(41, "AVX-512"); t(12, "NX"); t(20, "SLAT");
            return string.Join(", ", l);
        }

        static string HklmValue(string sub, string name)
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(sub)) { var v = k == null ? null : k.GetValue(name); return v == null ? "" : RegText(v); } } catch { return ""; }
        }

        static string ActivePowerPlan()
        {
            try { var p = Wmi.Query(@"root\cimv2\power", "SELECT ElementName, IsActive FROM Win32_PowerPlan").FirstOrDefault(x => Wmi.Bool(x, "IsActive")); return p == null ? "" : Wmi.Str(p, "ElementName"); }
            catch { return ""; }
        }

        static string OnOff(string v, string on, string off) { return v == "1" ? on : v == "0" ? off : ""; }

        static volatile bool boostStop;

        /// <summary>
        /// Windows does not publish the turbo ceiling, so it is measured: one core is kept busy for about a second and a half
        /// while the "% Processor Performance" counter of every core is watched. The highest value times the base speed is the real maximum clock.
        /// </summary>
        static long BoostMhz(long baseMhz)
        {
            if (baseMhz <= 0) return 0;
            var counters = new List<System.Diagnostics.PerformanceCounter>();
            try
            {
                var cat = new System.Diagnostics.PerformanceCounterCategory("Processor Information");
                foreach (var n in cat.GetInstanceNames()) if (n.IndexOf("_Total", StringComparison.OrdinalIgnoreCase) < 0) counters.Add(new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Performance", n, true));
                if (counters.Count == 0) return 0;
                foreach (var c in counters) c.NextValue();
                boostStop = false;
                var worker = new Thread(() => { double x = 1; while (!boostStop) x = Math.Sqrt(x + 1.0001); }) { IsBackground = true };
                worker.Start();
                double peak = 0; var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 1500) { Thread.Sleep(250); foreach (var c in counters) { double v = c.NextValue(); if (v > peak) peak = v; } }
                boostStop = true;
                return peak <= 0 ? 0 : (long)Math.Round(baseMhz * peak / 100.0);
            }
            catch { return 0; }
            finally { boostStop = true; foreach (var c in counters) c.Dispose(); }
        }

        static string CpuTemperature()
        {
            try
            {
                foreach (var m in Wmi.Query(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                {
                    double c = Wmi.Dbl(m, "CurrentTemperature") / 10.0 - 273.15;
                    if (c > 0 && c < 150) return c.ToString("0") + " °C";
                }
            }
            catch { }
            return "Not exposed by this PC";
        }

        // ── memory ──
        static HwCard MemoryCard(System.Management.ManagementBaseObject cs)
        {
            var mods = Wmi.Query("SELECT DataWidth, TotalWidth, Capacity, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, SMBIOSMemoryType, FormFactor, DeviceLocator, BankLabel, ConfiguredVoltage, SerialNumber FROM Win32_PhysicalMemory");
            long total = cs == null ? 0 : Wmi.Long(cs, "TotalPhysicalMemory");
            long t, a; Mem.Status(out t, out a);
            if (total <= 0) total = t;
            double used = t > 0 ? 100.0 * (t - a) / t : 0;
            var first = mods.FirstOrDefault();
            string type = "RAM"; string tn;
            if (first != null && MemType.TryGetValue((int)Wmi.Long(first, "SMBIOSMemoryType"), out tn)) type = tn;
            long speed = mods.Count == 0 ? 0 : mods.Max(x => Math.Max(Wmi.Long(x, "ConfiguredClockSpeed"), Wmi.Long(x, "Speed")));
            long slots = 0;
            var arr = Wmi.Query("SELECT MemoryDevices, MaxCapacity FROM Win32_PhysicalMemoryArray").FirstOrDefault();
            if (arr != null) slots = Wmi.Long(arr, "MemoryDevices");
            var card = new HwCard
            {
                Title = "MEMORY", IconData = Vec.Memory, AccentBrush = Accent("#8B7CFF"), Headline = Ui.Fmt(total) + "  " + type,
                Sub = (speed > 0 ? speed + " MHz  ·  " : "") + mods.Count + " module(s)" + (slots > 0 ? " of " + slots + " slots" : "")
            };
            card.Rows.Add(R("Installed", Ui.Fmt(total)));
            card.Rows.Add(R("Type", type));
            card.Rows.Add(R("Speed", speed > 0 ? speed + " MHz" : "—"));
            card.Rows.Add(R("Modules", mods.Count + (slots > 0 ? " of " + slots + " slots" : "")));
            card.Rows.Add(R("Available now", Ui.Fmt(a) + "  of  " + Ui.Fmt(t) + " usable"));
            if (total > t && t > 0) card.Rows.Add(R("Reserved by hardware", Ui.Fmt(total - t)));
            foreach (var m in mods)
            {
                string slot = Wmi.Str(m, "DeviceLocator"); if (slot.Length == 0) slot = "Module";
                long sp = Math.Max(Wmi.Long(m, "ConfiguredClockSpeed"), Wmi.Long(m, "Speed"));
                card.Rows.Add(R(slot, Ui.Fmt(Wmi.Long(m, "Capacity")) + (sp > 0 ? "  ·  " + sp + " MHz" : "") + "  ·  " + Wmi.Str(m, "Manufacturer")));
            }
            int i = 0;
            foreach (var m in mods)
            {
                i++;
                string loc = Wmi.Str(m, "DeviceLocator"); if (loc.Length == 0) loc = "Slot " + i;
                string ff; if (!FormFactor.TryGetValue((int)Wmi.Long(m, "FormFactor"), out ff)) ff = "";
                card.AdvRows.Add(R(loc, Ui.Fmt(Wmi.Long(m, "Capacity")) + "  ·  " + Wmi.Str(m, "Manufacturer") + "  ·  " + Wmi.Str(m, "PartNumber") + (ff.Length > 0 ? "  ·  " + ff : "")));
            }
            if (arr != null && Wmi.Long(arr, "MaxCapacity") > 0) card.AdvRows.Add(R("Max supported", Ui.Fmt(Wmi.Long(arr, "MaxCapacity") * 1024)));
            if (first != null && Wmi.Long(first, "TotalWidth") > 0) card.AdvRows.Add(R("Bus width / ECC", Wmi.Long(first, "DataWidth") + "-bit data  ·  " + (Wmi.Long(first, "TotalWidth") > Wmi.Long(first, "DataWidth") ? "ECC memory" : "no ECC")));
            try
            {
                var pf = Wmi.Query("SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage").FirstOrDefault();
                if (pf != null) card.AdvRows.Add(R("Page file", Wmi.Str(pf, "Name") + "  ·  " + Wmi.Long(pf, "AllocatedBaseSize") + " MB  (using " + Wmi.Long(pf, "CurrentUsage") + " MB, peak " + Wmi.Long(pf, "PeakUsage") + " MB)"));
                else card.AdvRows.Add(R("Page file", "None"));
                var osv = Wmi.Query("SELECT TotalVirtualMemorySize FROM Win32_OperatingSystem").FirstOrDefault();
                if (osv != null) card.AdvRows.Add(R("Virtual memory limit", Ui.Fmt(Wmi.Long(osv, "TotalVirtualMemorySize") * 1024)));
            }
            catch { }
            long volt = first == null ? 0 : Wmi.Long(first, "ConfiguredVoltage");
            if (volt > 0) card.AdvRows.Add(R("Voltage", (volt / 1000.0).ToString("0.00") + " V"));
            return card;
        }

        // ── graphics ──
        static long VramFromRegistry(string name)
        {
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls == null) return 0;
                    foreach (var sub in cls.GetSubKeyNames())
                    {
                        if (sub.Length != 4 || !char.IsDigit(sub[0])) continue;
                        using (var k = cls.OpenSubKey(sub))
                        {
                            if (k == null || !string.Equals(Convert.ToString(k.GetValue("DriverDesc")), name, StringComparison.OrdinalIgnoreCase)) continue;
                            var v = k.GetValue("HardwareInformation.qwMemorySize");
                            if (v is long) return (long)v;
                            var b = v as byte[]; if (b != null && b.Length >= 8) return BitConverter.ToInt64(b, 0);
                        }
                    }
                }
            }
            catch { }
            return 0;
        }

        static string VendorName(string pnp, string compat)
        {
            var m = Regex.Match(pnp ?? "", @"VEN_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            switch (m.Success ? m.Groups[1].Value.ToUpperInvariant() : "")
            {
                case "10DE": return "NVIDIA"; case "1002": case "1022": return "AMD"; case "8086": return "Intel"; case "1414": return "Microsoft (basic driver)";
            }
            return compat ?? "";
        }

        static string RegText(object v) { var b = v as byte[]; return (b != null ? Encoding.Unicode.GetString(b) : Convert.ToString(v) ?? "").Replace("\0", "").Trim(); }

        /// <summary>Extra graphics-driver values (chip, VBIOS, provider ...) from the display adapter class key.</summary>
        static Dictionary<string, string> GpuRegistry(string name)
        {
            var d = new Dictionary<string, string>();
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (cls == null) return d;
                    foreach (var sub in cls.GetSubKeyNames())
                    {
                        if (sub.Length != 4 || !char.IsDigit(sub[0])) continue;
                        using (var k = cls.OpenSubKey(sub))
                        {
                            if (k == null || !string.Equals(Convert.ToString(k.GetValue("DriverDesc")), name, StringComparison.OrdinalIgnoreCase)) continue;
                            foreach (var n in new[] { "ProviderName", "HardwareInformation.ChipType", "HardwareInformation.DACType", "HardwareInformation.BiosString", "HardwareInformation.AdapterString", "MatchingDeviceId" })
                            { var v = k.GetValue(n); if (v != null) d[n] = RegText(v); }
                            return d;
                        }
                    }
                }
            }
            catch { }
            return d;
        }

        /// <summary>NVIDIA only: clocks, power, PCIe link and VBIOS from nvidia-smi (installed with the driver). Empty for other makers.</summary>
        static Dictionary<string, string> NvidiaSmi(string gpuName)
        {
            var r = new Dictionary<string, string>();
            try
            {
                string exe = Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe");
                if (!File.Exists(exe)) exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
                if (!File.Exists(exe)) return r;
                string[] keys = { "name", "memory.total", "clocks.max.graphics", "clocks.max.memory", "clocks.current.graphics", "clocks.current.memory", "power.limit", "pcie.link.gen.current", "pcie.link.gen.max", "pcie.link.width.current", "pcie.link.width.max", "vbios_version", "temperature.gpu" };
                var psi = new ProcessStartInfo(exe, "--query-gpu=" + string.Join(",", keys) + " --format=csv,noheader,nounits") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    var read = p.StandardOutput.ReadToEndAsync();
                    if (!read.Wait(5000)) { try { p.Kill(); } catch { } return r; }
                    string[] first = null;
                    foreach (var line in read.Result.Split('\n'))
                    {
                        var f = line.Trim().Split(',').Select(x => x.Trim()).ToArray();
                        if (f.Length < keys.Length) continue;
                        if (first == null) first = f;
                        if (gpuName.IndexOf(f[0], StringComparison.OrdinalIgnoreCase) >= 0 || f[0].IndexOf(gpuName, StringComparison.OrdinalIgnoreCase) >= 0) { first = f; break; }
                    }
                    if (first != null) for (int i = 0; i < keys.Length; i++) if (!first[i].StartsWith("[") && first[i].Length > 0) r[keys[i]] = first[i];
                }
            }
            catch { }
            return r;
        }

        static IEnumerable<HwCard> GpuCards()
        {
            var res = new List<HwCard>();
            foreach (var g in Wmi.Query("SELECT Name, AdapterRAM, AdapterCompatibility, DriverVersion, DriverDate, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, VideoProcessor, AdapterDACType, PNPDeviceID, Status, CurrentBitsPerPixel, InfFilename, VideoArchitecture, VideoMemoryType FROM Win32_VideoController"))
            {
                string name = Wmi.Str(g, "Name"); if (name.Length == 0) continue;
                long vram = VramFromRegistry(name); if (vram <= 0) vram = Wmi.Long(g, "AdapterRAM");
                long w = Wmi.Long(g, "CurrentHorizontalResolution"), h = Wmi.Long(g, "CurrentVerticalResolution"), hz = Wmi.Long(g, "CurrentRefreshRate");
                var dd = Wmi.Date(g, "DriverDate");
                string pnp = Wmi.Str(g, "PNPDeviceID"), vendor = VendorName(pnp, Wmi.Str(g, "AdapterCompatibility"));
                var reg = GpuRegistry(name);
                var nv = vendor == "NVIDIA" ? NvidiaSmi(name) : new Dictionary<string, string>();
                Func<string, string> N = k => { string v; return nv.TryGetValue(k, out v) ? v : ""; };
                if (vram <= 0 && N("memory.total").Length > 0) { long mb; if (long.TryParse(N("memory.total"), out mb)) vram = mb * 1024 * 1024; }
                string drv = Wmi.Str(g, "DriverVersion");
                var card = new HwCard
                {
                    Title = "GRAPHICS", IconData = Vec.Gpu, AccentBrush = Accent("#FF6B8A"), Headline = name,
                    Sub = (vendor.Length > 0 ? vendor + "  ·  " : "") + (vram > 0 ? Ui.Fmt(vram) + " video memory  ·  " : "") + "driver " + drv
                };
                card.Rows.Add(R("Manufacturer", vendor));
                string chip; reg.TryGetValue("HardwareInformation.ChipType", out chip);
                card.Rows.Add(R("GPU chip", string.IsNullOrWhiteSpace(chip) ? Wmi.Str(g, "VideoProcessor") : chip));
                card.Rows.Add(R("Video memory", vram > 0 ? Ui.Fmt(vram) : "Shared with system memory"));
                if (N("clocks.max.graphics").Length > 0) card.Rows.Add(R("Max GPU clock", N("clocks.max.graphics") + " MHz"));
                if (N("clocks.max.memory").Length > 0) card.Rows.Add(R("Max memory clock", N("clocks.max.memory") + " MHz"));
                if (N("power.limit").Length > 0) card.Rows.Add(R("Power limit", N("power.limit") + " W"));
                if (N("pcie.link.gen.max").Length > 0) card.Rows.Add(R("PCI Express", "Gen " + N("pcie.link.gen.current") + " x" + N("pcie.link.width.current") + "  (card supports Gen " + N("pcie.link.gen.max") + " x" + N("pcie.link.width.max") + ")"));
                card.Rows.Add(R("Driver version", drv));
                card.Rows.Add(R("Driver date", dd.HasValue ? dd.Value.ToString("yyyy-MM-dd") : ""));
                string prov; if (reg.TryGetValue("ProviderName", out prov)) card.Rows.Add(R("Driver provider", prov));
                card.Rows.Add(R("Resolution", w > 0 ? w + " × " + h + (hz > 0 ? "  @ " + hz + " Hz" : "") : "Not active"));
                card.Rows.Add(R("Status", Wmi.Str(g, "Status")));
                string bios; if (reg.TryGetValue("HardwareInformation.BiosString", out bios) && bios.Length > 0) card.AdvRows.Add(R("Video BIOS", bios));
                if (N("vbios_version").Length > 0) card.AdvRows.Add(R("VBIOS version", N("vbios_version")));
                if (N("temperature.gpu").Length > 0) card.AdvRows.Add(R("Temperature now", N("temperature.gpu") + " °C"));
                if (N("clocks.current.graphics").Length > 0) card.AdvRows.Add(R("GPU clock now", N("clocks.current.graphics") + " MHz  ·  memory " + N("clocks.current.memory") + " MHz"));
                string adapter; if (reg.TryGetValue("HardwareInformation.AdapterString", out adapter)) card.AdvRows.Add(R("Adapter string", adapter));
                card.AdvRows.Add(R("Video processor", Wmi.Str(g, "VideoProcessor")));
                card.AdvRows.Add(R("DAC type", Wmi.Str(g, "AdapterDACType")));
                card.AdvRows.Add(R("Color depth", Wmi.Long(g, "CurrentBitsPerPixel") > 0 ? Wmi.Long(g, "CurrentBitsPerPixel") + " bit" : ""));
                card.AdvRows.Add(R("Driver file", Wmi.Str(g, "InfFilename")));
                var ids = Regex.Match(pnp, @"VEN_(\w{4})&DEV_(\w{4})(&SUBSYS_(\w{8}))?", RegexOptions.IgnoreCase);
                if (ids.Success) card.AdvRows.Add(R("Vendor / device ID", ids.Groups[1].Value.ToUpper() + " / " + ids.Groups[2].Value.ToUpper() + (ids.Groups[4].Success ? "  ·  subsystem " + ids.Groups[4].Value.ToUpper() : "")));
                card.AdvRows.Add(R("Hardware ID", pnp));
                res.Add(card);
            }
            return res;
        }

        // ── motherboard & firmware ──
        static HwCard BoardCard(System.Management.ManagementBaseObject cs)
        {
            var bb = Wmi.Query("SELECT Manufacturer, Product, SerialNumber, Version FROM Win32_BaseBoard").FirstOrDefault();
            var bios = Wmi.Query("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate, SerialNumber, SMBIOSMajorVersion, SMBIOSMinorVersion FROM Win32_BIOS").FirstOrDefault();
            var prod = Wmi.Query("SELECT UUID, Version FROM Win32_ComputerSystemProduct").FirstOrDefault();
            if (bb == null && bios == null) return null;
            var rd = bios == null ? null : Wmi.Date(bios, "ReleaseDate");
            var card = new HwCard
            {
                Title = "MOTHERBOARD & BIOS", IconData = Vec.Board, AccentBrush = Accent("#FFB84D"),
                Headline = Squash((bb == null ? "" : Wmi.Str(bb, "Manufacturer") + " " + Wmi.Str(bb, "Product"))),
                Sub = bios == null ? "" : "BIOS " + Wmi.Str(bios, "SMBIOSBIOSVersion") + (rd.HasValue ? "  ·  " + rd.Value.ToString("yyyy-MM-dd") : "")
            };
            if (cs != null) card.Rows.Add(R("System", Squash(Wmi.Str(cs, "Manufacturer") + " " + Wmi.Str(cs, "Model"))));
            if (bb != null) { card.Rows.Add(R("Board", Wmi.Str(bb, "Product"))); card.Rows.Add(R("Board vendor", Wmi.Str(bb, "Manufacturer"))); }
            if (bios != null) { card.Rows.Add(R("BIOS vendor", Wmi.Str(bios, "Manufacturer"))); card.Rows.Add(R("BIOS version", Wmi.Str(bios, "SMBIOSBIOSVersion"))); card.Rows.Add(R("BIOS date", rd.HasValue ? rd.Value.ToString("yyyy-MM-dd") : "")); }
            int fw; string mode = "—"; try { if (GetFirmwareType(out fw)) mode = fw == 2 ? "UEFI" : fw == 1 ? "Legacy BIOS" : "—"; } catch { }
            card.AdvRows.Add(R("Firmware mode", mode));
            if (bios != null) card.AdvRows.Add(R("SMBIOS version", Wmi.Str(bios, "SMBIOSMajorVersion") + "." + Wmi.Str(bios, "SMBIOSMinorVersion")));
            if (bb != null) { card.AdvRows.Add(R("Board version", Wmi.Str(bb, "Version"))); card.AdvRows.Add(R("Board serial", Wmi.Str(bb, "SerialNumber"))); }
            if (prod != null) card.AdvRows.Add(R("System UUID", Wmi.Str(prod, "UUID")));
            if (bios != null) card.AdvRows.Add(R("BIOS serial", Wmi.Str(bios, "SerialNumber")));
            if (cs != null)
            {
                var more = Wmi.Query("SELECT SystemSKUNumber, SystemFamily FROM Win32_ComputerSystem").FirstOrDefault();
                if (more != null) { card.AdvRows.Add(R("System SKU", Wmi.Str(more, "SystemSKUNumber"))); card.AdvRows.Add(R("System family", Wmi.Str(more, "SystemFamily"))); }
            }
            try
            {
                var enc = Wmi.Query("SELECT ChassisTypes, Manufacturer, SerialNumber FROM Win32_SystemEnclosure").FirstOrDefault();
                var ct = enc == null ? null : enc["ChassisTypes"] as ushort[];
                if (ct != null && ct.Length > 0)
                {
                    string[] names = { "", "Other", "Unknown", "Desktop", "Low-profile desktop", "Pizza box", "Mini tower", "Tower", "Portable", "Laptop", "Notebook", "Handheld", "Docking station", "All-in-one", "Sub-notebook", "Space-saving", "Lunch box", "Main system chassis", "Expansion chassis", "Sub-chassis", "Bus expansion", "Peripheral", "RAID", "Rack-mount", "Sealed-case PC", "Multi-system", "CompactPCI", "AdvancedTCA", "Blade", "Blade enclosure", "Tablet", "Convertible", "Detachable", "IoT gateway", "Embedded PC", "Mini PC", "Stick PC" };
                    card.Rows.Add(R("Chassis type", ct[0] < names.Length ? names[ct[0]] : "Type " + ct[0]));
                }
            }
            catch { }
            return card;
        }

        // ── storage ──
        static HwCard StorageCard()
        {
            List<PhysDisk> disks;
            try { disks = DiskInfo.Get(300); } catch { return null; }
            if (disks == null || disks.Count == 0) return null;
            long total = disks.Sum(d => d.Size);
            var card = new HwCard
            {
                Title = "STORAGE", IconData = Vec.Drives, AccentBrush = Accent("#3DDC97"), Headline = disks.Count + " drive(s)  ·  " + Ui.Fmt(total),
                Sub = string.Join("  ·  ", disks.Select(d => d.Kind).Distinct())
            };
            long vTotal = disks.Sum(d => d.Volumes.Sum(v => v.Size)), vFree = disks.Sum(d => d.Volumes.Sum(v => v.Free));
            if (vTotal > 0)
            {
                card.Rows.Add(R("Space used", Ui.Fmt(vTotal - vFree) + "  of  " + Ui.Fmt(vTotal) + "  (" + (100.0 * (vTotal - vFree) / vTotal).ToString("0") + "%)"));
                card.Rows.Add(R("Free space", Ui.Fmt(vFree)));
            }
            card.Rows.Add(R("Drive types", string.Join("  ·  ", disks.GroupBy(d => d.Kind).Select(g => g.Count() + " × " + g.Key))));
            int weak = disks.Count(d => d.Score < 70);
            card.Rows.Add(R("Drives needing attention", weak == 0 ? "None - all drives look healthy" : weak + " drive(s) with a health score below 70"));
            foreach (var d in disks)
            {
                string nm = Squash(string.IsNullOrWhiteSpace(d.Model) ? "Disk " + d.Number : d.Model);
                string meta = Ui.Fmt(d.Size) + "  ·  " + d.Kind + (d.Letters.Length > 0 ? "  ·  " + d.Letters : "");
                long used = d.Volumes.Sum(v => v.Size - v.Free), vsize = d.Volumes.Sum(v => v.Size);
                card.Disks.Add(new HwDisk { Name = nm, Meta = meta, Kind = d.Kind, Health = d.Score.ToString(), Grade = d.Grade, Tone = ScoreTheme.Solid(d.Score), Percent = vsize > 0 ? 100.0 * used / vsize : 0,
                    Plain = "Disk " + d.Number + ": " + nm + "  ·  " + meta + "  ·  health " + d.Score + "/100 (" + d.Grade + ")" });
            }
            foreach (var d in disks)
            {
                card.AdvRows.Add(R("Disk " + d.Number + " volumes", d.Letters.Length > 0 ? d.Letters : "—"));
                card.AdvRows.Add(R("Disk " + d.Number + " interface", (d.Bus.Length > 0 ? d.Bus : d.Interface) + (d.PartStyle.Length > 0 ? "  ·  " + d.PartStyle : "")));
                card.AdvRows.Add(R("Disk " + d.Number + " firmware", d.Firmware));
            }
            return card;
        }

        // ── displays ──
        static string UShort(object o)
        {
            var a = o as ushort[]; if (a == null) return "";
            var sb = new StringBuilder(); foreach (var u in a) { if (u == 0) break; sb.Append((char)u); }
            return sb.ToString().Trim();
        }

        static string ModelOf(string instance) { var m = Regex.Match(instance ?? "", @"DISPLAY[\\#]([A-Za-z0-9]+)[\\#]", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value.ToUpperInvariant() : ""; }

        static HwCard DisplayCard()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens == null || screens.Length == 0) return null;
            var primary = screens.FirstOrDefault(s => s.Primary) ?? screens[0];
            var mons = DisplayCfg.Read();

            // monitor identity (EDID) and physical size, matched by the model code in the device path
            var ids = new Dictionary<string, System.Management.ManagementBaseObject>(); var sizes = new Dictionary<string, Tuple<long, long>>();
            try { foreach (var m in Wmi.Query(@"root\wmi", "SELECT InstanceName, ManufacturerName, UserFriendlyName, YearOfManufacture, WeekOfManufacture FROM WmiMonitorID")) ids[ModelOf(Wmi.Str(m, "InstanceName"))] = m; } catch { }
            try { foreach (var m in Wmi.Query(@"root\wmi", "SELECT InstanceName, MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams")) sizes[ModelOf(Wmi.Str(m, "InstanceName"))] = Tuple.Create(Wmi.Long(m, "MaxHorizontalImageSize"), Wmi.Long(m, "MaxVerticalImageSize")); } catch { }

            Func<System.Windows.Forms.Screen, DisplayCfg.Mon> monOf = sc => mons.FirstOrDefault(x => string.Equals(x.GdiName, sc.DeviceName, StringComparison.OrdinalIgnoreCase));
            var pm = monOf(primary);
            var card = new HwCard
            {
                Title = "DISPLAY", IconData = Vec.Monitor, AccentBrush = Accent("#5B9BFF"),
                Headline = primary.Bounds.Width + " × " + primary.Bounds.Height + (pm != null && pm.Hz > 0 ? "  @ " + Math.Round(pm.Hz) + " Hz" : ""),
                Sub = screens.Length + " display(s) connected"
            };
            int i = 0;
            foreach (var s in screens)
            {
                i++;
                var mon = monOf(s);
                System.Management.ManagementBaseObject id = null; Tuple<long, long> sz = null;
                if (mon != null) { ids.TryGetValue(mon.ModelCode, out id); sizes.TryGetValue(mon.ModelCode, out sz); }
                string name = mon != null && mon.Name.Length > 0 ? mon.Name : (id != null ? UShort(id["UserFriendlyName"]) : "");
                if (name.Length == 0) name = "Generic monitor";
                card.Rows.Add(R("Display " + i, name + (s.Primary ? "  (primary)" : "")));
                card.Rows.Add(R("Resolution", s.Bounds.Width + " × " + s.Bounds.Height));
                card.Rows.Add(R("Refresh rate", mon != null && mon.Hz > 0 ? Math.Round(mon.Hz) + " Hz" : "—"));
                if (mon != null && mon.BitsPerChannel > 0)
                    card.Rows.Add(R("Color depth", mon.BitsPerChannel + " bits per channel  (" + mon.BitsPerChannel * 3 + "-bit color)"));
                else card.Rows.Add(R("Color depth", s.BitsPerPixel + "-bit desktop"));
                card.Rows.Add(R("HDR", mon == null ? "—" : mon.HdrSupported ? (mon.HdrEnabled ? "Supported  ·  turned on" : "Supported  ·  turned off in Windows") : "Not supported"));
                if (mon != null && mon.Connection.Length > 0) card.Rows.Add(R("Connection", mon.Connection));
                if (sz != null && sz.Item1 > 0 && sz.Item2 > 0)
                    card.Rows.Add(R("Screen size", Math.Round(Math.Sqrt(sz.Item1 * sz.Item1 + sz.Item2 * sz.Item2) / 2.54, 1).ToString("0.#") + " inch  ·  " + sz.Item1 + " × " + sz.Item2 + " cm"));

                string pre = "Display " + i + " · ";
                if (mon != null && mon.Hz > 0) card.AdvRows.Add(R(pre + "exact rate", mon.Hz.ToString("0.###") + " Hz"));
                if (mon != null && mon.ColorEncoding < 5 && DisplayCfg.Encoding(mon.ColorEncoding).Length > 0) card.AdvRows.Add(R(pre + "color format", DisplayCfg.Encoding(mon.ColorEncoding)));
                if (mon != null) card.AdvRows.Add(R(pre + "wide gamut", mon.WideColor ? "Enforced by Windows" : mon.HdrSupported ? "Available" : "Standard (sRGB)"));
                if (id != null)
                {
                    card.AdvRows.Add(R(pre + "maker code", UShort(id["ManufacturerName"])));
                    card.AdvRows.Add(R(pre + "made in", Wmi.Long(id, "YearOfManufacture") > 0 ? "week " + Wmi.Long(id, "WeekOfManufacture") + " of " + Wmi.Long(id, "YearOfManufacture") : ""));
                }
                if (mon != null) card.AdvRows.Add(R(pre + "model code", mon.ModelCode));
                card.AdvRows.Add(R(pre + "position", s.Bounds.X + ", " + s.Bounds.Y));
                card.AdvRows.Add(R(pre + "work area", s.WorkingArea.Width + " × " + s.WorkingArea.Height));
                card.AdvRows.Add(R(pre + "device", s.DeviceName));
            }
            return card;
        }

        // ── network ──
        static HwCard NetworkCard()
        {
            try
            {
                var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel).ToList();
                var up = nics.Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up).ToList();
                var main = up.FirstOrDefault(n => n.GetIPProperties().GatewayAddresses.Any()) ?? up.FirstOrDefault();
                if (main == null) return new HwCard { Title = "NETWORK", IconData = Vec.Network, AccentBrush = Accent("#3DD6F5"), Headline = "Not connected", Sub = "No active network adapter" };
                var ip = main.GetIPProperties();
                var v4 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                var gw = ip.GatewayAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                string mac = string.Join(":", main.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
                string kind = main.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : main.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ? "Ethernet" : main.NetworkInterfaceType.ToString();
                string speed = main.Speed > 0 ? (main.Speed >= 1000000000 ? (main.Speed / 1e9).ToString("0.#") + " Gbps" : (main.Speed / 1e6).ToString("0") + " Mbps") : "—";
                var card = new HwCard { Title = "NETWORK", IconData = Vec.Network, AccentBrush = Accent("#3DD6F5"), Headline = main.Description, Sub = kind + "  ·  " + speed + "  ·  connected" };
                card.Rows.Add(R("IPv4 address", v4 == null ? "" : v4.Address.ToString()));
                card.Rows.Add(R("Gateway", gw == null ? "" : gw.Address.ToString()));
                card.Rows.Add(R("DNS", string.Join(", ", ip.DnsAddresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(a => a.ToString()))));
                card.Rows.Add(R("MAC address", mac));
                card.Rows.Add(R("Link speed", speed));
                var v6 = ip.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal);
                if (v4 != null && v4.IPv4Mask != null) card.Rows.Add(R("Subnet mask", v4.IPv4Mask.ToString()));
                if (main.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                {
                    var wl = WifiInfo();
                    string val;
                    if (wl.TryGetValue("SSID", out val)) card.Rows.Add(R("Wi-Fi network", val));
                    if (wl.TryGetValue("Signal", out val)) card.Rows.Add(R("Signal strength", val));
                    string radio = wl.TryGetValue("Radio type", out val) ? val : "", chan = wl.TryGetValue("Channel", out val) ? val : "";
                    if (radio.Length + chan.Length > 0) card.Rows.Add(R("Wi-Fi standard / channel", radio + (chan.Length > 0 ? "  ·  channel " + chan : "")));
                    if (wl.TryGetValue("Receive rate (Mbps)", out val)) card.AdvRows.Add(R("Receive rate", val + " Mbps"));
                    if (wl.TryGetValue("Transmit rate (Mbps)", out val)) card.AdvRows.Add(R("Transmit rate", val + " Mbps"));
                    if (wl.TryGetValue("Authentication", out val)) card.AdvRows.Add(R("Security", val));
                }
                try
                {
                    var v4p = ip.GetIPv4Properties(); var st = main.GetIPv4Statistics();
                    card.AdvRows.Add(R("DHCP", v4p.IsDhcpEnabled ? "Enabled" + (ip.DhcpServerAddresses.Count > 0 ? "  ·  server " + ip.DhcpServerAddresses[0] : "") : "Static address"));
                    card.AdvRows.Add(R("MTU", v4p.Mtu.ToString()));
                    card.AdvRows.Add(R("Data received", Ui.Fmt(st.BytesReceived)));
                    card.AdvRows.Add(R("Data sent", Ui.Fmt(st.BytesSent)));
                }
                catch { }
                card.AdvRows.Add(R("IPv6 address", v6 == null ? "" : v6.Address.ToString()));
                card.AdvRows.Add(R("DNS suffix", ip.DnsSuffix));
                card.AdvRows.Add(R("Host name", System.Net.Dns.GetHostName()));
                foreach (var n in nics.Where(x => x != main)) card.AdvRows.Add(R(n.Name, n.Description + "  ·  " + n.OperationalStatus));
                return card;
            }
            catch { return null; }
        }

        /// <summary>Current Wi-Fi connection from "netsh wlan show interfaces" (English Windows only; empty otherwise).</summary>
        static Dictionary<string, string> WifiInfo()
        {
            var d = new Dictionary<string, string>();
            try
            {
                string o;
                if (Fixer.Exec("netsh.exe", "wlan show interfaces", 6000, out o) != 0) return d;
                foreach (var line in o.Split('\n'))
                {
                    int i = line.IndexOf(':'); if (i <= 0) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                    if (k.Length > 0 && !d.ContainsKey(k)) d[k] = v;
                }
            }
            catch { }
            return d;
        }

        // ── operating system ──
        static HwCard OsCard(System.Management.ManagementBaseObject os, System.Management.ManagementBaseObject cs, DateTime? boot)
        {
            if (os == null) return null;
            string display = "", edition = "", ubr = "";
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    if (k != null) { display = Convert.ToString(k.GetValue("DisplayVersion") ?? k.GetValue("ReleaseId")); edition = Convert.ToString(k.GetValue("EditionID")); ubr = Convert.ToString(k.GetValue("UBR")); }
            }
            catch { }
            var inst = Wmi.Date(os, "InstallDate");
            var card = new HwCard
            {
                Title = "OPERATING SYSTEM", IconData = Vec.Windows, AccentBrush = Accent("#8B7CFF"), Headline = Squash(Wmi.Str(os, "Caption")),
                Sub = (display.Length > 0 ? "Version " + display + "  ·  " : "") + "build " + Wmi.Str(os, "BuildNumber") + (ubr.Length > 0 ? "." + ubr : "") + "  ·  " + Wmi.Str(os, "OSArchitecture")
            };
            card.Rows.Add(R("Computer name", Environment.MachineName));
            card.Rows.Add(R("User", Environment.UserName));
            card.Rows.Add(R("Installed", inst.HasValue ? inst.Value.ToString("yyyy-MM-dd") : ""));
            card.Rows.Add(R("Last boot", boot.HasValue ? boot.Value.ToString("yyyy-MM-dd HH:mm") : ""));
            card.Rows.Add(R("Uptime", boot.HasValue ? (DateTime.Now - boot.Value).Days + " d " + (DateTime.Now - boot.Value).Hours + " h " + (DateTime.Now - boot.Value).Minutes + " min" : ""));
            card.AdvRows.Add(R("Edition", edition));
            card.AdvRows.Add(R("Windows folder", Wmi.Str(os, "WindowsDirectory")));
            card.AdvRows.Add(R("Registered owner", Wmi.Str(os, "RegisteredUser")));
            card.AdvRows.Add(R("Time zone", TimeZoneInfo.Local.DisplayName));
            card.AdvRows.Add(R("System language", System.Globalization.CultureInfo.InstalledUICulture.DisplayName));
            card.AdvRows.Add(R("Domain / workgroup", cs == null ? "" : Wmi.Str(cs, "Domain")));
            card.AdvRows.Add(R(".NET Framework", Environment.Version.ToString()));
            string act = "Unknown";
            try
            {
                var lic = Wmi.Query("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f'").FirstOrDefault();
                if (lic != null) act = Wmi.Long(lic, "LicenseStatus") == 1 ? "Activated" : "Not activated";
            }
            catch { }
            card.Rows.Add(R("Activation", act));
            try
            {
                var sysRoot = Path.GetPathRoot(Environment.SystemDirectory); var di = new DriveInfo(sysRoot);
                card.Rows.Add(R("System drive", sysRoot.TrimEnd('\\') + "  ·  " + Ui.Fmt(di.AvailableFreeSpace) + " free of " + Ui.Fmt(di.TotalSize)));
            }
            catch { }
            card.AdvRows.Add(R("Build lab", HklmValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "BuildLabEx")));
            card.AdvRows.Add(R("Fast startup", OnOff(HklmValue(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled"), "On (shut down keeps part of the session)", "Off")));
            card.AdvRows.Add(R("Hibernation file", File.Exists(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "hiberfil.sys")) ? "Present" : "Not present"));
            try { card.AdvRows.Add(R("Running processes", Process.GetProcesses().Length.ToString())); } catch { }
            card.AdvRows.Add(R("Product name", HklmValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName")));
            return card;
        }

        // ── battery (laptops only) ──
        static HwCard BatteryCard()
        {
            var ps = System.Windows.Forms.SystemInformation.PowerStatus;
            if (ps.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) return null;
            double pct = Math.Max(0, Math.Min(100, ps.BatteryLifePercent * 100));
            bool ac = ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            var card = new HwCard
            {
                Title = "BATTERY", IconData = Vec.Battery, AccentBrush = Accent("#3DDC97"), Headline = pct.ToString("0") + "%",
                Sub = ac ? "Plugged in" : "On battery", HasGauge = true, GaugeLabel = "CHARGE", GaugeValue = pct, GaugeText = pct.ToString("0") + "%", GaugeBrush = ScoreTheme.Gradient((int)pct)
            };
            card.Rows.Add(R("Power source", ac ? "AC power" : "Battery"));
            card.Rows.Add(R("Time remaining", ps.BatteryLifeRemaining > 0 ? TimeSpan.FromSeconds(ps.BatteryLifeRemaining).Hours + " h " + TimeSpan.FromSeconds(ps.BatteryLifeRemaining).Minutes + " min" : "—"));
            try
            {
                var design = Wmi.Query(@"root\wmi", "SELECT DesignedCapacity FROM BatteryStaticData").FirstOrDefault();
                var full = Wmi.Query(@"root\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity").FirstOrDefault();
                if (design != null && full != null && Wmi.Long(design, "DesignedCapacity") > 0)
                {
                    long dc = Wmi.Long(design, "DesignedCapacity"), fc = Wmi.Long(full, "FullChargedCapacity");
                    card.Rows.Add(R("Battery health", (100.0 * fc / dc).ToString("0") + "% of original capacity"));
                    card.AdvRows.Add(R("Design capacity", dc + " mWh"));
                    card.AdvRows.Add(R("Full charge capacity", fc + " mWh"));
                }
            }
            catch { }
            try
            {
                var cyc = Wmi.Query(@"root\wmi", "SELECT CycleCount FROM BatteryCycleCount").FirstOrDefault();
                if (cyc != null && Wmi.Long(cyc, "CycleCount") > 0) card.Rows.Add(R("Charge cycles", Wmi.Long(cyc, "CycleCount").ToString()));
                var b = Wmi.Query("SELECT Name, Chemistry, DesignVoltage, DeviceID FROM Win32_Battery").FirstOrDefault();
                if (b != null)
                {
                    string[] chem = { "", "Other", "Unknown", "Lead acid", "Nickel cadmium", "Nickel metal hydride", "Lithium-ion", "Zinc air", "Lithium polymer" };
                    long cm = Wmi.Long(b, "Chemistry");
                    card.AdvRows.Add(R("Battery name", Wmi.Str(b, "Name")));
                    card.AdvRows.Add(R("Chemistry", cm > 0 && cm < chem.Length ? chem[cm] : ""));
                    if (Wmi.Long(b, "DesignVoltage") > 0) card.AdvRows.Add(R("Design voltage", (Wmi.Long(b, "DesignVoltage") / 1000.0).ToString("0.0") + " V"));
                }
            }
            catch { }
            return card;
        }

        // ── security ──
        static HwCard SecurityCard(List<System.Management.ManagementBaseObject> cpu)
        {
            string secure = "Not available";
            try { using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State")) if (k != null) secure = Convert.ToInt32(k.GetValue("UEFISecureBootEnabled") ?? 0) == 1 ? "On" : "Off"; } catch { }
            string tpm = "Not found", tpmVer = "";
            try
            {
                var t = Wmi.Query(@"root\cimv2\security\microsofttpm", "SELECT IsEnabled_InitialValue, SpecVersion FROM Win32_Tpm").FirstOrDefault();
                if (t != null) { tpm = Wmi.Bool(t, "IsEnabled_InitialValue") ? "Present and enabled" : "Present but disabled"; tpmVer = Wmi.Str(t, "SpecVersion").Split(',')[0]; }
            }
            catch { }
            string defender = "Unknown";
            try { var m = Wmi.Query(@"root\Microsoft\Windows\Defender", "SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus").FirstOrDefault(); if (m != null) defender = Wmi.Bool(m, "RealTimeProtectionEnabled") ? "Real-time protection on" : "Real-time protection off"; } catch { }
            bool virt = cpu.Any(c => Wmi.Bool(c, "VirtualizationFirmwareEnabled"));
            bool admin = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            var card = new HwCard { Title = "SECURITY", IconData = Vec.Shield, AccentBrush = Accent("#FF9F6B"), Headline = defender, Sub = "Secure Boot " + secure + "  ·  TPM " + (tpmVer.Length > 0 ? tpmVer : tpm) };
            card.Rows.Add(R("Secure Boot", secure));
            card.Rows.Add(R("TPM", tpm + (tpmVer.Length > 0 ? "  (" + tpmVer + ")" : "")));
            card.Rows.Add(R("Antivirus", defender));
            string fw = "";
            try
            {
                var prof = new[] { "StandardProfile", "PublicProfile", "DomainProfile" };
                int on = prof.Count(pn => HklmValue(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + pn, "EnableFirewall") == "1");
                fw = on == prof.Length ? "On for all networks" : on == 0 ? "Off" : "On for " + on + " of " + prof.Length + " network types";
            }
            catch { }
            card.Rows.Add(R("Firewall", fw));
            string uac = HklmValue(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");
            card.Rows.Add(R("User Account Control", OnOff(uac, "On", "Off")));
            string bl = "";
            try
            {
                var vol = Wmi.Query(@"root\cimv2\Security\MicrosoftVolumeEncryption", "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume").FirstOrDefault(v => string.Equals(Wmi.Str(v, "DriveLetter"), Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                if (vol != null) bl = Wmi.Long(vol, "ProtectionStatus") == 1 ? "On for the system drive" : "Off for the system drive";
            }
            catch { }
            card.Rows.Add(R("BitLocker", bl.Length > 0 ? bl : "Not available"));
            string hvci = HklmValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
            card.AdvRows.Add(R("Memory integrity (HVCI)", OnOff(hvci, "On", "Off")));
            try
            {
                var av = Wmi.Query(@"root\SecurityCenter2", "SELECT displayName FROM AntivirusProduct").Select(a => Wmi.Str(a, "displayName")).Where(n => n.Length > 0).Distinct().ToList();
                if (av.Count > 0) card.AdvRows.Add(R("Registered antivirus", string.Join(", ", av)));
                var mp = Wmi.Query(@"root\Microsoft\Windows\Defender", "SELECT AntivirusSignatureAge, AntivirusSignatureLastUpdated FROM MSFT_MpComputerStatus").FirstOrDefault();
                if (mp != null) card.AdvRows.Add(R("Virus definitions", Wmi.Long(mp, "AntivirusSignatureAge") + " day(s) old"));
            }
            catch { }
            card.AdvRows.Add(R("CPU virtualization", virt ? "Enabled" : "Disabled or not supported"));
            card.AdvRows.Add(R("Running as administrator", admin ? "Yes" : "No"));
            return card;
        }

        /// <summary>Plain-text version of the report for sharing with support.</summary>
        public static string ToText(HwReport rep)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CyberDisk - PC hardware report");
            sb.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "   PC: " + rep.PcName);
            sb.AppendLine(new string('=', 70));
            foreach (var c in rep.Cards)
            {
                sb.AppendLine(); sb.AppendLine(c.Title + "  -  " + c.Headline); if (!string.IsNullOrEmpty(c.Sub)) sb.AppendLine("  " + c.Sub);
                foreach (var dk in c.Disks) sb.AppendLine("  " + dk.Plain);
                foreach (var r in c.Rows) sb.AppendLine("  " + r.K.PadRight(26) + r.V);
                if (c.AdvRows.Count > 0) { sb.AppendLine("  -- advanced --"); foreach (var r in c.AdvRows) sb.AppendLine("  " + r.K.PadRight(26) + r.V); }
            }
            return sb.ToString();
        }
    }
}
