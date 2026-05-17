using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace SiteLink.API
{
    public static class ECDSA
    {
        public static bool Verify(string data, string signature, AsymmetricKeyParameter pubKey)
        {
            return ECDSA.VerifyBytes(data, Convert.FromBase64String(signature), pubKey);
        }

        public static bool VerifyBytes(string data, byte[] signature, AsymmetricKeyParameter pubKey)
        {
            bool result;
            try
            {
                ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");
                signer.Init(false, pubKey);
                byte[] array = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(data.Length));
                int bytes = Utf8.GetBytes(data, array);
                signer.BlockUpdate(array, 0, bytes);
                ArrayPool<byte>.Shared.Return(array, false);
                result = signer.VerifySignature(signature);
            }
            catch (Exception ex)
            {
                SiteLinkLogger.Info("ECDSA Verification Error (BouncyCastle): " + ex.Message + ", " + ex.StackTrace);
                result = false;
            }
            return result;
        }

        public static AsymmetricKeyParameter PublicKeyFromString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            AsymmetricKeyParameter result;
            using (TextReader textReader = new StringReader(key))
            {
                result = (AsymmetricKeyParameter)new PemReader(textReader).ReadObject();
            }
            return result;
        }

        public static string KeyToString(AsymmetricKeyParameter key)
        {
            string result;
            using (TextWriter textWriter = new StringWriter())
            {
                PemWriter pemWriter = new PemWriter(textWriter);
                pemWriter.WriteObject(key);
                pemWriter.Writer.Flush();
                result = textWriter.ToString();
            }
            return result;
        }
    }
}
