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
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.TechnicalAnswer', N'AiApplicationScore') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TechnicalAnswer] ADD [AiApplicationScore] decimal(18,2) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.TechnicalAnswer', N'AiApplicationScore') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[TechnicalAnswer] DROP COLUMN [AiApplicationScore];
                END
            ");
        }
    }
}
