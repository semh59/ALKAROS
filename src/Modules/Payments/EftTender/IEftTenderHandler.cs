using ALKAROS.Payments.TenderRouting;

namespace ALKAROS.Payments.EftTender;

/// <summary>
/// Marker interface distinguishing the real EFT/Havale
/// <see cref="ITenderHandler"/> implementation for DI resolution — the
/// registry composition (V13-PAY-003's <c>TenderCompositionModule</c>) needs
/// to resolve this one specifically, the same way Cash is resolved through
/// <c>ICashTenderHandler</c> rather than a bare <see cref="ITenderHandler"/>
/// (which multiple concrete handlers implement).
/// </summary>
public interface IEftTenderHandler : ITenderHandler;
