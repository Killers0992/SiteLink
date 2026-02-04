using Hazards;
using LabApi.Events.Handlers;
using LabApi.Loader.Features.Plugins;
using MapGeneration;
using Mirror;
using PlayerRoles.PlayableScps.HumanTracker;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

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
        ProtocolGenerator.Generate();
    }

    public override void Disable()
    {
        ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;
    }
}
