namespace ALKAROS.Inventory.PortionReservations.CancellationEffects;

public class CancellationDecisionException : Exception
{
    public CancellationDecisionException(string message) : base(message) { }
    public CancellationDecisionException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class InvalidCancellationCommandException : CancellationDecisionException
{
    public InvalidCancellationCommandException(string message) : base(message) { }
}
