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
  - **High-Quality Extraction:** Uses Win32 `SHGetFileInfo` and `IShellLink` for full-color, 32-bit alpha-transparent icons.
  - **LNK Precision:** Specifically extracts the icon assigned to a `.lnk` file (shortcut), even if it differs from the target's default icon (e.g., for Web Apps).
  - **Folder Support:** Correctly identifies and displays Windows-associated folder icons.
  - **PNG Storage:** Icons are cached as PNG files to preserve transparency and quality.
- **🔍 Fast Search:** 
  - Quickly find any shortcut with a built-in search box.
  - **Instant Search:** Press any letter (`a-z`) while the app is active to instantly focus the search box and start typing.
  - **Auto-Highlight:** The first search result is automatically selected for immediate launching.
- **🎨 Modern Fluent UI:** 
  - Clean, borderless design using **Desktop Acrylic** backdrop.
  - **Windows 11 Fluent Icons** (Segoe Fluent Icons) throughout the interface.
  - **Smooth Animations:** Integrated `RepositionThemeTransition` for fluid layout shifts during group expansion.
  - Visual feedback with hover and selection highlights.
- **📥 System Tray Integration:** 
  - Stays out of your way in the system tray.
  - **Intelligent Toggling:** Clicking the tray icon brings the window to the absolute front if it's minimized or behind other windows, otherwise it toggles visibility.
- **⚡ Admin Support:** Option to launch specific applications with administrative privileges.
- **📏 Dynamic Sizing:** The window automatically adjusts its height based on the number of shortcuts and expanded groups.
- **🎹 Keyboard & Mouse Friendly:** 
  - `Double-Click`: Launch a shortcut.
  - `Enter`: Launch the currently highlighted shortcut.
  - `Delete`: Remove the currently highlighted shortcut (prompts for confirmation).
  - `Esc`: Clear selection, clear search, or hide the window.
  - `Right-Click`: Access comprehensive context menus for shortcuts, groups, and the application background.
  - **Reload:** Quickly refresh the application state from `shortcuts.json` via the context menu.
  - **Navigation:** Use **Arrow Keys** (Left, Right, Up, Down) to navigate between shortcuts and across different groups.
  - **Hotkeys:**
    - `F1 - F5`: Toggle expansion of the first 5 shortcut groups.
    - `Alt + 1 - 5`: Quick-launch the first 5 items (from search results or the active group).
    - `a - z`: Focus search box and start typing.
- **🛠️ Self-Healing Configuration:**
  - **Auto-Initialization:** Automatically generates a "Default" group if `shortcuts.json` is missing or empty.
  - **Path Validation:** Automatically verifies file/directory existence before performing any action.
  - Interactive recovery options: If a path is broken, you can choose to **Remove** the shortcut or **Edit** its properties immediately.

## Tech Stack

- **Framework:** .NET 10 + WinUI 3 (Windows App SDK)
- **Target OS:** Windows 11 (build 22000+)
- **UI Components:** Microsoft UI Xaml
- **Icons:** Segoe Fluent Icons
- **Tray Icon:** [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon)
- **Logging:** Serilog
- **Serialization:** System.Text.Json (with Source Generators for NativeAOT/Trim support)

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

## Getting Started

### Prerequisites
- Windows 11 version 21H2 (build 22000) or later.
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

### Running the App
1. Clone the repository.
2. Open `ShortcutManager.sln` in Visual Studio 2022.
3. Build and Run the `ShortcutManager` project (Targeting x64).
4. Locate the star icon in your system tray to open the manager.

## License

This project is licensed under the MIT License.

# Backlog Features
- Keyboard navigation improvements (Tab support)
- Support for custom group icons
- Support for URL-based shortcuts
- Global hotkey to show/hide the application
