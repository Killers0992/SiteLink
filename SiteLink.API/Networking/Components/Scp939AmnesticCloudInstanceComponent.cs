using Mirror;
using System;
using RelativePositioning;

namespace SiteLink.API.Networking.Components;

public class Scp939AmnesticCloudInstanceComponent : TemporaryHazardComponent
{
    private byte _syncHoldTime;

    private byte _syncState;

    private uint _syncOwner;

    private RelativePosition _syncPos;

    public byte SyncHoldTime
    {
        get => _syncHoldTime;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _syncHoldTime = value;
        }
    }

    public byte SyncState
    {
        get => _syncState;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _syncState = value;
        }
    }

    public uint SyncOwner
    {
        get => _syncOwner;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _syncOwner = value;
        }
    }

    public RelativePosition SyncPos
    {
        get => _syncPos;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _syncPos = value;
        }
    }

    public Scp939AmnesticCloudInstanceComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteByte(_syncHoldTime);
            writer.WriteByte(_syncState);
            writer.WriteUInt(_syncOwner);
            writer.WriteRelativePosition(_syncPos);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteByte(_syncHoldTime);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteByte(_syncState);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteUInt(_syncOwner);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteRelativePosition(_syncPos);
        }
    }

}
