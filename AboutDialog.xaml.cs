using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace ShortcutManager
{
    public sealed partial class AboutDialog : ContentDialog
    {
        private string _latestReleaseUrl;
        private string _currentVersion;

        public AboutDialog()
        {
            this.InitializeComponent();
            LoadVersion();
        }

        private void LoadVersion()
        {
            _currentVersion = typeof(AboutDialog).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "Unknown";
            
            VersionTextBox.Text = _currentVersion;
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/SkyStrike/shortcut-manager");
        }

        private async void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateCheckButton.IsEnabled = false;
            UpdateCheckText.Text = "Checking for updates...";
            UpdateInfoBar.IsOpen = false;

            try
            {
                using var client = new HttpClient();
                // GitHub API requires a User-Agent
                client.DefaultRequestHeaders.Add("User-Agent", "ShortcutManager-UpdateCheck");
                
                var release = await client.GetFromJsonAsync<GitHubRelease>(
                    "https://api.github.com/repos/SkyStrike/shortcut-manager/releases/latest",
                    ShortcutSerializationContext.Default.GitHubRelease);

                if (release != null)
                {
                    if (IsNewerVersion(release.TagName, _currentVersion))
                    {
                        _latestReleaseUrl = release.HtmlUrl;
                        UpdateInfoBar.Title = "New version available!";
                        UpdateInfoBar.Message = $"Version {release.TagName} is now available.";
                        UpdateInfoBar.Severity = InfoBarSeverity.Success;
                        UpdateInfoBar.IsOpen = true;
                        ((Button)UpdateInfoBar.ActionButton).Visibility = Visibility.Visible;
                    }
                    else
                    {
                        UpdateInfoBar.Title = "Up to date";
                        UpdateInfoBar.Message = "You are running the latest version.";
                        UpdateInfoBar.Severity = InfoBarSeverity.Informational;
                        UpdateInfoBar.IsOpen = true;
                        // Hide action button if up to date
                        ((Button)UpdateInfoBar.ActionButton).Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error checking for updates");
                UpdateInfoBar.Title = "Update Check Failed";
                UpdateInfoBar.Message = "Could not connect to GitHub to check for updates.";
                UpdateInfoBar.Severity = InfoBarSeverity.Error;
                UpdateInfoBar.IsOpen = true;
                ((Button)UpdateInfoBar.ActionButton).Visibility = Visibility.Collapsed;
            }
            finally
            {
                UpdateCheckButton.IsEnabled = true;
                UpdateCheckText.Text = "Check for Updates";
            }
        }

        private void ViewRelease_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_latestReleaseUrl))
            {
                OpenUrl(_latestReleaseUrl);
            }
        }

        private bool IsNewerVersion(string latestTag, string currentVersion)
        {
            if (string.IsNullOrEmpty(latestTag) || currentVersion == "Unknown") return false;

            // Trim 'v' prefix if present
            string latestStr = latestTag.TrimStart('v').Split('+')[0];
            string currentStr = currentVersion.TrimStart('v').Split('+')[0];

            if (Version.TryParse(latestStr, out Version latest) && 
                Version.TryParse(currentStr, out Version current))
            {
                return latest > current;
            }

            return false;
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not open URL: {Url}", url);
            }
        }
    }
}
