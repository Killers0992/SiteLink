using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class WorkstationControllerComponent : BehaviourComponent
{
    private byte _status;

    public byte Status
    {
        get => _status;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _status = value;
        }
    }

    public WorkstationControllerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public WorkstationControllerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_status);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteByte(_status);
        }
    }

}
