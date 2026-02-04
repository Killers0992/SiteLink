using Mirror;
using System;
using UnityEngine;

namespace SiteLink.API.Networking.Components;

public class StructurePositionSyncComponent : BehaviourComponent
{
    private sbyte _rotationY;

    private Vector3 _position;

    public sbyte RotationY
    {
        get => _rotationY;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _rotationY = value;
        }
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _position = value;
        }
    }

    public StructurePositionSyncComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public StructurePositionSyncComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteSByte(_rotationY);
            writer.WriteVector3(_position);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteSByte(_rotationY);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteVector3(_position);
        }
    }

}
