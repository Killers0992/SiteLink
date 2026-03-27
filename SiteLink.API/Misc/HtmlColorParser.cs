using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteLink.API.Misc
{
    public static class HtmlColorParser
    {
        private static readonly Dictionary<string, Color> NamedColors =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "black", new Color(0f,0f,0f) },
            { "white", new Color(1f,1f,1f) },
            { "red", new Color(1f,0f,0f) },
            { "green", new Color(0f,1f,0f) },
            { "blue", new Color(0f,0f,1f) },
            { "yellow", new Color(1f,1f,0f) },
            { "cyan", new Color(0f,1f,1f) },
            { "magenta", new Color(1f,0f,1f) },
            { "gray", new Color(0.5f,0.5f,0.5f) },
            { "clear", new Color(0f,0f,0f,0f) }
        };

        public static bool TryParseHtmlString(string htmlString, out Color color)
        {
            color = new Color();

            if (string.IsNullOrWhiteSpace(htmlString))
                return false;

            htmlString = htmlString.Trim();

            // Named color
            if (NamedColors.TryGetValue(htmlString, out color))
                return true;

            if (!htmlString.StartsWith("#"))
                return false;

            string hex = htmlString.Substring(1);

            try
            {
                if (hex.Length == 3) // RGB
                {
                    float r = Convert.ToInt32(hex[0].ToString() + hex[0], 16) / 255f;
                    float g = Convert.ToInt32(hex[1].ToString() + hex[1], 16) / 255f;
                    float b = Convert.ToInt32(hex[2].ToString() + hex[2], 16) / 255f;
                    color = new Color(r, g, b, 1f);
                    return true;
                }
                else if (hex.Length == 4) // RGBA
                {
                    float r = Convert.ToInt32(hex[0].ToString() + hex[0], 16) / 255f;
                    float g = Convert.ToInt32(hex[1].ToString() + hex[1], 16) / 255f;
                    float b = Convert.ToInt32(hex[2].ToString() + hex[2], 16) / 255f;
                    float a = Convert.ToInt32(hex[3].ToString() + hex[3], 16) / 255f;
                    color = new Color(r, g, b, a);
                    return true;
                }
                else if (hex.Length == 6) // RRGGBB
                {
                    float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    color = new Color(r, g, b, 1f);
                    return true;
                }
                else if (hex.Length == 8) // RRGGBBAA
                {
                    float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    float a = Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
                    color = new Color(r, g, b, a);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }

}
