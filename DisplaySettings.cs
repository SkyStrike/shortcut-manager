using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShortcutManager
{
    public class DisplaySettings : INotifyPropertyChanged
    {
        private double _searchBoxFontSize = 32;
        [JsonPropertyName("searchBoxFontSize")]
        public double SearchBoxFontSize
        {
            get => _searchBoxFontSize;
            set { _searchBoxFontSize = value; OnPropertyChanged(); }
        }

        private double _groupNameFontSize = 18;
        [JsonPropertyName("groupNameFontSize")]
        public double GroupNameFontSize
        {
            get => _groupNameFontSize;
            set { _groupNameFontSize = value; OnPropertyChanged(); }
        }

        private double _appMinHeightMultiplier = 0.50;
        [JsonPropertyName("appMinHeightMultiplier")]
        public double AppMinHeightMultiplier
        {
            get => _appMinHeightMultiplier;
            set { _appMinHeightMultiplier = value; OnPropertyChanged(); }
        }

        private double _appTopMarginMultiplier = 0.30;
        [JsonPropertyName("appTopMarginMultiplier")]
        public double AppTopMarginMultiplier
        {
            get => _appTopMarginMultiplier;
            set { _appTopMarginMultiplier = value; OnPropertyChanged(); }
        }

        private int _appWidthLogical = 1080;
        [JsonPropertyName("appWidthLogical")]
        public int AppWidthLogical
        {
            get => _appWidthLogical;
            set { _appWidthLogical = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
