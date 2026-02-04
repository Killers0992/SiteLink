using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class RoundSummaryComponent : BehaviourComponent
{
    private int _extraTargets;

    private int _targetCount;

    public int ExtraTargets
    {
        get => _extraTargets;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _extraTargets = value;
        }
    }

    public int TargetCount
    {
        get => _targetCount;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _targetCount = value;
        }
    }

    public RoundSummaryComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public RoundSummaryComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteInt(_extraTargets);
            writer.WriteInt(_targetCount);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteInt(_extraTargets);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteInt(_targetCount);
        }
    }

}
