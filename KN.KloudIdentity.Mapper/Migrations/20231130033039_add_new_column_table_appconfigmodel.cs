using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class add_new_column_table_appconfigmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PATCHAPIForRemoveAllMembersFromGroup",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PATCHAPIForRemoveAllMembersFromGroup",
                table: "AppConfig");
        }
    }
}
