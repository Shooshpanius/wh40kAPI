using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "wh40k")]
[Route("api/wh40k/[controller]")]
public class EnhancementsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Enhancement>> GetAll([FromQuery] string? factionId) =>
        await db.Enhancements.AsNoTracking()
            .Where(e => factionId == null || e.FactionId == factionId)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Enhancement>> GetById(string id)
    {
        var item = await db.Enhancements.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }
}
