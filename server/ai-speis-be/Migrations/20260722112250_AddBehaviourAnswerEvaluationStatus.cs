using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddBehaviourAnswerEvaluationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration can be applied after the interview-flow migration in
            // environments that upgraded the feature branch before merging dev.
            // Conditional DDL keeps both migration histories safe.
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.BehaviourAnswer', 'AiErrorCode') IS NULL
                    ALTER TABLE [BehaviourAnswer] ADD [AiErrorCode] nvarchar(max) NULL;
                """);
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.BehaviourAnswer', 'EvaluationStatus') IS NULL
                    ALTER TABLE [BehaviourAnswer] ADD [EvaluationStatus] nvarchar(max) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.BehaviourAnswer', 'AiErrorCode') IS NOT NULL
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiErrorCode];
                """);
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.BehaviourAnswer', 'EvaluationStatus') IS NOT NULL
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [EvaluationStatus];
                """);
        }
    }
}
