using BlazorServerChat2.Data;
using BlazorServerChat2.Hubs;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// AI関連のWeb APIコントローラー
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController(
    AgentFrameworkLogic agentLogic,
    ApplicationDbContext context,
    IHubContext<BlazorChatHub> hubContext,
    ILogger<AiController> logger) : ControllerBase
{
    /// <summary>
    /// AI（ほのか）にメッセージを送信
    /// </summary>
    /// <param name="request">AIチャットリクエスト</param>
    /// <returns>AIのレスポンス</returns>
    [HttpPost("honoka")]
    public async Task<ActionResult<AiChatResponse>> ChatWithHonoka([FromBody] AiChatRequest request)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return Unauthorized();
        }

        logger.LogInformation("Honoka AI request from user {UserName}: {Message}", userName, request.Message);

        try
        {
            // 残高チェック（1000円必要）
            var osaifu = await context.Osaifus
                .Where(o => o.Name == userName)
                .FirstOrDefaultAsync();

            var currentBalance = osaifu?.Kingaku ?? 0;
            if (currentBalance < 1000)
            {
                return BadRequest(new AiChatResponse
                {
                    Success = false,
                    ErrorMessage = $"残高不足です（現在: {currentBalance}円、必要: 1000円）"
                });
            }

            // AI呼び出し（既存のRunメソッドを使用）
            var response = await agentLogic.Run(request.Message);

            // AIの回答をDBに保存
            var aiChat = new Chat
            {
                Time = DateTime.Now,
                Name = "ほのか",
                Message = response,
                UserId = "AI_HONOKA"
            };
            context.Chats.Add(aiChat);
            await context.SaveChangesAsync();

            // SignalRでリアルタイム送信
            await hubContext.Clients.All.SendAsync("AiMessageReceived", "ほのか", response, aiChat.Time);
            logger.LogInformation("AI response saved and broadcast: {Response}", response);

            // 残高を減らす
            if (osaifu != null)
            {
                osaifu.Kingaku -= 1000;
                await context.SaveChangesAsync();
            }

            return Ok(new AiChatResponse
            {
                Success = true,
                Response = response,
                RemainingBalance = osaifu?.Kingaku ?? 0
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Honoka AI chat");
            return StatusCode(500, new AiChatResponse
            {
                Success = false,
                ErrorMessage = "AIとの通信中にエラーが発生しました"
            });
        }
    }

    /// <summary>
    /// グループチャット（ほのか＆みずき）にメッセージを送信
    /// </summary>
    /// <param name="request">グループチャットリクエスト</param>
    /// <returns>AIのレスポンス</returns>
    [HttpPost("group")]
    public async Task<ActionResult<AiChatResponse>> ChatWithGroup([FromBody] GroupChatRequest request)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return Unauthorized();
        }

        logger.LogInformation("Group AI request from user {UserName}: {Message}", userName, request.Message);

        try
        {
            // 残高チェック（2000円必要）
            var osaifu = await context.Osaifus
                .Where(o => o.Name == userName)
                .FirstOrDefaultAsync();

            var currentBalance = osaifu?.Kingaku ?? 0;
            if (currentBalance < 2000)
            {
                return BadRequest(new AiChatResponse
                {
                    Success = false,
                    ErrorMessage = $"残高不足です（現在: {currentBalance}円、必要: 2000円）"
                });
            }

            // グループチャット呼び出し（コールバックでレスポンスを収集＆ストリーミング）
            var responses = new List<string>();
            await agentLogic.RunGroupChat(request.Message, async (agentName, message) =>
            {
                responses.Add($"【{agentName}】{message}");
                
                // AIの回答をDBに保存
                var aiChat = new Chat
                {
                    Time = DateTime.Now,
                    Name = agentName,
                    Message = message,
                    UserId = agentName == "ほのか" ? "AI_HONOKA" : "AI_MIZUKI"
                };
                context.Chats.Add(aiChat);
                await context.SaveChangesAsync();

                // SignalRでリアルタイム送信（ストリーミング）
                await hubContext.Clients.All.SendAsync("AiMessageReceived", agentName, message, aiChat.Time);
                logger.LogInformation("Group AI response saved and broadcast from {AgentName}: {Message}", agentName, message);
                
                // 少し待機してストリーミング効果を出す
                await Task.Delay(100);
            });

            // 残高を減らす
            if (osaifu != null)
            {
                osaifu.Kingaku -= 2000;
                await context.SaveChangesAsync();
            }

            return Ok(new AiChatResponse
            {
                Success = true,
                Response = string.Join("\n\n", responses),
                GroupResponses = responses,
                RemainingBalance = osaifu?.Kingaku ?? 0
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Group AI chat");
            return StatusCode(500, new AiChatResponse
            {
                Success = false,
                ErrorMessage = "AIとの通信中にエラーが発生しました"
            });
        }
    }

    /// <summary>
    /// AIチャット履歴をクリア
    /// </summary>
    /// <returns>成功状態</returns>
    [HttpPost("clear-history")]
    public ActionResult ClearHistory()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            return Unauthorized();
        }

        logger.LogInformation("Clearing chat history for user {UserName}", userName);

        // 共有履歴をクリア（現在の実装では全ユーザー共通）
        agentLogic.Clear();

        return Ok();
    }
}
