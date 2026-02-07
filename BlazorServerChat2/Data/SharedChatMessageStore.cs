using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BlazorServerChat2.Data;

/// <summary>
/// 共有のインメモリチャット履歴ストア
/// 通常チャットとグループチャットで履歴を共有できるようにする
/// </summary>
/// <remarks>
/// Agent Frameworkの ChatHistoryProvider を継承して実装
/// InvokingAsync/InvokedAsync で履歴の取得・追加を行う
/// </remarks>
public sealed class SharedChatMessageStore : ChatHistoryProvider
{
    private readonly List<ChatMessageRecord> _messages = [];
    private readonly object _lock = new();
    private string? _threadKey;

    /// <summary>
    /// 新規スレッド用コンストラクタ
    /// </summary>
    public SharedChatMessageStore()
    {
        _threadKey = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// シリアル化された状態から復元するコンストラクタ
    /// </summary>
    /// <param name="serializedStoreState">シリアル化された状態</param>
    /// <param name="jsonSerializerOptions">JSONシリアライザオプション</param>
    public SharedChatMessageStore(
        JsonElement serializedStoreState,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (serializedStoreState.ValueKind is JsonValueKind.String)
        {
            _threadKey = serializedStoreState.Deserialize<string>(jsonSerializerOptions);
        }
        _threadKey ??= Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// スレッドの一意キー
    /// </summary>
    public string ThreadKey => _threadKey ?? throw new InvalidOperationException("ThreadKey is not initialized");

    /// <summary>
    /// エージェント実行後にリクエスト/レスポンスメッセージを履歴に追加する
    /// </summary>
    public override ValueTask InvokedAsync(
        ChatHistoryProvider.InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // リクエストメッセージを追加
            foreach (var message in context.RequestMessages)
            {
                _messages.Add(new ChatMessageRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = message
                });
            }
            // レスポンスメッセージを追加
            foreach (var message in context.ResponseMessages)
            {
                _messages.Add(new ChatMessageRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = message
                });
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 外部からメッセージを直接追加する（グループチャット履歴用）
    /// </summary>
    /// <param name="role">チャットロール</param>
    /// <param name="content">メッセージ内容</param>
    public void AddMessage(ChatRole role, string content)
    {
        lock (_lock)
        {
            _messages.Add(new ChatMessageRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTimeOffset.UtcNow,
                Message = new ChatMessage(role, content)
            });
        }
    }

    /// <summary>
    /// エージェント実行前に履歴メッセージを取得する（昇順で返す）
    /// </summary>
    public override ValueTask<IEnumerable<ChatMessage>> InvokingAsync(
        ChatHistoryProvider.InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // タイムスタンプ昇順でソートして返す
            // モデルのコンテキスト制限を考慮して最新50件に制限
            var result = _messages
                .OrderBy(x => x.Timestamp)
                .TakeLast(50)
                .Select(x => x.Message)
                .ToList();

            return ValueTask.FromResult<IEnumerable<ChatMessage>>(result);
        }
    }

    /// <summary>
    /// 状態をシリアル化する
    /// </summary>
    public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        return JsonSerializer.SerializeToElement(_threadKey, jsonSerializerOptions);
    }

    /// <summary>
    /// 現在のメッセージ数を取得
    /// </summary>
    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }

    /// <summary>
    /// 履歴をクリアする
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
        }
    }

    /// <summary>
    /// メッセージレコード（内部用）
    /// </summary>
    private sealed class ChatMessageRecord
    {
        public required string Id { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
        public required ChatMessage Message { get; init; }
    }
}
