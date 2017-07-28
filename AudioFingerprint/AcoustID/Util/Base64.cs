// -----------------------------------------------------------------------
// <copyright file="Base64.cs" company="">
// Original C++ implementation by Lukas Lalinsky, http://acoustid.org/chromaprint
// </copyright>
// -----------------------------------------------------------------------

namespace AcoustID.Util
{
    using System.Text;

    /// <summary>
    /// Encode and decode Base64 data.
    /// </summary>
    /// <remarks>
    /// This is a custom base64 implementation. Using Convert.ToBase64String and Convert.FromBase64String
    /// is not an option here.
    /// </remarks>
    internal static class Base64
    {
        internal static Encoding ByteEncoding = Encoding.GetEncoding("Latin1");

        static byte[] kBase64Chars = ByteEncoding.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");
        static byte[] kBase64CharsReversed = {
	        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
	        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 62, 0, 0, 52,
	        53, 54, 55, 56, 57, 58, 59, 60, 61, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5,
	        6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
	        25, 0, 0, 0, 0, 63, 0, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37,
	        38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 0, 0, 0, 0, 0
        };

        public static string Encode(string orig)
        {
            int size = orig.Length;
            int encoded_size = (size * 4 + 2) / 3;
            byte[] encoded = new byte[encoded_size];

            byte[] src = ByteEncoding.GetBytes(orig);
            int src_offset = 0;
            int dest = 0;

            while (size > 0)
            {
                encoded[dest++] = kBase64Chars[(src[0 + src_offset] >> 2)];
                encoded[dest++] = kBase64Chars[((src[0 + src_offset] << 4) | ((--size > 0) ? (src[1 + src_offset] >> 4) : 0)) & 63];
                if (size > 0)
                {
                    encoded[dest++] = kBase64Chars[((src[1 + src_offset] << 2) | ((--size > 0) ? (src[2 + src_offset] >> 6) : 0)) & 63];
                    if (size > 0)
                    {
                        encoded[dest++] = kBase64Chars[src[2 + src_offset] & 63];
                        --size;
                    }
                }
                src_offset += 3;
            }
            return ByteEncoding.GetString(encoded);
        }

        public static string Decode(string encoded)
        {
            int size = encoded.Length;
            byte[] str = new byte[(3 * size) / 4];
            byte[] src = ByteEncoding.GetBytes(encoded);
            int src_offset = 0;
            int dest = 0;
            while (size > 0)
            {
                int b0 = kBase64CharsReversed[src[src_offset++]];
                if (--size > 0)
                {
                    int b1 = kBase64CharsReversed[src[src_offset++]];
                    int r = (b0 << 2) | (b1 >> 4);
                    //assert(dest != str.end());
                    str[dest++] = (byte)r;
                    if (--size > 0)
                    {
                        int b2 = kBase64CharsReversed[src[src_offset++]];
                        r = ((b1 << 4) & 255) | (b2 >> 2);
                        //assert(dest != str.end());
                        str[dest++] = (byte)r;
                        if (--size > 0)
                        {
                            int b3 = kBase64CharsReversed[src[src_offset++]];
                            r = ((b2 << 6) & 255) | b3;
                            //assert(dest != str.end());
                            str[dest++] = (byte)r;
                            --size;
                        }
                    }
                }
            }
            return ByteEncoding.GetString(str);
        }
    }
}
