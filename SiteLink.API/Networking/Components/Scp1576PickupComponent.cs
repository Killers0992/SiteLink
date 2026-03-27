using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class Scp1576PickupComponent : CollisionDetectionPickupComponent
{
    private byte _syncHorn;

    public byte SyncHorn
    {
        get => _syncHorn;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncHorn = value;
        }
    }

    public Scp1576PickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_syncHorn);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteByte(_syncHorn);
        }
    }

}
