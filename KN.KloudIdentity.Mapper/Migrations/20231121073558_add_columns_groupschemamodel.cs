using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KN.KloudIdentity.Mapper.Migrations
{
    /// <inheritdoc />
    public partial class add_columns_groupschemamodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrayElementMappingField",
                table: "UserSchema",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArrayElementType",
                table: "UserSchema",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrayElementMappingField",
                table: "GroupSchema",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArrayElementType",
                table: "GroupSchema",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "GroupSchema",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupSchema_ParentId",
                table: "GroupSchema",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSchema_GroupSchema_ParentId",
                table: "GroupSchema",
                column: "ParentId",
                principalTable: "GroupSchema",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupSchema_GroupSchema_ParentId",
                table: "GroupSchema");

            migrationBuilder.DropIndex(
                name: "IX_GroupSchema_ParentId",
                table: "GroupSchema");

            migrationBuilder.DropColumn(
                name: "ArrayElementMappingField",
                table: "UserSchema");

            migrationBuilder.DropColumn(
                name: "ArrayElementType",
                table: "UserSchema");

            migrationBuilder.DropColumn(
                name: "ArrayElementMappingField",
                table: "GroupSchema");

            migrationBuilder.DropColumn(
                name: "ArrayElementType",
                table: "GroupSchema");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "GroupSchema");
        }
    }
}
