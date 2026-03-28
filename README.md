# ShortcutManager

A simple Shortcut Manager that organize and launch your favorite applications, files, and scripts from a searchable search box. 

This is by far **NOT** the most creative app you will ever come across. Most shortcut managers uses search to find the shortcuts you want... This is a behavior which I personally do not like. 
I just need one that allows me to create my own shortcut groups rather than finding my whole computer for it. Since I wasn't able to find this available in any of the open source projects 20years ago (now too), I made my own.


Note that this is still an unfinished product. I'm still trying to migrate the features from the old Desktop Shortcut Manager that was last maintained 15 years ago using Winforms. The main aim is to use modernized UI/frameworks to achieve the same functionality.

The old shortcut manager is a side bar that shows/collapses. But this version takes inspiration from Flow launcher for the UI.

## Features

- **📂 Group Management:** Create, rename, delete, and rearrange groups using an intuitive context menu.
- **🖱️ Drag & Drop:**
  - Drag files, folders, or Windows shortcuts (`.lnk`) directly onto a group to add them.
  - Rearrange shortcuts within a group or move them between groups by dragging icons.
  - Automatic resolution of `.lnk` targets and command-line arguments.
- **🖼️ Smart Icon Cache:** 
  - Centralized icon extraction and caching based on file extensions or executable names.
  - Portable configuration using relative paths for cached icons.
  - Manually change icons by selecting `.ico` files or extracting from other executables.
- **🔍 Fast Search:** Quickly find any shortcut with a built-in search box. Press `Enter` to launch the first match.
- **🎨 Modern Fluent UI:** 
  - Clean, borderless design using **Desktop Acrylic** backdrop.
  - **Windows 11 Fluent Icons** (Segoe Fluent Icons) throughout the interface.
  - Visual feedback with hover and selection highlights.
- **📥 System Tray Integration:** Stays out of your way in the system tray. Click the tray icon to toggle visibility.
- **⚡ Admin Support:** Option to launch specific applications with administrative privileges.
- **📏 Dynamic Sizing:** The window automatically adjusts its height based on the number of shortcuts and expanded groups.
- **🎹 Keyboard & Mouse Friendly:** 
  - `Double-Click`: Launch a shortcut.
  - `Esc`: Clear search or hide the window.
  - `Enter`: Launch the top search result.
  - `Right-Click`: Access comprehensive context menus for shortcuts, groups, and the application.

## Tech Stack

- **Framework:** .NET 10 + WinUI 3 (Windows App SDK)
- **UI Components:** Microsoft UI Xaml
- **Icons:** Segoe Fluent Icons
- **Tray Icon:** [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon)
- **Serialization:** System.Text.Json

## Configuration

The application loads shortcuts from `shortcuts.json`.

### JSON Structure

```json
[
  {
    "GroupName": "Browsers",
    "IsExpanded": true,
    "Shortcuts": [
      {
        "text": "Google Chrome",
        "application": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
        "icon": "icons/chrome.ico",
        "args": "--incognito",
        "runasAdmin": false,
        "id": "guid-here"
      }
    ]
  }
]
```

- `GroupName`: The display name for the group.
- `IsExpanded`: Initial state of the group expander.
- `Shortcuts`:
    - `text`: Display name of the shortcut.
    - `application`: Full path to the executable or file.
    - `icon`: Relative path to the cached icon file.
    - `args`: Command-line arguments.
    - `runasAdmin`: Set to `true` to prompt for UAC on launch.
    - `id`: Unique identifier for management and dragging.

## Getting Started

### Prerequisites
- Windows 10 version 1809 (build 17763) or later.
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

### Running the App
1. Clone the repository.
2. Open `ShortcutManager.sln` in Visual Studio 2022.
3. Build and Run the `ShortcutManager` project.
4. Locate the star icon in your system tray to open the manager.

## License

This project is licensed under the MIT License - see the LICENSE file for details (if applicable).

# Backlog Features
- Clean up unused icons
- Keyboard navigation for shortcut items
