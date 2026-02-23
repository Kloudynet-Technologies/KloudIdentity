using KN.KloudIdentity.Mapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer.Configurations;

public class AppConfigSnapshotConfigurations : IEntityTypeConfiguration<AppConfigSnapshot>
{
    public void Configure(EntityTypeBuilder<AppConfigSnapshot> builder)
    {
        builder.ToTable("AppConfigSnapshots");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(x => x.AppId)
            .HasColumnName("AppId")
            .HasColumnType("nvarchar(50)")
            .IsRequired();
        
        builder.Property(x => x.Etag)
            .HasColumnName("Etag")
            .HasColumnType("nvarchar(512)")
            .IsRequired();
        
        builder.Property(x => x.ConfigJson)
            .HasColumnName("ConfigJson")
            .HasColumnType("nvarchar(max)")
            .IsRequired();
        
        builder.Property(x => x.CreatedBy)
            .HasColumnName("CreatedBy")
            .HasColumnType("nvarchar(50)")
            .IsRequired();
        
        builder.Property(x => x.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime2")
            .IsRequired();
        
        builder.Property(x => x.ModifiedBy)
            .HasColumnName("ModifiedBy")
            .HasColumnType("nvarchar(50)")
            .IsRequired(false);
        
        builder.Property(x => x.ModifiedDate)
            .HasColumnName("ModifiedDate")
            .HasColumnType("datetime2")
            .IsRequired(false);
    }
}