namespace BlazorServerChat2.Components.Account;

/// <summary>
/// パスキー入力モデル - パスキーサインイン操作とパスキー追加で使用
/// </summary>
public class PasskeyInputModel
{
    /// <summary>
    /// JSONパスキー資格情報
    /// </summary>
    public string? CredentialJson { get; set; }

    /// <summary>
    /// エラーメッセージ
    /// </summary>
    public string? Error { get; set; }
}
