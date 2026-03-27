using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class Scp079GeneratorComponent : SpawnableStructureComponent
{
    private float _totalActivationTime;

    private float _totalDeactivationTime;

    private byte _flags;

    private short _syncTime;

    public float TotalActivationTime
    {
        get => _totalActivationTime;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _totalActivationTime = value;
        }
    }

    public float TotalDeactivationTime
    {
        get => _totalDeactivationTime;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _totalDeactivationTime = value;
        }
    }

    public byte Flags
    {
        get => _flags;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _flags = value;
        }
    }

    public short SyncTime
    {
        get => _syncTime;
        set
        {
            SetSyncVarDirtyBit(8UL);
            _syncTime = value;
        }
    }

    public Scp079GeneratorComponent(NetworkObject networkObject) : base(networkObject)
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteFloat(_totalActivationTime);
            writer.WriteFloat(_totalDeactivationTime);
            writer.WriteByte(_flags);
            writer.WriteShort(_syncTime);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteFloat(_totalActivationTime);
        }

        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteFloat(_totalDeactivationTime);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteByte(_flags);
        }

        if ((SyncVarDirtyBits & 8UL) != 0UL)
        {
            writer.WriteShort(_syncTime);
        }
    }

}
