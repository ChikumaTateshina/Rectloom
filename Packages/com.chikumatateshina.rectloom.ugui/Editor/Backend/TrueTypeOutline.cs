#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace Rectloom.Ugui.Backend
{
    // TrueType contours become SVG quadratic paths. Compound glyphs preserve their component transforms.
    internal sealed class TrueTypeOutline
    {
        private readonly byte[] _bytes;
        private readonly int _glyf, _loca;
        private readonly bool _longOffsets;
        internal int UnitsPerEm { get; }
        internal TrueTypeOutline(byte[] bytes)
        {
            _bytes = bytes; _glyf = Table(bytes, "glyf"); _loca = Table(bytes, "loca");
            int head = Table(bytes, "head");
            if (_glyf < 0 || _loca < 0 || head < 0) throw new NotSupportedException("The color font needs TrueType outlines.");
            _longOffsets = U16(bytes, head + 50) != 0; UnitsPerEm = U16(bytes, head + 18);
        }
        internal string Outline(int glyph, out int left, out int bottom, out int right, out int top)
        {
            int offset = Offset(glyph);
            left = I16(offset + 2); bottom = I16(offset + 4); right = I16(offset + 6); top = I16(offset + 8);
            return Shape(glyph, 0);
        }
        private int Offset(int glyph) => _glyf + (_longOffsets
            ? checked((int)U32(_bytes, _loca + glyph * 4)) : U16(_bytes, _loca + glyph * 2) * 2);
        private string Shape(int glyph, int depth)
        {
            if (depth > 16) throw new FormatException("Recursive compound glyph.");
            int offset = Offset(glyph);
            if (offset == Offset(glyph + 1)) return string.Empty;
            int contours = I16(offset), pos = offset + 10;
            if (contours < 0)
            {
                var output = new StringBuilder(); int flags;
                do
                {
                    flags = U16(_bytes, pos); int child = U16(_bytes, pos + 2); pos += 4;
                    int x, y;
                    if ((flags & 1) != 0) { x = I16(pos); y = I16(pos + 2); pos += 4; }
                    else { x = (sbyte)_bytes[pos++]; y = (sbyte)_bytes[pos++]; }
                    if ((flags & 2) == 0) throw new NotSupportedException("Point-aligned compound glyphs are not supported.");
                    double a = 1, b = 0, c = 0, d = 1;
                    if ((flags & 8) != 0) { a = d = I16(pos) / 16384.0; pos += 2; }
                    else if ((flags & 64) != 0) { a = I16(pos) / 16384.0; d = I16(pos + 2) / 16384.0; pos += 4; }
                    else if ((flags & 128) != 0)
                    { a = I16(pos) / 16384.0; b = I16(pos + 2) / 16384.0; c = I16(pos + 4) / 16384.0; d = I16(pos + 6) / 16384.0; pos += 8; }
                    output.Append("<g transform=\"matrix(").Append(N(a)).Append(' ').Append(N(b)).Append(' ')
                        .Append(N(c)).Append(' ').Append(N(d)).Append(' ').Append(x).Append(' ').Append(y).Append(")\">")
                        .Append(Shape(child, depth + 1)).Append("</g>");
                } while ((flags & 32) != 0);
                return output.ToString();
            }
            if (contours == 0) return string.Empty;
            var ends = new int[contours];
            for (int i = 0; i < contours; i++) { ends[i] = U16(_bytes, pos); pos += 2; }
            int count = ends[contours - 1] + 1;
            int instructions = U16(_bytes, pos); pos += 2 + instructions;
            var flagsList = new byte[count];
            for (int i = 0; i < count; i++)
            {
                byte flag = _bytes[pos++]; flagsList[i] = flag;
                if ((flag & 8) != 0)
                {
                    int repeat = _bytes[pos++];
                    if (i + repeat >= count) throw new FormatException("Invalid glyph flag repeat.");
                    while (repeat-- > 0) flagsList[++i] = flag;
                }
            }
            var xs = Coordinates(flagsList, ref pos, 2, 16);
            var ys = Coordinates(flagsList, ref pos, 4, 32);
            var path = new StringBuilder(); int start = 0;
            foreach (int end in ends)
            {
                bool firstOn = (flagsList[start] & 1) != 0, lastOn = (flagsList[end] & 1) != 0;
                double sx = firstOn ? xs[start] : lastOn ? xs[end] : (xs[start] + xs[end]) / 2.0;
                double sy = firstOn ? ys[start] : lastOn ? ys[end] : (ys[start] + ys[end]) / 2.0;
                path.Append("M").Append(N(sx)).Append(' ').Append(N(sy));
                int begin = firstOn ? start + 1 : start;
                int finish = !firstOn && lastOn ? end - 1 : end;
                bool pending = false; double px = 0, py = 0;
                for (int i = begin; i <= finish; i++)
                {
                    bool on = (flagsList[i] & 1) != 0;
                    if (on)
                    {
                        if (pending) path.Append("Q").Append(N(px)).Append(' ').Append(N(py)).Append(' ');
                        else path.Append("L");
                        path.Append(xs[i]).Append(' ').Append(ys[i]); pending = false;
                    }
                    else
                    {
                        if (pending) path.Append("Q").Append(N(px)).Append(' ').Append(N(py)).Append(' ')
                            .Append(N((px + xs[i]) / 2)).Append(' ').Append(N((py + ys[i]) / 2));
                        px = xs[i]; py = ys[i]; pending = true;
                    }
                }
                if (pending) path.Append("Q").Append(N(px)).Append(' ').Append(N(py)).Append(' ').Append(N(sx)).Append(' ').Append(N(sy));
                path.Append("Z"); start = end + 1;
            }
            return "<path d=\"" + path + "\"/>";
        }
        private int[] Coordinates(byte[] flags, ref int pos, int shortBit, int sameBit)
        {
            var values = new int[flags.Length]; int coordinate = 0;
            for (int i = 0; i < flags.Length; i++)
            {
                int delta = 0;
                if ((flags[i] & shortBit) != 0) delta = _bytes[pos++] * ((flags[i] & sameBit) != 0 ? 1 : -1);
                else if ((flags[i] & sameBit) == 0) { delta = I16(pos); pos += 2; }
                coordinate += delta; values[i] = coordinate;
            }
            return values;
        }
        private int I16(int offset) => (short)U16(_bytes, offset);
        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        internal static int U16(byte[] bytes, int offset) => (bytes[offset] << 8) | bytes[offset + 1];
        internal static uint U32(byte[] bytes, int offset) => ((uint)U16(bytes, offset) << 16) | (uint)U16(bytes, offset + 2);
        internal static int Table(byte[] bytes, string tag)
        {
            int count = U16(bytes, 4);
            for (int i = 0; i < count; i++)
            {
                int pos = 12 + i * 16;
                if (Encoding.ASCII.GetString(bytes, pos, 4) == tag) return checked((int)U32(bytes, pos + 8));
            }
            return -1;
        }
    }
}
