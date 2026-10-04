using NUnit.Framework;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class HashingTests
    {
        // Golden values computed outside the package, by an independent FNV-1a implementation. A change to any of them
        // means every saved reference, compiled table and fingerprint in every project would silently break.

        [TestCase("", 0xCBF29CE484222325UL)]
        [TestCase("Purchase", 0x6D289C670845619EUL)]
        [TestCase("Shop", 0xC8FB4EAFC9401791UL)]
        [TestCase("English", 0x8CAD85410EEB4987UL)]
        [TestCase("Localization", 0x331780DC7A154DCEUL)]
        [TestCase("Item10234", 0x26E371916E788844UL)]
        [TestCase("Line_9", 0x478BCFBA9704A585UL)]
        public void ComputeNameHash_MatchesTheGoldenValue(string name, ulong expectedHash)
        {
            Assert.That(Hashing.ComputeNameHash(name), Is.EqualTo(expectedHash));
        }

        [Test]
        public void ComputeNameHash_IgnoresCase()
        {
            ulong hash = Hashing.ComputeNameHash("Purchase");

            Assert.That(Hashing.ComputeNameHash("purchase"), Is.EqualTo(hash));
            Assert.That(Hashing.ComputeNameHash("PURCHASE"), Is.EqualTo(hash));
        }

        [TestCase("", 0xCBF29CE484222325UL)]
        [TestCase("Buy", 0x9A12484C1C36245BUL)]
        [TestCase("buy", 0x05174403EAB3B73BUL)]
        [TestCase("\u041A\u0443\u043F\u0438\u0442\u044C", 0x36B8C5309F3C6E0DUL)]
        [TestCase("You have {coins, plural, one {# coin} other {# coins}}.", 0x59797FC6638DC809UL)]
        public void ComputeTextHash_MatchesTheGoldenValue(string text, ulong expectedHash)
        {
            Assert.That(Hashing.ComputeTextHash(text), Is.EqualTo(expectedHash));
        }

        [Test]
        public void ComputeFingerprint_IsTheTopTwentyFourBitsOfTheTextHash()
        {
            Assert.That(Hashing.ComputeFingerprint("Buy"), Is.EqualTo(0x9A1248u));
            Assert.That(Hashing.ComputeFingerprint("\u041A\u0443\u043F\u0438\u0442\u044C"), Is.EqualTo(0x36B8C5u));
        }
    }
}
