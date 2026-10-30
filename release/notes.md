## [1.4.2] - 2026-10-27

### Added
- `tools/release.ps1`: one command builds CyberDisk.exe, writes its SHA-256, commits, pushes, tags and publishes the GitHub release with the .exe attached.

### Changed
- **My PC** cards now fill the page in even columns (no empty gaps next to a tall card).
- **Tray menu**: every entry has an aligned vector icon, the My PC entry uses the processor icon, and the "close button hides to the tray" option shows a clear on / off icon.
- Temperatures card: removed the LibreHardwareMonitor note and button.
- The GitHub workflow is now started by hand (Actions > Release (manual)); the normal way to publish is `tools/release.ps1`.

---
**SHA-256:** `cd9c649d9b645ed500b1fa7537ed9f295a81e550d09a070a39f1b5fa87f07af8`

Windows may show a SmartScreen notice for a new unsigned program: click **More info**, then **Run anyway**.
