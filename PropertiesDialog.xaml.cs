using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace ShortcutManager
{
    /// <summary>
    /// Dialog for editing the properties of a specific ShortcutItem.
    /// </summary>
    public sealed partial class PropertiesDialog : ContentDialog
    {
        private ShortcutItem _item;

        public PropertiesDialog(ShortcutItem item)
        {
            this.InitializeComponent();
            _item = item;

            // Initialize fields with current item values
            NameTextBox.Text = item.Name;
            PathTextBox.Text = item.Path;
            ArgsTextBox.Text = item.Arguments;
            WorkDirTextBox.Text = item.WorkingDirectory;
            AdminCheckBox.IsChecked = item.RunAsAdmin;

            this.PrimaryButtonClick += PropertiesDialog_PrimaryButtonClick;
        }

        /// <summary>
        /// Validates input and saves changes to the ShortcutItem before closing.
        /// </summary>
        private void PropertiesDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string path = PathTextBox.Text.Trim();
            string workDir = WorkDirTextBox.Text.Trim();
            
            // Basic path validation - ensures the file or directory actually exists
            if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                ErrorTextBlock.Text = "Invalid path. Please enter a valid file or directory path.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                
                // Prevent the dialog from closing
                args.Cancel = true;
                return;
            }

            // Optional working directory validation
            if (!string.IsNullOrEmpty(workDir) && !Directory.Exists(workDir))
            {
                ErrorTextBlock.Text = "Invalid working directory. Please enter a valid directory path or leave it empty.";
                ErrorTextBlock.Visibility = Visibility.Visible;

                // Prevent the dialog from closing
                args.Cancel = true;
                return;
            }

            // Apply changes to the data model
            _item.Name = NameTextBox.Text.Trim();
            _item.Path = path;
            _item.Arguments = ArgsTextBox.Text.Trim();
            _item.WorkingDirectory = workDir;
            _item.RunAsAdmin = AdminCheckBox.IsChecked ?? false;
        }
    }
}
