using System;
using System.Runtime.CompilerServices;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The 64-bit FNV-1a hashes that names and source texts are identified by. Saved references, compiled tables and
    /// fingerprints all depend on these exact values, so the functions must never change; golden-value tests pin them.
    /// </summary>
    /// <remarks>
    /// Both hash the UTF-16LE bytes of their input, low byte first. FNV-1a is simple enough for any outside tool to
    /// reproduce, and at 64 bits a collision between the names of one table is practically impossible; the importer
    /// still checks for one.
    /// </remarks>
    internal static class Hashing
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        /// <summary>
        /// Hashes a name with its ASCII letters lowercased first, so names that differ only in case hash alike, the
        /// same way the naming rule already treats them as one name.
        /// </summary>
        public static ulong ComputeNameHash(ReadOnlySpan<char> name)
        {
            ulong hash = OffsetBasis;
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                // Only ASCII is lowercased, which keeps the hash independent of any culture. Names are ASCII by rule.
                if ((uint)(character - 'A') <= 'Z' - 'A')
                {
                    character = (char)(character | 0x20);
                }
                hash = Append(hash, character);
            }
            return hash;
        }

        /// <summary>Hashes a text exactly as written, which is what fingerprints of source texts need.</summary>
        public static ulong ComputeTextHash(ReadOnlySpan<char> text)
        {
            ulong hash = OffsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash = Append(hash, text[i]);
            }
            return hash;
        }

        /// <summary>
        /// Returns the fingerprint of a source text: the top 24 bits of its text hash, which a translation file writes
        /// as six hexadecimal digits.
        /// </summary>
        public static uint ComputeFingerprint(ReadOnlySpan<char> sourceText) => (uint)(ComputeTextHash(sourceText) >> 40);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Append(ulong hash, char character)
        {
            unchecked
            {
                hash = (hash ^ (byte)character) * Prime;
                return (hash ^ (byte)(character >> 8)) * Prime;
            }
        }
    }
}
