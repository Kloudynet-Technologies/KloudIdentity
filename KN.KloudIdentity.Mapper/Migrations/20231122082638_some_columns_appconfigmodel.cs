using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class some_columns_appconfigmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DELETEAPIForGroups",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GETAPIForGroups",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LISTAPIForGroups",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PATCHAPIForGroups",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PUTAPIForGroups",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DELETEAPIForGroups",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "GETAPIForGroups",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "LISTAPIForGroups",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "PATCHAPIForGroups",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "PUTAPIForGroups",
                table: "AppConfig");
        }
    }
}
