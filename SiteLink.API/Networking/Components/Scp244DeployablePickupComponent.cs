using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class Scp244DeployablePickupComponent : CollisionDetectionPickupComponent
{
    private byte _syncSizePercent;

    private byte _syncState;

    public byte SyncSizePercent
    {
        get => _syncSizePercent;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncSizePercent = value;
        }
    }

    public byte SyncState
    {
        get => _syncState;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _syncState = value;
        }
    }

    public Scp244DeployablePickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_syncSizePercent);
            writer.WriteByte(_syncState);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteByte(_syncSizePercent);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteByte(_syncState);
        }
    }

}
