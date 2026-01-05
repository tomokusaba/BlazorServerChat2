using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// おさいふ関連のWeb APIコントローラー
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OsaifuController(
    ApplicationDbContext context,
    ILogger<OsaifuController> logger) : ControllerBase
{
    /// <summary>
    /// 現在のユーザーのおさいふ情報を取得
    /// </summary>
    /// <returns>おさいふ情報</returns>
    [HttpGet]
    public async Task<ActionResult<OsaifuDto>> GetOsaifu()
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userName))
        {
            return Unauthorized();
        }

        var osaifu = await context.Osaifus
            .Where(o => o.Name == userName)
            .Select(o => new OsaifuDto
            {
                UserId = userId,
                Name = o.Name,
                Kingaku = o.Kingaku
            })
            .FirstOrDefaultAsync();

        if (osaifu == null)
        {
            // おさいふが存在しない場合は新規作成
            var newOsaifu = new Osaifu
            {
                Name = userName,
                Kingaku = 0
            };
            context.Osaifus.Add(newOsaifu);
            await context.SaveChangesAsync();

            osaifu = new OsaifuDto
            {
                UserId = userId,
                Name = userName,
                Kingaku = 0
            };
        }

        return Ok(osaifu);
    }

    /// <summary>
    /// おさいふに入金
    /// </summary>
    /// <param name="request">入金リクエスト</param>
    /// <returns>更新後のおさいふ情報</returns>
    [HttpPost("deposit")]
    public async Task<ActionResult<OsaifuDto>> Deposit([FromBody] UpdateOsaifuRequest request)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return Unauthorized();
        }

        if (request.Amount <= 0)
        {
            return BadRequest("入金額は正の値である必要があります");
        }

        logger.LogInformation("Deposit request: User {UserName}, Amount {Amount}", userName, request.Amount);

        var osaifu = await context.Osaifus
            .Where(o => o.Name == userName)
            .FirstOrDefaultAsync();

        if (osaifu == null)
        {
            osaifu = new Osaifu
            {
                Name = userName,
                Kingaku = request.Amount
            };
            context.Osaifus.Add(osaifu);
        }
        else
        {
            osaifu.Kingaku += request.Amount;
        }

        await context.SaveChangesAsync();

        return Ok(new OsaifuDto
        {
            Name = userName,
            Kingaku = osaifu.Kingaku
        });
    }
}
