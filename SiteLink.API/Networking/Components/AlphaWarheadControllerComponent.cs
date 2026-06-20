using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class AlphaWarheadControllerComponent : BehaviourComponent
{
    private AlphaWarheadSyncInfo _info;

    private double _cooldownEndTime;

    public AlphaWarheadSyncInfo Info
    {
        get => _info;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _info = value;
        }
    }

    public double CooldownEndTime
    {
        get => _cooldownEndTime;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _cooldownEndTime = value;
        }
    }

    public AlphaWarheadControllerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public AlphaWarheadControllerComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteAlphaWarheadSyncInfo(_info);
            writer.WriteDouble(_cooldownEndTime);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteAlphaWarheadSyncInfo(_info);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteDouble(_cooldownEndTime);
        }
    }

}
