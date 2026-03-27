using Mirror;
using UnityEngine;
using System;

namespace SiteLink.API.Networking.Components;

public class AdminToyBaseComponent : BehaviourComponent
{
    private Vector3 _position;

    private Quaternion _rotation = Quaternion.identity;

    private Vector3 _scale = Vector3.one;

    private byte _movementSmoothing;

    private bool _isStatic;

    public Vector3 Position
    {
        get => _position;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _position = value;
        }
    }

    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _rotation = value;
        }
    }

    public Vector3 Scale
    {
        get => _scale;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _scale = value;
        }
    }

    public byte MovementSmoothing
    {
        get => _movementSmoothing;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _movementSmoothing = value;
        }
    }

    public bool IsStatic
    {
        get => _isStatic;
        set
        {
            SetSyncVarDirtyBit(16UL);
            _isStatic = value;
        }
    }

    public AdminToyBaseComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public AdminToyBaseComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteVector3(_position);
            writer.WriteQuaternion(_rotation);
            writer.WriteVector3(_scale);
            writer.WriteByte(_movementSmoothing);
            writer.WriteBool(_isStatic);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteVector3(_position);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteQuaternion(_rotation);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteVector3(_scale);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteByte(_movementSmoothing);
        }

        if ((SyncVarDirtyBits & 16UL) != 0UL)
        {
            writer.WriteBool(_isStatic);
        }
    }

    public override void OnSerialize(NetworkWriter writer, bool initialState)
    {
        base.OnSerialize(writer, initialState);

        // parent
        if (initialState)
            writer.WriteUInt(0);
    }
}
