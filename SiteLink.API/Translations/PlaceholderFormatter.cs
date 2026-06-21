using System.Globalization;
using System.Text.RegularExpressions;

namespace SiteLink.API.Translations;

/// <summary>
/// Formats translation templates using named placeholders such as {server}.
/// </summary>
public sealed class PlaceholderFormatter
{
    private readonly string _template;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public PlaceholderFormatter(string template)
    {
        _template = template ?? string.Empty;
    }

    public PlaceholderFormatter Add(string name, object value, string format = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return this;

        string key = name.Trim().Trim('{', '}');
        string text = value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        _values[key] = text;
        return this;
    }

    public string Format()
    {
        string result = _template;

        foreach ((string key, string value) in _values)
        {
            string placeholder = Regex.Escape($"{{{key}}}");
            result = Regex.Replace(
                result,
                placeholder,
                _ => value,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return result;
    }

    public override string ToString() => Format();
}
