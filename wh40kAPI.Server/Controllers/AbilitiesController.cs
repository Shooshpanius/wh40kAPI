using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AbilitiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Ability>> GetAll([FromQuery] string? factionId) =>
        await db.Abilities.AsNoTracking()
            .Where(a => factionId == null || a.FactionId == factionId)
            .ToListAsync();
}
