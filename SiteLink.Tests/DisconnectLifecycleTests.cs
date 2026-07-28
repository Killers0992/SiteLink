using System.Runtime.CompilerServices;
using SiteLink.API.Models;
using SiteLink.API.Networking;
using Xunit;

namespace SiteLink.Tests;

public sealed class DisconnectLifecycleTests
{
    [Fact]
    public void Cancel_RemovesPreparedMessageBeforeSessionConnects()
    {
        DisconnectServer server = new();
        Session session = (Session)RuntimeHelpers.GetUninitializedObject(typeof(Session));

        server.Prepare(session, "<color=white>Default</color>");
        server.Cancel(session);

        Assert.False(server.TryTakeMessage(session, out _));
    }

    [Fact]
    public void RemoveSlotIfEmpty_DoesNotRemoveAReplacementSlot()
    {
        SessionManager manager = new();
        SessionSlot staleSlot = new();
        SessionSlot replacementSlot = new();
        manager.Slots["player"] = replacementSlot;

        manager.RemoveSlotIfEmpty("player", staleSlot);

        Assert.True(manager.Slots.TryGetValue("player", out SessionSlot? current));
        Assert.Same(replacementSlot, current);
    }

    [Fact]
    public void RemoveSlotIfEmpty_RemovesTheCurrentEmptySlot()
    {
        SessionManager manager = new();
        SessionSlot slot = new();
        manager.Slots["player"] = slot;

        manager.RemoveSlotIfEmpty("player", slot);

        Assert.False(manager.Slots.ContainsKey("player"));
    }
}
