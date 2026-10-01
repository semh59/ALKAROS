using System.Globalization;
using System.Text;

namespace ALKAROS.Purchasing.PurchaseInvoices.Tests;

/// <summary>A minimal UBL-TR sales invoice as a buyer receives it; every field the parser reads can be varied.</summary>
internal static class InvoiceXml
{
    public sealed record Line(string Id, string? Code, string Name, string Quantity, string Unit, string Net);

    public static string Build(
        Guid ettn, string supplierTax = "1234567890", string supplierName = "Anadolu Gıda A.Ş.", string number = "ABC2026000000001",
        string issueDate = "2026-09-20", IEnumerable<Line>? lines = null, string root = "Invoice", string typeCode = "SATIS",
        string? doctype = null, string schemeId = "VKN")
    {
        lines ??= [new Line("1", "KIYMA-01", "Dana Kıyma", "10", "KGM", "1500.00")];
        var sb = new StringBuilder();
        sb.Append(doctype ?? string.Empty);
        sb.Append(CultureInfo.InvariantCulture, $"""
            <{root} xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                     xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                     xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
              <cbc:ID>{number}</cbc:ID>
              <cbc:UUID>{ettn}</cbc:UUID>
              <cbc:IssueDate>{issueDate}</cbc:IssueDate>
              <cbc:InvoiceTypeCode>{typeCode}</cbc:InvoiceTypeCode>
              <cbc:DocumentCurrencyCode>TRY</cbc:DocumentCurrencyCode>
              <cac:AccountingSupplierParty>
                <cac:Party>
                  <cac:PartyIdentification><cbc:ID schemeID="{schemeId}">{supplierTax}</cbc:ID></cac:PartyIdentification>
                  <cac:PartyName><cbc:Name>{supplierName}</cbc:Name></cac:PartyName>
                </cac:Party>
              </cac:AccountingSupplierParty>
            """);
        foreach (var line in lines)
        {
            sb.Append(CultureInfo.InvariantCulture, $"""
                  <cac:InvoiceLine>
                    <cbc:ID>{line.Id}</cbc:ID>
                    <cbc:InvoicedQuantity unitCode="{line.Unit}">{line.Quantity}</cbc:InvoicedQuantity>
                    <cbc:LineExtensionAmount currencyID="TRY">{line.Net}</cbc:LineExtensionAmount>
                    <cac:Item>
                      <cbc:Name>{line.Name}</cbc:Name>
                      {(line.Code is null ? "" : $"<cac:SellersItemIdentification><cbc:ID>{line.Code}</cbc:ID></cac:SellersItemIdentification>")}
                    </cac:Item>
                  </cac:InvoiceLine>
            """);
        }

        sb.Append(CultureInfo.InvariantCulture, $"</{root}>");
        return sb.ToString();
    }

}
