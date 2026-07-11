namespace SiteLink.API.Networking;

public static class AssetsDatabase
{
    public static Dictionary<uint, Func<(World, Session, uint), NetworkObject>> Objects = new Dictionary<uint, Func<(World, Session, uint), NetworkObject>>()
    {
        { 180257209, (p) => new RespawnManagerObject(p.Item1, p.Item2, p.Item3) },
        { 3816198336, (p) => new PlayerObject(p.Item1, p.Item2, p.Item3) },
        { 1321952889, (p) => new PrimitiveObjectToyObject(p.Item1, p.Item2, p.Item3) },
        { 3956448839, (p) => new LightSourceToyObject(p.Item1, p.Item2, p.Item3) },
        { 162530276, (p) => new TextToyObject(p.Item1, p.Item2, p.Item3) },
        { 3938583646, (p) => new WaypointToyObject(p.Item1, p.Item2, p.Item3) },
    };
}
