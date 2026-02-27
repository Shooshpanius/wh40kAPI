using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/wh40k/[controller]")]
public class DetachmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Detachment>> GetAll([FromQuery] string? factionId) =>
        await db.Detachments.AsNoTracking()
            .Where(d => factionId == null || d.FactionId == factionId)
            .ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Detachment>> GetById(string id)
    {
        var item = await db.Detachments.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id}/abilities")]
    public async Task<IEnumerable<DetachmentAbility>> GetAbilities(string id) =>
        await db.DetachmentAbilities.AsNoTracking()
            .Where(a => a.DetachmentId == id)
            .ToListAsync();
}
