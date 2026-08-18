namespace ALKAROS.Clients.Cashier.TableShell;

/// <summary>
/// Authenticated cashier terminal session state (V1-CUI-001, PDF:I.7, V1-IAM-003).
/// </summary>
public sealed record CashierSession(
    Guid SessionId,
    Guid UserId,
    string UserName,
    string TerminalId,
    DateTimeOffset ExpiresAt,
    bool IsActive)
{
    public bool IsExpired(DateTimeOffset utcNow) => !IsActive || utcNow >= ExpiresAt;
}
