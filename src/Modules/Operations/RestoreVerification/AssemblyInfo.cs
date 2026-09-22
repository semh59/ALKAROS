using System.Runtime.CompilerServices;

// V15-BKP-002 dispose-masking regression test needs to construct
// NpgsqlIsolatedRestoreDatabase directly (it is internal) with a
// deliberately broken maintenance connection string, to prove a failed
// `DROP DATABASE` during DisposeAsync no longer masks an original
// exception that is already propagating.
[assembly: InternalsVisibleTo("ALKAROS.Operations.RestoreVerification.Tests")]
