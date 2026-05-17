using Org.BouncyCastle.Crypto;
using System.Text;

namespace SiteLink.API
{
    public static class CentralServerKeyCache
    {
        public static string ReadCache()
        {
            string result;
            try
            {
                if (!File.Exists("./centralcache.txt"))
                {
                    result = null;
                }
                else if (!File.Exists("./centralkeysignature.txt"))
                {
                    result = null;
                }
                else
                {
                    string[] source = File.ReadAllLines("./centralcache.txt");
                    string[] array = File.ReadAllLines("./centralkeysignature.txt");
                    if (array.Length == 0)
                    {
                        result = null;
                    }
                    else
                    {
                        string text = source.Aggregate("", (string current, string line) => current + line + "\r\n").Trim();
                        try
                        {
                            if (ECDSA.Verify(text, array[0], CentralServerKeyCache.MasterKey))
                            {
                                result = text;
                            }
                            else
                            {
                                result = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            result = null;
                        }
                    }
                }
            }
            catch (Exception ex2)
            {

                result = null;
            }
            return result;
        }

        public static void SaveCache(string key, string signature)
        {
            try
            {
                if (!ECDSA.Verify(key, signature, CentralServerKeyCache.MasterKey))
                {
                }
                else
                {
                    if (File.Exists("./centralcache.txt"))
                    {
                        if (key == CentralServerKeyCache.ReadCache())
                            return;

                        File.Delete("./centralcache.txt");
                    }

                    File.WriteAllText($"./centralcache.txt", key, Encoding.UTF8);
                    File.WriteAllText($"./centralkeysignature.txt", signature, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
            }
        }

        internal static readonly AsymmetricKeyParameter MasterKey = ECDSA.PublicKeyFromString("-----BEGIN PUBLIC KEY-----\r\nMIGbMBAGByqGSM49AgEGBSuBBAAjA4GGAAQAbL0YvrhVB2meqCq5XzjAJD8Ii0hb\r\nBHdIQ587N583cP8twjDhcITjZhBHJPJDuA85XdpgG04HwT0SD3WcAvoQXBUAUsG1\r\nLS9TR4urHwfgfroq4tH2HAQE6ZxFZeIFSglLO8nxySim4yKBj96HLG624lzKvzoD\r\nId+GOwjcd3XskOq9Dwc=\r\n-----END PUBLIC KEY-----");
    }
}