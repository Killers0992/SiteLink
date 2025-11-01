using Org.BouncyCastle.Crypto;

namespace SiteLink.API.Structs;

public struct PublicKey
{
    public static AsymmetricKeyParameter CurrentKey;

    public PublicKey(string key, string signature, string credits)
    {
        this.Key = key;
        this.Signature = signature;
        this.Credits = credits;
    }

    public string Key;
    public string Signature;
    public string Credits;
}
