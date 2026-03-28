using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ShortcutManager
{
    public class ShortcutItem
    {
        [JsonPropertyName("text")]
        public string Name { get; set; } = "";

        [JsonPropertyName("application")]
        public string Path { get; set; } = "";

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "";

        [JsonPropertyName("runasAdmin")]
        public bool RunAsAdmin { get; set; } = false;

        [JsonPropertyName("args")]
        public string Arguments { get; set; } = "";

        [JsonPropertyName("id")]
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
    }
}
