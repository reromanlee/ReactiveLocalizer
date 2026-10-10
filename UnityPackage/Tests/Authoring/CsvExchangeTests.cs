using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CsvExchangeTests
    {
        private const string ExpectedShop =
            "Key,Context,Maximum length,English,Russian,Pirate\r\n" +
            "Shop.Balance,,,\"You have {coins, plural, one {# coin} other {# coins}}.\",,\r\n" +
            "Shop.Purchase,Button that confirms buying the selected item.,16,Buy,Kupit,\r\n" +
            "Shop.Title,,,Shop,Magazin,\r\n";

        [Test]
        public void Write_GivesTheGridWithAByteOrderMark()
        {
            ExportBook book = ExchangeFixture.Export(null);

            byte[] bytes = CsvExchange.Write(book, book.Tables[1]);

            Assert.That(bytes[0], Is.EqualTo(0xEF));
            Assert.That(bytes[1], Is.EqualTo(0xBB));
            Assert.That(bytes[2], Is.EqualTo(0xBF));
            Assert.That(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Is.EqualTo(ExpectedShop));
        }

        [Test]
        public void Read_GivesBackWhatWriteWrote()
        {
            ExportBook book = ExchangeFixture.Export(null);

            ImportedFile file = CsvExchange.Read("Shop.csv", CsvExchange.Write(book, book.Tables[1]));

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.Languages.Count, Is.EqualTo(3));
            Assert.That(file.Languages[0].Label, Is.EqualTo("English"));
            Assert.That(file.Languages[1].Label, Is.EqualTo("Russian"));
            Assert.That(file.Rows.Count, Is.EqualTo(3));
            ImportedRow balance = file.Rows[0];
            Assert.That(balance.TableName, Is.EqualTo("Shop"));
            Assert.That(balance.Key, Is.EqualTo("Balance"));
            Assert.That(balance.GetText(0), Is.EqualTo("You have {coins, plural, one {# coin} other {# coins}}."));
            Assert.That(balance.GetText(1), Is.Null);
            Assert.That(file.Rows[1].GetText(1), Is.EqualTo("Kupit"));
            Assert.That(file.Rows[1].Location, Is.EqualTo("Shop.csv, row 3"));
        }

        [Test]
        public void Read_TakesSemicolonsQuotesAndLineBreaks()
        {
            string text = "Key;English;Russian\r\nPurchase;Buy;\"Kupit;\r\n\"\"seichas\"\"\"\r\n\r\n";

            ImportedFile file = CsvExchange.Read("Shop.Russian.csv", Encoding.UTF8.GetBytes(text));

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.Rows.Count, Is.EqualTo(1));
            Assert.That(file.Rows[0].TableName, Is.EqualTo("Shop"), "A key without its table belongs to the table the file is named after.");
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("Kupit;\n\"seichas\""));
        }

        [Test]
        public void Read_TakesTabSeparatedUtf16()
        {
            byte[] text = Encoding.Unicode.GetBytes("Key\tEnglish\tRussian\r\nShop.Title\tShop\tMagazin\r\n");
            byte[] data = new byte[text.Length + 2];
            data[0] = 0xFF;
            data[1] = 0xFE;
            text.CopyTo(data, 2);

            ImportedFile file = CsvExchange.Read("Export.txt", data);

            Assert.That(file.Rows.Count, Is.EqualTo(1));
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("Magazin"));
        }

        [Test]
        public void Read_ReportsTextThatIsNotUnicode()
        {
            // 'Caf' and an e with acute accent, as a Western code page saves it.
            ImportedFile file = CsvExchange.Read("Shop.csv", new byte[] { 0x4B, 0x65, 0x79, 0x0A, 0x43, 0x61, 0x66, 0xE9 });

            Assert.That(file.Problems.Count, Is.EqualTo(1));
            Assert.That(file.Problems[0], Does.Contain("CSV UTF-8"));
        }

        [Test]
        public void Read_ReportsAFileWithoutKeys()
        {
            ImportedFile file = CsvExchange.Read("Shop.csv", Encoding.UTF8.GetBytes("Name,English\nTitle,Shop\n"));

            Assert.That(file.Rows, Is.Empty);
            Assert.That(file.Problems[0], Does.Contain("no 'Key' column"));
        }

        [Test]
        public void Read_LeavesTheTranslatorColumnsAlone()
        {
            ImportedFile file = CsvExchange.Read("Shop.csv", Encoding.UTF8.GetBytes("Key,Context,Maximum length,English\nShop.Title,A title,5,Shop\n"));

            Assert.That(file.Languages.Count, Is.EqualTo(1));
            Assert.That(file.Rows[0].GetText(0), Is.EqualTo("Shop"));
        }

        [Test]
        public void Read_SkipsSpreadsheetErrorsForTextsTakenForFormulas()
        {
            ImportedFile file = CsvExchange.Read("Shop.csv", Encoding.UTF8.GetBytes("Key,English,Russian\nShop.Bonus,+5 HP,#NAME?\n"));

            Assert.That(file.Rows[0].GetText(1), Is.Null);
            Assert.That(file.Problems.Count, Is.EqualTo(1));
            Assert.That(file.Problems[0], Does.StartWith("Shop.csv, row 2: its Russian cell holds #NAME?"));
        }

        [Test]
        public void DetectSeparator_PrefersWhatTheHeaderUses()
        {
            Assert.That(CsvExchange.DetectSeparator("Key;English;\"A,B\"\nx,y,z"), Is.EqualTo(';'));
            Assert.That(CsvExchange.DetectSeparator("Key\tEnglish"), Is.EqualTo('\t'));
            Assert.That(CsvExchange.DetectSeparator("Key"), Is.EqualTo(','));
        }
    }
}
