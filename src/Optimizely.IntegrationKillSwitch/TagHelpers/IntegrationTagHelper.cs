namespace Optimizely.IntegrationKillSwitch.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

/// <summary>
/// Renders its contents only while the named integration is active. The &lt;integration&gt; element itself is not rendered.
/// </summary>
/// <example>
/// <code>
/// @addTagHelper *, Optimizely.IntegrationKillSwitch
///
/// &lt;integration name="GoogleTagManager"&gt;
///     &lt;script src="https://www.googletagmanager.com/gtm.js?id=GTM-XXXX"&gt;&lt;/script&gt;
/// &lt;/integration&gt;
/// </code>
/// </example>
[HtmlTargetElement("integration", Attributes = "name")]
public sealed class IntegrationTagHelper(IIntegrationStatusService integrationStatus) : TagHelper
{
    public string Name { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        if (!integrationStatus.IsActive(Name))
        {
            output.SuppressOutput();
        }
    }
}
