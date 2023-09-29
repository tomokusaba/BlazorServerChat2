using BlazorServerChat2.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using static BlazorServerChat2.Pages.Index;

namespace BlazorServerChat2.Data
{
    /// <summary>
    /// SignalRのクライアント側接続管理クラス
    /// Blazorの接続単位にDIすることを想定
    /// </summary>
    public class ClientHub : NavigationManager
    {
        private HubConnection? _hubConnection;
        private string? _hubUrl;
        /// <summary>
        /// チャットのメッセージリスト
        /// </summary>
        public List<Message> _messages = new List<Message>();
        private AuthenticationStateProvider _authenticationStateProvider;
        private NavigationManager _navigationManager;
        private ApplicationDbContext _applicationDbContext;
        private string? _username;
        private string? UserId;
        private Room _room;
        public event Action? OnChange;
        public int Room = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="navigation"></param>
        /// <param name="authentication"></param>
        /// <param name="DbContect">EF</param>
        /// <param name="room">チャットルーム在室人数管理クラス</param>
        public ClientHub(NavigationManager navigation, AuthenticationStateProvider authentication, ApplicationDbContext DbContect, Room room)
        {
            _authenticationStateProvider = authentication;
            _navigationManager = navigation;
            _applicationDbContext = DbContect;
            _room = room;
        }


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

            if (_hubConnection == null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl)
                    .Build();

                _hubConnection.On<string, Message>("Broadcast", BroadcastMessage);


                await _hubConnection.StartAsync();


                var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
                _username = authState.User.Identity?.Name;
                UserId = authState.User.Claims.Where(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Select(x => x.Value).FirstOrDefault();

                Message message = new Message(_username ?? string.Empty, $"[ほのか] {_username} さんおかえりなさい", false, UserId ?? string.Empty);

                Chat chat = new Chat();
                chat.Message = message.Body;
                chat.Name = _username ?? string.Empty;
                chat.UserId = UserId ?? string.Empty;
                _applicationDbContext.Chats.Add(chat);
                await _applicationDbContext.SaveChangesAsync();

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
            _room.SendMsg(message.UserId);
            await _hubConnection!.SendAsync("Broadcast", message.Username, message);
            Room = _room.room.Count;
            NotifyStateChanged();
        }

        /// <summary>
        /// ハブコネクション切断
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            await _hubConnection!.StopAsync();
            await _hubConnection.DisposeAsync();
            Room = _room.room.Count;
            NotifyStateChanged();

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
                
            } catch (Exception) { }

        }

        /// <summary>
        /// 変更通知イベント
        /// </summary>
        private void NotifyStateChanged() => OnChange?.Invoke();

    }
}
