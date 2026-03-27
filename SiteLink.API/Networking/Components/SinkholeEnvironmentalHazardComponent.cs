
namespace SiteLink.API.Networking.Components;

public class SinkholeEnvironmentalHazardComponent : EnvironmentalHazardComponent
{
    public SinkholeEnvironmentalHazardComponent(NetworkObject networkObject) : base(networkObject)
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
