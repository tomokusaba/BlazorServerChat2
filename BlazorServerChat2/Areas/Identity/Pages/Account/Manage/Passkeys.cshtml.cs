using System.Buffers.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlazorServerChat2.Areas.Identity.Pages.Account.Manage;

public class PasskeysModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<PasskeysModel> _logger;

    public PasskeysModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        ILogger<PasskeysModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    public IList<UserPasskeyInfo>? CurrentPasskeys { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"ユーザーID '{_userManager.GetUserId(User)}' を読み込めませんでした。");
        }

        CurrentPasskeys = await _userManager.GetPasskeysAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync(string credentialId)
    {
        if (string.IsNullOrEmpty(credentialId))
        {
            StatusMessage = "エラー: パスキーIDが指定されていません。";
            return RedirectToPage();
        }

        return RedirectToPage("./RenamePasskey", new { id = credentialId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string credentialId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"ユーザーID '{_userManager.GetUserId(User)}' を読み込めませんでした。");
        }

        if (string.IsNullOrEmpty(credentialId))
        {
            StatusMessage = "エラー: パスキーIDが指定されていません。";
            return RedirectToPage();
        }

        try
        {
            var credentialIdBytes = Base64Url.DecodeFromChars(credentialId.AsSpan());
            var result = await _userManager.RemovePasskeyAsync(user, credentialIdBytes);

            if (result.Succeeded)
            {
                _logger.LogInformation("パスキーが削除されました。ユーザー: {UserId}", user.Id);
                StatusMessage = "パスキーが削除されました。";
            }
            else
            {
                StatusMessage = "エラー: パスキーの削除に失敗しました。";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "パスキー削除中にエラーが発生しました。");
            StatusMessage = "エラー: パスキーの削除中に問題が発生しました。";
        }

        return RedirectToPage();
    }
}
