namespace BlazorServerChat2.Components.Account;

/// <summary>
/// パスキー操作の種類を定義
/// </summary>
public enum PasskeyOperation
{
    /// <summary>
    /// 新しいパスキーを登録
    /// </summary>
    Create = 0,

    /// <summary>
    /// 既存のパスキーで認証
    /// </summary>
    Request = 1,
}
