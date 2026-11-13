namespace Optimizely.IntegrationKillSwitch;

using EPiServer.Shell.Modules;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using Optimizely.IntegrationKillSwitch.Admin;
using Optimizely.IntegrationKillSwitch.Configuration;
using Optimizely.IntegrationKillSwitch.Enforcement;
using Optimizely.IntegrationKillSwitch.Models;
using Optimizely.IntegrationKillSwitch.Storage;

public static class IntegrationKillSwitchServiceExtensions
{
    /// <summary>
    /// Registers the kill switch services and the admin page, and adds outbound host blocking to every IHttpClientFactory client.
    /// </summary>
    public static IServiceCollection AddIntegrationKillSwitches(
        this IServiceCollection services,
        Action<IntegrationKillSwitchOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<IntegrationKillSwitchOptions>();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<IIntegrationKillSwitchRepository, IntegrationKillSwitchRepository>();
        services.TryAddSingleton<IIntegrationStatusService, IntegrationStatusService>();
        services.TryAddTransient<IntegrationKillSwitchHttpHandler>();

        services.ConfigureHttpClientDefaults(builder => builder.AddHttpMessageHandler<IntegrationKillSwitchHttpHandler>());

        // The policy is built from options when first used, so sites can change it after this call.
        services.AddAuthorization();
        services.AddOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>()
            .Configure<IOptions<IntegrationKillSwitchOptions>>((authorization, killSwitch) =>
                authorization.AddPolicy(IntegrationKillSwitchOptions.AdminPolicyName, killSwitch.Value.ConfigureAdminPolicy));

        services.AddOptions<MvcOptions>()
            .Configure<IOptions<IntegrationKillSwitchOptions>, IOptions<ProtectedModuleOptions>>((mvc, killSwitch, protectedModules) =>
                mvc.Conventions.Add(new AdminRouteConvention(
                    AdminRouteConvention.BuildTemplate(protectedModules.Value.RootPath, killSwitch.Value.AdminPath))));

        return services;
    }

    /// <summary>
    /// Blocks inbound requests to the paths of integrations that are switched off.
    /// Call this early in the pipeline, before any add-on middleware.
    /// </summary>
    public static IApplicationBuilder UseIntegrationKillSwitches(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<IntegrationKillSwitchMiddleware>();
    }

    /// <summary>
    /// For Startup: decides whether to register an add-on at all. Reads Integrations:{name}:Enabled, which defaults to <paramref name="defaultValue"/>.
    /// </summary>
    public static bool IsIntegrationEnabled(
        this IConfiguration configuration,
        string name,
        bool defaultValue = true,
        string? legacyEnabledConfigKey = null,
        string sectionName = IntegrationConfigReader.DefaultSectionName)
    {
        var descriptor = new IntegrationDescriptor
        {
            Name = name,
            DisplayName = name,
            EnabledByDefault = defaultValue,
            LegacyEnabledConfigKey = legacyEnabledConfigKey
        };

        return IntegrationConfigReader.IsEnabled(configuration, descriptor, sectionName);
    }

    /// <summary>For Startup: the same as above, using a registered descriptor's default and legacy key.</summary>
    public static bool IsIntegrationEnabled(
        this IConfiguration configuration,
        IntegrationDescriptor descriptor,
        string sectionName = IntegrationConfigReader.DefaultSectionName)
    {
        return IntegrationConfigReader.IsEnabled(configuration, descriptor, sectionName);
    }
}
