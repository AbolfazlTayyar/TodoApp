using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Domain;
using TodoApp.Shared.Infrastructure.Auth;

namespace TodoApp.Modules.Users.Infrastructure.Security;

internal sealed class TokenProvider(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenProvider
{
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public AccessToken Create(User user)
    {
        var jwt = options.Value;
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(jwt.ExpiryMinutes);

        var token = _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName)
            ]),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.GetSecurityKey(), SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expiresAt);
    }
}
