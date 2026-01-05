using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace BlazorServerChat2.Services;

/// <summary>
/// InteractiveAuto用のHttpClient設定サービス
/// サーバーサイドとWASM両方で認証付きリクエストを送信できるようにする
/// </summary>
public class AuthenticatedHttpClientFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthenticatedHttpClientFactory(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// 認証Cookie付きのHttpClientを作成（サーバーサイド用）
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = _httpClientFactory.CreateClient();
        
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // リクエストからCookieを転送
            var cookies = httpContext.Request.Cookies;
            if (cookies.Any())
            {
                var cookieHeader = string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}"));
                client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
            }
        }
        
        return client;
    }

    /// <summary>
    /// 現在のユーザーIDを取得（サーバーサイド用）
    /// </summary>
    public string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// 現在のユーザー名を取得（サーバーサイド用）
    /// </summary>
    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    }
}
