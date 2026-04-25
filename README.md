# ShortcutManager

A simple Shortcut Manager that organize and launch your favorite applications, files, and scripts from a searchable search box. 

## 🚀 TL;DR (Quick Start)
If you just want to use the app without building from source:
1.  **Download**: Get the latest `ShortcutManager_vX.X.X_x64.zip` from the [Releases](https://github.com/SkyStrike/shortcut-manager/releases) page.
2.  **Extract**: Unzip the folder to a location of your choice (e.g., `C:\Tools\ShortcutManager`).
3.  **Run**: Launch `ShortcutManager.exe`.
4.  **Tray**: Look for the star icon in your system tray to toggle the window.
5.  **Setup**: Drag and drop your favorite `.exe`, folders, or `.lnk` files directly onto the app to start building your groups.

---

This is by far **NOT** the most creative app you will ever come across. Most shortcut managers uses search to find the shortcuts you want... This is a behavior which I personally do not like. 
I just need one that allows me to create my own shortcut groups rather than finding my whole computer for it. Since I wasn't able to find this available in any of the open source projects 20 years ago in 2006 (now too), I made my own.

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
  - **Clean Icons:** Automatically removes the shortcut arrow overlay for a cleaner, high-quality look.
  - **Robust Cleanup:** "Clean Up Unused Icons" feature now cross-references both active memory and the on-disk `shortcuts.json` to safely remove orphaned icons without risking active ones. Includes detailed reporting of items removed.
  - **Folder Support:** Correctly identifies and displays Windows-associated folder icons.
  - **PNG Storage:** Icons are cached as PNG files to preserve transparency and quality.
- **🔍 Fast Search:** 
  - Quickly find any shortcut with a built-in search box.
  - **Instant Search:** Press any letter (`a-z`) while the app is active to instantly focus the search box and start typing.
  - **Auto-Highlight:** The first search result is automatically selected for immediate launching.
- **🎨 Modern Fluent UI:** 
  - **Truly Borderless Design:** Implements a custom borderless window using Win32 `WS_POPUP` style to eliminate the persistent white borders typically found in WinUI 3 applications.
  - **Desktop Acrylic** backdrop for a modern Windows 11 feel.
  - **Windows 11 Fluent Icons** (Segoe Fluent Icons) throughout the interface.
  - **Smooth Animations:** Integrated `RepositionThemeTransition` for fluid layout shifts during group expansion.
  - Visual feedback with hover and selection highlights.
- **📥 System Tray Integration:** 
  - Stays out of your way in the system tray.
  - **Intelligent Toggling:** Clicking the tray icon brings the window to the absolute front if it's minimized or behind other windows, otherwise it toggles visibility.
- **⚡ Admin & Shell Support:** 
  - Option to launch specific applications with administrative privileges.
  - **Working Directory:** Specify a custom directory for an application to start in, useful for tools that depend on local configuration files.
- **📏 Dynamic Sizing & Positioning:** 
  - **Customizable Window Scaling:** Control the application's width and initial vertical position via the **Display Preferences** dialog.
  - **Interactive Scaling:** Real-time font size adjustments for the Search Box and Group Headers.
  - **Monitor Switching:** "Move to next monitor" feature in the main context menu allows for seamless transitions between multiple displays, centering the application on each monitor.
  - **Dynamic Visibility:** The monitor move option automatically adapts its visibility based on the current number of detected displays, even if they are connected after the app is launched.
  - The window automatically adjusts its height based on the number of shortcuts and expanded groups.
- **🎹 Keyboard & Mouse Friendly:** 
  - **About Dialog:** Quickly view the current version and access the GitHub repository from the context menu.
  - **Display Preferences:** Access via context menu to fine-tune font sizes, logical width, and screen positioning multipliers without rebuilding.
  - `Double-Click`: Launch a shortcut.
  - `Enter`: Launch the currently highlighted shortcut.
  - `Delete`: Remove the currently highlighted shortcut. **Intelligent Safety:** Automatically disabled when any dialog or flyout is active to prevent accidental deletions.
  - `Esc`: Clear selection, clear search, or hide the window.
  - `Right-Click`: Access comprehensive context menus for shortcuts, groups, and the application background.
  - **Reload:** Quickly refresh the application state from `shortcuts.json` via the context menu.
  - **Navigation:** Use **Arrow Keys** (Left, Right, Up, Down) to navigate between shortcuts and across different groups. **Aligned Layout:** Navigation is precisely calculated to match the `UniformGridLayout` for perfect row-to-row movement.
  - **Hotkeys:**
    - `F1 - F5`: Toggle expansion of the first 5 shortcut groups.
    - `Alt + 1 - 5`: Quick-launch the first 5 items (from search results or the active group).
    - `a - z`: Focus search box and start typing.
- **🛡️ Dialog & Task Safety:**
  - **Serializing Dialogs:** Implements a `SemaphoreSlim` to safely manage multiple dialog requests, preventing WinUI 3 "Single Dialog" crashes.
  - **Non-Disruptive Settings:** Destructive actions in Settings now use **Modern Flyout Confirmations** with headers and caution icons, eliminating the distracting "hide/show" dialog pattern.
  - **InfoBar Feedback:** Real-time status updates (Success/Error) are shown via an in-place `InfoBar` within the Settings dialog for a smoother workflow.
- **🛠️ Self-Healing Configuration:**
  - **Auto-Initialization:** Automatically generates a "Default" group if `shortcuts.json` is missing or empty.
  - **Persistence:** Ensuring `shortcuts.json` and `display_settings.json` are always correctly synced to the application's base directory for reliable loading.
  - **Optimized State Management:** Implements **Debounced Saving** (2-second idle delay) to reduce disk I/O during frequent UI interactions, with an **Immediate Exit Flush** to prevent data loss on application close.
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
- **Create Backup:** Generates a timestamped backup of your `shortcuts.json` in a `backups/` folder.
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