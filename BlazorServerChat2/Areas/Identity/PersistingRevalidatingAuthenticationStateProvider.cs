using BlazorServerChat2.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Claims;

namespace BlazorServerChat2.Areas.Identity;

/// <summary>
/// InteractiveAuto対応の認証状態プロバイダー 🔐
/// サーバーからWASMへPersistentComponentStateで認証情報を渡す
/// </summary>
public class PersistingRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PersistentComponentState _state;
    private readonly IdentityOptions _options;

    private readonly PersistingComponentStateSubscription _subscription;

    private Task<AuthenticationState>? _authenticationStateTask;

    public PersistingRevalidatingAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        PersistentComponentState state,
        IOptions<IdentityOptions> optionsAccessor)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _options = optionsAccessor.Value;

        // WASM切り替え時に認証情報を永続化 💾
        AuthenticationStateChanged += OnAuthenticationStateChanged;
        _subscription = state.RegisterOnPersisting(OnPersistingAsync, RenderMode.InteractiveWebAssembly);
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromDays(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateScope();
        try
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            return await ValidateSecurityStampAsync(userManager, authenticationState.User);
        }
        finally
        {
            if (scope is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                scope.Dispose();
            }
        }
    }

    private async Task<bool> ValidateSecurityStampAsync(UserManager<IdentityUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            return false;
        }
        else if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }
        else
        {
            var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
            var userStamp = await userManager.GetSecurityStampAsync(user);
            return principalStamp == userStamp;
        }
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _authenticationStateTask = task;
    }

    private async Task OnPersistingAsync()
    {
        if (_authenticationStateTask is null)
        {
            throw new UnreachableException($"Authentication state not set in {nameof(OnPersistingAsync)}().");
        }

        var authenticationState = await _authenticationStateTask;
        var principal = authenticationState.User;

        if (principal.Identity?.IsAuthenticated == true)
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var name = principal.Identity.Name;

            if (userId != null && name != null)
            {
                var roles = principal.FindAll(ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToArray();

                _state.PersistAsJson(nameof(UserInfo), new UserInfo
                {
                    UserId = userId,
                    Name = name,
                    Roles = roles
                });

                Console.WriteLine($"PersistingAuthStateProvider: User persisted for WASM - {name} 💾");
            }
        }
    }

    public void Dispose()
    {
        _subscription.Dispose();
        AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
