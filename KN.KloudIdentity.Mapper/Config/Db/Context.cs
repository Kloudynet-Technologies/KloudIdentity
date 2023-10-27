using Microsoft.EntityFrameworkCore;

namespace KN.KloudIdentity.Mapper.Config.Db;

/// <summary>
/// This class contains the database context for the application.
/// </summary>
public class Context : DbContext
{
    public Context(DbContextOptions<Context> options) : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<ConfigModel> Config { get; set; }

    public DbSet<AuthConfigModel> AuthConfig { get; set; }

    public DbSet<UserSchemaModel> UserSchema { get; set; }

    public DbSet<GroupSchemaModel> GroupSchema { get; set; }

    /// <summary>
    /// This method configures the database context.
    /// </summary>
    /// <param name="options">The options for the database context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer("Data Source=kn-kloudidentity-mapper.db");
    }
}
