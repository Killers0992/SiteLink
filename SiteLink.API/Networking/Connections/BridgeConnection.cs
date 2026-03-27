namespace SiteLink.API.Networking.Connections
{
    public class BridgeConnection : Connection
    {
        public BridgeConnection(Listener listener, ConnectionRequest request, PreAuth preAuth) : base(listener, request, preAuth)
        {
        }

        public Server TargetServer;

        public override void ReceiveDataFromListener(int length, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            ushort messageId = reader.GetUShort();

            SiteLinkBridge.Dispatch(messageId, reader, TargetServer);
        }

        public override void Disconnected()
        {
            SiteLinkBridge.DetachServerPeer(TargetServer, new DisconnectInfo());
        }
    }
}
