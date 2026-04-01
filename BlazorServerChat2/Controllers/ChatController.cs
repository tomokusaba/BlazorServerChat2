using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// チャット関連のWeb APIコントローラー
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(
    ApplicationDbContext context,
    Room room,
    IUserChatSettingCache userChatSettingCache,
    ILogger<ChatController> logger) : ControllerBase
{
    /// <summary>
    /// チャット一覧を取得
    /// </summary>
    /// <param name="page">ページ番号（デフォルト: 1）</param>
    /// <param name="pageSize">ページサイズ（デフォルト: 50）</param>
    /// <returns>チャット一覧</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatDto>>> GetChats(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50)
    {
        logger.LogInformation("Getting chats - Page: {Page}, PageSize: {PageSize}", page, pageSize);
        
        var chats = await context.Chats
            .OrderByDescending(c => c.Time)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChatDto
            {
                Time = c.Time,
                Name = c.Name,
                Message = c.Message,
                UserId = c.UserId
            })
            .ToListAsync();

        return Ok(chats);
    }

    /// <summary>
    /// 指定日時より新しいチャットを取得
    /// </summary>
    /// <param name="afterTime">この日時以降のチャットを取得</param>
    /// <returns>チャット一覧</returns>
    [HttpGet("after")]
    public async Task<ActionResult<IEnumerable<ChatDto>>> GetChatsAfter([FromQuery] DateTime afterTime)
    {
        var chats = await context.Chats
            .Where(c => c.Time > afterTime)
            .OrderBy(c => c.Time)
            .Select(c => new ChatDto
            {
                Time = c.Time,
                Name = c.Name,
                Message = c.Message,
                UserId = c.UserId
            })
            .ToListAsync();

        return Ok(chats);
    }

    /// <summary>
    /// 最新のチャット時刻を取得
    /// </summary>
    /// <returns>最新チャット時刻</returns>
    [HttpGet("latest-time")]
    public async Task<ActionResult<DateTime?>> GetLatestTime()
    {
        var latestTime = await context.Chats
            .OrderByDescending(c => c.Time)
            .Select(c => (DateTime?)c.Time)
            .FirstOrDefaultAsync();

        return Ok(latestTime);
    }

    /// <summary>
    /// 在室状況を取得
    /// </summary>
    /// <returns>在室状況</returns>
    [HttpGet("room-status")]
    [AllowAnonymous]
    public ActionResult<RoomStatusDto> GetRoomStatus()
    {
        // UserIdからユーザー名に変換
        var userNames = room.roomNames
            .Select(userId => userChatSettingCache.GetUserName(userId) ?? userId)
            .ToList();
            
        return Ok(new RoomStatusDto
        {
            RoomCount = room.roomCount,
            RoomNames = userNames,
            RoomUserIds = room.roomNames.ToList()
        });
    }

    /// <summary>
    /// チャットを送信（通常発言 - 100円獲得）
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<SendChatResponse>> SendChat([FromBody] SendChatRequest request)
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
        
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new SendChatResponse { Success = false, ErrorMessage = "メッセージを入力してください" });
        }

        // [ほのか]から始まるメッセージは送信不可
        if (request.Message.StartsWith("[ほのか]"))
        {
            return BadRequest(new SendChatResponse { Success = false, ErrorMessage = "このメッセージは送信できません" });
        }

        // チャットを保存
        var chat = new Chat
        {
            Name = userName ?? string.Empty,
            Message = request.Message,
            UserId = userId ?? string.Empty
        };
        context.Chats.Add(chat);

        // お財布に100円追加
        var osaifu = await context.Osaifus.FindAsync(userName);
        if (osaifu != null)
        {
            osaifu.Kingaku += 100;
        }
        else
        {
            context.Osaifus.Add(new Osaifu { Name = userName, Kingaku = 100 });
        }

        await context.SaveChangesAsync();

        return Ok(new SendChatResponse 
        { 
            Success = true, 
            RemainingBalance = osaifu?.Kingaku ?? 100,
            Chat = new ChatDto
            {
                Time = chat.Time,
                Name = chat.Name,
                Message = chat.Message,
                UserId = chat.UserId
            }
        });
    }

    /// <summary>
    /// ネタをランダムで取得（ほのかが質問）
    /// </summary>
    [HttpPost("neta")]
    public async Task<ActionResult<SendChatResponse>> GetRandomNeta()
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        var netas = await context.NetaMastar.ToListAsync();
        if (netas.Count == 0)
        {
            return Ok(new SendChatResponse { Success = false, ErrorMessage = "ネタがありません" });
        }

        var randomNeta = netas.RandomElementAt();
        var message = $"[ほのか] : {userName}さん{randomNeta.Neta}";

        // チャットを保存
        var chat = new Chat
        {
            Name = userName ?? string.Empty,
            Message = message,
            UserId = userId ?? string.Empty
        };
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        return Ok(new SendChatResponse 
        { 
            Success = true, 
            Chat = new ChatDto
            {
                Time = chat.Time,
                Name = chat.Name,
                Message = chat.Message,
                UserId = chat.UserId
            }
        });
    }

    /// <summary>
    /// 入室（おかえりメッセージ生成）
    /// </summary>
    [HttpPost("enter")]
    public async Task<ActionResult<SendChatResponse>> Enter()
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        // ルームに追加（Roomクラスのメソッドを使用）
        if (!string.IsNullOrEmpty(userName))
        {
            room.SendMsg(userName);
        }

        // 入室メッセージを保存
        var message = $"[ほのか] : {userName}さん おかえりなさい";
        var chat = new Chat
        {
            Name = userName ?? string.Empty,
            Message = message,
            UserId = userId ?? string.Empty
        };
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        return Ok(new SendChatResponse 
        { 
            Success = true, 
            Chat = new ChatDto
            {
                Time = chat.Time,
                Name = chat.Name,
                Message = chat.Message,
                UserId = chat.UserId
            }
        });
    }

    /// <summary>
    /// 退室
    /// </summary>
    [HttpPost("exit")]
    public async Task<ActionResult<SendChatResponse>> Exit()
    {
        var userName = User.Identity?.Name;
        var userId = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        // ルームから削除（Roomクラスのメソッドを使用）
        if (!string.IsNullOrEmpty(userName))
        {
            room.LeaveRoom(userName);
        }

        // 退室メッセージを保存
        var message = $"[ほのか] : {userName}さん いってらっしゃい";
        var chat = new Chat
        {
            Name = userName ?? string.Empty,
            Message = message,
            UserId = userId ?? string.Empty
        };
        context.Chats.Add(chat);
        await context.SaveChangesAsync();

        return Ok(new SendChatResponse 
        { 
            Success = true, 
            Chat = new ChatDto
            {
                Time = chat.Time,
                Name = chat.Name,
                Message = chat.Message,
                UserId = chat.UserId
            }
        });
    }
}
