using Microsoft.UI.Xaml.Controls;

namespace ShortcutManager
{
    /// <summary>
    /// A reusable dialog for getting simple text input from the user.
    /// Used for creating new groups and renaming existing ones.
    /// </summary>
    public sealed partial class InputDialog : ContentDialog
    {
        /// <summary>
        /// Gets the text entered in the input box.
        /// </summary>
        public string InputText => InputTextBox.Text;

        public InputDialog(string title, string message, string initialValue = "")
        {
            this.InitializeComponent();
            this.Title = title;
            MessageTextBlock.Text = message;
            InputTextBox.Text = initialValue;
            
            // Focus and select all text for immediate replacement
            InputTextBox.SelectAll();
        }
    }
}
