namespace ALKAROS.Security.DataProtectionRetention;

/// <summary>
/// The disposal action V0-CMP-003's matrix assigns to a data category.
/// </summary>
public enum DisposalAction
{
    /// <summary>Irreversibly scrub the sensitive envelope in place; the row (id/category/timestamps) survives as its own audit trail.</summary>
    Anonymize = 0,

    /// <summary>Queue for hard deletion; <see cref="DeletionQueueProcessor"/> removes the row entirely.</summary>
    Delete = 1,

    /// <summary>Legal requirement to keep the data indefinitely; never auto-disposed by this engine.</summary>
    Retain = 2,
}
