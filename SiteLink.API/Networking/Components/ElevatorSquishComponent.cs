
namespace SiteLink.API.Networking.Components;

public class ElevatorSquishComponent : BehaviourComponent
{
    public ElevatorSquishComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ElevatorSquishComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);

    }

}
