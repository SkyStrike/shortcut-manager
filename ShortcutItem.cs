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
        [JsonPropertyName("id")]
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private bool _isSelected = false;
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
