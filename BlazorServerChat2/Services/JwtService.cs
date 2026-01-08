using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BlazorServerChat2.Services;

/// <summary>
/// JWT トークン生成・検証サービス 🔐
/// </summary>
public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        _secret = configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is not configured");
        _issuer = configuration["Jwt:Issuer"] ?? "BlazorServerChat2";
        _audience = configuration["Jwt:Audience"] ?? "BlazorServerChat2-Client";
        _expirationMinutes = int.TryParse(configuration["Jwt:ExpirationMinutes"], out var exp) ? exp : 60;
    }

    /// <summary>
    /// ユーザー情報からJWTトークンを生成 🎫
    /// </summary>
    public string GenerateToken(string userId, string userName, IEnumerable<string>? roles = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // ロールがあれば追加
        if (roles != null)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// トークンを検証してClaimsPrincipalを返す 🔍
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// トークン検証パラメータを取得（Program.cs用）📋
    /// </summary>
    public TokenValidationParameters GetTokenValidationParameters()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    }
}
