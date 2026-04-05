using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updatedCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Check_Reservation_ReservationId",
                table: "Check");

            migrationBuilder.RenameColumn(
                name: "ReservationId",
                table: "Check",
                newName: "TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_Check_ReservationId",
                table: "Check",
                newName: "IX_Check_TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Check_Transaction_TransactionId",
                table: "Check",
                column: "TransactionId",
                principalTable: "Transaction",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Check_Transaction_TransactionId",
                table: "Check");

            migrationBuilder.RenameColumn(
                name: "TransactionId",
                table: "Check",
                newName: "ReservationId");

            migrationBuilder.RenameIndex(
                name: "IX_Check_TransactionId",
                table: "Check",
                newName: "IX_Check_ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Check_Reservation_ReservationId",
                table: "Check",
                column: "ReservationId",
                principalTable: "Reservation",
                principalColumn: "ReservationId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
