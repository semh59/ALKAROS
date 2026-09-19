using Xunit;

namespace ALKAROS.Invoicing.Qnb.Draft.Tests;

/// <summary>
/// Every branch here matches a real if/else check found inside QNB's own
/// saved `GidenBelgeDurumSorgulaExt` C# sample (not invented) — see
/// `evidence/v0/integrations/V0-QNB-001/2026-09-18-public-docs-research.md`'s
/// "DÜZELTME" section for the exact source.
/// </summary>
public sealed class QnbDocumentStatusInterpreterTests
{
    private static QnbDocumentStatus Status(int durumKodu, int gonderimDurumu = 0, int? yanitDurumu = null) =>
        new(null, "belge-1", durumKodu, null, null, null, gonderimDurumu, null, null, yanitDurumu, false, false, null);

    [Fact]
    public void DurumKodu1IsPending()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 1));
        Assert.IsType<QnbStatusInterpretation.Pending>(result);
    }

    [Fact]
    public void DurumKodu2IsRetryableFailure()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 2));
        Assert.IsType<QnbStatusInterpretation.RetryableFailure>(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DurumKodu3WithInFlightGonderimDurumuIsPending(int gonderimDurumu)
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: gonderimDurumu));
        Assert.IsType<QnbStatusInterpretation.Pending>(result);
    }

    [Fact]
    public void GonderimDurumuMinus2IsTerminalFailure()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: -2));
        Assert.IsType<QnbStatusInterpretation.TerminalFailure>(result);
    }

    [Fact]
    public void GonderimDurumu3NeverGuessesASingleOutcomeBecauseItSharesSixRealScenarios()
    {
        // The public sample assigns 6+ different `durumAciklamaStr` texts
        // under the exact same `gonderimDurumu == 3` numeric branch with no
        // further code-level distinction — collapsing this to a single
        // verdict would be a guess, not a documented fact.
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 3));
        Assert.IsType<QnbStatusInterpretation.RequiresHumanReview>(result);
    }

    [Fact]
    public void GonderimDurumu4WithYanitDurumuMinus1IsTerminalSuccess()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 4, yanitDurumu: -1));
        Assert.IsType<QnbStatusInterpretation.TerminalSuccess>(result);
    }

    [Fact]
    public void GonderimDurumu4WithYanitDurumu0IsPendingAwaitingCounterpartyReply()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 4, yanitDurumu: 0));
        Assert.IsType<QnbStatusInterpretation.Pending>(result);
    }

    [Fact]
    public void GonderimDurumu4WithYanitDurumu1IsTerminalFailureRejected()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 4, yanitDurumu: 1));
        Assert.IsType<QnbStatusInterpretation.TerminalFailure>(result);
    }

    [Fact]
    public void GonderimDurumu4WithYanitDurumu2IsTerminalSuccessAccepted()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 4, yanitDurumu: 2));
        Assert.IsType<QnbStatusInterpretation.TerminalSuccess>(result);
    }

    [Fact]
    public void GonderimDurumu4WithUnknownYanitDurumuRequiresHumanReviewRatherThanGuessing()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 4, yanitDurumu: 99));
        Assert.IsType<QnbStatusInterpretation.RequiresHumanReview>(result);
    }

    [Fact]
    public void UnknownDurumKoduRequiresHumanReviewRatherThanGuessing()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 99));
        Assert.IsType<QnbStatusInterpretation.RequiresHumanReview>(result);
    }

    [Fact]
    public void UnknownGonderimDurumuRequiresHumanReviewRatherThanGuessing()
    {
        var result = QnbDocumentStatusInterpreter.Interpret(Status(durumKodu: 3, gonderimDurumu: 42));
        Assert.IsType<QnbStatusInterpretation.RequiresHumanReview>(result);
    }
}
