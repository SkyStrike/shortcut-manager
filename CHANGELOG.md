# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.9] - 2026-04-30

### Added
- **Manual Shortcut Creation**: Added "New Shortcut" to the group context menus, allowing users to create shortcuts manually within a specific group without drag-and-drop.
- **Properties Integration**: Seamlessly integrated the existing Properties dialog for the creation workflow, including validation for paths and working directories.

## [1.1.8] - 2026-04-25

### Added
- **Working Directory Support**: Added a "Working Directory" field to the Shortcut Properties dialog, allowing shortcuts to be executed in a specific directory.
- **Improved Launch Logic**: Updated the process execution engine to respect the configured working directory when launching applications or scripts.

## [1.1.7] - 2026-04-03

### Fixed
- **Icon Visuals**: Refined icon extraction for `.lnk` shortcuts to documents (PDFs, Excel, etc.) to ensure they appear clean and without the shell's "shortcut arrow" overlay by targeting the resolved file instead of the shortcut.

## [1.1.6] - 2026-04-03

### Added
- **Application Icon**: Integrated the star icon (`Assets\star.ico`) as the official application executable icon and programmatically applied it to the main window.
- **Cleanup Reporting**: Updated the Settings dialog to display the exact number of unused icons and invalid shortcuts removed during cleanup operations.

## [1.1.5] - 2026-03-31

### Added
- **Dedicated About Dialog**: Migrated the "About" display to a standalone XAML dialog (`AboutDialog.xaml`) for better modularity and layout control.
- **About UI Refinement**: Reorganized the About dialog layout with description-first ordering and improved horizontal alignment for version and repository metadata.

### Fixed
- **Settings Consolidation**: Removed redundant version display logic from `SettingsDialog` to focus general settings on application behavior.

## [1.1.4] - 2026-03-31

### Fixed
- **UI Refinement**: Removed redundant version information from the general Settings dialog.
- **About Dialog**: Improved version display by using a read-only `TextBox`, allowing for better presentation of long version strings.

## [1.1.3] - 2026-03-31

### Added
- **About Dialog**: New "About" context menu item displaying the application version and a direct link to the GitHub repository.

## [1.1.2] - 2026-03-30

### Added
- **Debounced State Saving**: Implemented a `DispatcherTimer` to batch frequent state changes (like expanding/collapsing multiple groups), reducing disk I/O by waiting for a 2-second idle period before writing to disk.
- **Reliability (Exit Flush)**: Added logic to immediately flush any pending debounced saves when the application is closed, ensuring no preference changes are lost on exit.

### Fixed
- **Expander State Persistence**: Resolved an issue where group expansion states were not consistently saved when triggered via mouse clicks, keyboard navigation, or function keys (F1-F5).
- **Concurrency Safety**: Added internal state guards to prevent redundant or conflicting save operations during bulk UI updates.

## [1.1.1] - 2026-03-30

### Added
- **Display Preferences Dialog**: New interactive dialog to customize UI metrics without modifying source code.
- **Dynamic UI Scaling**: Real-time adjustment of Search Box and Group Header font sizes via data binding.
- **Persistent Preferences**: Externalized display settings to `display_settings.json` for persistent user customization across sessions.
- **Fine-tuned Layout Control**: User-adjustable logical width, vertical margin, and minimum height multipliers.

### Fixed
- **Build Configuration**: Reverted experimental single-file publishing changes to maintain standard WinUI 3 deployment model.

## [1.1.0] - 2026-03-30

### Added
- **Multi-Monitor Support**: Introduced "Move to next monitor" functionality, allowing seamless window cycling across multiple displays.
- **High-DPI Optimization**: Re-engineered window management to utilize `RasterizationScale`, ensuring consistent logical sizing and positioning across varying DPI environments.
- **Configurable Layout**: Added `_appWidthLogical` and `_appTopMarginMultiplier` variables for centralized control over application dimensions and vertical alignment.
- **Advanced Icon Maintenance**: Enhanced "Clean Up Unused Icons" utility with dual-source validation, cross-referencing active memory state with the physical `shortcuts.json` configuration.
- **UI Refinement**: Increased Search Box font size to 32pt for improved legibility and accessibility.

### Fixed
- **LNK Asset Management**: Resolved naming collisions for web application shortcuts by implementing unique, name-based cache identifiers.
- **Visual Polish**: Eliminated the standard Windows shortcut arrow overlay from extracted icons for a cleaner, modern aesthetic.
- **Reliability**: Integrated fatal error reporting to capture and log critical failures prior to application termination.
- **Navigation Precision**: Aligned keyboard navigation logic with the `UniformGridLayout` metrics to ensure perfect row-to-row movement.

## [1.0.2] - 2026-03-30

### Fixed
- **Concurrency Management**: Implemented `SemaphoreSlim` to serialize asynchronous dialog requests, resolving application crashes caused by concurrent modal activations.

## [1.0.1] - 2026-03-29

### Added
- **Persistence & Portability**: Added a "Create Backup" feature for automated redundancy of the `shortcuts.json` configuration.
- **Launch Preferences**: Added support for starting the application in a hidden state (minimized to system tray).

### Fixed
- **Architectural Overrides**: Eliminated the persistent 1px white border characteristic of standard WinUI 3 windows by implementing `WS_POPUP` Win32 style overrides.

## [1.0.0] - 2026-03-29

### Added
- **Input Enhancements**: Enabled direct shortcut deletion via the `Delete` key with contextual safety checks.
- **Asset Extraction**: Improved icon extraction heuristics for executables and standard Windows shell associations.

### Fixed
- **Animation Fluidity**: Resolved "jumpy" layout transitions during group expansion and collapse operations.
- **Tray Interaction**: Refined system tray toggle logic for more predictable show/hide behavior.

## [Unversioned / Initial Development] - 2026-03-28

### Added
- **Core Engine**: Initial release featuring shortcut grouping, searching, and execution.
- **Dynamic Management**: Comprehensive CRUD operations for shortcut groups via context menus.
- **Interaction Model**: Advanced drag-and-drop support for internal reorganization and external file system ingestion.
- **Keyboard Workflow**: Full arrow-key navigation and hotkey support for power users.
- **Telemetry**: Integrated file-based logging via Serilog.
- **DevOps**: Automated CI/CD pipelines via GitHub Actions.
