namespace Optimizely.IntegrationKillSwitch.Enforcement;

using Microsoft.Extensions.Logging;

using Optimizely.IntegrationKillSwitch.Models;

using System.Net;

/// <summary>
/// Added to every HttpClient made by IHttpClientFactory. Stops outbound calls to the hosts of any integration
/// that is switched off and returns 503 without touching the network.
/// </summary>
/// <remarks>
/// This does not cover clients that make their own HttpClient instead of using the factory,
/// for example the Search &amp; Navigation (Find) client or SMTP.
/// </remarks>
public sealed class IntegrationKillSwitchHttpHandler(
    IIntegrationStatusService integrationStatus,
    ILogger<IntegrationKillSwitchHttpHandler> logger
) : DelegatingHandler
{
    public const string BlockedReasonPhrase = "Integration disabled";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var blocked = FindBlockingIntegration(request.RequestUri, integrationStatus);

        if (blocked is not null)
        {
            logger.LogWarning(
                "Blocked outbound request to {Host} because integration {Integration} is switched off.",
                request.RequestUri?.Host,
                blocked.Name);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = BlockedReasonPhrase,
                RequestMessage = request,
                Content = new StringContent(string.Empty)
            });
        }

        return base.SendAsync(request, cancellationToken);
    }

    public static IntegrationDescriptor? FindBlockingIntegration(Uri? uri, IIntegrationStatusService integrationStatus)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return null;
        }

        var host = uri.Host;

        return integrationStatus.GetInactive().FirstOrDefault(d => d.BlockedHosts.Any(h =>
            string.Equals(host, h, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)));
    }
}
