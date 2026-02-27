using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/wh40k/[controller]")]
public class FactionsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Faction>> GetAll() =>
        await db.Factions.AsNoTracking().ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Faction>> GetById(string id)
    {
        var faction = await db.Factions.FindAsync(id);
        return faction is null ? NotFound() : Ok(faction);
    }
}
