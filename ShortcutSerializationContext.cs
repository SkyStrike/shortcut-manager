using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShortcutManager
{
    /// <summary>
    /// Source-generated JSON serialization context for high-performance and trimmer-friendly serialization.
    /// Includes all types required for shortcuts.json management.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<ShortcutGroup>))]
    [JsonSerializable(typeof(ShortcutGroup))]
    [JsonSerializable(typeof(ShortcutItem))]
    [JsonSerializable(typeof(DisplaySettings))]
    internal partial class ShortcutSerializationContext : JsonSerializerContext
    {
    }
}
