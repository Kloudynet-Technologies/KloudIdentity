using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthModelColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "AuthConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantType",
                table: "AuthConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OAuth2TokenUrl",
                table: "AuthConfig",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "AuthConfig");

            migrationBuilder.DropColumn(
                name: "GrantType",
                table: "AuthConfig");

            migrationBuilder.DropColumn(
                name: "OAuth2TokenUrl",
                table: "AuthConfig");
        }
    }
}
