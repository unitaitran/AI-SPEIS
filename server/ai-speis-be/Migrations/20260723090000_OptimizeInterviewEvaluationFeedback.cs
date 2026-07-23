using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ai_speis_be.Models;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260723090000_OptimizeInterviewEvaluationFeedback")]
    public partial class OptimizeInterviewEvaluationFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnswerVersion",
                table: "BehaviourAnswer",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(name: "AudioId", table: "BehaviourAnswer", type: "nvarchar(200)", maxLength: 200, nullable: true);
            migrationBuilder.AddColumn<string>(name: "SubmissionIdempotencyKey", table: "BehaviourAnswer", type: "nvarchar(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<string>(name: "AiEvidenceJson", table: "BehaviourAnswer", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "AiMissingAspectsJson", table: "BehaviourAnswer", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "FinalQuestionScore", table: "BehaviourAnswer", type: "decimal(18,2)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "EvaluationModel", table: "BehaviourAnswer", type: "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "EvaluationPromptVersion", table: "BehaviourAnswer", type: "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<int>(name: "EvaluationInputTokens", table: "BehaviourAnswer", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "EvaluationOutputTokens", table: "BehaviourAnswer", type: "int", nullable: true);
            migrationBuilder.AddColumn<long>(name: "EvaluationLatencyMs", table: "BehaviourAnswer", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<int>(name: "EvaluationRetryCount", table: "BehaviourAnswer", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<string>(name: "FinalFeedbackJson", table: "BehaviourRoundResult", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "FinalFeedbackStatus", table: "BehaviourRoundResult", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "NOT_STARTED");
            migrationBuilder.AddColumn<DateTime>(name: "FinalFeedbackStartedAt", table: "BehaviourRoundResult", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FeedbackConcurrencyVersion", table: "BehaviourRoundResult", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "FinalFeedbackModel", table: "BehaviourRoundResult", type: "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "FinalFeedbackPromptVersion", table: "BehaviourRoundResult", type: "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<int>(name: "FeedbackInputTokens", table: "BehaviourRoundResult", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FeedbackOutputTokens", table: "BehaviourRoundResult", type: "int", nullable: true);
            migrationBuilder.AddColumn<long>(name: "FeedbackLatencyMs", table: "BehaviourRoundResult", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<int>(name: "FeedbackRetryCount", table: "BehaviourRoundResult", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "FinalFeedbackError", table: "BehaviourRoundResult", type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<string>(name: "TechnicalFinalFeedbackStatus", table: "InterviewSession", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "NOT_STARTED");
            migrationBuilder.AddColumn<DateTime>(name: "TechnicalFinalFeedbackStartedAt", table: "InterviewSession", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(name: "TechnicalFinalFeedbackError", table: "InterviewSession", type: "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_BehaviourRoundResult_InterviewSessionId",
                table: "BehaviourRoundResult");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourRoundResult_InterviewSessionId",
                table: "BehaviourRoundResult",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourAnswer_SubmissionIdempotencyKey",
                table: "BehaviourAnswer",
                column: "SubmissionIdempotencyKey",
                unique: true,
                filter: "[SubmissionIdempotencyKey] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_BehaviourAnswer_SubmissionIdempotencyKey", table: "BehaviourAnswer");
            migrationBuilder.DropIndex(name: "IX_BehaviourRoundResult_InterviewSessionId", table: "BehaviourRoundResult");
            migrationBuilder.CreateIndex(
                name: "IX_BehaviourRoundResult_InterviewSessionId",
                table: "BehaviourRoundResult",
                column: "InterviewSessionId");

            foreach (var column in new[]
            {
                "AnswerVersion", "AudioId", "SubmissionIdempotencyKey", "AiEvidenceJson",
                "AiMissingAspectsJson", "FinalQuestionScore", "EvaluationModel",
                "EvaluationPromptVersion", "EvaluationInputTokens", "EvaluationOutputTokens",
                "EvaluationLatencyMs", "EvaluationRetryCount"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "BehaviourAnswer");
            }

            foreach (var column in new[]
            {
                "FinalFeedbackJson", "FinalFeedbackStatus", "FinalFeedbackModel",
                "FinalFeedbackPromptVersion", "FeedbackInputTokens", "FeedbackOutputTokens",
                "FeedbackLatencyMs", "FeedbackRetryCount", "FinalFeedbackError",
                "FinalFeedbackStartedAt", "FeedbackConcurrencyVersion"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "BehaviourRoundResult");
            }

            foreach (var column in new[]
            {
                "TechnicalFinalFeedbackStatus", "TechnicalFinalFeedbackStartedAt", "TechnicalFinalFeedbackError"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "InterviewSession");
            }
        }
    }
}
