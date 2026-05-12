using System;
using System.Drawing;

namespace ColorPicks
{
    /// <summary>
    /// Utility methods for color conversion — mirrors Java ColorUtils.
    /// </summary>
    public static class ColorUtils
    {
        /// <summary>Returns a "#RRGGBB" hex string for the given color.</summary>
        public static string ColorToHex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }

        /// <summary>Parses a "#RRGGBB" hex string into a Color.</summary>
        public static Color HexToColor(string hex)
        {
            if (hex == null) return Color.Black;
            string s = hex.Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            return ColorTranslator.FromHtml(s);
        }

        /// <summary>Returns an "hsl(H, S%, L%)" string for the given color.</summary>
        public static string RgbToHslString(Color c)
        {
            float[] hsl = RgbToHsl(c.R, c.G, c.B);
            return string.Format("hsl({0:0}, {1:0}%, {2:0}%)", hsl[0], hsl[1] * 100f, hsl[2] * 100f);
        }

        /// <summary>
        /// Converts RGB (0–255 each) to HSL.
        /// Returns float[3] { H (0–360), S (0–1), L (0–1) }.
        /// </summary>
        public static float[] RgbToHsl(int r, int g, int b)
        {
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;

            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float h, s;
            float l = (max + min) / 2f;

            if (Math.Abs(max - min) < float.Epsilon)
            {
                h = s = 0f;
            }
            else
            {
                float d = max - min;
                s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

                if (Math.Abs(max - rf) < float.Epsilon)
                    h = (gf - bf) / d + (gf < bf ? 6f : 0f);
                else if (Math.Abs(max - gf) < float.Epsilon)
                    h = (bf - rf) / d + 2f;
                else
                    h = (rf - gf) / d + 4f;

                h *= 60f;
            }

            return new float[] { h, s, l };
        }
    }
}
