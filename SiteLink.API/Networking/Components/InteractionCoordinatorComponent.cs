
namespace SiteLink.API.Networking.Components;

public class InteractionCoordinatorComponent : BehaviourComponent
{
    public InteractionCoordinatorComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public InteractionCoordinatorComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
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
