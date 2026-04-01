namespace BlazorServerChat2.Data;

/// <summary>
/// AIエージェントロジックのインターフェース（テスト容易性のため）
/// </summary>
public interface IAgentFrameworkLogic
{
    /// <summary>シングルエージェント（ほのか）への問いかけ。HTML文字列を返す。</summary>
    Task<string> Run(string input);

    /// <summary>グループチャット（ほのか＆みずき）。各エージェントの応答をコールバックで通知する。</summary>
    Task RunGroupChat(string input, Func<string, string, Task> onMessageReceived);

    /// <summary>会話履歴をクリアして新しいスレッドを開始する。</summary>
    Task ClearAsync();

    /// <summary>ユーザーメッセージを履歴に追加する（AI応答なし）。</summary>
    void NonGenerateMessage(string input);

    /// <summary>アシスタントメッセージを履歴に追加する。</summary>
    void AddAssistantMessage(string message);
}
