# Changelog

All notable changes to CyberDisk are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/).

## [1.3.2] - 2026-10-25

### Fixed
- The app did not start: the startup-apps panel declared its grid rows twice, which Windows rejects when it loads the window. Removed the duplicate.

## [1.3.1] - 2026-10-25

### Fixed
- Startup apps switch used a template trigger that Windows can reject; it is now built with plain alignment.
- Every feature is now set up inside a guard: if one fails, the rest of the app still starts, the failing part is named in a message and in `%TEMP%\CyberDisk-uninstall.log`.
- If the window itself cannot start, a message with the reason is shown and written to `%TEMP%\CyberDisk-crash.log` instead of closing silently.

## [1.3.0] - 2026-10-25

### Added
- **Health check**: IGNORE / RESTORE button on every finding. Ignored findings are left out of the score and out of FIX ALL, are remembered between checks, and can be shown again with SHOW IGNORED.
- **FIX ALL**: live progress panel (step x of y, current repair), a STOP button, quick repairs first and long ones last, a clear summary at the end with RE-CHECK and RESTART NOW, and one single restart offer.
- **App Errors**: REMOVE FROM LIST on every app, CLEAR LIST for all, and RESTORE REMOVED. An app comes back only if it crashes again; the Windows event log is never touched.
- **File preview** (Drives > folder): created / modified / opened dates, attributes, exact size, image size, a text preview of text files, a "what takes the space" breakdown by file type for folders, largest items with share bars, and OPEN / SHOW IN EXPLORER / COPY PATH buttons. All values are selectable.
- **My PC**: many more details on every component - hyper-threading, virtualization, power plan, instruction sets, microcode, memory slots / ECC / page file, chassis and board data, storage totals, Wi-Fi network / signal / standard, DHCP, subnet, traffic, Windows activation, fast startup, battery cycles and chemistry, firewall, UAC, BitLocker, memory integrity and registered antivirus.
- **Startup apps**: each entry shows its icon, publisher, scope badge and status; modern ON / OFF switch; search, ALL / ON / OFF filters; TURN ALL OFF; entries whose file no longer exists are flagged.
- Vector icons on the remaining buttons of the app.

### Changed
- **Fixes are more reliable**: one repair runs at a time, running programs can be stopped at once, services are started twice before giving up, administrator rights are checked first, and every result is written to the fix log.
- **Speed and smoothness**: colours and brushes are cached, page transitions are drawn from a cached picture, lists scroll by pixel, hardware cards are read in parallel, MY PC and disk data are prepared in the background shortly after start, and .NET remembers what to pre-compile for a faster next start.

## [1.2.0] - 2026-10-24

### Added
- **My PC**: measured maximum (boost) CPU clock next to the base and live clock.
- **My PC - Graphics**: manufacturer, GPU chip, video BIOS, driver provider, PCI IDs; on NVIDIA cards also max GPU / memory clock, power limit, PCI Express link and VBIOS version.
- **My PC - Display**: real refresh rate, colour depth (bits per channel), HDR support and state, connection type, screen size, monitor model and manufacture date for every monitor.
- **Apps**: install date column (sortable) and a richer preview panel with advanced details - install scope, architecture, installer type and product code, install source, modify / repair support, uninstall commands, help and support links, registry key, measured folder size and running processes. New buttons to open the install folder and copy all details.
- **Disk Health and My PC**: export a branded report as PDF or picture, save as text, or copy all text.
- **My PC**: every value is selectable and copyable.
- Consistent vector icon set for the sidebar, My PC cards and report headers.
- Developer email now opens a choice: send an email or copy the address.

### Changed
- **My PC**: removed CPU load and memory usage; storage is shown as clean per-disk rows with health and usage; cards adapt to one or two columns.
- **Disk Health**: volumes show label, file system, free space and usage; partitions are listed under Advanced.
- Removed the separate "Copy email" button.

### Fixed
- Volumes were empty on Disk Health, so the DISK HEALTH button always opened the first disk. Drive letters are now read from three independent sources.
- "A restart is pending" stayed on after restarting because of a leftover registry value. Only real Windows restart flags are used now, and the reason is shown.
- CPU max / current clock values were wrong; the current clock now follows turbo boost and power saving.
- Disk health no longer appears in the Health check.

## [1.1.0]

- Health check with fix buttons, application crash detection, Arabic language with right-to-left layout, settings page, disk health page, My PC page, About section, project split into organised files.

## [1.0.0]

- First release: drive analysis, cleanup, app uninstaller, PC booster.
