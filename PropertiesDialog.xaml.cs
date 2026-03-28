using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;

namespace ShortcutManager
{
    public sealed partial class PropertiesDialog : ContentDialog
    {
        private ShortcutItem _item;

        public PropertiesDialog(ShortcutItem item)
        {
            this.InitializeComponent();
            _item = item;

            NameTextBox.Text = item.Name;
            PathTextBox.Text = item.Path;
            ArgsTextBox.Text = item.Arguments;
            AdminCheckBox.IsChecked = item.RunAsAdmin;

            this.PrimaryButtonClick += PropertiesDialog_PrimaryButtonClick;
        }

        private void PropertiesDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string path = PathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path) && !Directory.Exists(path))
            {
                ErrorTextBlock.Text = "Invalid path. Please enter a valid file or directory path.";
                ErrorTextBlock.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            _item.Name = NameTextBox.Text.Trim();
            _item.Path = path;
            _item.Arguments = ArgsTextBox.Text.Trim();
            _item.RunAsAdmin = AdminCheckBox.IsChecked ?? false;
        }
    }
}
