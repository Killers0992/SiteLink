using Mirror;
using SiteLink.API.Networking.Common;
using SiteLink.API.Networking.Connections;
using Xunit;

namespace SiteLink.Tests;

public sealed class RemoteConnectionDisconnectTests
{
    private const string RichTextMessage = "[ Luna Cloud Labs ]\n<b><color=#78A3E8>D</color><color=#7EA8EB>e</color><color=#83ACEE>f</color><color=#89B1F1>a</color><color=#83ACEE>u</color><color=#7EA8EB>l</color><color=#78A3E8>t</color></b> sunucusu çevrimdışı.";

    [Theory]
    [InlineData("message", true, false, "BootstrapSession")]
    [InlineData("message", true, true, "BootstrapSession")]
    [InlineData("message", false, true, "SessionRpc")]
    [InlineData("message", false, false, "Transport")]
    [InlineData(null, true, true, "Transport")]
    public void SelectDisconnectDelivery_UsesTheAvailableRichTextCapablePath(
        string? message,
        bool hasRequest,
        bool hasSession,
        string expected)
    {
        Assert.Equal(expected, RemoteConnection.SelectDisconnectDelivery(message, hasRequest, hasSession).ToString());
    }

    [Fact]
    public void WriteDisconnectError_PreservesUnityRichTextExactly()
    {
        const uint networkId = 42;
        NetworkWriter writer = new();

        RemoteConnection.WriteDisconnectError(writer, networkId, RichTextMessage);

        NetworkReader reader = new(writer.ToArraySegment());
        Assert.Equal(NetworkMessages.RpcMessage, reader.ReadUShort());
        Assert.Equal(networkId, reader.ReadUInt());
        Assert.Equal(1, reader.ReadByte());
        Assert.Equal(unchecked((ushort)-2106075371), reader.ReadUShort());

        NetworkReader payloadReader = new(reader.ReadArraySegmentAndSize());
        Assert.Equal(RichTextMessage, payloadReader.ReadString());
    }
}
