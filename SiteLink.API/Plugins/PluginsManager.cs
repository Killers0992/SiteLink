namespace SiteLink.API.Plugins;

public static class PluginsManager
{
    private static IServiceCollection _serviceCollection;
    private static bool _resolverRegistered;

    public static string PluginsPath => Path.Combine("Plugins");
    public static string DependenciesPath => Path.Combine("Dependencies");

    public static List<Assembly> Dependencies = new List<Assembly>();

    public static Dictionary<Assembly, Plugin> AssemblyToPlugin = new Dictionary<Assembly, Plugin>();
    public static Dictionary<string, Assembly> NameToAssembly = new Dictionary<string, Assembly>();

    public static void Initialize(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;

        if (!Directory.Exists(PluginsPath))
            Directory.CreateDirectory(PluginsPath);

        if (!Directory.Exists(DependenciesPath))
            Directory.CreateDirectory(DependenciesPath);

        RegisterAssemblyResolver();
        LoadDependencies();

        var assemblies = LoadAssemblies(PluginsPath);
        RegisterAssemblies(assemblies);
        LoadPlugins(OrderByDependencies(assemblies));
    }

    public static List<Assembly> LoadAssemblies(string folder)
    {
        string[] files = Directory.GetFiles(folder, "*.dll");

        List<Assembly> assemblies = new List<Assembly>();

        for (int x = 0; x < files.Length; x++)
        {
            string name = Path.GetFileName(files[x]);

            // Disabled assembly, dont take it.
            if (name.StartsWith("-"))
                continue;

            byte[] data = File.ReadAllBytes(files[x]);

            Assembly assembly = Assembly.Load(data);

            assemblies.Add(assembly);
        }

        return assemblies;
    }

    private static void RegisterAssemblyResolver()
    {
        if (_resolverRegistered)
            return;

        AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        _resolverRegistered = true;
    }

    private static void RegisterAssemblies(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            AssemblyName name = assembly.GetName();

            if (!string.IsNullOrEmpty(assembly.FullName))
                NameToAssembly[assembly.FullName] = assembly;

            if (!string.IsNullOrEmpty(name.Name))
                NameToAssembly[name.Name] = assembly;
        }
    }

    private static List<Assembly> OrderByDependencies(List<Assembly> assemblies)
    {
        Dictionary<string, Assembly> pluginsByName = assemblies
            .Where(assembly => !string.IsNullOrEmpty(assembly.GetName().Name))
            .ToDictionary(assembly => assembly.GetName().Name, StringComparer.OrdinalIgnoreCase);

        List<Assembly> ordered = new(assemblies.Count);
        HashSet<Assembly> visiting = new();
        HashSet<Assembly> visited = new();

        void Visit(Assembly assembly)
        {
            if (visited.Contains(assembly))
                return;

            if (!visiting.Add(assembly))
                return;

            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name != null && pluginsByName.TryGetValue(reference.Name, out Assembly dependency))
                    Visit(dependency);
            }

            visiting.Remove(assembly);
            visited.Add(assembly);
            ordered.Add(assembly);
        }

        foreach (Assembly assembly in assemblies)
            Visit(assembly);

        return ordered;
    }

    public static void LoadDependencies()
    {
        string[] dependencies = Directory.GetFiles(DependenciesPath, "*.dll");

        SiteLinkLogger.Info($"Loading (f=yellow){dependencies.Length}(f=white) dependencies...", "PluginsManager");

        int loaded = 0;
        for (int x = 0; x < dependencies.Length; x++)
        {
            Dependencies.Add(Assembly.LoadFrom(dependencies[x]));
            loaded++;
        }

        SiteLinkLogger.Info($"Loaded (f=yellow){loaded}(f=white)/(f=yellow){dependencies.Length}(f=white) dependencies!", "PluginsManager");
    }

    public static void LoadPlugins(List<Assembly> assemblies)
    {
        SiteLinkLogger.Info($"Loading (f=yellow){assemblies.Count}(f=white) plugins...", "PluginsManager");

        int loaded = 0;
        for (int x = 0; x < assemblies.Count; x++)
        {
            int current = x+1;

            Assembly assembly = assemblies[x];
            string name = assembly.GetName()?.Name ?? "unknown";

            SiteLinkLogger.Info($"[(f=yellow){current}(f=white)/(f=yellow){assemblies.Count}(f=white)] Plugin '(f=yellow){name}(f=white)' is loading...", "PluginsManager");

            Dictionary<string, AssemblyName> loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName()).ToDictionary(x => x.Name, y => y);
            Dictionary<string, AssemblyName> pluginReferences = assembly.GetReferencedAssemblies().ToDictionary(x => x.Name, y => y);

            var missingAssemblies = pluginReferences.Where(x => !loadedAssemblies.ContainsKey(x.Key)).ToList();

            foreach (var missing in missingAssemblies)
            {
                SiteLinkLogger.Info($"Missing dependency '(f=yellow){missing.Key}(f=white)' v(f=yellow){missing.Value.Version.ToString(3)}(f=white)", "PluginsManager");
            }

            Type[] types = null;

            try
            {
                types = assembly.GetTypes();
            }
            catch (Exception typesException)
            {
                SiteLinkLogger.Error($"[(f=yellow){current}(f=red)/(f=yellow){assemblies.Count}(f=red)] Failed getting types for plugin '(f=green){name}(f=red)'\n{typesException}", "PluginsManager");
                continue;
            }

            Plugin plugin = null;

            foreach (var type in types)
            {
                if (!type.IsSubclassOf(typeof(Plugin)))
                    continue;

                plugin = (Plugin)Activator.CreateInstance(type);
                AssemblyToPlugin.Add(assembly, plugin);
                break;
            }

            if (plugin == null)
                continue;

            if (plugin.ApiVersion != null && plugin.ApiVersion.CompareTo(SiteLinkAPI.ApiVersion) > 0)
            {
                SiteLinkLogger.Error($"[(f=yellow){current}(f=red)/(f=yellow){assemblies.Count}(f=red)] Plugin '(f=green){plugin.Name}(f=red)' requires API Version '(f=green){plugin.ApiVersion}(f=red)' ( current (f=green){SiteLinkAPI.ApiVersionText}(f=red) )", "PluginsManager");
                continue;
            }

            if (!Load(plugin, out Exception ex))
            {
                SiteLinkLogger.Error($"[(f=yellow){current}(f=red)/(f=yellow){assemblies.Count}(f=red)] Plugin '(f=yellow){plugin.Name}(f=red)' failed to load!\n{ex}", "PluginsManager");
                continue;
            }

            SiteLinkLogger.Info($"[(f=yellow){current}(f=white)/(f=yellow){assemblies.Count}(f=white)] Plugin '(f=yellow){name}(f=white)' loaded successfully!", "PluginsManager");
            loaded++;
        }

        SiteLinkLogger.Info($"Loaded (f=yellow){loaded}(f=white)/(f=yellow){assemblies.Count}(f=white) plugins!", "PluginsManager");
    }

    public static bool Load(Plugin plugin, out Exception ex)
    {
        plugin.PluginDirectory = Path.Combine(PluginsPath, $"{plugin.Name}");

        try
        {
            plugin.LoadConfig();
            plugin.OnLoad(_serviceCollection);

            ex = null;
            return true;
        }
        catch (Exception exception)
        {
            ex = exception;
            return false;
        }
    }

    private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
    {
        if (NameToAssembly.TryGetValue(args.Name, out Assembly assembly))
            return assembly;

        string name = new AssemblyName(args.Name).Name;
        if (!string.IsNullOrEmpty(name) && NameToAssembly.TryGetValue(name, out assembly))
            return assembly;

        return null;
    }
}
