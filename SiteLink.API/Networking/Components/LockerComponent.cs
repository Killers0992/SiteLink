using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class LockerComponent : SpawnableStructureComponent
{
    private ushort _openedChambers;

    public ushort OpenedChambers
    {
        get => _openedChambers;
        set
        {
            SetSyncVarDirtyBit(1UL);
            _openedChambers = value;
        }
    }

    public LockerComponent(NetworkObject networkObject, params SyncedNetworkProperty[] objects) : base(networkObject, objects)
    {
        //
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteUShort(_openedChambers);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 1UL) != 0UL)
        {
            writer.WriteUShort(_openedChambers);
        }
    }

}
