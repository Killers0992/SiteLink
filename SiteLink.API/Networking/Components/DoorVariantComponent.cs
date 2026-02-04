using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class DoorVariantComponent : BehaviourComponent
{
    private bool _targetState;

    private ushort _activeLocks;

    private byte _doorId;

    public bool TargetState
    {
        get => _targetState;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _targetState = value;
        }
    }

    public ushort ActiveLocks
    {
        get => _activeLocks;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _activeLocks = value;
        }
    }

    public byte DoorId
    {
        get => _doorId;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _doorId = value;
        }
    }

    public DoorVariantComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public DoorVariantComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_targetState);
            writer.WriteUShort(_activeLocks);
            writer.WriteByte(_doorId);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteBool(_targetState);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteUShort(_activeLocks);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteByte(_doorId);
        }
    }

}
