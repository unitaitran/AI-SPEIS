using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiSuggestedActionFromTechnicalAnswerEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('TechnicalAnswerEvaluation', 'AiSuggestedAction') IS NOT NULL
                BEGIN
                    ALTER TABLE [TechnicalAnswerEvaluation] DROP COLUMN [AiSuggestedAction];
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiSuggestedAction",
                table: "TechnicalAnswerEvaluation",
                type: "int",
                nullable: true);
        }
    }
}
