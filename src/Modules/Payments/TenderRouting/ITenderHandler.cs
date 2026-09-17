namespace ALKAROS.Payments.TenderRouting;

/// <summary>
/// A handler for one canonical <see cref="TenderRouting.TenderMethod"/>.
/// Implemented by the tender-specific tasks (Cash: V13-CSH-003, BankCard:
/// V13-HUG-001, MealCard: V13-MCD-004) — this task defines only the shape.
/// </summary>
public interface ITenderHandler
{
    TenderMethod Method { get; }

    Task<TenderHandlerResult> HandleAsync(TenderRequest request, CancellationToken cancellationToken = default);
}
