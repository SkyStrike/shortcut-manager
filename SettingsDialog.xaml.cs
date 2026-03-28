using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ShortcutManager
{
    public sealed partial class SettingsDialog : ContentDialog
    {
        private const string ShortcutName = "ShortcutManager.lnk";

        public SettingsDialog()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private string GetStartupShortcutPath()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startupFolder, ShortcutName);
        }

        private void LoadSettings()
        {
            try
            {
                // Retrieve version
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                VersionTextBlock.Text = $"Build: {version?.ToString() ?? "Unknown"}";

                // Check Startup folder for shortcut
                StartupCheckBox.IsChecked = File.Exists(GetStartupShortcutPath());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                VersionTextBlock.Text = "Build: Version info unavailable";
            }
        }

        private void StartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                string shortcutPath = GetStartupShortcutPath();
                if (!File.Exists(shortcutPath))
                {
                    string targetPath = Process.GetCurrentProcess().MainModule.FileName;
                    CreateShortcut(targetPath, shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error enabling startup: {ex.Message}");
            }
        }

        private void StartupCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
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
                Debug.WriteLine($"Error disabling startup: {ex.Message}");
            }
        }

        private void CreateShortcut(string targetPath, string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Description = "Shortcut Manager";
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateShortcut error: {ex.Message}");
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
                Debug.WriteLine($"Error opening directory: {ex.Message}");
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
                Debug.WriteLine($"Error opening startup directory: {ex.Message}");
            }
        }
    }
}
