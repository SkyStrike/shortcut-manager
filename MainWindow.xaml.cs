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

            this.Closed += (s, e) => {
                if (MyTrayIcon != null)
                {
                    MyTrayIcon.Dispose();
                }
            };
        }

        private void OnEscPressed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (!string.IsNullOrEmpty(SidebarSearchBox.Text))
            {
                SidebarSearchBox.Text = string.Empty;
                SidebarSearchBox.Focus(FocusState.Programmatic);
            }
            else
            {
                if (_appWindow != null)
                {
                    _appWindow.Hide();
                }
            }
            args.Handled = true;
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

        private void OnShortcutClick(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Button btn && btn.DataContext is ShortcutItem item)
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

        private void SidebarSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
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

        //private void Exit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        //{
        //    if (MyTrayIcon != null)
        //    {
        //        MyTrayIcon.Dispose();
        //    }
        //    Microsoft.UI.Xaml.Application.Current.Exit();
        //}

        private void ToggleSidebarCommand_ExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
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
    }
}
