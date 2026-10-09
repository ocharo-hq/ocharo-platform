using System.Text;
using Microsoft.Extensions.Options;

namespace BrandWorkspace.Api.Sso;

public sealed class DeskSsoOptions
{
    public bool Enabled { get; set; }
    public DeskSsoClient[] Clients { get; set; } = [];
}

/// <summary>Rejects enabled but incomplete trust registrations at host startup.</summary>
public sealed class DeskSsoOptionsValidator(IConfiguration configuration) : IValidateOptions<DeskSsoOptions>
{
    public ValidateOptionsResult Validate(string? name, DeskSsoOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        var clients = options.Clients;
        if (clients is not { Length: > 0 } || clients.Any(client => client is null || !client.IsValid)
            || clients.Select(client => client.Id).Distinct(StringComparer.Ordinal).Count() != clients.Length)
            return ValidateOptionsResult.Fail("Enabled SSO requires unique client IDs, fixed HTTPS callbacks, explicit organizations and SHA-256 client secret hashes.");
        var key = configuration["Integrations:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            return ValidateOptionsResult.Fail("Enabled SSO requires an integration signing key of at least 32 bytes.");
        return ValidateOptionsResult.Success;
    }
}
