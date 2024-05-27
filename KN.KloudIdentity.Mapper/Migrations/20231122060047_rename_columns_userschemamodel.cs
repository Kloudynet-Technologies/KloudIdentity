using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class rename_columns_userschemamodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupSchema_UserSchema_UserSchemaModelID",
                table: "GroupSchema");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSchema_GroupSchema_ParentId",
                table: "UserSchema");

            migrationBuilder.DropIndex(
                name: "IX_GroupSchema_UserSchemaModelID",
                table: "GroupSchema");

            migrationBuilder.DropColumn(
                name: "UserSchemaModelID",
                table: "GroupSchema");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSchema_UserSchema_ParentId",
                table: "UserSchema",
                column: "ParentId",
                principalTable: "UserSchema",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSchema_UserSchema_ParentId",
                table: "UserSchema");

            migrationBuilder.AddColumn<int>(
                name: "UserSchemaModelID",
                table: "GroupSchema",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupSchema_UserSchemaModelID",
                table: "GroupSchema",
                column: "UserSchemaModelID");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSchema_UserSchema_UserSchemaModelID",
                table: "GroupSchema",
                column: "UserSchemaModelID",
                principalTable: "UserSchema",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSchema_GroupSchema_ParentId",
                table: "UserSchema",
                column: "ParentId",
                principalTable: "GroupSchema",
                principalColumn: "ID");
        }
    }
}
