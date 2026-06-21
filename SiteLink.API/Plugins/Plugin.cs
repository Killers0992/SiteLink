namespace SiteLink.API.Plugins;

public abstract class Plugin
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Author { get; }
    public abstract Version Version { get; }
    public abstract Version ApiVersion { get; }
    public virtual string Repository => null;

    public string PluginDirectory { get; internal set; }
    public virtual ITranslationCatalog TranslationCatalog => null;

    public virtual void LoadConfig() { }
    public virtual void SaveConfig() { }
    public virtual void LoadTranslations() { }
    public virtual void ReloadTranslations() => TranslationCatalog?.Reload();

    public virtual void OnLoad(IServiceCollection collection) { }
    public virtual void OnUnload()
    {
        if (TranslationCatalog != null)
            TranslationManager.UnregisterCatalog(TranslationCatalog);
    }
}
