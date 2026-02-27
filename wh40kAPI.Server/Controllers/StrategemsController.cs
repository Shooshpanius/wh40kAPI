using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/wh40k/[controller]")]
public class StrategemsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Stratagem>> GetAll([FromQuery] string? factionId) =>
        await db.Stratagems.AsNoTracking()
            .Where(s => factionId == null || s.FactionId == factionId)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Stratagem>> GetById(string id)
    {
        var item = await db.Stratagems.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }
}
