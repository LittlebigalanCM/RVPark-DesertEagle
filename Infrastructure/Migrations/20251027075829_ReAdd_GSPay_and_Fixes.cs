using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReAdd_GSPay_and_Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GSPayId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GSPayGrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GSPayGrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_GSPayId",
                table: "AspNetUsers",
                column: "GSPayId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_GSPayGrades_GSPayId",
                table: "AspNetUsers",
                column: "GSPayId",
                principalTable: "GSPayGrades",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_GSPayGrades_GSPayId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "GSPayGrades");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_GSPayId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GSPayId",
                table: "AspNetUsers");
        }
    }
}
