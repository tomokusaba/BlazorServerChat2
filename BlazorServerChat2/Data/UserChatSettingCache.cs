using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace BlazorServerChat2.Data;

/// <summary>
/// ユーザーチャット設定とアイコンマスタのキャッシュサービス
/// DBアクセスを削減し、会話ごとのSQL Server負荷を軽減します 🚀
/// </summary>
public class UserChatSettingCache : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserChatSettingCache> _logger;
    
    /// <summary>
    /// ユーザーIDをキーとしたUserChatSettingのキャッシュ
    /// </summary>
    private readonly ConcurrentDictionary<string, UserChatSetting?> _userSettings = new();
    
    /// <summary>
    /// アイコン番号をキーとしたアイコンBase64文字列のキャッシュ
    /// </summary>
    private readonly ConcurrentDictionary<int, string?> _iconBase64Cache = new();
    
    /// <summary>
    /// ユーザーIDをキーとしたユーザー名のキャッシュ
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _userNameCache = new();
    
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private bool _isInitialized;

    public UserChatSettingCache(IServiceScopeFactory scopeFactory, ILogger<UserChatSettingCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 初期データを一括でロードする
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _loadSemaphore.WaitAsync();
        try
        {
            if (_isInitialized) return;

            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // UserChatSettingを一括ロード
            var settings = await dbContext.UserChatSetting.AsNoTracking().ToListAsync();
            foreach (var setting in settings)
            {
                if (setting.Id is not null)
                {
                    _userSettings[setting.Id] = setting;
                }
            }

            // IconMasterを一括ロード（Base64変換済み）
            var icons = await dbContext.IconMaster.AsNoTracking().ToListAsync();
            foreach (var icon in icons)
            {
                _iconBase64Cache[icon.IconNumber] = icon.Icon is not null 
                    ? Convert.ToBase64String(icon.Icon) 
                    : null;
            }

            _logger.LogInformation(
                "キャッシュ初期化完了: UserChatSetting {SettingCount}件, IconMaster {IconCount}件",
                settings.Count, icons.Count);

            _isInitialized = true;
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// ユーザーIDからUserChatSettingを取得する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <returns>UserChatSetting または null</returns>
    public UserChatSetting? GetUserChatSetting(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;
        
        return _userSettings.GetValueOrDefault(userId);
    }

    /// <summary>
    /// ユーザーIDからアイコンのBase64文字列を取得する
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <returns>Base64文字列 または null</returns>
    public string? GetUserIconBase64(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var setting = GetUserChatSetting(userId);
        if (setting is null || setting.IconNumber == 0) return null;

        return _iconBase64Cache.GetValueOrDefault(setting.IconNumber);
    }

    /// <summary>
    /// アイコン番号からアイコンのBase64文字列を取得する
    /// </summary>
    /// <param name="iconNumber">アイコン番号</param>
    /// <returns>Base64文字列 または null</returns>
    public string? GetIconBase64(int iconNumber)
    {
        if (iconNumber == 0) return null;
        
        return _iconBase64Cache.GetValueOrDefault(iconNumber);
    }

    /// <summary>
    /// ユーザーIDからユーザー名を取得する（遅延ロード）
    /// </summary>
    /// <param name="userId">ユーザーID</param>
    /// <returns>ユーザー名 または null</returns>
    public async Task<string?> GetUserNameAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        if (_userNameCache.TryGetValue(userId, out var cachedName))
        {
            return cachedName;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await dbContext.Users.FindAsync(userId);
        var userName = user?.ToString();
        
        _userNameCache[userId] = userName;
        return userName;
    }

    /// <summary>
    /// ユーザーIDからユーザー名を同期的に取得する
    /// </summary>
    public string? GetUserName(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        if (_userNameCache.TryGetValue(userId, out var cachedName))
        {
            return cachedName;
        }

        // キャッシュにない場合は非同期でロードする必要があるため、nullを返す
        // 後続の処理で GetUserNameAsync を呼び出すか、LoadUserNamesAsync で事前ロードする
        return null;
    }

    /// <summary>
    /// 複数のユーザーIDのユーザー名を一括ロードする
    /// </summary>
    public async Task LoadUserNamesAsync(IEnumerable<string> userIds)
    {
        var missingIds = userIds
            .Where(id => !string.IsNullOrEmpty(id) && !_userNameCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (missingIds.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var users = await dbContext.Users
            .Where(u => missingIds.Contains(u.Id))
            .AsNoTracking()
            .ToListAsync();

        foreach (var user in users)
        {
            _userNameCache[user.Id] = user.ToString();
        }

        // 見つからなかったIDはnullをキャッシュ
        foreach (var id in missingIds.Where(id => !_userNameCache.ContainsKey(id)))
        {
            _userNameCache[id] = null;
        }

        _logger.LogDebug("ユーザー名を{Count}件ロード", users.Count);
    }

    /// <summary>
    /// ユーザー設定のキャッシュを更新する
    /// </summary>
    public void UpdateUserSetting(UserChatSetting setting)
    {
        if (setting.Id is not null)
        {
            _userSettings[setting.Id] = setting;
        }
    }

    /// <summary>
    /// アイコンのキャッシュを更新する
    /// </summary>
    public void UpdateIcon(IconMaster icon)
    {
        _iconBase64Cache[icon.IconNumber] = icon.Icon is not null 
            ? Convert.ToBase64String(icon.Icon) 
            : null;
    }

    /// <summary>
    /// 全キャッシュをクリアして再ロードする
    /// </summary>
    public async Task RefreshAsync()
    {
        _isInitialized = false;
        _userSettings.Clear();
        _iconBase64Cache.Clear();
        _userNameCache.Clear();
        await InitializeAsync();
    }

    public void Dispose()
    {
        _loadSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
