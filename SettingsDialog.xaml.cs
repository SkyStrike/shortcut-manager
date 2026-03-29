using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ShortcutManager
{
    /// <summary>
    /// Dialog for managing application-wide settings.
    /// </summary>
    public sealed partial class SettingsDialog : ContentDialog
    {
        private const string ShortcutName = "ShortcutManager.lnk";
        private MainWindow _mainWindow;
        private bool _isUpdatingSettings = false;

        public SettingsDialog(MainWindow mainWindow)
        {
            this.InitializeComponent();
            _mainWindow = mainWindow;
            LoadSettings();
        }

        /// <summary>
        /// Gets the full path to the shortcut file in the Windows Startup folder.
        /// </summary>
        private string GetStartupShortcutPath()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startupFolder, ShortcutName);
        }

        /// <summary>
        /// Populates the dialog with current system and application states.
        /// </summary>
        private void LoadSettings()
        {
            _isUpdatingSettings = true;
            try
            {
                // This pulls the version GitVersion calculated during the build
                var version = typeof(MainWindow).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                VersionTextBox.Text = $"Version: {version}";

                // Check if the application is already configured to run at startup
                string shortcutPath = GetStartupShortcutPath();
                bool exists = File.Exists(shortcutPath);
                
                // Set these without triggering the logic in event handlers
                StartupCheckBox.IsChecked = exists;
                if (exists)
                {
                    StartHiddenCheckBox.IsChecked = CheckIfShortcutIsHidden(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading settings");
                VersionTextBox.Text = "Build: Version info unavailable";
            }
            finally
            {
                _isUpdatingSettings = false;
            }
        }

        private bool CheckIfShortcutIsHidden(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                object shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                
                string args = shortcut.Arguments;
                bool isHidden = !string.IsNullOrEmpty(args) && (args.Contains("-hidden") || args.Contains("/hidden"));

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                
                return isHidden;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read shortcut arguments: {Path}", shortcutPath);
                return false;
            }
        }

        /// <summary>
        /// Handles the Checked event to create a startup shortcut.
        /// </summary>
        private void StartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSettings) return;

            try
            {
                UpdateStartupShortcut();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error enabling startup shortcut");
            }
        }

        /// <summary>
        /// Handles the Unchecked event to remove the startup shortcut.
        /// </summary>
        private void StartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSettings) return;

            try
            {
                string shortcutPath = GetStartupShortcutPath();
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error disabling startup shortcut");
            }
        }

        private void StartHiddenCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSettings) return;
            UpdateStartupShortcut();
        }

        private void StartHiddenCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingSettings) return;
            UpdateStartupShortcut();
        }

        private void UpdateStartupShortcut()
        {
            if (StartupCheckBox.IsChecked != true) return;

            try
            {
                string shortcutPath = GetStartupShortcutPath();
                string targetPath = Process.GetCurrentProcess().MainModule.FileName;
                bool startHidden = StartHiddenCheckBox.IsChecked ?? false;
                
                CreateShortcut(targetPath, shortcutPath, startHidden);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating startup shortcut");
            }
        }

        /// <summary>
        /// Uses COM (WScript.Shell) to create a Windows shell shortcut.
        /// </summary>
        /// <param name="targetPath">The application executable path.</param>
        /// <param name="shortcutPath">The destination .lnk path.</param>
        /// <param name="startHidden">Whether to add the /hidden argument.</param>
        private void CreateShortcut(string targetPath, string shortcutPath, bool startHidden)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                // Invoke CreateShortcut via reflection
                dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Arguments = startHidden ? "/hidden" : "";
                shortcut.Description = "Shortcut Manager";
                shortcut.Save();

                // Clean up COM objects
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CreateShortcut error for {ShortcutPath}", shortcutPath);
            }
        }

        private void OpenDirButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppContext.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening application directory");
            }
        }

        private void OpenStartupDirButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                Process.Start(new ProcessStartInfo
                {
                    FileName = startupFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error opening startup directory");
            }
        }

        private async void RegenerateAllIcons_Click(object sender, RoutedEventArgs e)
        {
            // Close the current settings dialog to allow the confirmation dialog to show clearly
            this.Hide();
            await _mainWindow.RegenerateAllIcons();
        }

        private async void CleanUpIcons_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            await _mainWindow.CleanUpUnusedIcons();
        }

        private async void RemoveInvalidShortcuts_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            await _mainWindow.CleanUpInvalidShortcuts();
        }

        /// <summary>
        /// Creates a backup of the shortcuts.json file in a dedicated backups directory.
        /// </summary>
        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string jsonPath = Path.Combine(baseDir, "shortcuts.json");
                
                if (!File.Exists(jsonPath))
                {
                    this.Hide(); // Hide settings dialog before showing result
                    await ShowMessageDialog("Backup Failed", "The source shortcuts.json file was not found.");
                    return;
                }

                string backupsDir = Path.Combine(baseDir, "backups");
                if (!Directory.Exists(backupsDir))
                {
                    Directory.CreateDirectory(backupsDir);
                }

                string timestamp = DateTime.Now.ToString("yyMMdd-HHmmss");
                string backupFileName = $"shortcuts-{timestamp}.json.bak";
                string backupPath = Path.Combine(backupsDir, backupFileName);

                File.Copy(jsonPath, backupPath, true);
                
                Log.Information("Backup created successfully: {BackupPath}", backupPath);
                
                this.Hide(); // Hide settings dialog before showing result
                await ShowMessageDialog("Backup Successful", $"Shortcuts backed up to:\n{backupFileName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating backup");
                this.Hide(); // Hide settings dialog before showing error
                await ShowMessageDialog("Backup Error", $"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays a simple message dialog to the user.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="content">The message content to display.</param>
        private async Task ShowMessageDialog(string title, string content)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
