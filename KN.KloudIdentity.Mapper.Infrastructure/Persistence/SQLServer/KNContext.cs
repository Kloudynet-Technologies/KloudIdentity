using KN.KloudIdentity.Mapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;

public class KNContext(DbContextOptions<KNContext> options) : DbContext(options)
{
   public DbSet<AppConfigSnapshot> AppConfigSnapshots { get; set; } = null!;
}