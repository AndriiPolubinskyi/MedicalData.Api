using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAbnormal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAbnormal",
                table: "LabResults",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAbnormal",
                table: "LabResults");
        }
    }
}
