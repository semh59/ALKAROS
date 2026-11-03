namespace ALKAROS.Cash.Contracts;

/// <summary>
/// Domain policy engine enforcing single-open session, variance threshold, and lifecycle invariants (V1-CSH-001).
/// </summary>
public interface ICashSessionPolicy
{
    /// <summary>
    /// Validates whether a new cash session can be opened on a terminal.
    /// Enforces CSH-INV-01: Terminal must not have any other active session.
    /// </summary>
    void ValidateCanOpenSession(
        OpenCashSessionCommand command,
        IEnumerable<CashSessionSnapshot> existingTerminalSessions);

    /// <summary>
    /// Validates whether counting phase can be initiated for a session. Only the session's cashier, or an actor who may
    /// act for the owner (a manager), starts it.
    /// </summary>
    void ValidateCanStartCount(CashSessionSnapshot session, Guid actorUserId, bool actorMayActForOwner);

    /// <summary>
    /// Validates whether a physical cash count entry can be recorded (same owner rule as <see cref="ValidateCanStartCount"/>).
    /// </summary>
    void ValidateCanRecordCount(CashSessionSnapshot session, decimal countedAmount, Guid actorUserId, bool actorMayActForOwner);

    /// <summary>
    /// Validates whether a cash session can be closed and calculates variance. The close needs a recorded count equal
    /// to the closing amount, an explanation for any non-zero difference, and a supervisor override when the
    /// difference exceeds the tolerance.
    /// </summary>
    decimal ValidateCanCloseSession(CashSessionSnapshot session, CashSessionCloseInput input);

    /// <summary>
    /// Validates whether a closed cash session can be reconciled. The session's cashier and everyone who handled it
    /// (<paramref name="handledBy"/>: the closer and whoever recorded a count) reconcile it only when <paramref name="selfReconcileAllowed"/>; returns true when that exception was used.
    /// </summary>
    bool ValidateCanReconcile(
        CashSessionSnapshot session, IReadOnlyCollection<Guid> handledBy, Guid reconcilerUserId, bool selfReconcileAllowed);
}

/// <summary>Everything a close is judged on besides the session itself.</summary>
/// <param name="LatestCountedAmount">The newest recorded physical count; null when none was recorded.</param>
/// <param name="VarianceTolerance">The largest difference (TL) a close accepts without a supervisor override.</param>
public sealed record CashSessionCloseInput(
    decimal ActualCash,
    decimal ExpectedCash,
    decimal? LatestCountedAmount,
    bool IsSupervisorOverride,
    decimal VarianceTolerance,
    string? DifferenceExplanation,
    Guid ActorUserId,
    bool ActorMayActForOwner);

/// <summary>
/// Domain policy engine implementation for CashSession (V1-CSH-001).
/// </summary>
public sealed class CashSessionPolicy : ICashSessionPolicy
{
    public const int MaxExplanationLength = 500;

    /// <summary>Free text written to the audit trail stays within the length every note of a close allows.</summary>
    public static void RequireShortText(string? text, string parameterName)
    {
        if (text is { Length: > MaxExplanationLength })
            throw new ArgumentException($"Text can be at most {MaxExplanationLength} characters.", parameterName);
    }

    /// <summary>The explanation as stored: trimmed, or null when blank.</summary>
    public static string? NormalizeExplanation(string? explanation)
        => string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();

    private static void RequireOwnerOrManager(CashSessionSnapshot session, Guid actorUserId, bool actorMayActForOwner)
    {
        if (session.CashierUserId != actorUserId && !actorMayActForOwner)
        {
            throw new CashSessionOwnerRequiredException(session.CashSessionId, actorUserId);
        }
    }

    public void ValidateCanOpenSession(
        OpenCashSessionCommand command,
        IEnumerable<CashSessionSnapshot> existingTerminalSessions)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(existingTerminalSessions);
        command.Validate();

        var activeExisting = existingTerminalSessions.FirstOrDefault(s => s.IsActive);
        if (activeExisting is not null)
        {
            throw new ActiveCashSessionExistsException(command.TerminalId, activeExisting.CashSessionId);
        }
    }

    public void ValidateCanStartCount(CashSessionSnapshot session, Guid actorUserId, bool actorMayActForOwner)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireOwnerOrManager(session, actorUserId, actorMayActForOwner);

        if (session.Status != CashSessionStatus.Open)
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "StartCount");
        }
    }

    public void ValidateCanRecordCount(CashSessionSnapshot session, decimal countedAmount, Guid actorUserId, bool actorMayActForOwner)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireOwnerOrManager(session, actorUserId, actorMayActForOwner);

        // V1-RMD-417 (V1-RMD-393 F-15): cash-session-design.md section 5 - a count is recorded only once counting
        // has started (sales locked); Open used to be accepted too.
        if (session.Status != CashSessionStatus.Counting)
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "RecordCashCount");
        }

        if (countedAmount < 0)
        {
            throw new NegativeCashAmountException(nameof(countedAmount), countedAmount);
        }
    }

    public decimal ValidateCanCloseSession(CashSessionSnapshot session, CashSessionCloseInput input)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(input.VarianceTolerance);
        RequireOwnerOrManager(session, input.ActorUserId, input.ActorMayActForOwner);

        // V1-RMD-417 (V1-RMD-393 F-15): cash-session-design.md section 5 - a session closes from Counting or Closing
        // only, so it cannot be closed without a count having been started.
        if (session.Status is not (CashSessionStatus.Counting or CashSessionStatus.Closing))
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "CloseSession");
        }

        if (input.ActualCash < 0)
        {
            throw new NegativeCashAmountException(nameof(input.ActualCash), input.ActualCash);
        }

        // The closing amount is the counted one, never a number typed into the close request alone.
        if (input.LatestCountedAmount is not { } recorded)
        {
            throw new CashCountRequiredException(session.CashSessionId);
        }

        if (recorded != input.ActualCash)
        {
            throw new CashCountMismatchException(session.CashSessionId, recorded, input.ActualCash);
        }

        var difference = input.ActualCash - input.ExpectedCash;
        var explanation = NormalizeExplanation(input.DifferenceExplanation);
        if ((difference != 0 && explanation is null) || explanation is { Length: > MaxExplanationLength })
        {
            throw new CashDifferenceExplanationRequiredException(session.CashSessionId, difference);
        }

        if (Math.Abs(difference) > input.VarianceTolerance && !input.IsSupervisorOverride)
        {
            throw new CashVarianceThresholdExceededException(session.CashSessionId, difference, input.VarianceTolerance);
        }

        return difference;
    }

    public bool ValidateCanReconcile(
        CashSessionSnapshot session, IReadOnlyCollection<Guid> handledBy, Guid reconcilerUserId, bool selfReconcileAllowed)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Status != CashSessionStatus.Closed)
        {
            throw new InvalidCashSessionStateException(session.CashSessionId, session.Status, "Reconcile");
        }

        var selfReconcile = reconcilerUserId == session.CashierUserId || handledBy.Contains(reconcilerUserId);
        if (selfReconcile && !selfReconcileAllowed)
        {
            throw new CashReconcileSelfNotAllowedException(session.CashSessionId, reconcilerUserId);
        }

        return selfReconcile;
    }
}
