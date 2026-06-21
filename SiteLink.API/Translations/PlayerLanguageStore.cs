namespace SiteLink.API.Translations;

public static class PlayerLanguageStore
{
    private const string DataKey = "preferred_language";

    public static string Get(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return StorageManager.Core.For(userId).Get<string>(DataKey);
    }

    public static void Set(string userId, string language) =>
        StorageManager.Core.For(userId).Set(
            DataKey,
            TranslationManager.NormalizeLanguage(language));

    public static bool Remove(string userId) =>
        !string.IsNullOrWhiteSpace(userId) &&
        StorageManager.Core.For(userId).Remove(DataKey);
}
