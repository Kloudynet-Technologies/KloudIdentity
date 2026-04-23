using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Infrastructure.Migrations
{
    public partial class add_tenantid_to_appconfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AppConfigSnapshots",
                type: "nvarchar(64)",
                nullable: false);

            migrationBuilder.DropIndex(
                name: "IX_AppConfigSnapshots_AppId",
                table: "AppConfigSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigSnapshots_TenantId_AppId",
                table: "AppConfigSnapshots",
                columns: new[] { "TenantId", "AppId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppConfigSnapshots_TenantId_AppId",
                table: "AppConfigSnapshots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppConfigSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigSnapshots_AppId",
                table: "AppConfigSnapshots",
                column: "AppId",
                unique: true);
        }
    }
}
