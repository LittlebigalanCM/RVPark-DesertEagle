using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentFromReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservation_Document_DocumentId",
                table: "Reservation");

            migrationBuilder.DropIndex(
                name: "IX_Reservation_DocumentId",
                table: "Reservation");

            migrationBuilder.DropColumn(
                name: "DocumentId",
                table: "Reservation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentId",
                table: "Reservation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservation_DocumentId",
                table: "Reservation",
                column: "DocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservation_Document_DocumentId",
                table: "Reservation",
                column: "DocumentId",
                principalTable: "Document",
                principalColumn: "Id");
        }
    }
}
