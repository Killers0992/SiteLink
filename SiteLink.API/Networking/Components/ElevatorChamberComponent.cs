using Mirror;
using Interactables.Interobjects;
using System;

namespace SiteLink.API.Networking.Components;

public class ElevatorChamberComponent : BehaviourComponent
{
    private ElevatorGroup _assignedGroup;

    private byte _syncDestinationLevel;

    private byte _waypointId;

    public ElevatorGroup AssignedGroup
    {
        get => _assignedGroup;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _assignedGroup = value;
        }
    }

    public byte SyncDestinationLevel
    {
        get => _syncDestinationLevel;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncDestinationLevel = value;
        }
    }

    public byte WaypointId
    {
        get => _waypointId;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _waypointId = value;
        }
    }

    public ElevatorChamberComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ElevatorChamberComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_assignedGroup);
            writer.WriteByte(_syncDestinationLevel);
            writer.WriteByte(_waypointId);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.Write(_assignedGroup);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteByte(_syncDestinationLevel);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteByte(_waypointId);
        }
    }

}
