namespace SiteLink.API.Commands;

public static class PluginsCommand
{
    [ConsoleCommand("plugins")]
    public static void OnPluginsCommand(string[] args)
    {
        if (args.Length == 0)
        {
            string plugins = string.Join(
                "\n",
                PluginsManager.AssemblyToPlugin.Values.Select(plugin =>
                    $" - {plugin.Name} {plugin.Version} ({plugin.Repository ?? "no repository"})"));
            SiteLinkLogger.Info(TranslationManager.Command(
                "plugins.list",
                new TranslationContext().With("plugins", plugins)), "plugins");
            return;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "install" when args.Length == 2:
                {
                    string path = PackageManager.InstallPluginAsync(args[1]).GetAwaiter().GetResult();
                    SiteLinkLogger.Info(TranslationManager.Command(
                        "plugins.installed",
                        new TranslationContext()
                            .With("repository", args[1])
                            .With("path", path)), "plugins");
                    return;
                }

                case "check":
                case "update":
                {
                    bool install = args[0].Equals("update", StringComparison.OrdinalIgnoreCase);
                    List<string> updates = new();

                    foreach (Plugin plugin in PluginsManager.AssemblyToPlugin.Values)
                    {
                        PackageManifest latest =
                            PackageManager.CheckPluginAsync(plugin).GetAwaiter().GetResult();
                        if (latest == null)
                            continue;

                        updates.Add($"{plugin.Name}: {plugin.Version} -> {latest.Version}");
                        if (install)
                            PackageManager.InstallPluginAsync(plugin.Repository).GetAwaiter().GetResult();
                    }

                    SiteLinkLogger.Info(TranslationManager.Command(
                        updates.Count == 0 ? "plugins.current" :
                        install ? "plugins.updated" : "plugins.updates",
                        new TranslationContext()
                            .With("count", updates.Count)
                            .With("updates", string.Join("\n", updates))), "plugins");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error(TranslationManager.Command(
                "plugins.failed",
                new TranslationContext().With("error", ex.Message)), "plugins");
            return;
        }

        SiteLinkLogger.Info(TranslationManager.Command("plugins.usage"), "plugins");
    }
}
