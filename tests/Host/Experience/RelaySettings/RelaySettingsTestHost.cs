using System.Security.Cryptography;
using ALKAROS.QrOrdering.RelayCredential;
using ALKAROS.Secrets;
using ALKAROS.SensitiveData;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Experience.RelaySettings.Tests;

internal static class RelaySettingsTestHost
{
    /// <summary>
    /// The production wiring (QrOrderingModule) uses EnvironmentVariableSecretProvider; a standalone test host uses the
    /// in-memory one instead, exactly as that provider's own doc comment intends.
    /// </summary>
    public static void AddRelayCredentialChain(IServiceCollection services)
    {
        var secretProvider = new InMemorySecretProvider();
        secretProvider.Set(new SecretReference("envelope-master-key"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        services.AddSingleton<ISecretProvider>(secretProvider);
        var accessPolicy = new RelayCredentialAccessPolicy();
        services.AddSingleton<ISecretAccessPolicy>(accessPolicy);
        services.AddSingleton<ISensitiveDataAccessPolicy>(accessPolicy);
        services.AddSingleton<ISecretResolver, SecretResolver>();
        services.AddSingleton<IEnvelopeCipher, AesGcmEnvelopeCipher>();
        services.AddSingleton<SensitivePayloadProtector>();
        services.AddSingleton<IRelayCredentialStore, PostgresRelayCredentialStore>();
    }
}
