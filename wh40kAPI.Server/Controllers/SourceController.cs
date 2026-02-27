using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Data;
using wh40kAPI.Server.Models;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/wh40k/[controller]")]
public class SourceController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Source>> GetAll() =>
        await db.Sources.AsNoTracking().ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Source>> GetById(string id)
    {
        var item = await db.Sources.FindAsync(id);
        return item is null ? NotFound() : Ok(item);
    }
}
