using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Signals;
using WoW.Two.Sdk.Beta.Be.Identity.Jwt;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Sends a signal the way an Identity instance does: a <c>webhook+jwt</c> signed with its key, bound to the signal URL and the body.</summary>
internal sealed class IdentitySignalSender(string issuer) : IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);

    public TrustedIssuer TrustedIssuer => new() { Issuer = issuer, SigningKeys = [new RsaSecurityKey(rsa) { KeyId = "signal" }] };

    public HttpRequestMessage SessionEnded(string signalUrl, string sessionId, string subject)
    {
        var body = JsonSerializer.Serialize(
            new SessionEndedEvent { Issuer = issuer, SessionId = sessionId, Subject = subject, At = DateTimeOffset.UtcNow, TokensExpireBy = DateTimeOffset.UtcNow.AddHours(1) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var signature = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = signalUrl,
            TokenType = "webhook+jwt",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = "signal" }, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["event"] = nameof(SessionEndedEvent),
                ["body_sha256"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(body))),
            },
        });
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(signalUrl).AbsolutePath)
        {
            Content = new StringContent(body, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        request.Headers.Add("X-Webhook-Signature", signature);
        request.Headers.Add("X-Webhook-Event", nameof(SessionEndedEvent));
        return request;
    }

    public void Dispose() => rsa.Dispose();
}
