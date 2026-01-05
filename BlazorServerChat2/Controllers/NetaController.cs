using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlazorServerChat2.Controllers;

/// <summary>
/// ネタ帳API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NetaController(ApplicationDbContext context) : ControllerBase
{
    /// <summary>
    /// ネタ一覧を取得
    /// </summary>
    [HttpGet]
    public ActionResult<List<NetaDto>> GetNetas()
    {
        var netas = context.NetaMastar
            .OrderByDescending(n => n.CreateDate)
            .Select(n => new NetaDto
            {
                NetaId = n.NetaId,
                Neta = n.Neta,
                CreateDate = n.CreateDate
            })
            .ToList();

        return Ok(netas);
    }

    /// <summary>
    /// ネタを追加
    /// </summary>
    [HttpPost]
    public ActionResult<NetaDto> AddNeta([FromBody] AddNetaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Neta))
        {
            return BadRequest("ネタを入力してください");
        }

        var neta = new NetaMastar
        {
            Neta = request.Neta,
            CreateDate = DateTime.Now
        };

        context.NetaMastar.Add(neta);
        context.SaveChanges();

        return Ok(new NetaDto
        {
            NetaId = neta.NetaId,
            Neta = neta.Neta,
            CreateDate = neta.CreateDate
        });
    }
}

/// <summary>
/// ネタ追加リクエスト
/// </summary>
public class AddNetaRequest
{
    /// <summary>
    /// ネタ内容
    /// </summary>
    public string? Neta { get; set; }
}
