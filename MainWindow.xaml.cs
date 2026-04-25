using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Reflection;

namespace ShortcutManager
{
    /// <summary>
    /// The main window of the application, managing shortcut groups, searching, and interactions.
    /// Supports high-quality icon management, complex drag-and-drop, and full keyboard navigation.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region Win32 and COM Interfaces for Shortcut and Icon Handling

        /// <summary>
        /// Standard Win32 File Information structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>
        /// Win32 Find Data structure used for file attribute retrieval.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATAW
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        /// <summary>
        /// COM Coclass for ShellLink.
        /// </summary>
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        /// <summary>
        /// Interface for managing Windows Shell Shortcuts (.lnk).
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, out WIN32_FIND_DATAW pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        /// <summary>
        /// Interface for loading and saving files via COM objects.
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder ppszFileName);
        }

        #endregion

        #region Win32 API Imports

        // For high-quality icon extraction (32-bit with Alpha)
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // For intelligent window foreground management
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LINKOVERLAY = 0x8000;
        private const uint SHGFI_ICONLOCATION = 0x1000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        #endregion

        // Main data collection for the UI
        public ObservableCollection<ShortcutGroup> MyGroups { get; set; } = [];
        
        // Special group for displaying search results
        private ShortcutGroup _searchResultGroup = new() { GroupName = "Search Result", IsExpanded = true };
        
        // Flag to prevent recursive state saving during bulk UI updates
        private bool _isUpdatingStates = false;
        
        // Flag to prevent multiple dialogs or flyouts from being opened concurrently.
        // This is used to disable keyboard shortcuts (like Delete) while a dialog is active.
        private bool _isDialogOpen = false;

        // Semaphore to serialize dialog requests. WinUI 3 only allows one ContentDialog 
        // to be open at a time. This ensures sequential display and prevents COMException.
        private readonly SemaphoreSlim _dialogSemaphore = new SemaphoreSlim(1, 1);

        // Timer for debouncing state saves to disk
        private readonly DispatcherTimer _saveDebounceTimer = new DispatcherTimer();
        
        private AppWindow _appWindow;
        
        // Window calculation settings
        public DisplaySettings AppSettings { get; private set; } = new DisplaySettings();

        // Paths to configuration files
        private string shortcutFile = Path.Combine(AppContext.BaseDirectory, "shortcuts.json");
        private string settingsFile = Path.Combine(AppContext.BaseDirectory, "display_settings.json");

        public MainWindow(bool startHidden = false)
        {
            LoadSettings();
            InitializeComponent();

            // Initialize save debounce timer (2 second delay)
            _saveDebounceTimer.Interval = TimeSpan.FromSeconds(2);
            _saveDebounceTimer.Tick += (s, e) => {
                _saveDebounceTimer.Stop();
                SaveStates(immediate: true);
            };

            // Handle settings changes to refresh UI immediately where needed
            AppSettings.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(DisplaySettings.AppWidthLogical) || 
                    e.PropertyName == nameof(DisplaySettings.AppMinHeightMultiplier) ||
                    e.PropertyName == nameof(DisplaySettings.AppTopMarginMultiplier))
                {
                    this.DispatcherQueue.TryEnqueue(() => UpdateWindowSize());
                }
            };

            // Set up borderless window using custom title bar and Win32 styles
            // Standard WinUI 3 windows often draw a 1px white border that cannot be removed via XAML or AppWindow.
            // Using WS_POPUP style and forcing a frame change completely eliminates these persistent borders.
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Set the window icon
                _appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "star.ico"));

                // Hide from task switcher (Alt-Tab) for a more "background app" feel
                _appWindow.IsShownInSwitchers = false;

                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = true;
                    presenter.IsMaximizable = false;
                    presenter.SetBorderAndTitleBar(false, false);

                    // Replace "Overlapped" style with "Popup" style to completely remove standard borders.
                    // This is the most reliable way to achieve a truly borderless look in WinUI 3.
                    SetWindowLongPtr(hWnd, GWL_STYLE, (IntPtr)unchecked(WS_POPUP | WS_VISIBLE | WS_CLIPSIBLINGS));
                    
                    // Force a frame update (SW_FRAMECHANGED) so the OS applies the style change immediately.
                    SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);
                }

                // Initial positioning: center horizontally, slightly above center vertically
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    // Use a safe default scale if XamlRoot isn't available yet
                    double scale = this.Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    var workArea = displayArea.WorkArea;
                    int widthPhysical = (int)(AppSettings.AppWidthLogical * scale);
                    int minHeightPhysical = (int)(workArea.Height * AppSettings.AppMinHeightMultiplier);
                    int x = workArea.X + (workArea.Width - widthPhysical) / 2;
                    int y = workArea.Y + (int)(workArea.Height * AppSettings.AppTopMarginMultiplier); 
                    _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, widthPhysical, minHeightPhysical));
                }

                if (startHidden)
                {
                    _appWindow.Hide();
                }
            }

            // Set system backdrop to Acrylic for modern Windows 11 aesthetics
            this.SystemBackdrop = new DesktopAcrylicBackdrop();

            LoadShortcuts();
            GroupsList.ItemsSource = MyGroups;
            
            // Adjust window size after initial layout when content is ready
            if (this.Content is FrameworkElement root)
            {
                root.Loaded += (s, e) => {
                    UpdateWindowSize();
                };
            }

            // Auto-clear selection when the app loses focus
            this.Activated += MainWindow_Activated;

            this.Closed += (s, e) => {
                // Flush any pending debounced saves immediately on exit
                if (_saveDebounceTimer.IsEnabled)
                {
                    _saveDebounceTimer.Stop();
                    SaveStates(immediate: true);
                }

                if (MyTrayIcon != null)
                {
                    MyTrayIcon.Dispose();
                }
            };
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                ClearSelection();
            }
        }

        private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Clear selection if clicking the background grid itself, not its children
            if (e.OriginalSource == sender)
            {
                ClearSelection();
            }
        }

        private void ClearSelection()
        {
            if (_selectedItem != null)
            {
                _selectedItem.IsSelected = false;
                _selectedItem = null;
            }
        }

        /// <summary>
        /// Loads shortcuts from the JSON file and resolves/extracts missing icons.
        /// </summary>
        private void LoadShortcuts()
        {
            try
            {
                string jsonPath = shortcutFile;
                if (!File.Exists(shortcutFile))
                {
                    jsonPath = Path.GetFullPath(shortcutFile);
                }

                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        CreateDefaultGroup();
                        return;
                    }

                    // Use source generator for deserialization to support NativeAOT/Trimming
                    var groups = JsonSerializer.Deserialize(json, ShortcutSerializationContext.Default.ListShortcutGroup);

                    if (groups != null && groups.Count > 0)
                    {
                        MyGroups.Clear();
                        foreach (var group in groups)
                        {
                            foreach (var item in group.Shortcuts)
                            {
                                // Resolve relative icon path to absolute for UI binding
                                if (!string.IsNullOrEmpty(item.Icon) && !Path.IsPathRooted(item.Icon))
                                {
                                    item.Icon = Path.Combine(AppContext.BaseDirectory, item.Icon);
                                }

                                // If icon is missing or not found, try to resolve from cache or extract it
                                if (string.IsNullOrEmpty(item.Icon) || !File.Exists(item.Icon))
                                {
                                    ExtractAndSaveIcon(item, null, false);
                                }
                            }
                            MyGroups.Add(group);
                        }
                    }
                    else
                    {
                        CreateDefaultGroup();
                    }
                }
                else
                {
                    CreateDefaultGroup();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error loading shortcuts: {ex.Message}");
                if (MyGroups.Count == 0)
                {
                    CreateDefaultGroup();
                }
            }
        }

        private void CreateDefaultGroup()
        {
            MyGroups.Clear();
            MyGroups.Add(new ShortcutGroup { GroupName = "Default", IsExpanded = true });
            SaveStates();
        }

        /// <summary>
        /// Converts an absolute path to a path relative to the application base directory if possible.
        /// </summary>
        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;
            string baseDir = AppContext.BaseDirectory;
            if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(baseDir, fullPath);
            }
            return fullPath;
        }

        /// <summary>
        /// Persists the current state of groups and shortcuts to the JSON file.
        /// Saves relative paths for icons to maintain portability.
        /// </summary>
        /// <param name="immediate">If true, writes to disk immediately. If false, debounces the save via timer.</param>
        private void SaveStates(bool immediate = false)
        {
            if (_isUpdatingStates) return;

            if (!immediate)
            {
                _saveDebounceTimer.Stop();
                _saveDebounceTimer.Start();
                return;
            }

            try
            {
                string jsonPath = shortcutFile;
                if (!File.Exists(shortcutFile))
                {
                    jsonPath = Path.GetFullPath(shortcutFile);
                }

                // Create a clone with relative icon paths for portable saving
                var groupsToSave = MyGroups.Where(g => g.GroupName != "Search Result")
                    .Select(g => new ShortcutGroup {
                        GroupName = g.GroupName,
                        IsExpanded = g.IsExpanded,
                        Shortcuts = new ObservableCollection<ShortcutItem>(
                            g.Shortcuts.Select(s => new ShortcutItem {
                                Name = s.Name,
                                Path = s.Path,
                                Icon = GetRelativePath(s.Icon),
                                RunAsAdmin = s.RunAsAdmin,
                                Arguments = s.Arguments,
                                WorkingDirectory = s.WorkingDirectory,
                                Id = s.Id
                            })
                        )
                    }).ToList();

                // Use source generator for serialization to support NativeAOT/Trimming
                string json = JsonSerializer.Serialize(groupsToSave, ShortcutSerializationContext.Default.ListShortcutGroup);
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error saving states: {ex.Message}");
            }
        }

        /// <summary>
        /// Adjusts the window height dynamically based on the content height and screen constraints.
        /// Accounts for DPI scaling (physical vs logical pixels).
        /// </summary>
        private void UpdateWindowSize()
        {
            if (this.Content is FrameworkElement root && _appWindow != null && this.Content.XamlRoot != null)
            {
                double scale = this.Content.XamlRoot.RasterizationScale;
                
                // Use fixed logical width
                double logicalWidth = AppSettings.AppWidthLogical;
                root.Measure(new Windows.Foundation.Size(logicalWidth, double.PositiveInfinity));
                double desiredHeightLogical = root.DesiredSize.Height;
                
                // Convert to physical pixels
                int widthPhysical = (int)(logicalWidth * scale);
                int desiredHeightPhysical = (int)((desiredHeightLogical + 10) * scale);

                var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea; // Physical pixels
                    
                    // Use multiplier for vertical position
                    int newY = workArea.Y + (int)(workArea.Height * AppSettings.AppTopMarginMultiplier);
                    
                    // Ensure width doesn't exceed work area
                    widthPhysical = Math.Min(widthPhysical, workArea.Width);
                    
                    int minHeightPhysical = (int)(workArea.Height * AppSettings.AppMinHeightMultiplier);
                    int maxHeightPhysical = workArea.Height - (newY - workArea.Y) - (int)(20 * scale); 
                    
                    int newHeightPhysical = Math.Max(minHeightPhysical, desiredHeightPhysical);
                    newHeightPhysical = Math.Min(newHeightPhysical, maxHeightPhysical);
                    
                    // Recalculate X for horizontal centering
                    int newX = workArea.X + (workArea.Width - widthPhysical) / 2;
                    
                    if (Math.Abs(newHeightPhysical - _appWindow.Size.Height) > 1 || 
                        Math.Abs(widthPhysical - _appWindow.Size.Width) > 1 ||
                        Math.Abs(newX - _appWindow.Position.X) > 1 ||
                        Math.Abs(newY - _appWindow.Position.Y) > 1)
                    {
                        _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(newX, newY, widthPhysical, newHeightPhysical));
                    }
                }
            }
        }

        /// <summary>
        /// Launches the targeted application or file, respecting arguments and admin privileges.
        /// </summary>
        private void ExecuteShortcut(ShortcutItem item) {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = item.Path,
                    Arguments = item.Arguments,
                    WorkingDirectory = item.WorkingDirectory,
                    UseShellExecute = true,
                    Verb = item.RunAsAdmin ? "runas" : ""
                };
                Process.Start(startInfo);
            }
            catch (System.ComponentModel.Win32Exception ex) 
            { 
                Log.Warning(ex, "User cancelled UAC or error launching {Path}", item.Path);
            }
        }

        private ShortcutItem _selectedItem;

        private void Shortcut_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is ShortcutItem item)
            {
                SelectShortcut(item, grid);
            }
        }

        private void Shortcut_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (sender is Grid grid && grid.DataContext is ShortcutItem item)
            {
                SelectShortcut(item, grid);
            }
        }

        private void SelectShortcut(ShortcutItem item, Grid grid)
        {
            if (_selectedItem != null && _selectedItem != item)
            {
                _selectedItem.IsSelected = false;
            }

            _selectedItem = item;
            _selectedItem.IsSelected = true;
            
            // If grid is provided (from click), focus it. 
            grid?.Focus(FocusState.Pointer);
        }

        /// <summary>
        /// Logic for keyboard navigation using arrow keys.
        /// Moves selection within the current group and transitions between groups.
        /// </summary>
        private void NavigateShortcuts(Windows.System.VirtualKey key)
        {
            if (!MyGroups.Any()) return;

            // Find current group and item index
            int currentGroupIdx = -1;
            int currentItemIdx = -1;

            if (_selectedItem != null)
            {
                for (int i = 0; i < MyGroups.Count; i++)
                {
                    int itemIdx = MyGroups[i].Shortcuts.IndexOf(_selectedItem);
                    if (itemIdx != -1)
                    {
                        currentGroupIdx = i;
                        currentItemIdx = itemIdx;
                        break;
                    }
                }
            }

            // If nothing selected, start at the first expanded group or first group
            if (currentGroupIdx == -1)
            {
                var targetGroup = MyGroups.FirstOrDefault(g => g.IsExpanded) ?? MyGroups[0];
                currentGroupIdx = MyGroups.IndexOf(targetGroup);
                if (targetGroup.Shortcuts.Any())
                {
                    SelectShortcut(targetGroup.Shortcuts[0], null);
                }
                return;
            }

            var currentGroup = MyGroups[currentGroupIdx];
            int itemsPerRow = CalculateItemsPerRow();
            int newGroupIdx = currentGroupIdx;
            int newItemIdx = currentItemIdx;
            bool stateChanged = false;

            switch (key)
            {
                case Windows.System.VirtualKey.Left:
                    newItemIdx--;
                    break;
                case Windows.System.VirtualKey.Right:
                    newItemIdx++;
                    break;
                case Windows.System.VirtualKey.Up:
                    newItemIdx -= itemsPerRow;
                    break;
                case Windows.System.VirtualKey.Down:
                    newItemIdx += itemsPerRow;
                    break;
            }

            // Handle wrap-around or group transition
            if (newItemIdx < 0)
            {
                // Move to previous group
                do { newGroupIdx--; } while (newGroupIdx >= 0 && MyGroups[newGroupIdx].Shortcuts.Count == 0);
                
                if (newGroupIdx >= 0)
                {
                    var prevGroup = MyGroups[newGroupIdx];
                    if (!prevGroup.IsExpanded)
                    {
                        prevGroup.IsExpanded = true;
                        stateChanged = true;
                    }
                    // If moving Up, try to stay in the same "column" in the last row
                    if (key == Windows.System.VirtualKey.Up)
                    {
                        int lastRowStart = (prevGroup.Shortcuts.Count - 1) / itemsPerRow * itemsPerRow;
                        newItemIdx = Math.Min(prevGroup.Shortcuts.Count - 1, lastRowStart + currentItemIdx % itemsPerRow);
                    }
                    else
                    {
                        newItemIdx = prevGroup.Shortcuts.Count - 1;
                    }
                }
                else
                {
                    newGroupIdx = currentGroupIdx;
                    newItemIdx = 0;
                }
            }
            else if (newItemIdx >= currentGroup.Shortcuts.Count)
            {
                // Move to next group
                do { newGroupIdx++; } while (newGroupIdx < MyGroups.Count && MyGroups[newGroupIdx].Shortcuts.Count == 0);

                if (newGroupIdx < MyGroups.Count)
                {
                    var nextGroup = MyGroups[newGroupIdx];
                    if (!nextGroup.IsExpanded)
                    {
                        nextGroup.IsExpanded = true;
                        stateChanged = true;
                    }
                    // If moving Down, try to stay in the same "column" in the first row
                    if (key == Windows.System.VirtualKey.Down)
                    {
                        newItemIdx = Math.Min(nextGroup.Shortcuts.Count - 1, currentItemIdx % itemsPerRow);
                    }
                    else
                    {
                        newItemIdx = 0;
                    }
                }
                else
                {
                    newGroupIdx = currentGroupIdx;
                    newItemIdx = currentGroup.Shortcuts.Count - 1;
                }
            }

            if (stateChanged)
            {
                SaveStates();
            }

            // Apply new selection
            if (newGroupIdx >= 0 && newGroupIdx < MyGroups.Count)
            {
                var targetGroup = MyGroups[newGroupIdx];
                if (newItemIdx >= 0 && newItemIdx < targetGroup.Shortcuts.Count)
                {
                    SelectShortcut(targetGroup.Shortcuts[newItemIdx], null);
                }
            }
        }

        /// <summary>
        /// Estimates the number of items per row based on current window width and layout properties.
        /// </summary>
        private int CalculateItemsPerRow()
        {
            try
            {
                if (this.Content is Grid rootGrid)
                {
                    var scrollViewer = rootGrid.Children.OfType<ScrollViewer>().FirstOrDefault();
                    if (scrollViewer != null && scrollViewer.ViewportWidth > 0)
                    {
                        // Width: 120 (min item) + 5 (column spacing). Padding: ~40px.
                        double availableWidth = scrollViewer.ViewportWidth - 40; 
                        int count = (int)(availableWidth / 125);
                        return Math.Max(1, count);
                    }
                }
            }
            catch { }

            // Dynamic fallback based on current logical width
            return Math.Max(1, (int)((AppSettings.AppWidthLogical - 40) / 125));
        }

        private async void OnShortcutDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShortcutItem item)
            {
                if (await EnsurePathValid(item))
                {
                    ExecuteShortcut(item);
                }
            }
        }

        private async void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                if (await EnsurePathValid(item))
                {
                    ExecuteShortcut(item);
                }
            }
        }

        private async void MenuOpenDir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item && !string.IsNullOrEmpty(item.Path))
            {
                if (!await EnsurePathValid(item)) return;

                try
                {
                    string folder = Path.GetDirectoryName(item.Path);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = folder,
                            UseShellExecute = true
                        });
                    }
                    else if (File.Exists(item.Path))
                    {
                        // Fallback: select the file if directory lookup failed
                         Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{item.Path}\"",
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error opening directory: {ex.Message}");
                }
            }
        }

        private async void MenuRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                await RemoveShortcutWithConfirmation(item);
            }
        }

        /// <summary>
        /// Prompts the user for confirmation before removing a shortcut item.
        /// </summary>
        private async Task RemoveShortcutWithConfirmation(ShortcutItem item)
        {
            if (item == null) return;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Remove Shortcut",
                Content = $"Are you sure you want to remove '{item.Name}'?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await ShowDialogAsync(confirmDialog);
            if (result == ContentDialogResult.Primary)
            {
                RemoveShortcutInternal(item);
            }
        }

        /// <summary>
        /// Performs the actual removal of a shortcut from all groups and clears the selection state.
        /// </summary>
        private void RemoveShortcutInternal(ShortcutItem item)
        {
            if (item == null) return;

            if (_selectedItem == item)
            {
                _selectedItem.IsSelected = false;
                _selectedItem = null;
            }

            // Remove from all groups
            foreach (var group in MyGroups)
            {
                if (group.Shortcuts.Contains(item))
                {
                    group.Shortcuts.Remove(item);
                }
            }

            // Ensure it's removed from search results too
            if (_searchResultGroup.Shortcuts.Contains(item))
            {
                _searchResultGroup.Shortcuts.Remove(item);
            }

            SaveStates();
            UpdateWindowSize();
        }

        /// <summary>
        /// Safely shows a ContentDialog by ensuring only one can be open at a time.
        /// Serializes requests using a semaphore to allow nested/sequential dialogs.
        /// </summary>
        public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
        {
            await _dialogSemaphore.WaitAsync();
            _isDialogOpen = true;
            try
            {
                return await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error showing dialog");
                return ContentDialogResult.None;
            }
            finally
            {
                _isDialogOpen = false;
                _dialogSemaphore.Release();
            }
        }

        private void MenuRegenerateIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                TriggerRegenerateIcon(item);
            }
        }

        private async void TriggerRegenerateIcon(ShortcutItem item)
        {
            if (await EnsurePathValid(item))
            {
                if (ExtractAndSaveIcon(item, force: true))
                {
                    SaveStates();
                }
            }
        }

        private async void MenuChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                if (!await EnsurePathValid(item)) return;

                var picker = new FileOpenPicker();
                var hWnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hWnd);

                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".ico");
                picker.FileTypeFilter.Add(".exe");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    if (ExtractAndSaveIcon(item, sourcePath: file.Path, force: true))
                    {
                        // Success
                    }
                    SaveStates();
                }
            }
        }

        private async void MenuProperties_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                await ShowPropertiesDialogInternal(item);
            }
        }

        private async Task ShowPropertiesDialogInternal(ShortcutItem item)
        {
            var dialog = new PropertiesDialog(item);
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await ShowDialogAsync(dialog);
            if (result == ContentDialogResult.Primary)
            {
                // Refresh icon in case path or name changed
                ExtractAndSaveIcon(item, force: false);
                SaveStates();
            }
        }

        /// <summary>
        /// Checks if a path exists. If not, prompts the user to remove the shortcut or edit properties.
        /// </summary>
        private async Task<bool> EnsurePathValid(ShortcutItem item)
        {
            if (string.IsNullOrEmpty(item.Path) || (!File.Exists(item.Path) && !Directory.Exists(item.Path)))
            {
                var dialog = new ContentDialog
                {
                    Title = "Invalid Path",
                    Content = $"The path '{item.Path}' is invalid or no longer exists. What would you like to do?",
                    PrimaryButtonText = "Remove Shortcut",
                    SecondaryButtonText = "Edit Properties",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await ShowDialogAsync(dialog);
                if (result == ContentDialogResult.Primary)
                {
                    RemoveShortcutInternal(item);
                    return false;
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    await ShowPropertiesDialogInternal(item);
                    return false; 
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Generates a standardized path for an icon file in the centralized cache.
        /// Uses file extension for non-executables and filename for executables.
        /// </summary>
        private string GetIconCachePath(string filePath, string shortcutName = null)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string iconsDir = Path.Combine(AppContext.BaseDirectory, "icons");
            string fileName;

            if (Directory.Exists(filePath))
            {
                fileName = "folder_default.png";
            }
            else
            {
                string extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
                if (extension == "lnk" && !string.IsNullOrEmpty(shortcutName))
                {
                    // Use shortcut name for .lnk files to support custom/web-app icons
                    fileName = shortcutName.ToLower() + ".png";
                }
                else if (extension == "exe" || extension == "ico" || extension == "lnk")
                {
                    fileName = Path.GetFileNameWithoutExtension(filePath).ToLower() + ".png";
                    if (string.IsNullOrEmpty(fileName) || fileName == ".png")
                    {
                        fileName = "unknown_app.png";
                    }
                }
                else
                {
                    fileName = $"ext_{extension}.png";
                }
            }

            // Sanitize filename
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(iconsDir, fileName);
        }

        /// <summary>
        /// Extracts a high-quality icon from a file and saves it to the centralized cache as a PNG.
        /// </summary>
        /// <summary>
        /// Extracts a high-quality icon for a shortcut item and saves it to the local cache.
        /// Supports folders, executables, .ico files, and complex .lnk shortcuts.
        /// </summary>
        /// <param name="item">The shortcut item to process.</param>
        /// <param name="sourcePath">Optional explicit source path (e.g. when manually changing an icon).</param>
        /// <param name="force">If true, regenerates the icon even if it already exists in cache.</param>
        /// <returns>True if extraction was successful.</returns>
        private bool ExtractAndSaveIcon(ShortcutItem item, string sourcePath = null, bool force = false)
        {
            try
            {
                string effectiveSource = sourcePath ?? item.Path;
                if (string.IsNullOrEmpty(effectiveSource))
                    return false;

                bool isFile = File.Exists(effectiveSource);
                bool isDir = Directory.Exists(effectiveSource);

                // Use effectiveSource (the .lnk file) for cache naming, not the target .exe
                string iconPath = GetIconCachePath(effectiveSource, item.Name);
                if (iconPath == null) return false;

                // Skip extraction if icon already exists and we're not forcing a refresh
                if (!force && File.Exists(iconPath) && sourcePath == null)
                {
                    item.Icon = iconPath;
                    return true;
                }

                string iconsDir = Path.GetDirectoryName(iconPath);
                if (!Directory.Exists(iconsDir)) 
                {
                    Directory.CreateDirectory(iconsDir);
                }

                if (isFile && Path.GetExtension(effectiveSource).ToLower() == ".ico" && sourcePath != null)
                {
                    // If manually selecting an .ico, we still convert to PNG for consistency and quality
                    using (var icon = new Icon(effectiveSource))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(iconPath, ImageFormat.Png);
                        }
                    }
                }
                else if (isFile && Path.GetExtension(effectiveSource).ToLower() == ".lnk")
                {
                    // For .lnk files, we use IShellLink to get the clean icon source (path + index)
                    // and then ExtractIconEx to get the icon WITHOUT the shell's link overlay.
                    try
                    {
                        ShellLink shellLink = new ShellLink();
                        IShellLinkW link = (IShellLinkW)shellLink;
                        IPersistFile file = (IPersistFile)shellLink;
                        file.Load(effectiveSource, 0);

                        System.Text.StringBuilder iconPathBuilder = new System.Text.StringBuilder(260);
                        link.GetIconLocation(iconPathBuilder, iconPathBuilder.Capacity, out int iconIndex);
                        
                        string finalIconSource = iconPathBuilder.ToString();
                        if (string.IsNullOrEmpty(finalIconSource))
                        {
                            // If no specific icon is set, resolve the target path to get its icon
                            System.Text.StringBuilder targetPathBuilder = new System.Text.StringBuilder(260);
                            WIN32_FIND_DATAW fd = new WIN32_FIND_DATAW();
                            link.GetPath(targetPathBuilder, targetPathBuilder.Capacity, out fd, 0);
                            finalIconSource = targetPathBuilder.ToString();
                            iconIndex = 0;
                        }

                        // Resolve environment variables like %SystemRoot%
                        finalIconSource = Environment.ExpandEnvironmentVariables(finalIconSource);
                        
                        IntPtr[] largeIcons = new IntPtr[1];
                        if (ExtractIconEx(finalIconSource, iconIndex, largeIcons, null, 1) > 0 && largeIcons[0] != IntPtr.Zero)
                        {
                            using (var icon = Icon.FromHandle(largeIcons[0]))
                            {
                                using (var bitmap = icon.ToBitmap())
                                {
                                    bitmap.Save(iconPath, ImageFormat.Png);
                                }
                            }
                            DestroyIcon(largeIcons[0]);
                        }
                        else
                        {
                            // Fallback to ShellInfo if extraction fails
                            // CRITICAL: Use the resolved target (finalIconSource) instead of the .lnk file (effectiveSource)
                            // to avoid the shell automatically adding the shortcut arrow overlay for document links.
                            string shellSource = string.IsNullOrEmpty(finalIconSource) ? effectiveSource : finalIconSource;
                            ExtractUsingShellInfo(shellSource, iconPath, isFile, isDir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to extract clean icon via IShellLink for {Path}, falling back", effectiveSource);
                        ExtractUsingShellInfo(effectiveSource, iconPath, isFile, isDir);
                    }
                }
                else
                {
                    ExtractUsingShellInfo(effectiveSource, iconPath, isFile, isDir);
                }

                // Force UI refresh by triggering property change (even if path is identical)
                item.Icon = ""; 
                item.Icon = iconPath;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error extracting icon for {Path}", item.Path);
            }
            return false;
        }

        /// <summary>
        /// Uses the Win32 Shell API (SHGetFileInfo) to extract high-quality, alpha-transparent icons.
        /// Also handles folder and file-type associated icons.
        /// </summary>
        private void ExtractUsingShellInfo(string effectiveSource, string iconPath, bool isFile, bool isDir)
        {
            // Use Win32 SHGetFileInfo for high-quality extraction (32-bit with Alpha)
            SHFILEINFO shinfo = new SHFILEINFO();
            // We intentionally do NOT use SHGFI_LINKOVERLAY (0x8000) to avoid the shortcut arrow.
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            uint attributes = FILE_ATTRIBUTE_NORMAL;

            string extension = Path.GetExtension(effectiveSource).ToLower();

            // Try to get icon without overlay by using SHGFI_ICONLOCATION first.
            // This retrieves the file and index of the actual icon, bypassing the shell's overlay logic.
            // For .lnk files, this is essential to avoid the shortcut arrow.
            if (isFile)
            {
                if (SHGetFileInfo(effectiveSource, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICONLOCATION) != IntPtr.Zero)
                {
                    string loc = shinfo.szDisplayName;
                    int index = shinfo.iIcon;
                    if (!string.IsNullOrEmpty(loc))
                    {
                        loc = Environment.ExpandEnvironmentVariables(loc);
                        IntPtr[] hIconLarge = new IntPtr[1];
                        // ExtractIconEx handles negative resource IDs correctly
                        if (ExtractIconEx(loc, index, hIconLarge, null, 1) > 0 && hIconLarge[0] != IntPtr.Zero)
                        {
                            using (var icon = Icon.FromHandle(hIconLarge[0]))
                            {
                                using (var bitmap = icon.ToBitmap())
                                {
                                    bitmap.Save(iconPath, ImageFormat.Png);
                                }
                            }
                            DestroyIcon(hIconLarge[0]);
                            return;
                        }
                    }
                }
            }

            // If it doesn't exist, or if it's a directory, we can use SHGFI_USEFILEATTRIBUTES
            // to get the icon associated with the file type or folder.
            if (!isFile && !isDir)
            {
                flags |= SHGFI_USEFILEATTRIBUTES;
                if (string.IsNullOrEmpty(extension))
                {
                    attributes = FILE_ATTRIBUTE_DIRECTORY;
                }
            }
            else if (isDir)
            {
                attributes = FILE_ATTRIBUTE_DIRECTORY;
            }

            IntPtr hImgLarge = SHGetFileInfo(effectiveSource, attributes, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);

            if (shinfo.hIcon != IntPtr.Zero)
            {
                using (var icon = Icon.FromHandle(shinfo.hIcon))
                {
                    using (var bitmap = icon.ToBitmap())
                    {
                        bitmap.Save(iconPath, ImageFormat.Png);
                    }
                }
                DestroyIcon(shinfo.hIcon);
            }
            else if (isFile)
            {
                // Fallback to legacy method if SHGetFileInfo fails
                using (var icon = Icon.ExtractAssociatedIcon(effectiveSource))
                {
                    if (icon != null)
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            bitmap.Save(iconPath, ImageFormat.Png);
                        }
                    }
                }
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                string searchTerm = sender.Text.ToLower();

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    if (MyGroups.Contains(_searchResultGroup))
                    {
                        MyGroups.Remove(_searchResultGroup);
                    }
                }
                else
                {
                    // Find all shortcuts matching name or path
                    var matches = MyGroups
                        .Where(g => g.GroupName != "Search Result")
                        .SelectMany(g => g.Shortcuts)
                        .Where(s => s.Name.ToLower().Contains(searchTerm) || s.Path.ToLower().Contains(searchTerm))
                        .Distinct()
                        .ToList();

                    _searchResultGroup.Shortcuts = new ObservableCollection<ShortcutItem>(matches);

                    if (matches.Any())
                    {
                        if (!MyGroups.Contains(_searchResultGroup))
                        {
                            MyGroups.Insert(0, _searchResultGroup);
                        }
                        
                        // Collapse other groups while searching
                        _isUpdatingStates = true;
                        foreach (var group in MyGroups)
                        {
                            if (group != _searchResultGroup)
                            {
                                group.IsExpanded = false;
                            }
                        }
                        _isUpdatingStates = false;

                        // Auto-highlight the first result
                        ClearSelection();
                        if (_searchResultGroup.Shortcuts.Any())
                        {
                            _selectedItem = _searchResultGroup.Shortcuts[0];
                            _selectedItem.IsSelected = true;
                        }
                    }
                    else if (MyGroups.Contains(_searchResultGroup))
                    {
                        MyGroups.Remove(_searchResultGroup);
                    }
                }
                UpdateWindowSize();
            }
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            // Launch first match on Enter
            var firstMatch = _searchResultGroup.Shortcuts.FirstOrDefault();
            if (firstMatch == null)
            {
                firstMatch = MyGroups
                    .Where(g => g.GroupName != "Search Result")
                    .SelectMany(g => g.Shortcuts)
                    .FirstOrDefault(s => s.Name.ToLower().Contains(sender.Text.ToLower()));
            }

            if (firstMatch != null)
            {
                ExecuteShortcut(firstMatch);
            }
        }

        private void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            if (_isUpdatingStates) return;
            _isUpdatingStates = true;

            // Accordion behavior: only one group expanded at a time
            if (sender.DataContext is ShortcutGroup expandedGroup)
            {
                expandedGroup.IsExpanded = true; // Force update model before saving
                foreach (var group in MyGroups)
                {
                    if (group != expandedGroup)
                    {
                        group.IsExpanded = false;
                    }
                }
            }

            _isUpdatingStates = false;
            SaveStates();
            this.DispatcherQueue.TryEnqueue(() => UpdateWindowSize());
        }

        private void Expander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args)
        {
            if (_isUpdatingStates) return;
            SaveStates();
            UpdateWindowSize();
        }

        private async void MenuGroupLaunchAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                if (group.Shortcuts.Count == 0) return;

                // Confirm if launching a large number of items
                if (group.Shortcuts.Count > 5)
                {
                    ContentDialog confirmDialog = new ContentDialog
                    {
                        Title = "Launch All",
                        Content = $"Are you sure you want to launch all {group.Shortcuts.Count} shortcuts in '{group.GroupName}'?",
                        PrimaryButtonText = "Launch",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await ShowDialogAsync(confirmDialog);
                    if (result != ContentDialogResult.Primary) return;
                }

                foreach (var item in group.Shortcuts)
                {
                    ExecuteShortcut(item);
                }
            }
        }

        private void MenuGroupMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                int index = MyGroups.IndexOf(group);
                int limit = MyGroups.Contains(_searchResultGroup) ? 1 : 0;

                if (index > limit)
                {
                    MyGroups.Remove(group);
                    MyGroups.Insert(index - 1, group);
                    SaveStates();
                }
            }
        }

        private void MenuGroupMoveToTop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                int index = MyGroups.IndexOf(group);
                int target = MyGroups.Contains(_searchResultGroup) ? 1 : 0;

                if (index > target)
                {
                    MyGroups.Remove(group);
                    MyGroups.Insert(target, group);
                    SaveStates();
                }
            }
        }

        private void MenuGroupMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                int index = MyGroups.IndexOf(group);
                if (index < MyGroups.Count - 1)
                {
                    MyGroups.Remove(group);
                    MyGroups.Insert(index + 1, group);
                    SaveStates();
                }
            }
        }

        private void MenuGroupMoveToBottom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                int index = MyGroups.IndexOf(group);
                if (index < MyGroups.Count - 1)
                {
                    MyGroups.Remove(group);
                    MyGroups.Add(group);
                    SaveStates();
                }
            }
        }

        private async void MenuNewGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("New Group", "Enter the name for the new group:");
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await ShowDialogAsync(dialog);
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var newGroup = new ShortcutGroup { GroupName = dialog.InputText, IsExpanded = true };
                MyGroups.Add(newGroup);
                SaveStates();
            }
        }

        private void MenuReload_Click(object sender, RoutedEventArgs e)
        {
            LoadShortcuts();
            UpdateWindowSize();
        }

        private async void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog(this);
            dialog.XamlRoot = this.Content.XamlRoot;
            await ShowDialogAsync(dialog);
        }

        private void MenuHide_Click(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Hide();
        }

        /// <summary>
        /// Regenerates all shortcut icons.
        /// </summary>
        /// <param name="askConfirmation">If true, shows a confirmation dialog before proceeding.</param>
        public async Task RegenerateAllIcons(bool askConfirmation = true)
        {
            if (askConfirmation)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Regenerate All Icons",
                    Content = "This will refresh icons for all shortcuts in all groups. Any manually customized icons will be lost. Do you want to proceed?",
                    PrimaryButtonText = "Regenerate",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await ShowDialogAsync(confirmDialog);
                if (result != ContentDialogResult.Primary) return;
            }

            foreach (var group in MyGroups)
            {
                if (group == _searchResultGroup) continue;

                foreach (var item in group.Shortcuts)
                {
                    ExtractAndSaveIcon(item, force: true);
                }
            }
            SaveStates();
        }

        /// <summary>
        /// Deletes cached icons that are no longer referenced by any shortcut.
        /// </summary>
        /// <param name="askConfirmation">If true, shows a confirmation dialog before proceeding.</param>
        /// <returns>The number of icons deleted.</returns>
        public async Task<int> CleanUpUnusedIcons(bool askConfirmation = true)
        {
            if (askConfirmation)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Clean Up Icons",
                    Content = "This will delete all cached icons that are not currently used by any of your shortcuts. Do you want to proceed?",
                    PrimaryButtonText = "Clean Up",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await ShowDialogAsync(confirmDialog);
                if (result != ContentDialogResult.Primary) return 0;
            }

            int deletedCount = 0;
            try
            {
                string iconsDir = Path.Combine(AppContext.BaseDirectory, "icons");
                if (!Directory.Exists(iconsDir)) return 0;

                // Track all icon filenames that are in use
                var usedIcons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Check current memory state (MyGroups)
                foreach (var name in MyGroups
                    .Where(g => g.GroupName != "Search Result")
                    .SelectMany(g => g.Shortcuts)
                    .Select(s => Path.GetFileName(s.Icon))
                    .Where(n => !string.IsNullOrEmpty(n)))
                {
                    usedIcons.Add(name);
                }

                // 2. Check disk state (shortcuts.json) to catch anything not currently loaded or in sync
                try
                {
                    string jsonPath = shortcutFile;
                    if (!File.Exists(jsonPath)) jsonPath = Path.GetFullPath(shortcutFile);

                    if (File.Exists(jsonPath))
                    {
                        string json = File.ReadAllText(jsonPath);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            var groupsOnDisk = JsonSerializer.Deserialize(json, ShortcutSerializationContext.Default.ListShortcutGroup);
                            if (groupsOnDisk != null)
                            {
                                foreach (var name in groupsOnDisk
                                    .SelectMany(g => g.Shortcuts)
                                    .Select(s => Path.GetFileName(s.Icon))
                                    .Where(n => !string.IsNullOrEmpty(n)))
                                {
                                    usedIcons.Add(name);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not read shortcuts.json during icon cleanup; proceeding with memory-only check.");
                }

                // Get all files in the icons directory
                var allIconFiles = Directory.GetFiles(iconsDir);

                foreach (var file in allIconFiles)
                {
                    string fileName = Path.GetFileName(file);
                    if (!usedIcons.Contains(fileName))
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Could not delete unused icon file: {File}", file);
                        }
                    }
                }

                Log.Information("Cleaned up {Count} unused icon files.", deletedCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during icon cleanup");
            }
            return deletedCount;
        }

        /// <summary>
        /// Scans all shortcuts and removes those with invalid/non-existent paths.
        /// </summary>
        /// <param name="askConfirmation">If true, shows a confirmation dialog before proceeding.</param>
        /// <returns>The number of shortcuts removed.</returns>
        public async Task<int> CleanUpInvalidShortcuts(bool askConfirmation = true)
        {
            if (askConfirmation)
            {
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Remove Invalid Shortcuts",
                    Content = "This will scan all groups and remove shortcuts that point to files or directories that no longer exist. Do you want to proceed?",
                    PrimaryButtonText = "Remove Invalid",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await ShowDialogAsync(confirmDialog);
                if (result != ContentDialogResult.Primary) return 0;
            }

            int removedCount = 0;
            try
            {
                // We iterate backwards to allow safe removal while looping
                foreach (var group in MyGroups)
                {
                    if (group == _searchResultGroup) continue;

                    for (int i = group.Shortcuts.Count - 1; i >= 0; i--)
                    {
                        var item = group.Shortcuts[i];
                        if (string.IsNullOrEmpty(item.Path) || (!File.Exists(item.Path) && !Directory.Exists(item.Path)))
                        {
                            group.Shortcuts.RemoveAt(i);
                            removedCount++;
                        }
                    }
                }

                if (removedCount > 0)
                {
                    SaveStates();
                    UpdateWindowSize();
                    Log.Information("Cleaned up {Count} invalid shortcuts.", removedCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during invalid shortcut cleanup");
            }
            return removedCount;
        }

        private void MainContextMenu_Opening(object sender, object e)
        {
            // Dynamically show/hide "Move to next monitor" based on current display count
            if (MoveToNextMonitorMenuItem != null)
            {
                var displays = DisplayArea.FindAll();
                MoveToNextMonitorMenuItem.Visibility = displays.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuMoveToNextMonitor_Click(object sender, RoutedEventArgs e)
        {
            var allDisplays = DisplayArea.FindAll();
            var displaysList = new List<DisplayArea>();
            for (int i = 0; i < allDisplays.Count; i++)
            {
                displaysList.Add(allDisplays[i]);
            }
            
            var displays = displaysList.OrderBy(d => d.OuterBounds.X).ToList();
            if (displays.Count <= 1) return;

            var currentDisplay = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            int currentIndex = displays.FindIndex(d => d.DisplayId.Value == currentDisplay.DisplayId.Value);
            int nextIndex = (currentIndex + 1) % displays.Count;
            var nextDisplay = displays[nextIndex];

            double scale = this.Content.XamlRoot?.RasterizationScale ?? 1.0;
            var workArea = nextDisplay.WorkArea;
            int widthPhysical = (int)(AppSettings.AppWidthLogical * scale); 
            int heightPhysical = (int)(workArea.Height * AppSettings.AppMinHeightMultiplier);
            
            // Center horizontally on the next monitor, keep slightly above center vertically
            int x = workArea.X + (workArea.Width - widthPhysical) / 2;
            int y = workArea.Y + (int)(workArea.Height * AppSettings.AppTopMarginMultiplier); 
            
            _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, widthPhysical, heightPhysical));

            // Force update window size for the new monitor's constraints
            this.DispatcherQueue.TryEnqueue(() => UpdateWindowSize());
        }

        /// <summary>
        /// Loads display preferences from the external settings file.
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    string json = File.ReadAllText(settingsFile);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var settings = JsonSerializer.Deserialize(json, ShortcutSerializationContext.Default.DisplaySettings);
                        if (settings != null)
                        {
                            AppSettings = settings;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load display settings; using defaults.");
            }
        }

        /// <summary>
        /// Persists display preferences to the external settings file.
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(AppSettings, ShortcutSerializationContext.Default.DisplaySettings);
                File.WriteAllText(settingsFile, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving display settings");
            }
        }

        private async void MenuDisplayPreferences_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DisplaySettingsDialog(AppSettings);
            dialog.XamlRoot = this.Content.XamlRoot;

            var result = await ShowDialogAsync(dialog);
            if (result == ContentDialogResult.Primary)
            {
                SaveSettings();
                // Settings PropertyChanged events will trigger UpdateWindowSize
            }
        }

        private async void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            await ShowDialogAsync(dialog);
        }

        private async void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog exitDialog = new ContentDialog
            {
                Title = "Exit Shortcut Manager",
                Content = "Are you sure you want to exit the application?",
                PrimaryButtonText = "Exit",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await ShowDialogAsync(exitDialog);
            if (result == ContentDialogResult.Primary)
            {
                this.Close();
            }
        }

        private async void MenuGroupRename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                if (group == _searchResultGroup) return;

                var dialog = new InputDialog("Rename Group", "Enter the new name for the group:", group.GroupName);
                dialog.XamlRoot = this.Content.XamlRoot;

                var result = await ShowDialogAsync(dialog);
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    group.GroupName = dialog.InputText;
                    SaveStates();
                }
            }
        }

        private async void MenuGroupDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                if (group == _searchResultGroup) return;

                ContentDialog deleteDialog = new ContentDialog
                {
                    Title = "Delete Group",
                    Content = $"Are you sure you want to delete the group '{group.GroupName}'? All shortcuts within this group will be lost.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await ShowDialogAsync(deleteDialog);
                if (result == ContentDialogResult.Primary)
                {
                    MyGroups.Remove(group);
                    SaveStates();
                    UpdateWindowSize();
                }
            }
        }

        private ShortcutItem _draggedItem;
        private ShortcutGroup _draggedFromGroup;

        private void Shortcut_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShortcutItem item)
            {
                _draggedItem = item;
                _draggedFromGroup = MyGroups.FirstOrDefault(g => g.Shortcuts.Contains(item));
                
                e.Data.SetText(item.Id);
                e.Data.RequestedOperation = DataPackageOperation.Move;
            }
        }

        private void Shortcut_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem != null)
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Move here";
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private void Shortcut_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem != null && sender is FrameworkElement fe && fe.DataContext is ShortcutItem targetItem)
            {
                var targetGroup = MyGroups.FirstOrDefault(g => g.Shortcuts.Contains(targetItem));
                if (targetGroup != null && _draggedFromGroup != null)
                {
                    if (targetItem == _draggedItem) return;

                    int targetIndex = targetGroup.Shortcuts.IndexOf(targetItem);
                    
                    _draggedFromGroup.Shortcuts.Remove(_draggedItem);
                    targetGroup.Shortcuts.Insert(targetIndex, _draggedItem);

                    _draggedItem = null;
                    _draggedFromGroup = null;

                    SaveStates();
                    e.Handled = true;
                }
            }
        }

        private void Expander_DragOver(object sender, DragEventArgs e)
        {
            if (_draggedItem != null)
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = "Move to group";
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Add to group";
            }
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }

        [RequiresUnreferencedCode("Calls ShortcutManager.MainWindow.ResolveLnk(String)")]
        private async void Expander_Drop(object sender, DragEventArgs e)
        {
            // Internal Move: Shortcut to Group
            if (_draggedItem != null && sender is Expander expander && expander.DataContext is ShortcutGroup targetGroup)
            {
                if (_draggedFromGroup != null && _draggedFromGroup != targetGroup)
                {
                    _draggedFromGroup.Shortcuts.Remove(_draggedItem);
                    targetGroup.Shortcuts.Add(_draggedItem);
                }
                _draggedItem = null;
                _draggedFromGroup = null;
                SaveStates();
                UpdateWindowSize();
                e.Handled = true;
                return;
            }

            // External Move: File/Folder Drop
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                ShortcutGroup finalTargetGroup = null;

                if (sender is Expander expander2 && expander2.DataContext is ShortcutGroup group)
                {
                    finalTargetGroup = group;
                }
                else if (!MyGroups.Any(g => g.GroupName != "Search Result"))
                {
                    // If no groups exist, create a default one
                    finalTargetGroup = new ShortcutGroup { GroupName = "Default", IsExpanded = true };
                    MyGroups.Add(finalTargetGroup);
                }

                if (finalTargetGroup != null)
                {
                    foreach (var storageItem in items)
                    {
                        string targetPath = storageItem.Path;
                        string originalLnkPath = null;
                        string arguments = "";
                        string name = storageItem.Name;

                        if (storageItem is Windows.Storage.StorageFile file)
                        {
                            name = file.DisplayName;
                            if (file.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                originalLnkPath = targetPath;
                                try
                                {
                                    // Resolve Windows shortcuts to their targets
                                    var resolved = ResolveLnk(targetPath);
                                    if (!string.IsNullOrEmpty(resolved.target))
                                    {
                                        targetPath = resolved.target;
                                        arguments = resolved.args;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Error resolving .lnk for {LnkPath}", targetPath);
                                }
                            }
                        }

                        var newItem = new ShortcutItem
                        {
                            Name = name,
                            Path = targetPath,
                            Arguments = arguments,
                            Id = Guid.NewGuid().ToString()
                        };

                        ExtractAndSaveIcon(newItem, originalLnkPath, false);
                        finalTargetGroup.Shortcuts.Add(newItem);
                    }
                    SaveStates();
                    UpdateWindowSize();
                }
            }
        }

        /// <summary>
        /// Resolves a Windows .lnk file to its actual target path and arguments.
        /// </summary>
        [RequiresUnreferencedCode("Calls System.Runtime.InteropServices.Marshal.ReleaseComObject(Object)")]
        private (string target, string args) ResolveLnk(string lnkPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return (null, null);

                object shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                
                string target = shortcut.TargetPath;
                string args = shortcut.Arguments;

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                return (target, args);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ResolveLnk error for {LnkPath}", lnkPath);
                return (null, null);
            }
        }

        /// <summary>
        /// Toggles window visibility. Intelligent foreground management:
        /// Brings to front if visible but backgrounded/minimized, otherwise toggles show/hide.
        /// </summary>
        private void ToggleVisibilityCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                if (this.AppWindow.IsVisible)
                {
                    IntPtr foregroundHWnd = GetForegroundWindow();
                    bool isMinimized = IsIconic(hWnd);

                    if (foregroundHWnd != hWnd || isMinimized)
                    {
                        if (isMinimized)
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                        }
                        else
                        {
                            ShowWindow(hWnd, SW_SHOW);
                        }
                        SetForegroundWindow(hWnd);
                        this.Activate();
                    }
                    else
                    {
                        this.AppWindow.Hide();
                    }
                }
                else
                {
                    this.AppWindow.Show();
                    this.Activate();
                    SetForegroundWindow(hWnd);
                    
                    this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    if (this.AppWindow.Presenter is OverlappedPresenter presenter)
                    {
                        presenter.SetBorderAndTitleBar(false, false);
                    }
                }
            });
        }

        private void Shortcut_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.DataContext is ShortcutItem item)
            {
                SelectShortcut(item, grid);
            }
        }

        private void ClearOrHideApp() {
            ClearSelection();
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                SearchBox.Focus(FocusState.Programmatic);
            }
            else
            {
                this.AppWindow.Hide();
            }
        }

        private void OnEscPressed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ClearOrHideApp();
            args.Handled = true;
        }

        /// <summary>
        /// Handles application-wide keyboard shortcuts and navigation.
        /// </summary>
        private async void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (_isDialogOpen) return;

            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ClearOrHideApp();
                e.Handled = true; 
                return;
            }

            // Alpha-numeric: Focus Search Box if not focused and no modifier keys pressed
            var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            var altStateCurrent = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool isCtrlPressed = ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            bool isAltPressedCurrent = altStateCurrent.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (!isCtrlPressed && !isAltPressedCurrent && 
                e.Key >= Windows.System.VirtualKey.A && e.Key <= Windows.System.VirtualKey.Z)
            {
                var focusedElement = FocusManager.GetFocusedElement(this.Content.XamlRoot);
                
                // Don't snatch focus if we're already in a text input or a dialog-like control
                if (!(focusedElement is TextBox) && 
                    !(focusedElement is PasswordBox) && 
                    !(focusedElement is AutoSuggestBox) &&
                    !object.ReferenceEquals(focusedElement, SearchBox))
                {
                    SearchBox.Focus(FocusState.Keyboard);
                    // The character will naturally be typed into the focused AutoSuggestBox
                    // as long as we don't mark the event as handled here.
                }
            }

            // Enter: Launch Selected Item
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_selectedItem != null)
                {
                    if (await EnsurePathValid(_selectedItem))
                    {
                        ExecuteShortcut(_selectedItem);
                    }
                    e.Handled = true;
                    return;
                }
            }

            // Delete: Remove Selected Item
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                if (_selectedItem != null)
                {
                    await RemoveShortcutWithConfirmation(_selectedItem);
                    e.Handled = true;
                    return;
                }
            }

            // Navigation: Arrow Keys
            if (e.Key == Windows.System.VirtualKey.Left || e.Key == Windows.System.VirtualKey.Right ||
                e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
            {
                NavigateShortcuts(e.Key);
                e.Handled = true;
                return;
            }

            // F1 - F5: Toggle Expansion of Custom Groups
            if (e.Key >= Windows.System.VirtualKey.F1 && e.Key <= Windows.System.VirtualKey.F5)
            {
                int index = (int)e.Key - (int)Windows.System.VirtualKey.F1;
                int offset = MyGroups.Contains(_searchResultGroup) ? 1 : 0;
                int actualIndex = index + offset;

                if (actualIndex < MyGroups.Count)
                {
                    var group = MyGroups[actualIndex];
                    group.IsExpanded = !group.IsExpanded;
                    SaveStates();
                    e.Handled = true;
                }
                return;
            }

            // Alt + 1-5: Quick Launch
            var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
            bool isAltPressed = altState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (isAltPressed && e.Key >= Windows.System.VirtualKey.Number1 && e.Key <= Windows.System.VirtualKey.Number5)
            {
                int index = (int)e.Key - (int)Windows.System.VirtualKey.Number1;
                ShortcutItem itemToLaunch = null;

                // 1. Prioritize Search Results if active
                if (MyGroups.Contains(_searchResultGroup))
                {
                    if (index < _searchResultGroup.Shortcuts.Count)
                    {
                        itemToLaunch = _searchResultGroup.Shortcuts[index];
                    }
                }
                else
                {
                    // 2. Fallback to currently expanded custom group
                    var expandedGroup = MyGroups.FirstOrDefault(g => g.IsExpanded);
                    if (expandedGroup != null && index < expandedGroup.Shortcuts.Count)
                    {
                        itemToLaunch = expandedGroup.Shortcuts[index];
                    }
                }

                if (itemToLaunch != null)
                {
                    ExecuteShortcut(itemToLaunch);
                    e.Handled = true;
                }
                return;
            }
        }
    }
}
