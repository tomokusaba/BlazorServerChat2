using BlazorServerChat2.Data;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlazorServerChat2.Mcp;

/// <summary>
/// MCPサーバーとして公開するAIチャットツール
/// ほのかとみずきへの会話機能を提供します 🎀
/// </summary>
[McpServerToolType]
public class ChatMcpTools
{
    private readonly AgentFrameworkLogic _agentLogic;
    private readonly ILogger<ChatMcpTools> _logger;

    public ChatMcpTools(AgentFrameworkLogic agentLogic, ILogger<ChatMcpTools> logger)
    {
        _agentLogic = agentLogic;
        _logger = logger;
    }

    /// <summary>
    /// ほのかに話しかけて応答を得る
    /// </summary>
    /// <param name="message">ほのかへのメッセージ</param>
    /// <returns>ほのかからの応答（HTML形式）</returns>
    [McpServerTool(Name = "TalkToHonoka")]
    [Description("ほのかというAIアシスタントに話しかけます。くだけた女性の口調で人に役立つ回答をします。")]
    public async Task<string> TalkToHonoka(
        [Description("ほのかに伝えるメッセージ")] string message)
    {
        _logger.LogInformation("[MCP] TalkToHonoka called with message: {Message}", message);
        
        try
        {
            var response = await _agentLogic.Run(message);
            _logger.LogInformation("[MCP] TalkToHonoka response: {Response}", response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP] TalkToHonoka error");
            return $"<p>ごめんなさい、エラーが発生したよ〜😢: {ex.Message}</p>";
        }
    }

    /// <summary>
    /// ほのかとみずきのグループチャットを開始する
    /// </summary>
    /// <param name="message">グループチャットへのメッセージ</param>
    /// <returns>グループチャットの全応答（JSON形式）</returns>
    [McpServerTool(Name = "TalkToGroup")]
    [Description("ほのかとみずきの2人のAIアシスタントによるグループチャットを開始します。最大5回のやり取りが行われます。")]
    public async Task<string> TalkToGroup(
        [Description("グループチャットに投げかけるメッセージ")] string message)
    {
        _logger.LogInformation("[MCP] TalkToGroup called with message: {Message}", message);
        
        try
        {
            var responses = new List<GroupChatResponse>();
            
            await _agentLogic.RunGroupChat(message, async (agentName, htmlMessage) =>
            {
                responses.Add(new GroupChatResponse
                {
                    AgentName = agentName,
                    Message = htmlMessage
                });
                await Task.CompletedTask;
            });

            // JSON形式で結果を返す
            var result = System.Text.Json.JsonSerializer.Serialize(responses, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            _logger.LogInformation("[MCP] TalkToGroup completed with {Count} responses", responses.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP] TalkToGroup error");
            return System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new GroupChatResponse
                {
                    AgentName = "ほのか",
                    Message = $"<p>ごめんなさい、グループチャットでエラーが発生したよ〜😢: {ex.Message}</p>"
                }
            });
        }
    }

    /// <summary>
    /// チャット履歴をクリアする
    /// </summary>
    [McpServerTool(Name = "ClearChatHistory")]
    [Description("ほのかとみずきのチャット履歴をクリアして、新しい会話を開始します。")]
    public string ClearChatHistory()
    {
        _logger.LogInformation("[MCP] ClearChatHistory called");
        
        try
        {
            _agentLogic.Clear();
            return "チャット履歴をクリアしました！新しい会話を始められるよ〜✨";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MCP] ClearChatHistory error");
            return $"エラーが発生しました: {ex.Message}";
        }
    }

    /// <summary>
    /// グループチャットの応答を表す内部クラス
    /// </summary>
    private class GroupChatResponse
    {
        public string AgentName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
