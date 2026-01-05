namespace BlazorServerChat2.Shared.Models;

/// <summary>
/// お財布DTO
/// </summary>
public class OsaifuDto
{
    /// <summary>
    /// ユーザーID
    /// </summary>
    public string? UserId { get; set; }
    
    /// <summary>
    /// ユーザー名
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// 金額
    /// </summary>
    public int Kingaku { get; set; }
}

/// <summary>
/// お財布更新リクエスト
/// </summary>
public class UpdateOsaifuRequest
{
    /// <summary>
    /// 増減する金額（正で増加、負で減少）
    /// </summary>
    public int Amount { get; set; }
}
