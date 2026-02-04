
namespace SiteLink.API.Networking.Components;

public class HintDisplayComponent : BehaviourComponent
{
    public HintDisplayComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public HintDisplayComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
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
