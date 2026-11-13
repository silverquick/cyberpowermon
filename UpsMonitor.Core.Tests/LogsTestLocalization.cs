using System.Globalization;
using System.Xml.Linq;

namespace UpsMonitor.App;

// The linked Logs VM and event presentation have no WPF dependency. Resolve actual
// resource files here so their localization is tested by the cross-platform runner.
internal static class LocalizationManager
{
    internal static string Language { get; set; } = "en-US";
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new();
    internal static string Get(string key)
    {
        if (!Resources.TryGetValue(Language, out var values))
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../UpsMonitor.App/Resources", $"Strings.{Language}.xaml"));
            values = XDocument.Load(path).Root!.Elements().ToDictionary(
                e => e.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))!.Value, e => e.Value);
            Resources.Add(Language, values);
        }
        return values.GetValueOrDefault(key, key);
    }
    internal static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
