
namespace SiteLink.API.Networking.Objects;

//
// Name: Scp018Projectile
// NetworkID: 0
// AssetID: 3525743409
// SceneID: 0
// Path: Scp018Projectile
//
public class Scp018ProjectileObject : NetworkObject
{
    public const uint ObjectAssetId = 3525743409;

    public override uint AssetId { get; } = ObjectAssetId;
    public Scp018ProjectileComponent Scp018Projectile { get; }

    public Scp018ProjectileObject(World world, Session owner = null, uint networkId = 0) : base(world, owner, networkId)
    {
        //
        Behaviours = new BehaviourComponent[1];

        Scp018Projectile = new Scp018ProjectileComponent(this);
        Behaviours[0] = Scp018Projectile;
    }
}
