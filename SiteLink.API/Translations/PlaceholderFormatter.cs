using System.Globalization;
using System.Text.RegularExpressions;

namespace SiteLink.API.Translations;

/// <summary>
/// Formats translation templates using named placeholders such as {server}.
/// </summary>
public sealed class PlaceholderFormatter
{
    private readonly string _template;
    private readonly TranslationContext _context;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public PlaceholderFormatter(string template, TranslationContext context = null)
    {
        _template = template ?? string.Empty;
        _context = context ?? new TranslationContext();
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
        string result = Regex.Replace(
            _template,
            @"\{([a-zA-Z0-9_.-]+)\}",
            match =>
            {
                string key = match.Groups[1].Value;
                if (_values.TryGetValue(key, out string explicitValue))
                    return explicitValue;

                if (!PlaceholderRegistry.TryResolve(key, _context, out object value))
                    return match.Value;

                return ConvertValue(value, null);
            },
            RegexOptions.CultureInvariant);

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

    private static string ConvertValue(object value, string format) =>
        value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
}
