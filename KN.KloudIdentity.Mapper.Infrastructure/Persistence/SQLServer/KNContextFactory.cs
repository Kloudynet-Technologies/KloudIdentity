using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer
{
    public class KNContextFactory : IDesignTimeDbContextFactory<KNContext>
    {
        public KNContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<KNContext>();
            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ScimConnectorDb;User Id=sa;Password=Password@123;TrustServerCertificate=True;");
            return new KNContext(optionsBuilder.Options);
        }
    }
}
