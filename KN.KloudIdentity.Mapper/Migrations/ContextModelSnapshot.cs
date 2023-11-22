﻿// <auto-generated />
using System;
using KN.KloudIdentity.Mapper.Config.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    [DbContext(typeof(Context))]
    partial class ContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.13")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("KN.KloudIdentity.Mapper.AppConfigModel", b =>
                {
                    b.Property<string>("AppId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("DELETEAPIForUsers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GETAPIForUsers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GroupProvisioningApiUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("LISTAPIForUsers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PATCHAPIForUsers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PUTAPIForUsers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("UserProvisioningApiUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("AppId");

                    b.ToTable("AppConfig");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.AuthConfigModel", b =>
                {
                    b.Property<string>("AppId")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ApiKey")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ApiKeyHeader")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("AuthenticationMethod")
                        .HasColumnType("int");

                    b.Property<string>("Authority")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClientId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClientSecret")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GrantType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("OAuth2TokenUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Password")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RedirectUri")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Scope")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Token")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Username")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("AppId");

                    b.ToTable("AuthConfig");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.GroupSchemaModel", b =>
                {
                    b.Property<int>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ID"));

                    b.Property<string>("AppId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ArrayElementMappingField")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ArrayElementType")
                        .HasColumnType("int");

                    b.Property<int>("DataType")
                        .HasColumnType("int");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("bit");

                    b.Property<string>("MappedAttribute")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ParentId")
                        .HasColumnType("int");

                    b.HasKey("ID");

                    b.HasIndex("AppId");

                    b.HasIndex("ParentId");

                    b.ToTable("GroupSchema");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.UserSchemaModel", b =>
                {
                    b.Property<int>("ID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ID"));

                    b.Property<string>("AppId")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ArrayElementMappingField")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ArrayElementType")
                        .HasColumnType("int");

                    b.Property<int>("DataType")
                        .HasColumnType("int");

                    b.Property<string>("FieldName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsRequired")
                        .HasColumnType("bit");

                    b.Property<string>("MappedAttribute")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("ParentId")
                        .HasColumnType("int");

                    b.HasKey("ID");

                    b.HasIndex("AppId");

                    b.HasIndex("ParentId");

                    b.ToTable("UserSchema");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.AuthConfigModel", b =>
                {
                    b.HasOne("KN.KloudIdentity.Mapper.AppConfigModel", "ConfigModel")
                        .WithOne("AuthConfig")
                        .HasForeignKey("KN.KloudIdentity.Mapper.AuthConfigModel", "AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("ConfigModel");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.GroupSchemaModel", b =>
                {
                    b.HasOne("KN.KloudIdentity.Mapper.AppConfigModel", "AppConfigModel")
                        .WithMany("GroupSchema")
                        .HasForeignKey("AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("KN.KloudIdentity.Mapper.GroupSchemaModel", "ParentSchema")
                        .WithMany("ChildSchemas")
                        .HasForeignKey("ParentId");

                    b.Navigation("AppConfigModel");

                    b.Navigation("ParentSchema");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.UserSchemaModel", b =>
                {
                    b.HasOne("KN.KloudIdentity.Mapper.AppConfigModel", "AppConfigModel")
                        .WithMany("UserSchema")
                        .HasForeignKey("AppId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("KN.KloudIdentity.Mapper.UserSchemaModel", "ParentSchema")
                        .WithMany("ChildSchemas")
                        .HasForeignKey("ParentId");

                    b.Navigation("AppConfigModel");

                    b.Navigation("ParentSchema");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.AppConfigModel", b =>
                {
                    b.Navigation("AuthConfig")
                        .IsRequired();

                    b.Navigation("GroupSchema");

                    b.Navigation("UserSchema");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.GroupSchemaModel", b =>
                {
                    b.Navigation("ChildSchemas");
                });

            modelBuilder.Entity("KN.KloudIdentity.Mapper.UserSchemaModel", b =>
                {
                    b.Navigation("ChildSchemas");
                });
#pragma warning restore 612, 618
        }
    }
}
