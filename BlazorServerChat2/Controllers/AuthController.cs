using BlazorServerChat2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// 認証API - JWTトークン発行 🔐
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AuthController(JwtService jwtService) : ControllerBase
{
    /// <summary>
    /// Cookie認証済みユーザーにJWTトークンを発行 🎫
    /// WASM側から呼び出される
    /// </summary>
    [HttpGet("token")]
    [Authorize] // Cookie認証が必要
    public ActionResult<TokenResponse> GetToken()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Identity?.Name;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
        {
            return Unauthorized(new { error = "User not authenticated" });
        }

        // ロールを取得
        var roles = User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var token = jwtService.GenerateToken(userId, userName, roles);

        return Ok(new TokenResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            UserId = userId,
            UserName = userName
        });
    }

    /// <summary>
    /// トークンの有効性を確認 ✅
    /// </summary>
    [HttpGet("validate")]
    [Authorize] // Cookie or JWT
    public ActionResult<ValidateResponse> ValidateToken()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = User.Identity?.Name;

        return Ok(new ValidateResponse
        {
            IsValid = true,
            UserId = userId,
            UserName = userName
        });
    }
}

/// <summary>
/// トークンレスポンス
/// </summary>
public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// 検証レスポンス
/// </summary>
public class ValidateResponse
{
    public bool IsValid { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}
