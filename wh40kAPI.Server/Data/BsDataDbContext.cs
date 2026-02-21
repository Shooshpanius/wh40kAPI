using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models.BsData;

namespace wh40kAPI.Server.Data;

public class BsDataDbContext(DbContextOptions<BsDataDbContext> options) : DbContext(options)
{
    public DbSet<BsDataCatalogue> Catalogues => Set<BsDataCatalogue>();
    public DbSet<BsDataUnit> Units => Set<BsDataUnit>();
    public DbSet<BsDataProfile> Profiles => Set<BsDataProfile>();
}
