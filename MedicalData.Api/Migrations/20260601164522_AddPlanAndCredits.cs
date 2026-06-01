using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalData.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanAndCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiCreditsLeft",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiCreditsLeft",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Users");
        }
    }
}
