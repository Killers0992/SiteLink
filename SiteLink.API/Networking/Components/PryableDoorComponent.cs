using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class PryableDoorComponent : BasicDoorComponent
{
    private bool _restrict106WhileLocked;

    public bool Restrict106WhileLocked
    {
        get => _restrict106WhileLocked;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _restrict106WhileLocked = value;
        }
    }

    public PryableDoorComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_restrict106WhileLocked);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteBool(_restrict106WhileLocked);
        }
    }

}
