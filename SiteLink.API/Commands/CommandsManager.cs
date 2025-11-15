namespace SiteLink.API.Commands;

public static class CommandsManager
{
    public static Dictionary<string, CommandDelegate> RegisteredCommands { get; private set; } = new Dictionary<string, CommandDelegate>();

    public static void RegisterConsoleCommandsInAssembly(Assembly assembly)
    {
        SiteLinkLogger.Info("Register commands... ", "CommandsManager");

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
                    SiteLinkLogger.Info($"Command '(f=green){commandName}(f=white)' is already registered, skipping duplicate.", "CommandsManager");
                    continue;
                }

                CommandDelegate del = (CommandDelegate)Delegate.CreateDelegate(typeof(CommandDelegate), method);
                RegisteredCommands.Add(commandName, del);

                SiteLinkLogger.Info($"Command '(f=green){commandName}(f=white)' registered!", "CommandsManager");
                loaded++;
            }
        }

        SiteLinkLogger.Info($"Registered (f=green){loaded}(f=white) commands!", "CommandsManager");
    }

    public static void Initialize()
    {
        RegisterConsoleCommandsInAssembly(typeof(CommandsManager).Assembly);
    }
}
