namespace BlazorServerChat2.Shared.Models;

/// <summary>
/// ユーザーチャット設定DTO
/// </summary>
public class UserChatSettingDto
{
    /// <summary>
    /// ユーザーID
    /// </summary>
    public string? Id { get; set; }
    
    /// <summary>
    /// アイコン番号
    /// </summary>
    public int IconNumber { get; set; }
    
    /// <summary>
    /// 背景色
    /// </summary>
    public string BackGroundColor { get; set; } = string.Empty;
}

/// <summary>
/// アイコンDTO
/// </summary>
public class IconDto
{
    /// <summary>
    /// ユーザーID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// アイコン番号
    /// </summary>
    public int IconNumber { get; set; }
    
    /// <summary>
    /// アイコンデータ（Base64）
    /// </summary>
    public string? IconBase64 { get; set; }
    
    /// <summary>
    /// アイコン名
    /// </summary>
    public string IconName { get; set; } = string.Empty;
}

/// <summary>
/// ネタDTO
/// </summary>
public class NetaDto
{
    /// <summary>
    /// ネタID
    /// </summary>
    public int NetaId { get; set; }
    
    /// <summary>
    /// ネタ内容
    /// </summary>
    public string? Neta { get; set; }
    
    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreateDate { get; set; }
}

/// <summary>
/// 在室者情報DTO
/// </summary>
public class RoomStatusDto
{
    /// <summary>
    /// 在室人数
    /// </summary>
    public int RoomCount { get; set; }
    
    /// <summary>
    /// 在室者名リスト（表示用のユーザー名）
    /// </summary>
    public List<string> RoomNames { get; set; } = [];
    
    /// <summary>
    /// 在室者IDリスト（内部用）
    /// </summary>
    public List<string> RoomUserIds { get; set; } = [];
}
