## [1.5.1] - 2026-10-31

### Added
- **Startup apps**: a remove button on every entry deletes it from Windows startup (the program itself stays installed). A copy of each removed entry is kept, and RESTORE REMOVED puts them back. Works for the registry Run lists and for startup-folder shortcuts.

### Fixed
- **My PC > Processor**: virtualization showed "Disabled" on PCs where it is on. Windows hides the firmware flag once its own hypervisor runs, so the hypervisor and the processor feature flag are checked too, and the BIOS case is now worded clearly.

### Removed
- The screenshot tour and the screenshots section of the README.

---
**SHA-256:** `803b974bba473c75c60e0b270b9bcc82f3809ab1aac023afe89e7f30d30529de`

Windows may show a SmartScreen notice for a new unsigned program: click **More info**, then **Run anyway**.
