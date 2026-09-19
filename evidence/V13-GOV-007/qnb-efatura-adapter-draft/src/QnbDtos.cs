namespace ALKAROS.Invoicing.Qnb.Draft;

/// <summary>
/// V13-GOV-007 DRAFT — every field name below was copied from real saved
/// SOAP XML / C# examples in QNB eSolutions' own published API
/// documentation (`evidence/v0/integrations/V0-QNB-001/
/// qnb-esolutions-code-library-raw.txt`), not invented. None of it has
/// been exercised against a real QNB test tenant — see README.md
/// "Verified vs NOT verified".
/// </summary>
public sealed record BelgeGonderRequest(
    string VergiTcKimlikNo,
    string BelgeTuru,
    string BelgeNo,
    byte[] Veri,
    string MimeType = "application/xml",
    string BelgeVersiyon = "1.0",
    string? ErpKodu = null)
{
    public const string BelgeTuruFaturaUbl = "FATURA_UBL";
}

public sealed record QnbDocumentStatusQuery(
    string VergiTcKimlikNo,
    string BelgeNo,
    string BelgeNoTipi,
    string BelgeTuru,
    string DonusTipiVersiyon = "6.0")
{
    public const string BelgeNoTipiOid = "OID";
}

/// <summary>
/// `gidenBelgeDurumSorgulaExt` response shape — field names match the
/// real saved XML example exactly (`<durum>`, not `<durumKodu>`; the C#
/// sample assigns it to a local variable named `durumKodu`, which this
/// record's own property name follows instead, to be self-describing).
/// </summary>
public sealed record QnbDocumentStatus(
    string? AlimTarihi,
    string BelgeNo,
    int DurumKodu,
    string? Ettn,
    string? GonderimCevabiDetayi,
    int? GonderimCevabiKodu,
    int GonderimDurumu,
    string? OlusturulmaTarihi,
    string? YanitDetayi,
    int? YanitDurumu,
    bool UlastiMi,
    bool YenidenGonderilebilirMi,
    string? YerelBelgeOid)
{
    public const int DurumAlindi = 1;
    public const int DurumIslemeHatasi = 2;
    public const int DurumBasariylaIslendi = 3;
}
