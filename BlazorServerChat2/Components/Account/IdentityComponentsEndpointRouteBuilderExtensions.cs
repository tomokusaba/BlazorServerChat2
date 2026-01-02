using Microsoft.AspNetCore.Identity;

namespace BlazorServerChat2.Components.Account;

/// <summary>
/// パスキーエンドポイント拡張機能
/// </summary>
public static class IdentityComponentsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// パスキー関連のエンドポイントをマップ
    /// </summary>
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/Account");

        // パスキー作成オプションエンドポイント
        group.MapPost("/PasskeyCreationOptions", async (
            HttpContext context,
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var userEntity = new PasskeyUserEntity
            {
                Id = await userManager.GetUserIdAsync(user),
                Name = await userManager.GetUserNameAsync(user) ?? string.Empty,
                DisplayName = await userManager.GetUserNameAsync(user) ?? string.Empty
            };

            // MakePasskeyCreationOptionsAsync は JSON文字列を返す
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(userEntity);

            return Results.Content(optionsJson, contentType: "application/json");
        }).RequireAuthorization();

        // パスキー要求オプションエンドポイント（認証用）
        group.MapPost("/PasskeyRequestOptions", async (
            HttpContext context,
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            PasskeyRequestModel? request) =>
        {
            IdentityUser? user = null;

            // ユーザーがログイン中の場合
            if (context.User.Identity?.IsAuthenticated == true)
            {
                user = await userManager.GetUserAsync(context.User);
            }
            // メールアドレスが提供された場合
            else if (!string.IsNullOrEmpty(request?.Email))
            {
                user = await userManager.FindByEmailAsync(request.Email);
            }

            // MakePasskeyRequestOptionsAsync は JSON文字列を返す
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);

            return Results.Content(optionsJson, contentType: "application/json");
        });

        // パスキーサインインエンドポイント
        group.MapPost("/PasskeySignIn", async (
            HttpContext context,
            SignInManager<IdentityUser> signInManager,
            PasskeySignInModel request) =>
        {
            if (string.IsNullOrEmpty(request.CredentialJson))
            {
                return Results.BadRequest(new { error = "パスキー資格情報が必要です" });
            }

            var result = await signInManager.PasskeySignInAsync(request.CredentialJson);
            if (result.Succeeded)
            {
                return Results.Ok(new { returnUrl = request.ReturnUrl ?? "/" });
            }

            return Results.BadRequest(new { error = "パスキー認証に失敗しました" });
        });

        // パスキー追加エンドポイント
        group.MapPost("/AddPasskey", async (
            HttpContext context,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            AddPasskeyModel request) =>
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrEmpty(request.CredentialJson))
            {
                return Results.BadRequest(new { error = "パスキー資格情報が必要です" });
            }

            // パスキーのAttestation検証を実行してパスキー情報を取得
            var attestationResult = await signInManager.PerformPasskeyAttestationAsync(request.CredentialJson);
            if (!attestationResult.Succeeded)
            {
                return Results.BadRequest(new { error = attestationResult.Failure?.Message ?? "パスキーの検証に失敗しました" });
            }

            // パスキー名を設定（オプション）
            if (!string.IsNullOrEmpty(request.PasskeyName))
            {
                attestationResult.Passkey.Name = request.PasskeyName;
            }

            // パスキーを保存
            var result = await userManager.AddOrUpdatePasskeyAsync(user, attestationResult.Passkey);
            if (result.Succeeded)
            {
                return Results.Ok(new { success = true });
            }

            return Results.BadRequest(new { error = "パスキーの追加に失敗しました" });
        }).RequireAuthorization();

        return group;
    }
}

/// <summary>
/// パスキー要求モデル
/// </summary>
public class PasskeyRequestModel
{
    public string? Email { get; set; }
}

/// <summary>
/// パスキーサインインモデル
/// </summary>
public class PasskeySignInModel
{
    public string? CredentialJson { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// パスキー追加モデル
/// </summary>
public class AddPasskeyModel
{
    public string? CredentialJson { get; set; }
    public string? PasskeyName { get; set; }
}
