namespace SiteLink.API.Commands;

public static class CommandsManager
{
    public static Dictionary<string, CommandDelegate> RegisteredCommands { get; private set; } = new Dictionary<string, CommandDelegate>();

    public static void RegisterConsoleCommandsInAssembly(Assembly assembly)
    {
        SiteLinkLogger.Info(TranslationManager.Log("commands.registering"), "CommandsManager");

        int loaded = 0;
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var ev = method.GetCustomAttribute<ConsoleCommand>();

                if (ev == null)
                    continue;

                string commandName = ev.Name.ToLower();

                if (RegisteredCommands.ContainsKey(commandName))
                {
                    SiteLinkLogger.Info(TranslationManager.Log(
                        "commands.duplicate",
                        new TranslationContext().With("command", commandName)), "CommandsManager");
                    continue;
                }

                CommandDelegate del = (CommandDelegate)Delegate.CreateDelegate(typeof(CommandDelegate), method);
                RegisteredCommands.Add(commandName, del);

                SiteLinkLogger.Info(TranslationManager.Log(
                    "commands.registered_one",
                    new TranslationContext().With("command", commandName)), "CommandsManager");
                loaded++;
            }
        }

        SiteLinkLogger.Info(TranslationManager.Log(
            "commands.registered",
            new TranslationContext().With("count", loaded)), "CommandsManager");
    }

    public static void Initialize()
    {
        RegisterConsoleCommandsInAssembly(typeof(CommandsManager).Assembly);
    }
}
