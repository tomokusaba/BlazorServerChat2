using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using BlazorServerChat2.Data;
using Microsoft.EntityFrameworkCore;
using static BlazorServerChat2.Pages.Index;
using System.Collections.Concurrent;

namespace BlazorServerChat2.Hubs
{
    public class BlazorChatHub : Hub
    {
        public const string HubUrl = "/chat";

        /// <summary>
        /// クライアントへメッセージ送信
        /// </summary>
        /// <param name="username"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task Broadcast(string username, Message message)
        {
            
            await Clients.All.SendAsync("Broadcast", username, message);
        }

        /// <summary>
        /// コネクション接続時
        /// </summary>
        /// <returns></returns>
        public override  Task OnConnectedAsync()
        {
            Console.WriteLine($"{Context.ConnectionId} connected");
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// コネクション切断時
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public override async Task OnDisconnectedAsync(Exception? e)
        {
            Console.WriteLine($"Disconnected {e?.Message} {Context.ConnectionId}");
            await base.OnDisconnectedAsync(e);
        }
    }
}
