namespace SiteLink.API.Networking.Components;

public class PlayerEffectsControllerComponent : BehaviourComponent
{
    public PlayerEffectsControllerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public PlayerEffectsControllerComponent(NetworkObject networkObject) : this(networkObject, new[] {new SyncListObject<byte>(255) })
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
