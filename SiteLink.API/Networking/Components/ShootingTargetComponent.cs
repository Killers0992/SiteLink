using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class ShootingTargetComponent : AdminToyBaseComponent
{
    private bool _syncMode;

    public bool SyncMode
    {
        get => _syncMode;
        set
        {
            SetSyncVarDirtyBit(32UL);
            _syncMode = value;
        }
    }

    public ShootingTargetComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_syncMode);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 32UL) != 0UL)
        {
            writer.WriteBool(_syncMode);
        }
    }

}
