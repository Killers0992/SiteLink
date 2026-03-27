
namespace SiteLink.API.Networking.Objects;

//
// Name: JailbirdPickup
// NetworkID: 0
// AssetID: 2915316078
// SceneID: 0
// Path: JailbirdPickup
//
public class JailbirdPickupObject : NetworkObject
{
    public const uint ObjectAssetId = 2915316078;

    public override uint AssetId { get; } = ObjectAssetId;
    public JailbirdPickupComponent JailbirdPickup { get; }

    public JailbirdPickupObject(World world, Session owner = null, uint networkId = 0) : base(world, owner, networkId)
    {
        //
        Behaviours = new BehaviourComponent[1];

        JailbirdPickup = new JailbirdPickupComponent(this);
        Behaviours[0] = JailbirdPickup;
    }
}
