using Org.BouncyCastle.Crypto;
using System.Text;

namespace SiteLink.API.Misc;

public static class ScpCentralServer
{
    static AsymmetricKeyParameter _masterKey;

    private const string MasterPublicKey = @"-----BEGIN PUBLIC KEY-----
MIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQAbL0YvrhVB2meqCq5XzjAJD8Ii0hb
BHdIQ587N583cP8twjDhcITjZhBHJPJDuA85XdpgG04HwT0SD3WcAvoQXBUAUsG1
LS9TR4urHwfgfroq4tH2HAQE6ZxFZeIFSglLO8nxySim4yKBj96HLG624lzKvzoD
Id+GOwjcd3XskOq9Dwc=
-----END PUBLIC KEY-----";

    public static string ReadCache()
    {
        string result;
        try
        {
            if (!File.Exists("./centralcache.txt"))
            {
                SiteLinkLogger.Info($"Central server public key not found in cache.", $"CentralServerKeyCache");
                result = null;
            }
            else if (!File.Exists("./centralkeysignature.txt"))
            {
                SiteLinkLogger.Info($"Central server public key signature not found in cache.", $"CentralServerKeyCache");
                result = null;
            }
            else
            {
                string[] source = File.ReadAllLines("./centralcache.txt");
                string[] array = File.ReadAllLines("./centralkeysignature.txt");
                if (array.Length == 0)
                {
                    SiteLinkLogger.Error($"Can't load central server public key from cache - empty signature.", $"CentralServerKeyCache");
                    result = null;
                }
                else
                {
                    string text = source.Aggregate("", (string current, string line) => current + line + "\r\n").Trim();
                    try
                    {
                        if (ECDSA.Verify(text, array[0], ScpCentralServer.MasterKey))
                        {
                            result = text;
                        }
                        else
                        {
                            SiteLinkLogger.Error($"Invalid signature of Central Server Key in cache!", $"CentralServerKeyCache");
                            result = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        SiteLinkLogger.Error($"Can't load central server public key from cache - " + ex.Message, $"CentralServerKeyCache");
                        result = null;
                    }
                }
            }
        }
        catch (Exception ex2)
        {
            SiteLinkLogger.Error($"Can't read public key cache - " + ex2.Message, $"CentralServerKeyCache");
            result = null;
        }
        return result;
    }

    public static void SaveCache(string key, string signature)
    {
        try
        {
            if (!ECDSA.Verify(key, signature, ScpCentralServer.MasterKey))
            {
                SiteLinkLogger.Error($"Invalid signature of Central Server Key!", $"CentralServerKeyCache");
            }
            else
            {
                if (File.Exists("./centralcache.txt"))
                {
                    if (key == ScpCentralServer.ReadCache())
                    {
                        SiteLinkLogger.Info($"Key cache is up to date.", $"CentralServerKeyCache");
                        return;
                    }
                    File.Delete("./centralcache.txt");
                }

                SiteLinkLogger.Info($"Updating key cache...", $"CentralServerKeyCache");
                File.WriteAllText($"./centralcache.txt", key, Encoding.UTF8);
                File.WriteAllText($"./centralkeysignature.txt", signature, Encoding.UTF8);
                SiteLinkLogger.Info($"Key cache updated!", $"CentralServerKeyCache");
            }
        }
        catch (Exception ex)
        {
            SiteLinkLogger.Error("Can't write public key cache - " + ex.Message, $"CentralServerKeyCache");
        }
    }

    public static AsymmetricKeyParameter MasterKey
    {
        get
        {
            if (_masterKey == null)
            {
                _masterKey = ECDSA.PublicKeyFromString(MasterPublicKey);
            }

            return _masterKey;
        }
    }
}