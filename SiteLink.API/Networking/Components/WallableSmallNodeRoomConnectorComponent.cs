using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class WallableSmallNodeRoomConnectorComponent : SpawnableRoomConnectorComponent
{
    private byte _syncBitmask;

    public byte SyncBitmask
    {
        get => _syncBitmask;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _syncBitmask = value;
        }
    }

    public WallableSmallNodeRoomConnectorComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_syncBitmask);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteByte(_syncBitmask);
        }
    }

}
