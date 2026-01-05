using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using BlazorServerChat2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static BlazorServerChat2.Pages.Index;
using System.Collections.Concurrent;
using BlazorServerChat2.Shared.Models;

namespace BlazorServerChat2.Hubs
{
    /// <summary>
    /// チャット用SignalRハブ
    /// </summary>
    public class BlazorChatHub : Hub
    {
        public const string HubUrl = "/chat";
        private readonly ILogger<BlazorChatHub> _logger;

        public BlazorChatHub(ILogger<BlazorChatHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// クライアントへメッセージ送信
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Broadcast(string username, Message message)
        {
            _logger.LogDebug("Broadcast from {Username}: {Message}", username, message.Body);
            await Clients.All.SendAsync("Broadcast", username, message);
        }

        /// <summary>
        /// AIメッセージをストリーミング送信（全クライアントへ）
        /// </summary>
        public async Task BroadcastAiMessage(string agentName, string message, DateTime postTime)
        {
            _logger.LogDebug("AI Broadcast from {AgentName}: {Message}", agentName, message);
            await Clients.All.SendAsync("AiMessageReceived", agentName, message, postTime);
        }

        /// <summary>
        /// コネクション接続時
        /// </summary>
        /// <returns></returns>
        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("{ConnectionId} connected", Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// コネクション切断時
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public override async Task OnDisconnectedAsync(Exception? e)
        {
            if (e is not null)
            {
                _logger.LogWarning(e, "Disconnected with error: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("Disconnected: {ConnectionId}", Context.ConnectionId);
            }
            await base.OnDisconnectedAsync(e);
        }
    }
}
