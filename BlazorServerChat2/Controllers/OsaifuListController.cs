using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// お財布リストAPI（認証不要・全員表示用）
/// </summary>
[ApiController]
[Route("api/osaifu-list")]
public class OsaifuListController(ApplicationDbContext context) : ControllerBase
{
    /// <summary>
    /// 全員のお財布一覧を取得
    /// </summary>
    [HttpGet]
    public ActionResult<List<OsaifuDto>> GetAllOsaifus()
    {
        var osaifus = context.Osaifus
            .Select(o => new OsaifuDto
            {
                Name = o.Name ?? string.Empty,
                Kingaku = o.Kingaku
            })
            .ToList();

        return Ok(osaifus);
    }
}
