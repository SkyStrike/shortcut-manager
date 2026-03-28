using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using WinRT.Interop;

namespace ShortcutManager
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<ShortcutGroup> MyGroups { get; set; } = new();
        private ShortcutGroup _searchResultGroup = new() { GroupName = "Search Result", IsExpanded = true };
        private bool _isUpdatingStates = false;
        private AppWindow _appWindow;
        private double _appMinHeightMultiplier = 0.50;

        private string shortcutFile = Path.Combine(AppContext.BaseDirectory, "shortcuts.json");

        public MainWindow()
        {
            InitializeComponent();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(null);

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                _appWindow.IsShownInSwitchers = false;

                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMinimizable = true;
                    presenter.IsMaximizable = false;
                    presenter.IsAlwaysOnTop = true;
                    presenter.SetBorderAndTitleBar(false, false);
                }

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

            // Set system backdrop to Acrylic
            this.SystemBackdrop = new DesktopAcrylicBackdrop();

            LoadShortcuts();
            GroupsList.ItemsSource = MyGroups;
            
            this.DispatcherQueue.TryEnqueue(() => UpdateWindowSize());

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
            // Only clear if clicking the background grid itself, not its children
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
                    var groups = JsonSerializer.Deserialize<List<ShortcutGroup>>(json);
                    
                    if (groups != null)
                    {
                        MyGroups.Clear();
                        foreach (var group in groups)
                        {
                            foreach (var item in group.Shortcuts)
                            {
                                // Resolve relative path to absolute for UI
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
                System.Diagnostics.Debug.WriteLine($"Error loading shortcuts: {ex.Message}");
            }
        }

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

                // Create a clone with relative paths for saving
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

                string json = JsonSerializer.Serialize(groupsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving states: {ex.Message}");
            }
        }

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
            catch (System.ComponentModel.Win32Exception) { }
        }

        private ShortcutItem? _selectedItem;

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

        private void OnShortcutDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShortcutItem item)
            {
                ExecuteShortcut(item);
            }
        }

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                ExecuteShortcut(item);
            }
        }

        private void MenuOpenDir_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item && !string.IsNullOrEmpty(item.Path))
            {
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
                        // Fallback: use /select if Path.GetDirectoryName failed but file exists
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
                    System.Diagnostics.Debug.WriteLine($"Error opening directory: {ex.Message}");
                }
            }
        }

        private void MenuRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
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
        }

        private void MenuRegenerateIcon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
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
                        
                    }

                    //regardless of success or failure, save the config
                    SaveStates();
                }
            }
        }

        private async void MenuProperties_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutItem item)
            {
                var dialog = new PropertiesDialog(item);
                dialog.XamlRoot = this.Content.XamlRoot;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Refresh icon in case path or name changed significantly (though name change doesn't affect cache path anymore)
                    ExtractAndSaveIcon(item, force: false);
                    SaveStates();
                }
            }
        }

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

            // Sanitize Name for filename just in case
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(iconsDir, fileName);
        }

        private bool ExtractAndSaveIcon(ShortcutItem item, string sourcePath = null, bool force = false)
        {
            try
            {
                string effectiveSource = sourcePath ?? item.Path;
                if (string.IsNullOrEmpty(effectiveSource) || !File.Exists(effectiveSource))
                    return false;

                string iconPath = GetIconCachePath(item.Path);
                if (iconPath == null) return false;

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
                            // Overwrite existing file
                            using (var fs = new FileStream(iconPath, FileMode.Create))
                            {
                                icon.Save(fs);
                            }
                        }
                    }
                }

                // Update the icon path. To force UI refresh if path is same, we trigger property change
                item.Icon = ""; 
                item.Icon = iconPath;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting icon: {ex.Message}");
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

        private void MenuGroupMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem menuItem && menuItem.DataContext is ShortcutGroup group)
            {
                int index = MyGroups.IndexOf(group);
                // If Search Results is at 0, don't move past 1
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

        private ShortcutItem? _draggedItem;
        private ShortcutGroup? _draggedFromGroup;

        private void Shortcut_DragStarting(UIElement sender, DragStartingEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ShortcutItem item)
            {
                _draggedItem = item;
                // Find source group
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
                // Find target group
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

        private async void Expander_Drop(object sender, DragEventArgs e)
        {
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
                                    var resolved = ResolveLnk(targetPath);
                                    if (!string.IsNullOrEmpty(resolved.target))
                                    {
                                        targetPath = resolved.target;
                                        arguments = resolved.args;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error resolving .lnk: {ex.Message}");
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

        private (string target, string args) ResolveLnk(string lnkPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
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
                System.Diagnostics.Debug.WriteLine($"ResolveLnk error: {ex.Message}");
                return (null, null);
            }
        }


        private void ToggleVisibilityCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
        {
            // Ensure we are on the UI Thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (this.AppWindow.IsVisible)
                {
                    this.AppWindow.Hide();
                }
                else
                {
                    // Show the window
                    this.AppWindow.Show();
                    this.Activate();

                    // Re-apply the 'Borderless/Translucent' fixes just in case
                    // the window state reset brought the title bar back
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
            if (!string.IsNullOrEmpty(SidebarSearchBox.Text))
            {
                SidebarSearchBox.Text = string.Empty;
                SidebarSearchBox.Focus(FocusState.Programmatic);
            }
            else
            {
                // Close the sidebar or clear text
                this.AppWindow.Hide();
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ClearOrHideApp();

                // Mark as handled so the TextBox doesn't try to process it further
                e.Handled = true;
            }
        }

        private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ClearOrHideApp();
                e.Handled = true; // Prevents the 'Esc' from doing anything else
            }
        }
    }
}
