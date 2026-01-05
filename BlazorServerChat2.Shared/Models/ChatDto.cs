namespace BlazorServerChat2.Shared.Models;

/// <summary>
/// チャットメッセージDTO
/// </summary>
public class ChatDto
{
    /// <summary>
    /// 発言日時
    /// </summary>
    public DateTime Time { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 発言者名
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 発言内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// 発言者ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// チャット送信リクエスト
/// </summary>
public class SendChatRequest
{
    /// <summary>
    /// メッセージ内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// AIチャットリクエスト（ほのかへ話しかける）
/// </summary>
public class AiChatRequest
{
    /// <summary>
    /// メッセージ内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// グループチャットリクエスト（ほのか＋みずき）
/// </summary>
public class GroupChatRequest
{
    /// <summary>
    /// メッセージ内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// AIチャットレスポンス
/// </summary>
public class AiChatResponse
{
    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// AIからの応答メッセージ
    /// </summary>
    public string Response { get; set; } = string.Empty;
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// グループチャットの各エージェントの応答
    /// </summary>
    public List<string>? GroupResponses { get; set; }
    
    /// <summary>
    /// 残高
    /// </summary>
    public int RemainingBalance { get; set; }
}

/// <summary>
/// チャット送信レスポンス
/// </summary>
public class SendChatResponse
{
    /// <summary>
    /// 成功フラグ
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 残高
    /// </summary>
    public int RemainingBalance { get; set; }
    
    /// <summary>
    /// 送信されたチャット
    /// </summary>
    public ChatDto? Chat { get; set; }
}
