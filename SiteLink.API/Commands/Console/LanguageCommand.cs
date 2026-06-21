using SiteLink.API.Networking.Connections;
using System.Text;

namespace SiteLink.API.Commands;

public static class LanguageCommand
{
    [ConsoleCommand("language")]
    public static void OnLanguageCommand(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            Write("language.available", new TranslationContext()
                .With("languages", string.Join(", ", TranslationManager.AvailableLanguages)));
            return;
        }

        if (args.Length == 1 && args[0].Equals("reload", StringComparison.OrdinalIgnoreCase))
        {
            TranslationManager.ReloadAll();
            Write("language.reloaded");
            return;
        }

        if (args.Length == 1 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<TranslationValidationResult> results = TranslationManager.ValidateAll();
            List<string> issues = new();

            foreach (TranslationValidationResult result in results.Where(result => !result.IsValid))
            {
                issues.AddRange(result.Errors.Select(error =>
                    $"{result.Owner}/{result.Language}: {error}"));
                issues.AddRange(result.MissingKeys.Select(key =>
                    $"{result.Owner}/{result.Language}: missing '{key}'"));
                issues.AddRange(result.UnknownKeys.Select(key =>
                    $"{result.Owner}/{result.Language}: unknown key '{key}'"));
                issues.AddRange(result.UnknownPlaceholders.Select(placeholder =>
                    $"{result.Owner}/{result.Language}: unknown placeholder {placeholder}"));
            }

            if (issues.Count == 0)
                Write("language.valid");
            else
            {
                Write("language.invalid", new TranslationContext()
                    .With("count", issues.Count)
                    .With("issues", string.Join("\n", issues)));
            }

            return;
        }

        if (args.Length != 2)
        {
            Write("language.usage");
            return;
        }

        string userId = args[0];
        string language = args[1];

        if (language.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            PlayerLanguageStore.Remove(userId);
            Write("language.reset", new TranslationContext().With("user_id", userId));
            return;
        }

        if (!TranslationManager.SetPlayerLanguage(userId, language))
        {
            Write("language.not_found", new TranslationContext()
                .With("language", language)
                .With("languages", string.Join(", ", TranslationManager.AvailableLanguages)));
            return;
        }

        if (RemoteConnection.TryGet(userId, out RemoteConnection connection))
            connection.Session?.Connection?.AsServer.Hint(
                TranslationManager.Command("language.changed", TranslationContext.For(connection.Session)
                    .With("language", TranslationManager.NormalizeLanguage(language))),
                4f);

        Write("language.changed", new TranslationContext()
            .With("user_id", userId)
            .With("language", TranslationManager.NormalizeLanguage(language)));
    }

    private static void Write(string key, TranslationContext context = null) =>
        SiteLinkLogger.Info(TranslationManager.Command(key, context), "language");
}
