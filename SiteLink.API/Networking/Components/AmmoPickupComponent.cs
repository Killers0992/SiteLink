using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class AmmoPickupComponent : ItemPickupBaseComponent
{
    private ushort _savedAmmo;

    public ushort SavedAmmo
    {
        get => _savedAmmo;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _savedAmmo = value;
        }
    }

    public AmmoPickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteUShort(_savedAmmo);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteUShort(_savedAmmo);
        }
    }

}
