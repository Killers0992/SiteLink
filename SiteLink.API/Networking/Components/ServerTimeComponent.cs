using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class ServerTimeComponent : BehaviourComponent
{
    private int _timeFromStartup;

    public int TimeFromStartup
    {
        get => _timeFromStartup;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _timeFromStartup = value;
        }
    }

    public ServerTimeComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
    }

    public ServerTimeComponent(NetworkObject networkObject) : this(networkObject, Array.Empty<SyncedNetworkProperty>())
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteInt(_timeFromStartup);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteInt(_timeFromStartup);
        }
    }

}
