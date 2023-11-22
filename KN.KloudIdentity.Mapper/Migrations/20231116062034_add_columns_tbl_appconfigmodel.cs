using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class add_columns_tbl_appconfigmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DELETEAPIForUsers",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GETAPIForUsers",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LISTAPIForUsers",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PATCHAPIForUsers",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PUTAPIForUsers",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DELETEAPIForUsers",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "GETAPIForUsers",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "LISTAPIForUsers",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "PATCHAPIForUsers",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "PUTAPIForUsers",
                table: "AppConfig");
        }
    }
}
