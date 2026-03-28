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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WinRT.Interop;

namespace ShortcutManager
{
    public sealed partial class MainWindow : Window
    {
        public ObservableCollection<ShortcutGroup> MyGroups { get; set; } = new();
        private ShortcutGroup _searchResultGroup = new() { GroupName = "Search Result", IsExpanded = true };
        private bool _isUpdatingStates = false;
        private AppWindow _appWindow;
        private double dblMinHeight = 0.50;

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
                    presenter.SetBorderAndTitleBar(false, false);
                }

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    int width = 1600;
                    int minHeight = (int)(workArea.Height * dblMinHeight);
                    int x = workArea.X + (workArea.Width - width) / 2;
                    int y = workArea.Y + (workArea.Height - 600) / 2 - 50; 
                    _appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, minHeight));
                }
            }

            // Set system backdrop to Acrylic
            this.SystemBackdrop = new DesktopAcrylicBackdrop();

            LoadMigratedData();
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

        private void LoadMigratedData()
        {
            try
            {
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "tmp", "shortcuts.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "shortcuts.json"));
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
                                if (!string.IsNullOrEmpty(item.Icon)) {
                                    string defaultIconLocation = Path.Combine(AppContext.BaseDirectory, "icons", item.Icon);
                                    if (!File.Exists(item.Icon) && File.Exists(defaultIconLocation)) {
                                        item.Icon = defaultIconLocation;
                                    }
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

        private void SaveStates()
        {
            if (_isUpdatingStates) return;

            try
            {
                string jsonPath = Path.Combine(AppContext.BaseDirectory, "tmp", "shortcuts.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "shortcuts.json"));
                }

                var groupsToSave = MyGroups.Where(g => g.GroupName != "Search Result").ToList();
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
                    
                    int minHeight = (int)(workArea.Height * dblMinHeight);
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

        private void SidebarSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
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

        private void Exit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (MyTrayIcon != null)
            {
                MyTrayIcon.Dispose();
            }
            Microsoft.UI.Xaml.Application.Current.Exit();
        }

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
