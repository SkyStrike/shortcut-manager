using Microsoft.UI.Xaml.Controls;
using System;

namespace ShortcutManager
{
    public sealed partial class DisplaySettingsDialog : ContentDialog
    {
        private DisplaySettings _settings;

        public DisplaySettingsDialog(DisplaySettings settings)
        {
            this.InitializeComponent();
            _settings = settings;

            // Initialize controls with current settings
            SearchBoxFontSizeBox.Value = _settings.SearchBoxFontSize;
            GroupNameFontSizeBox.Value = _settings.GroupNameFontSize;
            AppWidthBox.Value = _settings.AppWidthLogical;
            MinHeightSlider.Value = _settings.AppMinHeightMultiplier;
            TopMarginSlider.Value = _settings.AppTopMarginMultiplier;

            this.PrimaryButtonClick += DisplaySettingsDialog_PrimaryButtonClick;
        }

        private void DisplaySettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Update settings object from controls
            _settings.SearchBoxFontSize = SearchBoxFontSizeBox.Value;
            _settings.GroupNameFontSize = GroupNameFontSizeBox.Value;
            _settings.AppWidthLogical = (int)AppWidthBox.Value;
            _settings.AppMinHeightMultiplier = MinHeightSlider.Value;
            _settings.AppTopMarginMultiplier = TopMarginSlider.Value;
        }
    }
}
