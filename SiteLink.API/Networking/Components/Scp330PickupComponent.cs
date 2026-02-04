using Mirror;
using InventorySystem.Items.Usables.Scp330;

namespace SiteLink.API.Networking.Components;

public class Scp330PickupComponent : CollisionDetectionPickupComponent
{
    private CandyKindID _exposedCandy;

    public CandyKindID ExposedCandy
    {
        get => _exposedCandy;
        set
        {
            SetSyncVarDirtyBit(2UL);
            _exposedCandy = value;
        }
    }

    public Scp330PickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            writer.Write(_exposedCandy);
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);


        if ((SyncVarDirtyBits & 2UL) != 0UL)
        {
            writer.Write(_exposedCandy);
        }
    }

}
