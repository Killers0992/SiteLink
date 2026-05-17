using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API
{
    public static class Utf8
    {
        private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

        public static int GetBytes(string data, byte[] buffer)
        {
            return Utf8.Encoding.GetBytes(data, 0, data.Length, buffer, 0);
        }
    }
}
