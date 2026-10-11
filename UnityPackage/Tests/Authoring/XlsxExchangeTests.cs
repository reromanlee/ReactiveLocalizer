using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class XlsxExchangeTests
    {
        private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static readonly string ExpectedShopSheet =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" +
            "<worksheet xmlns=\"" + Main + "\">" +
            "<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>" +
            "<cols><col min=\"1\" max=\"1\" width=\"32\" style=\"2\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"40\" style=\"2\" customWidth=\"1\"/>" +
            "<col min=\"3\" max=\"3\" width=\"10\" style=\"2\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"50\" style=\"2\" customWidth=\"1\"/>" +
            "<col min=\"5\" max=\"5\" width=\"50\" style=\"3\" customWidth=\"1\"/></cols><sheetData>" +
            "<row r=\"1\">" + Text("A1", 1, "Key") + Text("B1", 1, "Context") + Text("C1", 1, "Maximum length") + Text("D1", 1, "English") + Text("E1", 1, "Russian") + "</row>" +
            "<row r=\"2\">" + Text("A2", 2, "Shop.Balance") + Text("D2", 2, "You have {coins, plural, one {# coin} other {# coins}}.") + "</row>" +
            "<row r=\"3\">" + Text("A3", 2, "Shop.Purchase") + Text("B3", 2, "Button that confirms buying the selected item.") +
            "<c r=\"C3\" s=\"2\"><v>16</v></c>" + Text("D3", 2, "Buy") + Text("E3", 3, "Kupit") + "</row>" +
            "<row r=\"4\">" + Text("A4", 2, "Shop.Title") + Text("D4", 2, "Shop") + Text("E4", 4, "Magazin") + "</row>" +
            "</sheetData></worksheet>";

        [Test]
        public void Write_LaysOutASheetPerTable()
        {
            Dictionary<string, byte[]> parts = Unzip(XlsxExchange.Write(ExchangeFixture.ExportRussian(), null));

            Assert.That(Encoding.UTF8.GetString(parts["xl/worksheets/sheet2.xml"]), Is.EqualTo(ExpectedShopSheet));
            Assert.That(Encoding.UTF8.GetString(parts["xl/workbook.xml"]), Does.Contain("<sheet name=\"Common\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Shop\" sheetId=\"2\" r:id=\"rId2\"/>"));
            Assert.That(parts.ContainsKey("[Content_Types].xml"), Is.True);
            Assert.That(parts.ContainsKey("xl/styles.xml"), Is.True);
        }

        [Test]
        public void Read_GivesBackWhatWriteWrote()
        {
            ImportedFile file = XlsxExchange.Read("Localization.xlsx", XlsxExchange.Write(ExchangeFixture.Export(null), null));

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.Languages.Count, Is.EqualTo(3));
            Assert.That(file.Rows.Count, Is.EqualTo(4));
            ImportedRow purchase = file.Rows[2];
            Assert.That(purchase.ToString(), Is.EqualTo("Shop.Purchase"));
            Assert.That(purchase.GetText(0), Is.EqualTo("Buy"));
            Assert.That(purchase.GetText(1), Is.EqualTo("Kupit"));
            Assert.That(purchase.GetText(2), Is.Null);
            Assert.That(purchase.Location, Is.EqualTo("Localization.xlsx, sheet 'Shop', row 3"));
        }

        [Test]
        public void Write_ThenRead_KeepsCharactersXmlCantHold()
        {
            const string Backslash = "\\";
            string written = "Line = Tab" + Backslash + "tthen _x0041_ & <b>" + Backslash + "u0001end\n";
            ExportBook book = ExportBook.Collect(ExchangeFixture.Catalog, ExchangeFixture.Tables(("Odd", "English", "Line = Line\n"), ("Odd", "Russian", written)), null);
            byte[] workbook = XlsxExchange.Write(book, null);

            ImportedFile file = XlsxExchange.Read("Odd.xlsx", workbook);

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("Tab\tthen _x0041_ & <b>" + (char)1 + "end"));
            Assert.That(Encoding.UTF8.GetString(Unzip(workbook)["xl/worksheets/sheet1.xml"]), Does.Contain("then _x005F_x0041_ &amp; &lt;b&gt;_x0001_end"));
        }

        [Test]
        public void Read_TakesWhatSpreadsheetApplicationsSave()
        {
            Dictionary<string, byte[]> parts = new()
            {
                ["_rels/.rels"] = Xml("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                                      "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"),
                ["xl/workbook.xml"] = Xml("<workbook xmlns=\"" + Main + "\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                                          "<sheets><sheet name=\"Shop\" sheetId=\"1\" r:id=\"rId3\"/></sheets></workbook>"),
                ["xl/_rels/workbook.xml.rels"] = Xml("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                                                     "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"/xl/worksheets/sheet1.xml\"/>" +
                                                     "<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/></Relationships>"),
                ["xl/sharedStrings.xml"] = Xml("<sst xmlns=\"" + Main + "\"><si><t>Key</t></si><si><t>English</t></si><si><t>Russian</t></si>" +
                                               "<si><r><t>Kup</t></r><r><rPr><b/></rPr><t>it</t></r><rPh sb=\"0\" eb=\"1\"><t>KA</t></rPh><rPh sb=\"1\" eb=\"2\"><t>PU</t></rPh></si>" +
                                               "<si><t>Title</t></si><si><t>Line_x000D_\nTwo</t></si></sst>"),
                ["xl/worksheets/sheet1.xml"] = Xml("<worksheet xmlns=\"" + Main + "\"><sheetData>" +
                                                   "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"s\"><v>1</v></c><c r=\"C1\" t=\"s\"><v>2</v></c></row>" +
                                                   "<row r=\"2\"><c t=\"str\"><f>\"Shop.Purchase\"</f><v>Shop.Purchase</v></c><c><v>16</v></c><c t=\"s\"><v>3</v></c></row>" +
                                                   "<row r=\"3\"><c r=\"A3\" t=\"s\"><v>4</v></c><c r=\"C3\" t=\"s\"><v>5</v></c></row>" +
                                                   "<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>Shop.Lost</t></is></c><c r=\"C4\" t=\"e\"><v>#NAME?</v></c></row>" +
                                                   "</sheetData></worksheet>")
            };

            ImportedFile file = XlsxExchange.Read("Translated.xlsx", Zip(parts));

            Assert.That(file.Languages.Count, Is.EqualTo(2));
            Assert.That(file.Rows.Count, Is.EqualTo(3));
            Assert.That(file.Rows[0].ToString(), Is.EqualTo("Shop.Purchase"));
            Assert.That(file.Rows[0].GetText(0), Is.EqualTo("16"));
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("Kupit"), "Rich text runs join, and phonetic hints stay out.");
            Assert.That(file.Rows[1].ToString(), Is.EqualTo("Shop.Title"), "A key without its table belongs to the sheet's.");
            Assert.That(file.Rows[1].GetText(1), Is.EqualTo("Line\nTwo"));
            Assert.That(file.Problems.Count, Is.EqualTo(1));
            Assert.That(file.Problems[0], Does.Contain("row 4").And.Contain("#NAME?"));
        }

        [Test]
        public void Write_LeavesOutAndReportsTextsTooLongForExcel()
        {
            string longText = new('a', XlsxExchange.MaximumCellLength + 1);
            ExportBook book = ExportBook.Collect(ExchangeFixture.Catalog, ExchangeFixture.Tables(("Story", "English", $"Chapter = {longText}\n")), null);
            List<string> problems = new();

            XlsxExchange.Write(book, problems);

            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.StartWith("Story.Chapter:"));
        }

        [Test]
        public void Write_KeepsSheetNamesWithinExcelsLimit()
        {
            string longName = "Dialogue" + new string('x', 30);
            ExportBook book = ExportBook.Collect(ExchangeFixture.Catalog, ExchangeFixture.Tables(
                (longName + "One", "English", "Line = One\n"), (longName + "Two", "English", "Line = Two\n")), null);

            string workbook = Encoding.UTF8.GetString(Unzip(XlsxExchange.Write(book, null))["xl/workbook.xml"]);
            ImportedFile file = XlsxExchange.Read("Long.xlsx", XlsxExchange.Write(book, null));

            Assert.That(workbook, Does.Contain($"name=\"{longName.Substring(0, 31)}\""));
            Assert.That(workbook, Does.Contain($"name=\"{longName.Substring(0, 30)}2\""));
            Assert.That(file.Rows[1].TableName, Is.EqualTo(longName + "Two"), "Keys name their table, whatever the sheet is called.");
        }

        [Test]
        public void Read_ReportsWhatIsNotAWorkbook()
        {
            ImportedFile file = XlsxExchange.Read("Shop.xlsx", Encoding.UTF8.GetBytes("Key,English\n"));

            Assert.That(file.Problems.Count, Is.EqualTo(1));
            Assert.That(file.Problems[0], Does.Contain("can't be read as a workbook"));
        }

        private static string Text(string reference, int style, string text) =>
            $"<c r=\"{reference}\" s=\"{style}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{text}</t></is></c>";

        private static byte[] Xml(string text) => Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + text);

        private static Dictionary<string, byte[]> Unzip(byte[] archive)
        {
            Assert.That(ZipContainer.TryRead(archive, out Dictionary<string, byte[]> parts, out string problem), Is.True, problem);
            return parts;
        }

        private static byte[] Zip(Dictionary<string, byte[]> parts)
        {
            List<KeyValuePair<string, byte[]>> entries = new(parts);
            return ZipContainer.Write(entries);
        }
    }
}
