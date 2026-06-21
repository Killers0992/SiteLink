namespace SiteLink.API.Translations;

public sealed class TranslationValidationResult
{
    public string Owner { get; init; }
    public string Language { get; init; }
    public string Path { get; init; }
    public List<string> MissingKeys { get; } = new();
    public List<string> UnknownKeys { get; } = new();
    public List<string> UnknownPlaceholders { get; } = new();
    public List<string> Errors { get; } = new();
    public bool IsValid =>
        Errors.Count == 0 &&
        MissingKeys.Count == 0 &&
        UnknownKeys.Count == 0 &&
        UnknownPlaceholders.Count == 0;
}
