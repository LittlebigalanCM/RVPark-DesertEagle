using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChristianMerge33 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_TransactionType_TransactionTypeId",
                table: "Transaction");

            migrationBuilder.DropColumn(
                name: "IsOneTimeUse",
                table: "CustomDynamicField");

            migrationBuilder.RenameColumn(
                name: "TransactionTypeId",
                table: "Transaction",
                newName: "FeeId");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_TransactionTypeId",
                table: "Transaction",
                newName: "IX_Transaction_FeeId");

            migrationBuilder.RenameColumn(
                name: "IsTransactionField",
                table: "CustomDynamicField",
                newName: "IsEnabled");

            migrationBuilder.CreateTable(
                name: "Fee",
                columns: table => new
                {
                    FeeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggerRuleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriggerTemplateType = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    CalculationType = table.Column<int>(type: "int", nullable: true),
                    StaticAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fee", x => x.FeeId);
                });

            migrationBuilder.Sql("INSERT INTO Fee (Name, DisplayLabel, TriggerType, CalculationType, StaticAmount, IsEnabled) " +
                "SELECT Name, DisplayLabel, TriggerType, CalculationType, StaticAmount, 1 FROM TransactionType");

            migrationBuilder.DropTable(
                name: "TransactionType");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_Fee_FeeId",
                table: "Transaction",
                column: "FeeId",
                principalTable: "Fee",
                principalColumn: "FeeId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transaction_Fee_FeeId",
                table: "Transaction");

            migrationBuilder.RenameColumn(
                name: "FeeId",
                table: "Transaction",
                newName: "TransactionTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_Transaction_FeeId",
                table: "Transaction",
                newName: "IX_Transaction_TransactionTypeId");

            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "CustomDynamicField",
                newName: "IsTransactionField");

            migrationBuilder.AddColumn<bool>(
                name: "IsOneTimeUse",
                table: "CustomDynamicField",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TransactionType",
                columns: table => new
                {
                    TransactionTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CalculationType = table.Column<int>(type: "int", nullable: true),
                    DisplayLabel = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: 0),
                    Name = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StaticAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TriggerRuleJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriggerTemplateType = table.Column<string>(type: "nvarchar(100)", nullable: true),
                    TriggerType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionType", x => x.TransactionTypeId);
                });

            migrationBuilder.Sql("INSERT INTO TransactionType (Name, DisplayLabel, TriggerType, CalculationType, StaticAmount) " +
                "SELECT Name, DisplayLabel, TriggerType, CalculationType, StaticAmount FROM Fee");

            migrationBuilder.DropTable(
                name: "Fee");

            migrationBuilder.AddForeignKey(
                name: "FK_Transaction_TransactionType_TransactionTypeId",
                table: "Transaction",
                column: "TransactionTypeId",
                principalTable: "TransactionType",
                principalColumn: "TransactionTypeId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
