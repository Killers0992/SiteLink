namespace SiteLink.API.Networking.Components;

public class EncryptedChannelManagerComponent : BehaviourComponent
{
    private string _serverRandom;

    public string ServerRandom
    {
        get => _serverRandom;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _serverRandom = value;
        }
    }

    public EncryptedChannelManagerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public EncryptedChannelManagerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteString(_serverRandom);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteString(_serverRandom);
        }
    }

}
