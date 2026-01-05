using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// ユーザー設定関連のWeb APIコントローラー
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserSettingsController(
    ApplicationDbContext context,
    ILogger<UserSettingsController> logger) : ControllerBase
{
    /// <summary>
    /// 現在のユーザー設定を取得
    /// </summary>
    /// <returns>ユーザー設定</returns>
    [HttpGet]
    public async Task<ActionResult<UserChatSettingDto>> GetSettings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var settings = await context.UserChatSetting
            .Where(s => s.Id == userId)
            .Select(s => new UserChatSettingDto
            {
                Id = s.Id,
                IconNumber = s.IconNumber,
                BackGroundColor = s.BackGroundColor
            })
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            return NotFound();
        }

        return Ok(settings);
    }

    /// <summary>
    /// ユーザー設定を更新
    /// </summary>
    /// <param name="request">更新リクエスト</param>
    /// <returns>更新後のユーザー設定</returns>
    [HttpPut]
    public async Task<ActionResult<UserChatSettingDto>> UpdateSettings([FromBody] UserChatSettingDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        logger.LogInformation("Updating settings for user {UserId}", userId);

        var settings = await context.UserChatSetting
            .Where(s => s.Id == userId)
            .FirstOrDefaultAsync();

        if (settings == null)
        {
            settings = new UserChatSetting
            {
                Id = userId,
                IconNumber = request.IconNumber,
                BackGroundColor = request.BackGroundColor
            };
            context.UserChatSetting.Add(settings);
        }
        else
        {
            settings.IconNumber = request.IconNumber;
            settings.BackGroundColor = request.BackGroundColor;
        }

        await context.SaveChangesAsync();

        return Ok(new UserChatSettingDto
        {
            Id = settings.Id,
            IconNumber = settings.IconNumber,
            BackGroundColor = settings.BackGroundColor
        });
    }

    /// <summary>
    /// アイコン一覧を取得
    /// </summary>
    /// <returns>アイコン一覧</returns>
    [HttpGet("icons")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<IconDto>>> GetIcons()
    {
        var icons = await context.IconMaster
            .Select(i => new IconDto
            {
                IconNumber = i.IconNumber,
                IconBase64 = i.Icon != null ? Convert.ToBase64String(i.Icon) : null,
                IconName = i.IconName
            })
            .ToListAsync();

        return Ok(icons);
    }

    /// <summary>
    /// 指定アイコンを取得
    /// </summary>
    /// <param name="iconNumber">アイコン番号</param>
    /// <returns>アイコン</returns>
    [HttpGet("icons/{iconNumber}")]
    [AllowAnonymous]
    public async Task<ActionResult<IconDto>> GetIcon(int iconNumber)
    {
        var icon = await context.IconMaster
            .Where(i => i.IconNumber == iconNumber)
            .Select(i => new IconDto
            {
                IconNumber = i.IconNumber,
                IconBase64 = i.Icon != null ? Convert.ToBase64String(i.Icon) : null,
                IconName = i.IconName
            })
            .FirstOrDefaultAsync();

        if (icon == null)
        {
            return NotFound();
        }

        return Ok(icon);
    }

    /// <summary>
    /// 特定ユーザーのアイコンを取得
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <returns>アイコン</returns>
    [HttpGet("icons/user/{userId}")]
    [AllowAnonymous]
    public async Task<ActionResult<BlazorServerChat2.Shared.Models.IconDto>> GetUserIcon(string userId)
    {
        var userSetting = await context.UserChatSetting
            .Where(s => s.Id == userId)
            .FirstOrDefaultAsync();

        if (userSetting == null)
        {
            return NotFound();
        }

        var icon = await context.IconMaster
            .Where(i => i.IconNumber == userSetting.IconNumber)
            .FirstOrDefaultAsync();

        return Ok(new BlazorServerChat2.Shared.Models.IconDto
        {
            UserId = userId,
            IconBase64 = icon?.Icon != null ? Convert.ToBase64String(icon.Icon) : string.Empty
        });
    }
}
