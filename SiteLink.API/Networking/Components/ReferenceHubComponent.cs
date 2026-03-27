
namespace SiteLink.API.Networking.Components;

public class ReferenceHubComponent : BehaviourComponent
{
    private RecyclablePlayerId _playerId;

    public RecyclablePlayerId PlayerId
    {
        get => _playerId;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _playerId = value;
        }
    }

    public ReferenceHubComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ReferenceHubComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteRecyclablePlayerId(_playerId);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteRecyclablePlayerId(_playerId);
        }
    }

}
