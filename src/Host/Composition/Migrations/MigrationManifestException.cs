namespace ALKAROS.Host.Composition.Migrations;

/// <summary>
/// Thrown when the migration order manifest cannot be loaded or violates the
/// contract locked by V0-DAT-001 / CORR:C1.
/// </summary>
public sealed class MigrationManifestException : Exception
{
    public MigrationManifestException(string message)
        : base(message)
    {
    }
}
