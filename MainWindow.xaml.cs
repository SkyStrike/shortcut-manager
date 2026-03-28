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
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using WinRT.Interop; 

namespace ShortcutManager
{
    /// <summary>
    /// The main window of the application, managing shortcut groups, searching, and interactions.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Main data collection for the UI
        public ObservableCollection<ShortcutGroup> MyGroups { get; set; } = new();
        
        // Special group for displaying search results
        private ShortcutGroup _searchResultGroup = new() { GroupName = "Search Result", IsExpanded = true };
        
        // Flag to prevent recursive state saving during bulk UI updates
        private bool _isUpdatingStates = false;
        
        private AppWindow _appWindow;
        
        // Window height calculation settings
        private double _appMinHeightMultiplier = 0.50;

        // Path to the configuration file
        private string shortcutFile = Path.Combine(AppContext.BaseDirectory, "shortcuts.json");

        public MainWindow()
        {
            InitializeComponent();

            // Set up borderless/custom title bar window
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                // Hide from task switcher (Alt-Tab) for a more "background app" feel
                _appWindow.IsShownInSwitchers = false;

                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = true;
                    presenter.IsMaximizable = false;
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);
                }

                // Initial positioning: center horizontally, slightly above center vertically
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    int width = 1600;
                    int minHeight = (int)(workArea.Height * _appMinHeightMultiplier);
                    int x = workArea.X + (workArea.Width - width) / 2;
                    int y = workArea.Y + (workArea.Height - 600) / 2 - 50; 
                    _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, minHeight));
                }
            }

            // Set system backdrop to Acrylic for modern Windows 11 aesthetics
            this.SystemBackdrop = new DesktopAcrylicBackdrop();

            LoadShortcuts();
            GroupsList.ItemsSource = MyGroups;
            
            // Adjust window size after initial layout
            this.DispatcherQueue.TryEnqueue(() => UpdateWindowSize());

            // Auto-clear selection when the app loses focus
            this.Activated += MainWindow_Activated;

            this.Closed += (s, e) => {
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
                    var groups = JsonSerializer.Deserialize(json, ShortcutSerializationContext.Default.ListShortcutGroup);

                    if (groups != null)
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
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error loading shortcuts: {ex.Message}");
            }
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
        private void SaveStates()
        {
            if (_isUpdatingStates) return;

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
                                Id = s.Id
                            })
                        )
                    }).ToList();

                // Use source generator for serialization
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
        /// </summary>
        private void UpdateWindowSize()
        {
            if (this.Content is FrameworkElement root && _appWindow != null)
            {
                root.Measure(new Windows.Foundation.Size(_appWindow.Size.Width, double.PositiveInfinity));
                double desiredHeight = root.DesiredSize.Height;

                var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    int currentY = _appWindow.Position.Y;
                    
                    int minHeight = (int)(workArea.Height * _appMinHeightMultiplier);
                    int maxHeight = workArea.Height - (currentY - workArea.Y) - 20; 
                    
                    int newHeight = Math.Max(minHeight, (int)desiredHeight + 10);
                    newHeight = Math.Min(newHeight, maxHeight);
                    
                    if (newHeight != _appWindow.Size.Height)
                    {
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(_appWindow.Size.Width, newHeight));
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
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Path,
                    Arguments = item.Arguments,
                    UseShellExecute = true,
                    Verb = item.RunAsAdmin ? "runas" : ""
                };
                System.Diagnostics.Process.Start(startInfo);
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
            grid.Focus(FocusState.Pointer);
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
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = folder,
                            UseShellExecute = true
                        });
                    }
                    else if (File.Exists(item.Path))
                    {
                        // Fallback: select the file if directory lookup failed
                         System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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
                ContentDialog confirmDialog = new ContentDialog
                {
                    Title = "Remove Shortcut",
                    Content = $"Are you sure you want to remove '{item.Name}'?",
                    PrimaryButtonText = "Remove",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.Content.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    RemoveShortcutInternal(item);
                }
            }
        }

        private void RemoveShortcutInternal(ShortcutItem item)
        {
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

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Refresh icon in case path or name changed
                ExtractAndSaveIcon(item, force: false);
                SaveStates();
            }
        }

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

                var result = await dialog.ShowAsync();
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
        private string GetIconCachePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string iconsDir = Path.Combine(AppContext.BaseDirectory, "icons");
            string extension = Path.GetExtension(filePath).ToLower().TrimStart('.');
            string fileName;

            if (extension == "exe")
            {
                fileName = Path.GetFileNameWithoutExtension(filePath).ToLower() + ".ico";
            }
            else
            {
                fileName = $"ext_{extension}.ico";
            }

            // Sanitize filename
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(iconsDir, fileName);
        }

        /// <summary>
        /// Extracts an icon from a file and saves it to the centralized cache.
        /// </summary>
        private bool ExtractAndSaveIcon(ShortcutItem item, string sourcePath = null, bool force = false)
        {
            try
            {
                string effectiveSource = sourcePath ?? item.Path;
                if (string.IsNullOrEmpty(effectiveSource) || !File.Exists(effectiveSource))
                    return false;

                string iconPath = GetIconCachePath(item.Path);
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

                if (Path.GetExtension(effectiveSource).ToLower() == ".ico")
                {
                    File.Copy(effectiveSource, iconPath, true);
                }
                else
                {
                    using (var icon = Icon.ExtractAssociatedIcon(effectiveSource))
                    {
                        if (icon != null)
                        {
                            using (var fs = new FileStream(iconPath, FileMode.Create))
                            {
                                icon.Save(fs);
                            }
                        }
                    }
                }

                // Force UI refresh by triggering property change (even if path is identical)
                item.Icon = ""; 
                item.Icon = iconPath;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error extracting icon: {ex.Message}");
            }
            return false;
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

                    var result = await confirmDialog.ShowAsync();
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

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var newGroup = new ShortcutGroup { GroupName = dialog.InputText, IsExpanded = true };
                MyGroups.Add(newGroup);
                SaveStates();
            }
        }

        private async void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog();
            dialog.XamlRoot = this.Content.XamlRoot;
            await dialog.ShowAsync();
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

            var result = await exitDialog.ShowAsync();
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

                var result = await dialog.ShowAsync();
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

                var result = await deleteDialog.ShowAsync();
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
                if (sender is Expander expander2 && expander2.DataContext is ShortcutGroup group)
                {
                    foreach (var storageItem in items)
                    {
                        string targetPath = storageItem.Path;
                        string arguments = "";
                        string name = storageItem.Name;

                        if (storageItem is Windows.Storage.StorageFile file)
                        {
                            name = file.DisplayName;
                            if (file.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
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
                                    Log.Error(ex, $"Error resolving .lnk: {ex.Message}");
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

                        ExtractAndSaveIcon(newItem, null, false);
                        group.Shortcuts.Add(newItem);
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


        private void ToggleVisibilityCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (this.AppWindow.IsVisible)
                {
                    this.AppWindow.Hide();
                }
                else
                {
                    this.AppWindow.Show();
                    this.Activate();
                    this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;

                    if (this.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
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
            if (!string.IsNullOrEmpty(SidebarSearchBox.Text))
            {
                SidebarSearchBox.Text = string.Empty;
                SidebarSearchBox.Focus(FocusState.Programmatic);
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
        /// Handles application-wide keyboard shortcuts.
        /// </summary>
        private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ClearOrHideApp();
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
