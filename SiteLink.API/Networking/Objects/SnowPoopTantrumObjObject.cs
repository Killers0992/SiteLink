
namespace SiteLink.API.Networking.Objects;

//
// Name: SnowPoop - TantrumObj
// NetworkID: 0
// AssetID: 6069361
// SceneID: 0
// Path: SnowPoop - TantrumObj
//
public class SnowPoopTantrumObjObject : NetworkObject
{
    public const uint ObjectAssetId = 6069361;

    public override uint AssetId { get; } = ObjectAssetId;

    public SnowPoopTantrumObjObject(World world, Session owner = null, uint networkId = 0) : base(world, owner, networkId)
    {
        //
        Behaviours = new BehaviourComponent[0];
    }
}
