using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class Remove5RedundantFieldsFromBehaviourAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('BehaviourAnswer', 'AiEvidenceJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiEvidenceJson];
                END
                IF COL_LENGTH('BehaviourAnswer', 'AiMissingAspectsJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiMissingAspectsJson];
                END
                IF COL_LENGTH('BehaviourAnswer', 'AiOverallRubricScore') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiOverallRubricScore];
                END
                IF COL_LENGTH('BehaviourAnswer', 'AiAnswerQuality') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiAnswerQuality];
                END
                IF COL_LENGTH('BehaviourAnswer', 'AiRecommendedAction') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourAnswer] DROP COLUMN [AiRecommendedAction];
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
