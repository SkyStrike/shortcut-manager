using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ShortcutManager
{
    /// <summary>
    /// Represents an individual shortcut item within a group.
    /// Implements INotifyPropertyChanged to ensure UI updates when properties change.
    /// </summary>
    public class ShortcutItem : INotifyPropertyChanged
    {
        private string _name = "";
        [JsonPropertyName("text")]
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _path = "";
        [JsonPropertyName("application")]
        public string Path
        {
            get => _path;
            set { _path = value; OnPropertyChanged(); }
        }

        private string _icon = "";
        /// <summary>
        /// Path to the icon file. Can be relative to the application base directory.
        /// </summary>
        [JsonPropertyName("icon")]
        public string Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        private bool _runAsAdmin = false;
        [JsonPropertyName("runasAdmin")]
        public bool RunAsAdmin
        {
            get => _runAsAdmin;
            set { _runAsAdmin = value; OnPropertyChanged(); }
        }

        private string _arguments = "";
        [JsonPropertyName("args")]
        public string Arguments
        {
            get => _arguments;
            set { _arguments = value; OnPropertyChanged(); }
        }

        private string _id = System.Guid.NewGuid().ToString();
        /// <summary>
        /// Unique identifier used for drag-and-drop operations and lookup.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private bool _isSelected = false;
        /// <summary>
        /// UI state property to track if the item is currently selected/highlighted.
        /// Ignored during JSON serialization.
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
