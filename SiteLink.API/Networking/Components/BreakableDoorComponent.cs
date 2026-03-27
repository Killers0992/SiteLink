using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class BreakableDoorComponent : BasicDoorComponent
{
    private bool _destroyed;

    private bool _restrict106WhileLocked;

    public bool Destroyed
    {
        get => _destroyed;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _destroyed = value;
        }
    }

    public bool Restrict106WhileLocked
    {
        get => _restrict106WhileLocked;
        set
        {
            SetSyncVarDirtyBit(16UL);
            _restrict106WhileLocked = value;
        }
    }

    public BreakableDoorComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_destroyed);
            writer.WriteBool(_restrict106WhileLocked);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteBool(_destroyed);
        }

        if ((SyncVarDirtyBits & 16UL) != 0UL)
        {
            writer.WriteBool(_restrict106WhileLocked);
        }
    }

}
