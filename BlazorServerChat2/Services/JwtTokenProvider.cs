using BlazorServerChat2.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using System.Security.Claims;

namespace BlazorServerChat2.Services;

/// <summary>
/// JWTトークンをサーバー側で生成してPersistentComponentStateに保存するサービス 🔐
/// InteractiveAuto用のトークン受け渡しに使用
/// </summary>
public class JwtTokenProvider
{
    private readonly PersistentComponentState _persistentState;
    private readonly JwtService _jwtService;
    private readonly AuthenticationStateProvider _authStateProvider;
    private PersistingComponentStateSubscription? _subscription;

    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime TokenExpiry { get; private set; }

    public JwtTokenProvider(
        PersistentComponentState persistentState,
        JwtService jwtService,
        AuthenticationStateProvider authStateProvider)
    {
        _persistentState = persistentState;
        _jwtService = jwtService;
        _authStateProvider = authStateProvider;

        // サーバー側: 状態を永続化するためのサブスクリプション
        _subscription = _persistentState.RegisterOnPersisting(PersistTokenAsync, RenderMode.InteractiveAuto);

        // クライアント側: 永続化された状態を復元
        if (_persistentState.TryTakeFromJson<TokenState>("jwt_token", out var tokenState) && tokenState != null)
        {
            Token = tokenState.Token;
            UserId = tokenState.UserId;
            UserName = tokenState.UserName;
            TokenExpiry = tokenState.ExpiresAt;
            Console.WriteLine($"JwtTokenProvider: Restored token for {UserName} from persistent state 🔄");
        }
    }

    /// <summary>
    /// サーバー側でトークンを生成（認証状態から）
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!string.IsNullOrEmpty(Token))
        {
            Console.WriteLine($"JwtTokenProvider: Token already available for {UserName} ✅");
            return;
        }

        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                UserName = user.Identity.Name ?? string.Empty;

                var roles = user.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();

                Token = _jwtService.GenerateToken(UserId, UserName, roles);
                TokenExpiry = DateTime.UtcNow.AddMinutes(60);
                
                Console.WriteLine($"JwtTokenProvider: Generated new token for {UserName} 🎫");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"JwtTokenProvider: Error generating token - {ex.Message}");
        }
    }

    private Task PersistTokenAsync()
    {
        if (!string.IsNullOrEmpty(Token))
        {
            _persistentState.PersistAsJson("jwt_token", new TokenState
            {
                Token = Token,
                UserId = UserId ?? string.Empty,
                UserName = UserName ?? string.Empty,
                ExpiresAt = TokenExpiry
            });
            Console.WriteLine($"JwtTokenProvider: Persisted token for {UserName} 💾");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }

    private class TokenState
    {
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
