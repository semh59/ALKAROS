using FluentAssertions;
using Xunit;
using static ALKAROS.Purchasing.PurchaseInvoices.Tests.InvoiceXml;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

public sealed class UblPurchaseInvoiceParserTests
{
    [Fact]
    public void ReadsHeaderSupplierAndLines()
    {
        var ettn = Guid.NewGuid();
        var parsed = UblPurchaseInvoiceParser.Parse(Build(ettn, lines:
        [
            new Line("1", "KIYMA-01", "Dana Kıyma", "10", "kgm", "1500.00"),
            new Line("2", null, "Süt  1 lt", "24", "C62", "360.00"),
        ]));

        parsed.Ettn.Should().Be(ettn);
        parsed.InvoiceNumber.Should().Be("ABC2026000000001");
        parsed.IssueDate.Should().Be(new DateOnly(2026, 9, 20));
        parsed.SupplierTaxNumber.Should().Be("1234567890");
        parsed.SupplierName.Should().Be("Anadolu Gıda A.Ş.");
        parsed.Currency.Should().Be("TRY");
        parsed.Lines.Should().HaveCount(2);
        parsed.Lines[0].Should().Match<ParsedInvoiceLine>(l =>
            l.Quantity == 10m && l.UnitCode == "KGM" && l.UnitPrice == 150m && l.LineNet == 1500m && l.ItemKey == "CODE:KIYMA-01");
        parsed.Lines[1].ItemKey.Should().Be("NAME:SÜT 1 LT");
        parsed.Lines[1].UnitPrice.Should().Be(15m);
    }

    [Fact]
    public void UnitPriceIsTheNetLineAmountPerUnitSoALineDiscountLowersTheCost()
    {
        var parsed = UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), lines: [new Line("1", "X", "Un", "3", "KGM", "10.00")]));

        parsed.Lines[0].UnitPrice.Should().Be(3.333333m);
    }

    [Fact]
    public void ADocumentThatIsNotAnInvoiceOrReturnIsRefused()
    {
        var order = () => UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), root: "Order"));

        order.Should().Throw<UnsupportedPurchaseDocumentException>();
    }

    [Theory]
    [InlineData("Invoice", "IADE")]
    [InlineData("CreditNote", "SATIS")]
    public void AReturnInvoiceOrCreditNoteIsReadAsAReturnWithItsReferencedInvoice(string root, string typeCode)
    {
        var parsed = UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), root: root, typeCode: typeCode, referenced: "ABC2026000000099"));

        parsed.Kind.Should().Be(PurchaseInvoiceKinds.Return);
        parsed.ReferencedInvoiceNumber.Should().Be("ABC2026000000099");
        parsed.Lines.Should().ContainSingle().Which.Quantity.Should().Be(10m);
    }

    [Fact]
    public void APlainInvoiceIsNotAReturn()
    {
        var parsed = UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid()));

        parsed.Kind.Should().Be(PurchaseInvoiceKinds.Invoice);
        parsed.ReferencedInvoiceNumber.Should().BeNull();
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<Invoice><broken></Invoice>")]
    public void MalformedXmlIsRefused(string xml)
    {
        var act = () => UblPurchaseInvoiceParser.Parse(xml);

        act.Should().Throw<InvalidPurchaseInvoiceException>();
    }

    [Fact]
    public void AnExternalEntityIsNeverResolved()
    {
        var xml = Build(Guid.NewGuid(), doctype: "<!DOCTYPE Invoice [<!ENTITY x SYSTEM \"file:///C:/Windows/win.ini\">]>");

        var act = () => UblPurchaseInvoiceParser.Parse(xml);

        act.Should().Throw<InvalidPurchaseInvoiceException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public void ALineWithoutAPositiveQuantityIsRefused(string quantity)
    {
        var act = () => UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), lines: [new Line("1", "A", "Ürün", quantity, "KGM", "10")]));

        act.Should().Throw<InvalidPurchaseInvoiceException>();
    }

    [Fact]
    public void AnInvoiceWithoutLinesOrASupplierTaxNumberIsRefused()
    {
        var noLines = () => UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), lines: []));
        var noTax = () => UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), schemeId: "OTHER"));

        noLines.Should().Throw<InvalidPurchaseInvoiceException>();
        noTax.Should().Throw<InvalidPurchaseInvoiceException>();
    }

    [Fact]
    public void ADuplicateLineNumberIsRefused()
    {
        var act = () => UblPurchaseInvoiceParser.Parse(Build(Guid.NewGuid(), lines:
        [
            new Line("1", "A", "Bir", "1", "C62", "1"),
            new Line("1", "B", "İki", "1", "C62", "1"),
        ]));

        act.Should().Throw<InvalidPurchaseInvoiceException>();
    }
}
