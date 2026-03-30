# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-03-30

### Added
- **Monitor Switching**: New "Move to next monitor" feature in the main context menu.
- **DPI-Aware Positioning**: Window management now uses `RasterizationScale` for consistent sizing across different displays.
- **Layout Variables**: Introduced `_appWidthLogical` and `_appTopMarginMultiplier` for easy UI fine-tuning.
- **Robust Icon Cleanup**: Enhanced cleanup logic that cross-references both active memory and the on-disk `shortcuts.json`.
- **Search Improvements**: Increased the search box font size for better legibility.

### Fixed
- **LNK Icon Extraction**: Fixed naming bugs for web app icons using unique cache IDs based on shortcut names.
- **Clean Icons**: Automatically removes the shortcut arrow overlay from extracted icons.
- **Error Handling**: Added fatal error reporting before application exit.
- **Keyboard Navigation**: Aligned arrow key navigation logic with the `UniformGridLayout` pixel sizes.

## [1.0.2] - 2026-03-30

### Fixed
- **Modal Safety**: Implemented a `SemaphoreSlim` to serialize dialog requests, preventing crashes when multiple modals were triggered.

## [1.0.1] - 2026-03-29

### Added
- **Start Hidden**: Added option to launch the application minimized to the system tray.
- **Backup System**: Added a "Create Backup" feature for `shortcuts.json`.

### Fixed
- **Visuals**: Fixed the persistent 1px white border in WinUI 3 by implementing `WS_POPUP` Win32 style.

## [1.0.0] - 2026-03-29

### Added
- **Keyboard Deletion**: Remove shortcuts directly by pressing the `Delete` key.
- **Icon Quality**: Improved extraction for `.exe` files and standard Windows file types.

### Fixed
- **Transitions**: Resolved "jumpy" layout animations during group expansion.
- **Tray Interaction**: Refined the show/hide toggle behavior from the system tray icon.

## [Unversioned / Initial Development] - 2026-03-28

### Added
- **Initial Release**: Core Shortcut Manager functionality.
- **Group Management**: Create, rename, delete, and rearrange shortcut groups.
- **Drag & Drop**: Internal rearrangement and external file/folder dropping.
- **Keyboard Navigation**: Full arrow-key support for navigating shortcuts and groups.
- **Settings Dialog**: Initial implementation with "Run at Windows Startup" option.
- **Shortcut Properties**: Allow editing of name, path, arguments, and admin status.
- **Icon Regeneration**: Manual trigger to re-extract icons.
- **Logging**: Integration with Serilog for file and configuration-based logging.
- **CI Automation**: GitHub Actions workflow for build and versioning.
