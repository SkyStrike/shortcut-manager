using Microsoft.UI.Xaml.Controls;

namespace ShortcutManager
{
    public sealed partial class InputDialog : ContentDialog
    {
        public string InputText => InputTextBox.Text;

        public InputDialog(string title, string message, string initialValue = "")
        {
            this.InitializeComponent();
            this.Title = title;
            MessageTextBlock.Text = message;
            InputTextBox.Text = initialValue;
            InputTextBox.SelectAll();
        }
    }
}
