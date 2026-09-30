using System.Text;
using FlagForge.Application.Common;
using FlagForge.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlagForge.ManagementApi.Authentication;

/// <summary>Issues HS256 access tokens with the <c>sub</c>, <c>email</c>, <c>name</c>, and <c>role</c> claims.</summary>
internal sealed class JwtTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var auth = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(auth.AccessTokenMinutes);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.JwtIssuer,
            Audience = auth.JwtAudience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [TokenClaims.Subject] = user.Id.ToString(),
                [TokenClaims.Email] = user.Email,
                [TokenClaims.Name] = user.DisplayName,
                [TokenClaims.Role] = user.Role.ToString(),
            },
            SigningCredentials = new SigningCredentials(SigningKey(auth), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expiresAt);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions options) => new(Encoding.UTF8.GetBytes(options.JwtSigningKey));
}
