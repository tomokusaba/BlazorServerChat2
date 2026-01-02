using BlazorServerChat2.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using static BlazorServerChat2.Pages.Index;

namespace BlazorServerChat2.Data
{
    /// <summary>
    /// SignalRのクライアント側接続管理クラス
    /// Blazorの接続単位にDIすることを想定
    /// IAsyncDisposableを実装してリソースを適切に解放
    /// </summary>
    public class ClientHub : IAsyncDisposable
    {
        private HubConnection? _hubConnection;
        private string? _hubUrl;
        /// <summary>
        /// チャットのメッセージリスト
        /// </summary>
        public List<Message> _messages = [];
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly NavigationManager _navigationManager;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private string? _username;
        private string? UserId;
        private readonly Room _room;
        public event Action? OnChange;
        public int Room = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="navigation"></param>
        /// <param name="authentication"></param>
        /// <param name="dbContextFactory">EF DbContextFactory</param>
        /// <param name="room">チャットルーム在室人数管理クラス</param>
        public ClientHub(NavigationManager navigation, AuthenticationStateProvider authentication, IDbContextFactory<ApplicationDbContext> dbContextFactory, Room room)
        {
            _authenticationStateProvider = authentication;
            _navigationManager = navigation;
            _dbContextFactory = dbContextFactory;
            _room = room;
        }

        /// <summary>
        /// 接続状態を取得
        /// </summary>
        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        /// <summary>
        /// ページ初期表示に呼び出すメソッド
        /// SignalRHubへのコネクションがなければ張り直す
        /// 入室メッセージを表示する
        /// </summary>
        /// <returns></returns>
        public async Task InitIndexPage()
        {
            string baseUrl = _navigationManager.BaseUri;

            _hubUrl = baseUrl.TrimEnd('/') + BlazorChatHub.HubUrl;

            if (_hubConnection is null)
            {
                // 最新のHubConnectionBuilder設定（Microsoft Learn推奨）
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .WithServerTimeout(TimeSpan.FromSeconds(60))      // サーバータイムアウト（デフォルト30秒）
                    .WithKeepAliveInterval(TimeSpan.FromSeconds(15))  // KeepAlive間隔（デフォルト15秒）
                    .WithAutomaticReconnect()                          // 自動再接続を有効化
                    .Build();

                // ハンドシェイクタイムアウト設定
                _hubConnection.HandshakeTimeout = TimeSpan.FromSeconds(30);

                // メッセージ受信ハンドラ登録
                _hubConnection.On<string, Message>("Broadcast", BroadcastMessage);

                // 再接続イベントハンドラ
                _hubConnection.Reconnecting += error =>
                {
                    Console.WriteLine($"SignalR再接続中: {error?.Message}");
                    return Task.CompletedTask;
                };

                _hubConnection.Reconnected += connectionId =>
                {
                    Console.WriteLine($"SignalR再接続完了: {connectionId}");
                    return Task.CompletedTask;
                };

                _hubConnection.Closed += error =>
                {
                    Console.WriteLine($"SignalR接続終了: {error?.Message}");
                    return Task.CompletedTask;
                };

                await _hubConnection.StartAsync();

                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                _username = authState.User.Identity?.Name;
                UserId = authState.User.Claims
                    .Where(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                    .Select(x => x.Value)
                    .FirstOrDefault();

                Message message = new(_username ?? string.Empty, $"[ほのか] {_username} さんおかえりなさい", false, UserId ?? string.Empty);

                Chat chat = new()
                {
                    Message = message.Body,
                    Name = _username ?? string.Empty,
                    UserId = UserId ?? string.Empty
                };

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                dbContext.Chats.Add(chat);
                await dbContext.SaveChangesAsync();

                await SendAsync(message);
            }
        }

        /// <summary>
        /// メッセージ送信
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <returns></returns>
        public async Task SendAsync(Message message)
        {
            if (_hubConnection is not null && IsConnected)
            {
                _room.SendMsg(message.UserId);
                await _hubConnection.SendAsync("Broadcast", message.Username, message);
                Room = _room.room.Count;
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// ハブコネクション切断
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                Room = _room.room.Count;
                NotifyStateChanged();
            }
        }

        /// <summary>
        /// SignalRハブから呼び出されるメソッド
        /// 送られてきたメッセージを元に表示するメッセージリストに追加する
        /// </summary>
        /// <param name="name">送信元名前</param>
        /// <param name="message">メッセージ</param>
        public void BroadcastMessage(string name, Message message)
        {
            bool isMine = name.Equals(_username, StringComparison.OrdinalIgnoreCase);

            _messages.Add(new Message(name, message.Body, isMine, message.UserId));
            Room = _room.room.Count;
            try
            {
                NotifyStateChanged();
            }
            catch (Exception)
            {
                // 通知失敗時は無視
            }
        }

        /// <summary>
        /// 変更通知イベント
        /// </summary>
        private void NotifyStateChanged() => OnChange?.Invoke();

        /// <summary>
        /// IAsyncDisposable実装
        /// リソースを適切に解放
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_hubConnection is not null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
    }
}
