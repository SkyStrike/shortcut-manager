# ShortcutManager

A simple Shortcut Manager that organize and launch your favorite applications, files, and scripts from a searchable search box. 

This is by far **NOT** the most creative app you will ever come across. Most shortcut managers uses search to find the shortcuts you want... This is a behavior which I personally do not like. 
I just need one that allows me to create my own shortcut groups rather than finding my whole computer for it. Since I wasn't able to find this available in any of the open source projects 20years ago (now too), I made my own.


Note that this is still an unfinished product. I'm still trying to migrate the features from the old Desktop Shortcut Manager that was last maintained 15 years ago using Winforms. The main aim is to use modernized UI/frameworks to achieve the same functionality.

The old shortcut manager is a side bar that shows/collapses. But this version takes inspiration from Flow launcher for the UI.

## Features

- **📂 Group Management:**
  - Create, rename, delete, and rearrange groups using an intuitive context menu.
  - **Launch All:** Launch every application in a group with one click (includes a safety confirmation for groups with >5 items).
  - **Auto-Initialization:** Dragging items into an empty application automatically creates a "Default" group.
- **🖱️ Drag & Drop:**
  - Drag files, folders, or Windows shortcuts (`.lnk`) directly onto a group to add them.
  - Rearrange shortcuts within a group or move them between groups by dragging icons.
  - Automatic resolution of `.lnk` targets and command-line arguments.
- **🛡️ Path Validation:**
  - Automatically verifies file/directory existence before performing any action.
  - Interactive recovery options: If a path is broken, you can choose to **Remove** the shortcut or **Edit** its properties immediately.
- **🖼️ Smart Icon Cache:** 
  - **High-Quality Extraction:** Uses Win32 SHGetFileInfo for full-color, 32-bit alpha-transparent icons.
  - **PNG Storage:** Icons are cached as PNG files to preserve transparency and quality.
  - Centralized caching based on file extensions or executable names.
  - Portable configuration using relative paths for cached icons.
- **🔍 Fast Search:** 
  - Quickly find any shortcut with a built-in search box.
  - **Auto-Highlight:** The first search result is automatically selected for immediate launching.
- **🎨 Modern Fluent UI:** 
  - Clean, borderless design using **Desktop Acrylic** backdrop.
  - **Windows 11 Fluent Icons** (Segoe Fluent Icons) throughout the interface.
  - Visual feedback with hover and selection highlights.
- **📥 System Tray Integration:** Stays out of your way in the system tray. Click the tray icon to toggle visibility.
- **⚡ Admin Support:** Option to launch specific applications with administrative privileges.
- **📏 Dynamic Sizing:** The window automatically adjusts its height based on the number of shortcuts and expanded groups.
- **🎹 Keyboard & Mouse Friendly:** 
  - `Double-Click`: Launch a shortcut.
  - `Enter`: Launch the currently highlighted shortcut.
  - `Esc`: Clear selection, clear search, or hide the window.
  - `Right-Click`: Access comprehensive context menus for shortcuts, groups, and the application.
  - **Navigation:** Use **Arrow Keys** (Left, Right, Up, Down) to navigate between shortcuts and across different groups.
  - **Hotkeys:**
    - `F1 - F5`: Toggle expansion of the first 5 shortcut groups.
    - `Alt + 1 - 5`: Quick-launch the first 5 items (from search results or the active group).
- **🛡️ Error Logging:** Robust exception tracking using Serilog, with automatic log rotation (5MB limit) at `logs/error.log`. Configuration is externalized in `serilog.json`.

## Tech Stack

- **Framework:** .NET 10 + WinUI 3 (Windows App SDK)
- **UI Components:** Microsoft UI Xaml
- **Icons:** Segoe Fluent Icons
- **Tray Icon:** [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon)
- **Logging:** Serilog
- **Serialization:** System.Text.Json (with Source Generators for NativeAOT support)

## Configuration & Settings

The application stores its configuration in `shortcuts.json` and provides a built-in Settings menu.

### Settings Dialog
Access by right-clicking the application background:
- **Run at Windows Startup:** Automatically creates/removes a shortcut in the Windows Startup folder.
- **Regenerate All Icons:** Re-extracts all icons using the high-quality Win32 method.
- **Clean Up Unused Icons:** Removes orphaned icon files from the cache.
- **Remove Invalid Shortcuts:** Scans and prunes shortcuts with broken paths.
- **Open Application/Startup Directory:** Quick access to system folders.
- **Version Info:** Displays the current version.

## License

This project is licensed under the MIT License.

# Backlog Features
- Support for custom group icons
- Support for URL-based shortcuts
- Global hotkey to show/hide the application
