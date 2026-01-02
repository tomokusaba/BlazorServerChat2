using System.Buffers.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlazorServerChat2.Areas.Identity.Pages.Account.Manage;

public class RenamePasskeyModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<RenamePasskeyModel> _logger;

    public RenamePasskeyModel(
        UserManager<IdentityUser> userManager,
        ILogger<RenamePasskeyModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "パスキーIDが必要です")]
        public string CredentialId { get; set; } = string.Empty;

        [Required(ErrorMessage = "パスキー名を入力してください")]
        [Display(Name = "パスキー名")]
        [StringLength(100, ErrorMessage = "パスキー名は{1}文字以内で入力してください")]
        public string Name { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"ユーザーID '{_userManager.GetUserId(User)}' を読み込めませんでした。");
        }

        if (string.IsNullOrEmpty(id))
        {
            return RedirectToPage("./Passkeys");
        }

        // パスキーが存在するか確認
        var passkeys = await _userManager.GetPasskeysAsync(user);
        var targetPasskey = passkeys.FirstOrDefault(p => Convert.ToBase64String(p.CredentialId).Replace('+', '-').Replace('/', '_').TrimEnd('=') == id);
        
        if (targetPasskey is null)
        {
            StatusMessage = "エラー: 指定されたパスキーが見つかりませんでした。";
            return RedirectToPage("./Passkeys");
        }

        Input = new InputModel
        {
            CredentialId = id,
            Name = targetPasskey.Name ?? string.Empty
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound($"ユーザーID '{_userManager.GetUserId(User)}' を読み込めませんでした。");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var credentialIdBytes = Base64Url.DecodeFromChars(Input.CredentialId.AsSpan());
            
            // パスキーを取得して名前を更新
            var passkeys = await _userManager.GetPasskeysAsync(user);
            var targetPasskey = passkeys.FirstOrDefault(p => p.CredentialId.SequenceEqual(credentialIdBytes));

            if (targetPasskey is null)
            {
                StatusMessage = "エラー: 指定されたパスキーが見つかりませんでした。";
                return RedirectToPage("./Passkeys");
            }

            // 名前を更新
            targetPasskey.Name = Input.Name;
            var result = await _userManager.AddOrUpdatePasskeyAsync(user, targetPasskey);

            if (result.Succeeded)
            {
                _logger.LogInformation("パスキー名が変更されました。ユーザー: {UserId}", user.Id);
                StatusMessage = "パスキー名が変更されました。";
                return RedirectToPage("./Passkeys");
            }
            else
            {
                StatusMessage = "エラー: パスキー名の変更に失敗しました。";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "パスキー名変更中にエラーが発生しました。");
            StatusMessage = "エラー: パスキー名の変更中に問題が発生しました。";
        }

        return Page();
    }
}
