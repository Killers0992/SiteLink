using Mirror;
using System;

namespace SiteLink.API.Networking.Components;

public class RadioPickupComponent : CollisionDetectionPickupComponent
{
    private bool _savedEnabled;

    private byte _savedRange;

    public bool SavedEnabled
    {
        get => _savedEnabled;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _savedEnabled = value;
        }
    }

    public byte SavedRange
    {
        get => _savedRange;
        set
        {
            SetSyncVarDirtyBit(4UL);
            _savedRange = value;
        }
    }

    public RadioPickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.WriteBool(_savedEnabled);
            writer.WriteByte(_savedRange);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.WriteBool(_savedEnabled);
        }

        if ((SyncVarDirtyBits & 4UL) != 0UL)
        {
            writer.WriteByte(_savedRange);
        }
    }

}
