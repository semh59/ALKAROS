using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ALKAROS.Purchasing.PurchaseInvoices;

/// <summary>
/// Reads a UBL-TR sales invoice or return (IADE invoice, credit note) as received by the buyer. Navigation is by local name so the
/// prefix and namespace declarations an issuer happens to use do not matter. Any other document is refused.
/// </summary>
public static class UblPurchaseInvoiceParser
{
    private const int MaxCharacters = 5_000_000;

    // Quantities and amounts are NUMERIC(18,4) and the unit price NUMERIC(18,6): anything at or above this cannot be stored.
    private const decimal MaxAmount = 100_000_000_000_000m;

    public static ParsedPurchaseInvoice Parse(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        var root = Load(xml);

        var isCreditNote = root.Name.LocalName == "CreditNote";
        if (root.Name.LocalName != "Invoice" && !isCreditNote)
            throw new UnsupportedPurchaseDocumentException($"Only invoices can be imported; got '{root.Name.LocalName}'.");
        var kind = isCreditNote || string.Equals(Text(root, "InvoiceTypeCode"), "IADE", StringComparison.OrdinalIgnoreCase)
            ? PurchaseInvoiceKinds.Return : PurchaseInvoiceKinds.Invoice;

        var number = Required(root, "ID", "invoice number");
        if (number.Length > 64)
            throw new InvalidPurchaseInvoiceException("The invoice number is longer than 64 characters.");
        var ettnText = Required(root, "UUID", "ETTN");
        if (!Guid.TryParse(ettnText, out var ettn))
            throw new InvalidPurchaseInvoiceException($"The ETTN '{ettnText}' is not a valid UUID.");
        if (!DateOnly.TryParseExact(Required(root, "IssueDate", "issue date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issueDate))
            throw new InvalidPurchaseInvoiceException("The issue date is not a valid yyyy-MM-dd date.");
        var currency = Text(root, "DocumentCurrencyCode") ?? "TRY";
        if (!string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidPurchaseInvoiceException($"Only TRY invoices can be imported; got '{Truncate(currency, 8)}'.");

        var party = Child(Child(root, "AccountingSupplierParty"), "Party")
            ?? throw new InvalidPurchaseInvoiceException("The supplier party is missing.");
        var taxNumber = party.Elements().Where(e => e.Name.LocalName == "PartyIdentification")
            .Select(e => Child(e, "ID"))
            .FirstOrDefault(id => id is not null && IsTaxScheme((string?)id.Attribute("schemeID")) && !string.IsNullOrWhiteSpace(id.Value))?.Value.Trim()
            ?? throw new InvalidPurchaseInvoiceException("The supplier tax number (VKN/TCKN) is missing.");
        if (taxNumber.Length > 32)
            throw new InvalidPurchaseInvoiceException("The supplier tax number is longer than 32 characters.");
        var supplierName = Text(Child(party, "PartyName"), "Name") ?? PersonName(Child(party, "Person"))
            ?? throw new InvalidPurchaseInvoiceException("The supplier name is missing.");

        var lines = root.Elements().Where(e => e.Name.LocalName == (isCreditNote ? "CreditNoteLine" : "InvoiceLine")).Select(ParseLine).ToArray();
        if (lines.Length == 0)
            throw new InvalidPurchaseInvoiceException("The invoice has no lines.");
        if (lines.Select(l => l.LineNumber).Distinct().Count() != lines.Length)
            throw new InvalidPurchaseInvoiceException("The invoice has duplicate line numbers.");

        var referenced = Text(Child(Child(root, "BillingReference"), "InvoiceDocumentReference"), "ID");
        return new ParsedPurchaseInvoice(
            ettn, number, issueDate, taxNumber, Truncate(supplierName, 250), "TRY", lines, kind, referenced is null ? null : Truncate(referenced, 64));
    }

    private static ParsedInvoiceLine ParseLine(XElement line)
    {
        var lineNumber = int.TryParse(Text(line, "ID"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n : throw new InvalidPurchaseInvoiceException("An invoice line has no valid line number.");
        var quantityElement = Child(line, "InvoicedQuantity") ?? Child(line, "CreditedQuantity")
            ?? throw new InvalidPurchaseInvoiceException($"Line {lineNumber} has no quantity.");
        var quantity = Decimal(quantityElement.Value, $"line {lineNumber} quantity");
        if (quantity <= 0)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} must have a positive quantity.");
        var unit = ((string?)quantityElement.Attribute("unitCode"))?.Trim();
        if (string.IsNullOrEmpty(unit))
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} has no unit code.");
        if (unit.Length > 16)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} has a unit code longer than 16 characters.");
        if (quantity >= MaxAmount)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} quantity is too large.");

        var item = Child(line, "Item");
        var description = Text(item, "Name") ?? Text(item, "Description")
            ?? throw new InvalidPurchaseInvoiceException($"Line {lineNumber} has no item name.");
        var code = Text(Child(item, "SellersItemIdentification"), "ID");
        code = code is null ? null : Truncate(code, 100);

        var lineNet = Decimal(Required(line, "LineExtensionAmount", $"line {lineNumber} amount"), $"line {lineNumber} amount");
        if (lineNet < 0)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} must not have a negative amount.");
        if (lineNet >= MaxAmount)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} amount is too large.");

        // The net amount already carries any line discount, so it is the real cost per invoiced unit.
        var unitPrice = Math.Round(lineNet / quantity, 6, MidpointRounding.AwayFromZero);
        if (unitPrice >= 1_000_000_000_000m)
            throw new InvalidPurchaseInvoiceException($"Line {lineNumber} unit price is too large.");
        return new ParsedInvoiceLine(lineNumber, code, Truncate(description, 500), quantity, unit.ToUpperInvariant(), unitPrice, lineNet);
    }

    private static XElement Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxCharacters };
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader).Root ?? throw new InvalidPurchaseInvoiceException("The XML has no root element.");
        }
        catch (XmlException ex)
        {
            throw new InvalidPurchaseInvoiceException("The file is not valid XML.", ex);
        }
    }

    private static bool IsTaxScheme(string? scheme)
        => string.Equals(scheme, "VKN", StringComparison.OrdinalIgnoreCase) || string.Equals(scheme, "TCKN", StringComparison.OrdinalIgnoreCase);

    private static string? PersonName(XElement? person)
    {
        var name = string.Join(' ', new[] { Text(person, "FirstName"), Text(person, "FamilyName") }.Where(s => s is not null));
        return name.Length == 0 ? null : name;
    }

    private static decimal Decimal(string text, string what)
        => decimal.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value : throw new InvalidPurchaseInvoiceException($"The {what} is not a number.");

    private static string Required(XElement element, string name, string what)
        => Text(element, name) ?? throw new InvalidPurchaseInvoiceException($"The {what} is missing.");

    private static XElement? Child(XElement? parent, string name)
        => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name);

    private static string? Text(XElement? parent, string name)
    {
        var value = Child(parent, name)?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
