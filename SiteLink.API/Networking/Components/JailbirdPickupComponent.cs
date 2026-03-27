using Mirror;
using InventorySystem.Items.Jailbird;

namespace SiteLink.API.Networking.Components;

public class JailbirdPickupComponent : CollisionDetectionPickupComponent
{
    private JailbirdWearState _wear;

    public JailbirdWearState Wear
    {
        get => _wear;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _wear = value;
        }
    }

    public JailbirdPickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_wear);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.Write(_wear);
        }
    }

}
