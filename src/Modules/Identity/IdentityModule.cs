namespace ALKAROS.Identity;

using ALKAROS.Identity.Authentication;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Offline;
using ALKAROS.Identity.Authorization.Policies;
using ALKAROS.Identity.DeviceSessions;
using ALKAROS.ModuleComposition;

public sealed class IdentityModule : IModule
{
    public string Id => "Identity";
    public string DisplayName => "Identity and Access Management";
    public IReadOnlyCollection<string> DependsOn => [];

    public void Register(ModuleContext context)
    {
        context.RegisterTransient<IUserStore, PostgresUserStore>();
        context.RegisterTransient<AuthenticationService, AuthenticationService>();
        context.RegisterTransient<IRoleRepository, PostgresRoleRepository>();
        context.RegisterTransient<IPermissionRepository, PostgresPermissionRepository>();
        context.RegisterTransient<IDenialEventSink, PostgresDenialEventSink>();
        context.RegisterTransient<IAuthorizationService, AuthorizationService>();
        context.RegisterTransient<IRoleManagementService, RoleManagementService>();
        context.RegisterTransient<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        context.RegisterTransient<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        context.RegisterTransient<IAuthorizationDelegationRepository, PostgresAuthorizationDelegationRepository>();
        context.RegisterTransient<IEscalationResolver, DelegationEscalationResolver>();
        context.RegisterTransient<IAuthorizationGrantService, AuthorizationGrantService>();
        context.RegisterTransient<IOfflineAuthorityBudgetRepository, PostgresOfflineAuthorityBudgetRepository>();
        context.RegisterTransient<IOfflineReplayLedger, PostgresOfflineReplayLedger>();
        context.RegisterTransient<IOfflineAuthorityBudgetService, OfflineAuthorityBudgetService>();
        context.RegisterTransient<IOfflineGrantReconciler, OfflineGrantReconciler>();
        context.RegisterTransient<IDeviceSessionRepository, PostgresDeviceSessionRepository>();
        context.RegisterTransient<IDeviceSessionService, DeviceSessionService>();
    }
}
