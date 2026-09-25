namespace ValleyRail
{
    /// <summary>
    /// A 5×7 pixel font for the drive-in: the films' titles on the screen and the letters on its signs. Capitals, digits
    /// and a little punctuation; anything else draws as a gap.
    /// </summary>
    public static class PixelFont
    {
        public const int Width = 5, Height = 7, Advance = Width + 1;
        // Seven rows per glyph, top to bottom, as hex; bit 4 is the left column.
        static readonly string[] Glyphs =
        {
            "A 0E 11 11 1F 11 11 11", "B 1E 11 11 1E 11 11 1E", "C 0E 11 10 10 10 11 0E", "D 1E 11 11 11 11 11 1E",
            "E 1F 10 10 1E 10 10 1F", "F 1F 10 10 1E 10 10 10", "G 0E 11 10 17 11 11 0F", "H 11 11 11 1F 11 11 11",
            "I 0E 04 04 04 04 04 0E", "J 07 02 02 02 02 12 0C", "K 11 12 14 18 14 12 11", "L 10 10 10 10 10 10 1F",
            "M 11 1B 15 15 11 11 11", "N 11 11 19 15 13 11 11", "O 0E 11 11 11 11 11 0E", "P 1E 11 11 1E 10 10 10",
            "Q 0E 11 11 11 15 12 0D", "R 1E 11 11 1E 14 12 11", "S 0F 10 10 0E 01 01 1E", "T 1F 04 04 04 04 04 04",
            "U 11 11 11 11 11 11 0E", "V 11 11 11 11 11 0A 04", "W 11 11 11 15 15 15 0A", "X 11 11 0A 04 0A 11 11",
            "Y 11 11 11 0A 04 04 04", "Z 1F 01 02 04 08 10 1F",
            "0 0E 11 13 15 19 11 0E", "1 04 0C 04 04 04 04 0E", "2 0E 11 01 02 04 08 1F", "3 1F 02 04 02 01 11 0E",
            "4 02 06 0A 12 1F 02 02", "5 1F 10 1E 01 01 11 0E", "6 06 08 10 1E 11 11 0E", "7 1F 01 02 04 08 08 08",
            "8 0E 11 11 0E 11 11 0E", "9 0E 11 11 0F 01 02 0C",
            "! 04 04 04 04 04 00 04", "? 0E 11 01 02 04 00 04", "- 00 00 00 1F 00 00 00", ". 00 00 00 00 00 0C 0C",
            "' 04 04 08 00 00 00 00",
        };
        static readonly byte[][] rows = Parse();

        /// <summary>The seven rows of <paramref name="c"/> (lower case reads as upper case), or null for a gap.</summary>
        public static byte[] Rows(char c)
        {
            c = char.ToUpperInvariant(c);
            return c < rows.Length ? rows[c] : null;
        }
        /// <summary>Whether the pixel in column <paramref name="x"/> (0 left) and row <paramref name="y"/> (0 top) is lit.</summary>
        public static bool Lit(byte[] glyph, int x, int y) => glyph != null && (glyph[y] >> (Width - 1 - x) & 1) != 0;
        /// <summary>Width of a line of text in pixels, without the gap after the last letter.</summary>
        public static int Measure(string text) => text.Length == 0 ? 0 : text.Length * Advance - 1;

        static byte[][] Parse()
        {
            var table = new byte[128][];
            foreach (var line in Glyphs)
            {
                var parts = line.Split(' ');
                var glyph = new byte[Height];
                for (int r = 0; r < Height; r++)
                    glyph[r] = (byte)System.Convert.ToInt32(parts[r + 1], 16);
                table[parts[0][0]] = glyph;
            }
            return table;
        }
    }
}
