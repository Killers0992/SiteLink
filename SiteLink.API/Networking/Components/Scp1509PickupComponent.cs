
namespace SiteLink.API.Networking.Components;

public class Scp1509PickupComponent : CollisionDetectionPickupComponent
{
    public Scp1509PickupComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
    {
        // subscribe only once is done by root; here we only attach leaf hooks
    }

    protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        base.SerializeSyncVars(writer, forceAll);

        if (forceAll)
        {
            return;
        }

        writer.WriteULong(SyncVarDirtyBits);

    }

}
