using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddAiApplicationScoreToTechnicalAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AiApplicationScore",
                table: "TechnicalAnswer",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiApplicationScore",
                table: "TechnicalAnswer");
        }
    }
}
