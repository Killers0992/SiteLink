
namespace SiteLink.API.Networking.Components;

public class ExplosionGrenadeComponent : EffectGrenadeComponent
{
    public ExplosionGrenadeComponent(NetworkObject networkObject) : base(networkObject, new SyncListObject<byte>())
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
