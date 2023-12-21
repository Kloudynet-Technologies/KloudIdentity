//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Config.Db;

/// <summary>
/// This class contains the database context for the application.
/// </summary>
public class Context : DbContext
{
    private readonly IConfiguration configuration;

    public Context(DbContextOptions<Context> options, IConfiguration configuration)
        : base(options)
    {
        this.configuration = configuration;
        // Database.EnsureCreated();
    }

    public DbSet<AppConfigModel> AppConfig { get; set; }

    public DbSet<AuthConfigModel> AuthConfig { get; set; }

    public DbSet<UserSchemaModel> UserSchema { get; set; }

    public DbSet<GroupSchemaModel> GroupSchema { get; set; }

    public DbSet<UserIdMapperModel> UserIdMap { get; set; }

    /// <summary>
    /// This method configures the database context.
    /// </summary>
    /// <param name="options">The options for the database context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(this.configuration.GetConnectionString("DefaultConnection"));
    }
}
