# ShortcutManager

A simple Shortcut Manager that organize and launch your favorite applications, files, and scripts from a searchable search box. 

This is by far **NOT** the most creative app you will ever come across. Most shortcut managers uses search to find the shortcuts you want... This is a behavior which I personally do not like. 
I just need one that allows me to create my own shortcut groups rather than finding my whole computer for it. Since I wasn't able to find this available in any of the open source projects 20years ago (now too), I made my own.


Note that this is still an unfinished product. I'm still trying to migrate the features from the old Desktop Shortcut Manager that was last maintained 15 years ago using Winforms. The main aim is to use modernized UI/frameworks to achieve the same functionality.

The old shortcut manager is a side bar that shows/collapses. But this version takes inspiration from Flow launcher for the UI.

## Features

- **📂 Grouped Shortcuts:** Organize your shortcuts into logical categories using expanders.
- **🔍 Fast Search:** Quickly find any shortcut with a built-in search box. Press `Enter` to launch the first match.
- **🎨 Modern UI:** A clean, borderless design using **Desktop Acrylic** backdrop for a translucent glass effect.
- **📥 System Tray Integration:** Stays out of your way in the system tray. Click the tray icon to toggle visibility.
- **🛠️ Flexible Configuration:** All shortcuts are managed via a simple `shortcuts.json` file.
- **⚡ Admin Support:** Option to launch specific applications with administrative privileges.
- **📏 Dynamic Sizing:** The window automatically adjusts its height based on the number of shortcuts and expanded groups.
- **🎹 Keyboard Friendly:** 
  - `Esc`: Clear search or hide the window.
  - `Enter`: Launch the top search result.

## Tech Stack

- **Framework:** .NET 10 + WinUI 3 (Windows App SDK)
- **UI Components:** Microsoft UI Xaml
- **Tray Icon:** [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon)
- **Serialization:** System.Text.Json

## Configuration

The application loads shortcuts from `shortcuts.json`. You can find this file in the root directory or in a `tmp/` subfolder.

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
        "runasAdmin": false
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
    - `icon`: Path to the `.ico` or image file (relative to the app or absolute).
    - `args`: Command-line arguments.
    - `runasAdmin`: Set to `true` to prompt for UAC on launch.

## Icons

For copyright related reaons, no default icons will be provided.

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
- (Critical) Create Group
- (Critical) Add/Remove Shortcuts
- Clean up unused icons