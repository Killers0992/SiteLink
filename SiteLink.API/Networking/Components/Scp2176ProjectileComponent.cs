using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class Scp2176ProjectileComponent : EffectGrenadeComponent
{
    private bool _playedDropSound;

    public bool PlayedDropSound
    {
        get => _playedDropSound;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _playedDropSound = value;
        }
    }

    public Scp2176ProjectileComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_playedDropSound);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteBool(_playedDropSound);
        }
    }

}
