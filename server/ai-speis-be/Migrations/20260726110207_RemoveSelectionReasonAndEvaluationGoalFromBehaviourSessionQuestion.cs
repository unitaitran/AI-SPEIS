using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSelectionReasonAndEvaluationGoalFromBehaviourSessionQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('BehaviourSessionQuestion', 'SelectionReason') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourSessionQuestion] DROP COLUMN [SelectionReason];
                END
                IF COL_LENGTH('BehaviourSessionQuestion', 'EvaluationGoal') IS NOT NULL
                BEGIN
                    ALTER TABLE [BehaviourSessionQuestion] DROP COLUMN [EvaluationGoal];
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
