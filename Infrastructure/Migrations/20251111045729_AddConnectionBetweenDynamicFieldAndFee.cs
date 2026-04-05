using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectionBetweenDynamicFieldAndFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomDynamicFieldId",
                table: "Fee",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fee_CustomDynamicFieldId",
                table: "Fee",
                column: "CustomDynamicFieldId");

            migrationBuilder.AddForeignKey(
                name: "FK_Fee_CustomDynamicField_CustomDynamicFieldId",
                table: "Fee",
                column: "CustomDynamicFieldId",
                principalTable: "CustomDynamicField",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Fee_CustomDynamicField_CustomDynamicFieldId",
                table: "Fee");

            migrationBuilder.DropIndex(
                name: "IX_Fee_CustomDynamicFieldId",
                table: "Fee");

            migrationBuilder.DropColumn(
                name: "CustomDynamicFieldId",
                table: "Fee");
        }
    }
}
