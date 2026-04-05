using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NullableBranchRankAlsoDetachRankFromBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_MilitaryBranch_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_MilitaryRank_RankId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_MilitaryRank_MilitaryBranch_BranchId",
                table: "MilitaryRank");

            migrationBuilder.DropIndex(
                name: "IX_MilitaryRank_BranchId",
                table: "MilitaryRank");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MilitaryRank");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_MilitaryBranch_BranchId",
                table: "AspNetUsers",
                column: "BranchId",
                principalTable: "MilitaryBranch",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_MilitaryRank_RankId",
                table: "AspNetUsers",
                column: "RankId",
                principalTable: "MilitaryRank",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_MilitaryBranch_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_MilitaryRank_RankId",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MilitaryRank",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MilitaryRank_BranchId",
                table: "MilitaryRank",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_MilitaryBranch_BranchId",
                table: "AspNetUsers",
                column: "BranchId",
                principalTable: "MilitaryBranch",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_MilitaryRank_RankId",
                table: "AspNetUsers",
                column: "RankId",
                principalTable: "MilitaryRank",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MilitaryRank_MilitaryBranch_BranchId",
                table: "MilitaryRank",
                column: "BranchId",
                principalTable: "MilitaryBranch",
                principalColumn: "Id");
        }
    }
}
