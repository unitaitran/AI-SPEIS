using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class ScopeAiEvaluationFeedbackToRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationSnapshotJson",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "QuestionSnapshotJson",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "ScoreSnapshot",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "SessionQuestionId",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "TranscriptSnapshot",
                table: "AiEvaluationFeedback");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationSnapshotJson",
                table: "AiEvaluationFeedback",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionSnapshotJson",
                table: "AiEvaluationFeedback",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ScoreSnapshot",
                table: "AiEvaluationFeedback",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionQuestionId",
                table: "AiEvaluationFeedback",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TranscriptSnapshot",
                table: "AiEvaluationFeedback",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
