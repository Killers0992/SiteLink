using LabApi.Events.Handlers;
using LabApi.Loader.Features.Plugins;
using System;
using System.Collections.Generic;
using System.IO;

namespace SiteLink.Generator;

public class MainClass : Plugin
{
    public override string Name { get; } = "SiteLink.Generator";

    public override string Description { get; } = "Generator for sitelink which generates objects/components for network use.";

    public override string Author { get; } = "Killers0992";

    public override Version Version { get; } = new Version(1, 0, 0);

    public override Version RequiredApiVersion { get; } = new Version(LabApi.Features.LabApiProperties.CompiledVersion);

    public override void Enable()
    {
        ServerEvents.WaitingForPlayers += OnWaitingForPlayers;
    }

    private void OnWaitingForPlayers()
    {
        int index = 0;

        List<string> lines = new List<string>();
        lines.Add("namespace SiteLink.API.Enums");
        lines.Add("{");
        lines.Add("    public enum EffectType");
        lines.Add("    {");

        foreach (var effect in ReferenceHub.HostHub.playerEffectsController.AllEffects)
        {
            lines.Add($"        {effect.name} = {index},");
            index++;
        }

        lines.Add("    }");
        lines.Add("}");

        File.WriteAllLines("D:\\VS Projects\\SiteLink\\SiteLink.API\\Enums\\EffectType.cs", lines);

        ProtocolGenerator.Generate();
    }

    public override void Disable()
    {
        ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;
    }
}
