using Npgsql;

namespace ALKAROS.CustomerData.AnonymizationState;

/// <summary>
/// Persistence for anonymization requests (`customer_data.anonymization_requests`,
/// migration 160). This store only persists and optimistic-concurrency-checks
/// state - it does not itself enforce which transitions are legal
/// (`CustomerAnonymizationTransitions`/`CustomerAnonymizationService` own
/// that), matching V0-DOM-001's separation of the transition contract from
/// storage.
/// </summary>
public interface ICustomerAnonymizationRequestStore
{
    /// <summary>Creates a new request already resolved into <paramref name="initialStatus"/> (RetentionBlocked or Pending - never Requested, see that status's own doc comment).</summary>
    Task<Guid> CreateAsync(
        Guid customerId,
        AnonymizationRequestStatus initialStatus,
        string? requestedBy,
        string? blockedReason,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    /// <summary>As above; <paramref name="record"/> runs in the same transaction with the new request id, so a failure in it undoes the creation.</summary>
    Task<Guid> CreateAsync(
        Guid customerId,
        AnonymizationRequestStatus initialStatus,
        string? requestedBy,
        string? blockedReason,
        DateTimeOffset requestedAt,
        Func<Guid, NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task> record,
        CancellationToken cancellationToken = default);

    Task<CustomerAnonymizationRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces status/blockedReason/anonymizedAt entirely and bumps
    /// row_version. Throws <see cref="CustomerAnonymizationConcurrencyException"/>
    /// if <paramref name="expectedRowVersion"/> is stale, or
    /// <see cref="CustomerAnonymizationRequestNotFoundException"/> if the
    /// request does not exist.
    /// </summary>
    Task UpdateStatusAsync(
        Guid requestId,
        AnonymizationRequestStatus newStatus,
        string? blockedReason,
        DateTimeOffset? anonymizedAt,
        int expectedRowVersion,
        CancellationToken cancellationToken = default);

    /// <summary>As above; <paramref name="record"/> runs in the same transaction, so a failure in it undoes the status change.</summary>
    Task UpdateStatusAsync(
        Guid requestId,
        AnonymizationRequestStatus newStatus,
        string? blockedReason,
        DateTimeOffset? anonymizedAt,
        int expectedRowVersion,
        Func<NpgsqlConnection, NpgsqlTransaction, CancellationToken, Task> record,
        CancellationToken cancellationToken = default);
}
