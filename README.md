# OptiIntegrationKillSwitch

Kill switches for third-party integrations and add-ons in **Optimizely CMS 12 and CMS 13**.

This is a community package, not an official Optimizely product. The assembly and namespace are `Optimizely.IntegrationKillSwitch` for both CMS versions, which is the name used in `@addTagHelper` and `using` statements.

If an add-on or external service you depend on is compromised, a CMS administrator can switch it off from the CMS in seconds, on every instance, with no deploy or restart. While an integration is switched off:

- **Inbound requests** to its paths (its protected module UI, API endpoints, public routes) return **404**.
- **Outbound HTTP calls** to its hosts from any `IHttpClientFactory` client return **503** without touching the network.
- **Scripts and markup** wrapped in `<integration name="...">` are not rendered.

Every change is audited with who made it, when and why. A config flag can also remove an add-on at startup.

## Install

Install the package that matches your CMS version:

| CMS | Package | .NET |
|---|---|---|
| 12 | `OptiIntegrationKillSwitch` | 8 or 10 |
| 13 | `OptiIntegrationKillSwitch.Cms13` | 10 |

```bash
dotnet add package OptiIntegrationKillSwitch        # CMS 12
dotnet add package OptiIntegrationKillSwitch.Cms13  # CMS 13
```

Both packages have the same API and configuration, and are released together at the same version. When you upgrade a site from CMS 12 to 13, swap the package reference. Your code, config and stored switch state don't change.

```csharp
// Startup.ConfigureServices
services.AddIntegrationKillSwitches(o =>
{
    o.AddKnownAddOns(); // optional presets for common add-ons
    o.Add(new IntegrationDescriptor
    {
        Name = "GoogleTagManager",
        DisplayName = "Google Tag Manager",
        BlockedHosts = ["www.googletagmanager.com"]
    });
    o.Add(new IntegrationDescriptor
    {
        Name = "CalendarApi",
        DisplayName = "Calendar API",
        HostConfigKeys = ["CalendarServiceUrl"] // the host is read from this config URL
    });
});

// Remove an add-on completely at startup when Integrations:GetaSitemaps:Enabled is false
if (configuration.IsIntegrationEnabled("GetaSitemaps"))
{
    services.AddSitemaps(...);
}

// Startup.Configure: early, before any add-on middleware
app.UseIntegrationKillSwitches();
```

```cshtml
@* _ViewImports.cshtml *@
@addTagHelper *, Optimizely.IntegrationKillSwitch

@* _Layout.cshtml *@
<integration name="GoogleTagManager">
    <script src="https://www.googletagmanager.com/gtm.js?id=GTM-XXXX"></script>
</integration>
```

The admin page is at `{protected module root}/integration-kill-switches` (e.g. `/EPiServer/integration-kill-switches`) and appears in the CMS menu for users who meet the admin policy.

## Options

| Option | Default | |
|---|---|---|
| `ConfigureAdminPolicy` | `RequireRole("CmsAdmins", "WebAdmins")` | Who can use the admin page. For Entra ID "roles" claims, use `policy.RequireClaim("roles", "WebAdmins")`. |
| `AdminPath` | `integration-kill-switches` | Relative to the protected module root. |
| `ShowInMenu` | `true` | |
| `ConfigSectionName` | `Integrations` | |
| `StoreName` | `IntegrationKillSwitch` | Dynamic Data Store name. The audit store adds `Audit` to it. |
| `CacheDuration` | 2 minutes | A safety net only, because changes clear the cache on every instance straight away. |

## Configuration

```json
"Integrations": {
  "GetaSitemaps": { "Enabled": false },
  "CalendarApi": { "BlockedHosts": [ "backup-calendar.example.com" ] },
  "SomeVendor": {
    "DisplayName": "Some vendor add-on",
    "BlockedPaths": [ "{ProtectedRoot}/SomeVendor.Optimizely" ],
    "BlockedHosts": [ "api.somevendor.example" ]
  }
}
```

- `Enabled` defaults to `true`, or to the descriptor's `EnabledByDefault`. A descriptor's `LegacyEnabledConfigKey` (e.g. `GTMEnabled`) is still read when `Enabled` is not set.
- `BlockedPaths` and `BlockedHosts` add to those registered in code. `{ProtectedRoot}` is replaced with your protected module root.
- A section for a name not registered in code defines a new integration, with no code change. Use this for a newly installed add-on.

## Incident runbook

1. Switch the integration off on the admin page, with the incident reference as the reason.
2. If it adds anything to visitors' browsers, also remove its hosts from your Content Security Policy (e.g. in Stott Security).
3. Rotate any credentials the vendor holds.
4. Set `Integrations:{Name}:Enabled` to `false` and deploy, so its services are not loaded at all.
5. Remove the package if it is not coming back.

## Limits

- **Outbound blocking only covers `IHttpClientFactory` clients.** It doesn't cover clients that build their own `HttpClient`, such as Search & Navigation (Find), or SMTP.
- **The config flag only skips the `Add*`/`Use*` calls you wrap.** It can't stop `[InitializableModule]` classes in an add-on's assembly from loading. Removing the package is the only complete switch for those.
- **The `KnownAddOns` paths assume each add-on uses its assembly name as its protected module name.** Check them against your site.
- **If the store can't be read** (for example during a database outage), the package falls back to config flags only.
- **Supported versions:** CMS 12 needs `EPiServer.CMS.AspNetCore` 12.17.1 or later (.NET 8 or 10). CMS 13 works on any 13.x (.NET 10).

## Licence

MIT

## Building

CMS 12 and CMS 13 are built from the same source. The `CmsMajor` property picks the target, and defaults to 12:

```bash
dotnet test                  # CMS 12
dotnet test -p:CmsMajor=13   # CMS 13
```

The only CMS-specific code is the admin page layout. CMS 12 draws the navigation bar with HTML helpers, and CMS 13 uses the `<platform-navigation-wrapper>` tag helper. The `CMS13` compile constant is defined for the CMS 13 build.
