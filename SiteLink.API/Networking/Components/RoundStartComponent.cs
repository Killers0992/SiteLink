using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class RoundStartComponent : BehaviourComponent
{
    private short _timer;

    public short Timer
    {
        get => _timer;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _timer = value;
        }
    }

    public RoundStartComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public RoundStartComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteShort(_timer);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteShort(_timer);
        }
    }

}
