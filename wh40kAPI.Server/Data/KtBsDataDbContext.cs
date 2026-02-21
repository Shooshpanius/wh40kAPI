using Microsoft.EntityFrameworkCore;
using wh40kAPI.Server.Models.KtBsData;

namespace wh40kAPI.Server.Data;

public class KtBsDataDbContext(DbContextOptions<KtBsDataDbContext> options) : DbContext(options)
{
    public DbSet<KtBsDataCatalogue> Catalogues => Set<KtBsDataCatalogue>();
    public DbSet<KtBsDataUnit> Units => Set<KtBsDataUnit>();
    public DbSet<KtBsDataProfile> Profiles => Set<KtBsDataProfile>();
}
