using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API
{
    public static class Sha
    {
        public static string HashToString(byte[] hash)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (byte b in hash)
            {
                stringBuilder.Append(b.ToString("X2"));
            }
            string result = stringBuilder.ToString();
            return result;
        }

        public static byte[] Sha256(byte[] message)
        {
            byte[] result;
            using (SHA256 sha = SHA256.Create())
            {
                result = sha.ComputeHash(message);
            }
            return result;
        }

        public static byte[] Sha256(byte[] message, int offset, int length)
        {
            byte[] result;
            using (SHA256 sha = SHA256.Create())
            {
                result = sha.ComputeHash(message, offset, length);
            }
            return result;
        }

        public static byte[] Sha256(string message)
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(message.Length));
            int bytes = Utf8.GetBytes(message, array);
            byte[] result = Sha.Sha256(array, 0, bytes);
            ArrayPool<byte>.Shared.Return(array, false);
            return result;
        }
    }
}
