using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace BlazorServerChat2.Client;

/// <summary>
/// WASM側の認証状態プロバイダー 🔐
/// サーバーからPersistentComponentStateで渡された認証情報を復元する
/// </summary>
public class PersistentAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly Task<AuthenticationState> _defaultUnauthenticatedTask =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    private readonly Task<AuthenticationState> _authenticationStateTask = _defaultUnauthenticatedTask;

    public PersistentAuthenticationStateProvider(PersistentComponentState state)
    {
        // PersistentComponentStateからユーザー情報を復元
        if (!state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) || userInfo is null)
        {
            Console.WriteLine("PersistentAuthStateProvider: No user info found in PersistentState 🔍");
            return;
        }

        Console.WriteLine($"PersistentAuthStateProvider: User restored - {userInfo.Name} 👤");

        // クレームを構築
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userInfo.UserId),
            new Claim(ClaimTypes.Name, userInfo.Name)
        };

        // ロールを追加
        if (userInfo.Roles != null)
        {
            foreach (var role in userInfo.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        _authenticationStateTask = Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: nameof(PersistentAuthenticationStateProvider)))));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}

/// <summary>
/// サーバーからWASMに渡すユーザー情報
/// </summary>
public class UserInfo
{
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public string[]? Roles { get; set; }
}
