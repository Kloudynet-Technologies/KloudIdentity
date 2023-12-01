using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class add_cloumns_tbl_appconfigmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PATCHAPIForAddMemberToGroup",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PATCHAPIForRemoveMemberFromGroup",
                table: "AppConfig",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PATCHAPIForAddMemberToGroup",
                table: "AppConfig");

            migrationBuilder.DropColumn(
                name: "PATCHAPIForRemoveMemberFromGroup",
                table: "AppConfig");
        }
    }
}
