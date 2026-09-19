namespace ALKAROS.Invoicing.Qnb.Draft;

/// <summary>
/// V13-GOV-007 DRAFT. Models the FULL `durumKodu` → `gonderimDurumu` →
/// `yanitDurumu` decision tree found embedded (as working if/else logic,
/// not prose) inside QNB's own saved `GidenBelgeDurumSorgulaExt` C#
/// sample — see `evidence/v0/integrations/V0-QNB-001/
/// 2026-09-18-public-docs-research.md`'s "DÜZELTME" section for the exact
/// source lines. A pure, side-effect-free classifier — this is the QNB
/// counterpart of `ALKAROS.Payments.Token.Draft.TokenSaleResult`'s status
/// constants, meant as a direct reference for whichever task actually
/// implements `V14-QNB-004` (invoice reconciliation).
/// </summary>
public static class QnbDocumentStatusInterpreter
{
    public static QnbStatusInterpretation Interpret(QnbDocumentStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.DurumKodu switch
        {
            QnbDocumentStatus.DurumAlindi => new QnbStatusInterpretation.Pending(
                "Alındı durumudur. Durum Kodu 2 veya 3 olana kadar beklenmelidir."),
            QnbDocumentStatus.DurumIslemeHatasi => new QnbStatusInterpretation.RetryableFailure(
                "Fatura İşleme hatasıdır. Hata nedenine göre düzeltip yeniden gönderiniz."),
            QnbDocumentStatus.DurumBasariylaIslendi => InterpretGonderimDurumu(status),
            _ => new QnbStatusInterpretation.RequiresHumanReview(
                $"Bilinmeyen durumKodu={status.DurumKodu} — kamuya açık dokümantasyonda tanımlı değil."),
        };
    }

    private static QnbStatusInterpretation InterpretGonderimDurumu(QnbDocumentStatus status) =>
        status.GonderimDurumu switch
        {
            -2 => new QnbStatusInterpretation.TerminalFailure(
                "Fatura GİB'e gönderilemedi. İptal edildi, gönderilmeyecek."),
            -1 => new QnbStatusInterpretation.Pending(
                "Fatura GİB'e gönderim kuyruğuna eklendi."),
            0 => new QnbStatusInterpretation.Pending(
                "Fatura GİB'e gönderilemedi, sistem gönderim işlemini yeniden deneyecek."),
            1 => new QnbStatusInterpretation.Pending(
                "Fatura GİB'e gönderilecek."),
            2 => new QnbStatusInterpretation.Pending(
                "Fatura GİB'e gönderilmiştir, alıcıya iletim bekleniyor."),
            // gonderimDurumu==3 covers SEVERAL distinct real-world scenarios in
            // QNB's own sample (send error / GİB-alıcı arasında / GİB 4 kez daha
            // deneyecek / 5 deneme de başarısız oldu, yeni zarfla tekrar
            // gönderilebilir / alıcı sistem yanıtı bekleniyor / alıcı başarısız
            // yanıt verdi) that are NOT distinguished by any further numeric
            // code in the public sample — only by the free-text
            // `gonderimCevabiDetayi`. Collapsing these into one verdict here
            // would be a guess dressed up as certainty, so this stays
            // RequiresHumanReview until a real tenant's actual detail strings
            // are seen and can be pattern-matched safely.
            3 => new QnbStatusInterpretation.RequiresHumanReview(
                "gonderimDurumu=3 en az 6 farklı senaryoyu paylaşıyor (bkz. " +
                "gonderimCevabiDetayi); tek bir kod ile ayrıştırılamaz."),
            4 => InterpretYanitDurumu(status.YanitDurumu),
            _ => new QnbStatusInterpretation.RequiresHumanReview(
                $"Bilinmeyen gonderimDurumu={status.GonderimDurumu} — kamuya açık dokümantasyonda tanımlı değil."),
        };

    private static QnbStatusInterpretation InterpretYanitDurumu(int? yanitDurumu) => yanitDurumu switch
    {
        -1 => new QnbStatusInterpretation.TerminalSuccess(
            "Temel Faturadır. Karşıdan yanıt beklenmez."),
        0 => new QnbStatusInterpretation.Pending(
            "Ticari Faturadır. Karşıdan yanıt beklenmektedir."),
        1 => new QnbStatusInterpretation.TerminalFailure(
            "Ticari Faturadır. Red Uygulama Yanıtı Alınmıştır."),
        2 => new QnbStatusInterpretation.TerminalSuccess(
            "Ticari Faturadır. Kabul Uygulama Yanıtı Alınmıştır."),
        _ => new QnbStatusInterpretation.RequiresHumanReview(
            $"Bilinmeyen yanitDurumu={yanitDurumu} — kamuya açık dokümantasyonda tanımlı değil."),
    };
}

/// <summary>Mirrors `TokenTenderOutcome`'un discriminated-union şekli.</summary>
public abstract record QnbStatusInterpretation
{
    public sealed record Pending(string Reason) : QnbStatusInterpretation;

    public sealed record RetryableFailure(string Reason) : QnbStatusInterpretation;

    public sealed record TerminalSuccess(string Reason) : QnbStatusInterpretation;

    public sealed record TerminalFailure(string Reason) : QnbStatusInterpretation;

    /// <summary>
    /// The public documentation does not give enough signal to classify
    /// this outcome automatically — never silently treated as either
    /// success or failure (same CORR:C29 "never guess" invariant
    /// `TokenTenderOutcome.RequiresReconciliation` already follows).
    /// </summary>
    public sealed record RequiresHumanReview(string Reason) : QnbStatusInterpretation;
}
