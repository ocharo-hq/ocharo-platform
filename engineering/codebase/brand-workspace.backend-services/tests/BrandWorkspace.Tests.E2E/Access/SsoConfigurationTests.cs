using BrandWorkspace.Api.Sso;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BrandWorkspace.Tests.E2E.Access;

public sealed class SsoConfigurationTests
{
    private static readonly DeskSsoClient Client = new("catalogue", "https://catalogue.example.test/api/sso/callback", "ocharo-catalogue", Guid.NewGuid(), new string('a', 64));

    [Fact]
    public void EnabledConfiguration_ShouldRejectIncompleteTrustAndDuplicateClients()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Integrations:SigningKey"] = new string('k', 32) }).Build();
        var validator = new DeskSsoOptionsValidator(configuration);
        Assert.True(validator.Validate(null, new() { Enabled = true, Clients = [Client] }).Succeeded);
        Assert.True(validator.Validate(null, new() { Enabled = true }).Failed);
        Assert.True(validator.Validate(null, new() { Enabled = true, Clients = [Client, Client] }).Failed);
        foreach (var invalid in new[] { Client with { RedirectUri = "http://example.test/api/sso/callback" },
            Client with { RedirectUri = Client.RedirectUri + "?returnUrl=x" }, Client with { OrganizationId = Guid.Empty },
            Client with { SecretSha256 = "short" }, Client with { SecretSha256 = null! }, Client with { Audience = "desk-mcp" } })
            Assert.True(validator.Validate(null, new() { Enabled = true, Clients = [invalid] }).Failed);
        configuration["Integrations:SigningKey"] = "short";
        Assert.True(validator.Validate(null, new() { Enabled = true, Clients = [Client] }).Failed);
        Assert.True(validator.Validate(null, new() { Enabled = false }).Succeeded);
    }
}
