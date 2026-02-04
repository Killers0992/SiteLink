using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class TimeGrenadeComponent : ThrownProjectileComponent
{
    private double _syncTargetTime;

    public double SyncTargetTime
    {
        get => _syncTargetTime;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncTargetTime = value;
        }
    }

    public TimeGrenadeComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteDouble(_syncTargetTime);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteDouble(_syncTargetTime);
        }
    }

}
