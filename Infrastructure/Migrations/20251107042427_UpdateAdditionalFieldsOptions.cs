using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdditionalFieldsOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "CustomDynamicField",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FieldType",
                table: "CustomDynamicField",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsMultiline",
                table: "CustomDynamicField",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "CustomDynamicField",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxLength",
                table: "CustomDynamicField",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OptionsJson",
                table: "CustomDynamicField",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Placeholder",
                table: "CustomDynamicField",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StringDefaultValue",
                table: "CustomDynamicField",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "FieldType",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "IsMultiline",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "MaxLength",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "OptionsJson",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "Placeholder",
                table: "CustomDynamicField");

            migrationBuilder.DropColumn(
                name: "StringDefaultValue",
                table: "CustomDynamicField");
        }
    }
}
