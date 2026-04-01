namespace BlazorServerChat2.Data;

/// <summary>
/// ユーザーチャット設定キャッシュのインターフェース（テスト容易性のため）
/// </summary>
public interface IUserChatSettingCache
{
    Task InitializeAsync();
    UserChatSetting? GetUserChatSetting(string? userId);
    string? GetUserIconBase64(string? userId);
    string? GetIconBase64(int iconNumber);
    Task<string?> GetUserNameAsync(string? userId);
    string? GetUserName(string? userId);
    Task LoadUserNamesAsync(IEnumerable<string> userIds);
    void UpdateUserSetting(UserChatSetting setting);
    void UpdateIcon(IconMaster icon);
    Task RefreshAsync();
}
