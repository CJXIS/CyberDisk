# Changelog

All notable changes to CyberDisk are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/).

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
