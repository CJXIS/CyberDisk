## [1.5.1] - 2026-10-31

### Added
- **Startup apps**: a remove button on every entry deletes it from Windows startup (the program itself stays installed). A copy of each removed entry is kept, and RESTORE REMOVED puts them back. Works for the registry Run lists and for startup-folder shortcuts.

### Fixed
- **My PC > Processor**: virtualization showed "Disabled" on PCs where it is on. Windows hides the firmware flag once its own hypervisor runs, so the hypervisor and the processor feature flag are checked too, and the BIOS case is now worded clearly.

### Removed
- The screenshot tour and the screenshots section of the README.

---
**SHA-256:** `55d69f313724d821461be822c9bad9411ffe970641daf70bced2afbde788f601`

Windows may show a SmartScreen notice for a new unsigned program: click **More info**, then **Run anyway**.
