namespace ALKAROS.OnlineOrdering.CatalogPublishing;

/// <summary>What a channel's catalog interface can carry. A capability a channel lacks is reported on every publication, never silently dropped.</summary>
public enum CatalogCapability
{
    UpdatePrice,
    UpdateAvailability,
    CreateProduct,
    UpdateTitle,
    TaxMetadata,
    Modifiers,
    Categories
}

public enum CatalogValidationCode
{
    /// <summary>The product has no current catalog price to publish.</summary>
    PriceMissing,

    /// <summary>The product has modifier groups and the channel cannot carry modifiers.</summary>
    ModifiersNotSupported,

    /// <summary>The channel refused to give the product an external identifier.</summary>
    ExternalIdUnavailable
}

public sealed record CatalogValidationError(Guid ProductId, CatalogValidationCode Code, string? Detail = null);

/// <summary>One product exactly as it is published: the channel's external identifier plus what the channel is told.</summary>
public sealed record CatalogPublicationItem(Guid ProductId, string ExternalSku, string Title, decimal Price, bool Active);

public enum CatalogPublicationStatus
{
    /// <summary>Committed and waiting for (or retrying) delivery to the channel.</summary>
    Pending,

    Delivered,

    /// <summary>Identical to the last delivered publication of the same menu; the channel was not called again.</summary>
    Unchanged,

    /// <summary>Nothing on the menu could be published; the channel was not called.</summary>
    NothingToPublish,

    /// <summary>V12-RMD-005: a newer publication of the same menu was requested before this one was sent; never sent.</summary>
    Superseded
}

public sealed record CatalogPublicationSummary(
    Guid PublicationId,
    string Channel,
    CatalogPublicationStatus Status,
    int ItemCount,
    IReadOnlyList<CatalogValidationError> ValidationErrors,
    IReadOnlyList<CatalogCapability> UnsupportedCapabilities);

/// <summary>A catalog channel: its capabilities, its external identifiers and the provider call.</summary>
public interface ICatalogChannelPublisher
{
    string Channel { get; }

    IReadOnlySet<CatalogCapability> SupportedCapabilities { get; }

    /// <summary>
    /// The product's stable external identifier on this channel, returned unchanged on every call. When
    /// the product has none yet it is created only if <paramref name="createIfMissing"/>; null when the
    /// channel has or can give no identifier.
    /// </summary>
    Task<string?> AssignExternalIdAsync(
        Guid productId, string productSku, bool createIfMissing, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>Sends the items to the channel; returns the provider's job identifier when it gives one.</summary>
    Task<string?> PublishAsync(IReadOnlyList<CatalogPublicationItem> items, CancellationToken cancellationToken = default);
}

public sealed record CatalogPublicationRequested(Guid PublicationId, string Channel);

public sealed class UnknownCatalogChannelException : Exception
{
    public UnknownCatalogChannelException(string channel) : base($"No catalog channel '{channel}' is registered.") { }
}
