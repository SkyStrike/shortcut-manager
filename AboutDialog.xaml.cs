using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ShortcutManager
{
    public sealed partial class AboutDialog : ContentDialog
    {
        public AboutDialog()
        {
            this.InitializeComponent();
            LoadVersion();
        }

        private void LoadVersion()
        {
            var version = typeof(AboutDialog).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "Unknown";
            
            VersionTextBox.Text = version;
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/SkyStrike/shortcut-manager",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
