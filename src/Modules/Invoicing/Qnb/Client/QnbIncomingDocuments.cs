namespace ALKAROS.Invoicing.Qnb.Client;

/// <summary>One line of QNB's incoming-document list (<c>gelenBelgeleriListeleExt</c>); <see cref="SequenceNo"/> is <c>belgeSiraNo</c>, the resume point.</summary>
public sealed record QnbIncomingDocument(string Ettn, string DocumentNo, long SequenceNo, string SenderTaxNumber, string? SellerTitle);
