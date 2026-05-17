namespace SiteLink.API.Networking.Components;

public class SearchCoordinatorComponent : BehaviourComponent
{
    private float _rayDistance;

    public float RayDistance
    {
        get => _rayDistance;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _rayDistance = value;
        }
    }

    public SearchCoordinatorComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public SearchCoordinatorComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteFloat(_rayDistance);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteFloat(_rayDistance);
        }
    }

}
