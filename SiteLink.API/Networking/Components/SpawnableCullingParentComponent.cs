using Mirror;
using UnityEngine;

namespace SiteLink.API.Networking.Components;

public class SpawnableCullingParentComponent : BehaviourComponent
{
    private Vector3 _boundsPosition;

    private Vector3 _boundsSize;

    public Vector3 BoundsPosition
    {
        get => _boundsPosition;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _boundsPosition = value;
        }
    }

    public Vector3 BoundsSize
    {
        get => _boundsSize;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _boundsSize = value;
        }
    }

    public SpawnableCullingParentComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public SpawnableCullingParentComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteVector3(_boundsPosition);
            writer.WriteVector3(_boundsSize);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteVector3(_boundsPosition);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteVector3(_boundsSize);
        }
    }

}
