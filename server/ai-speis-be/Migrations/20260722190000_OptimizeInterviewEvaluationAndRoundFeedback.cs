using System;
using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260722190000_OptimizeInterviewEvaluationAndRoundFeedback")]
    public partial class OptimizeInterviewEvaluationAndRoundFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("SubmissionIdempotencyKey", "BehaviourAnswer", "nvarchar(128)", maxLength: 128, nullable: true);
            migrationBuilder.AddColumn<string>("EvaluationStatus", "BehaviourAnswer", "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "NOT_STARTED");
            migrationBuilder.AddColumn<string>("EvaluationModel", "BehaviourAnswer", "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>("EvaluationPromptVersion", "BehaviourAnswer", "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<int>("EvaluationInputTokens", "BehaviourAnswer", "int", nullable: true);
            migrationBuilder.AddColumn<int>("EvaluationOutputTokens", "BehaviourAnswer", "int", nullable: true);
            migrationBuilder.AddColumn<long>("EvaluationLatencyMs", "BehaviourAnswer", "bigint", nullable: true);
            migrationBuilder.AddColumn<string>("EvaluationError", "BehaviourAnswer", "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<DateTime>("ProcessingStartedAt", "BehaviourAnswer", "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>("ProcessingCompletedAt", "BehaviourAnswer", "datetime2", nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_BehaviourAnswer_SubmissionIdempotencyKey",
                table: "BehaviourAnswer",
                column: "SubmissionIdempotencyKey",
                unique: true,
                filter: "[SubmissionIdempotencyKey] IS NOT NULL");

            migrationBuilder.AddColumn<int>("FinalFeedbackStatus", "BehaviourRoundResult", "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>("FinalFeedbackModel", "BehaviourRoundResult", "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>("FinalFeedbackPromptVersion", "BehaviourRoundResult", "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<int>("FeedbackInputTokens", "BehaviourRoundResult", "int", nullable: true);
            migrationBuilder.AddColumn<int>("FeedbackOutputTokens", "BehaviourRoundResult", "int", nullable: true);
            migrationBuilder.AddColumn<long>("FeedbackLatencyMs", "BehaviourRoundResult", "bigint", nullable: true);
            migrationBuilder.AddColumn<string>("FeedbackError", "BehaviourRoundResult", "nvarchar(100)", maxLength: 100, nullable: true);

            migrationBuilder.AddColumn<int>("TechnicalFeedbackStatus", "InterviewSession", "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>("TechnicalFeedbackModel", "InterviewSession", "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>("TechnicalFeedbackPromptVersion", "InterviewSession", "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<int>("TechnicalFeedbackInputTokens", "InterviewSession", "int", nullable: true);
            migrationBuilder.AddColumn<int>("TechnicalFeedbackOutputTokens", "InterviewSession", "int", nullable: true);
            migrationBuilder.AddColumn<long>("TechnicalFeedbackLatencyMs", "InterviewSession", "bigint", nullable: true);
            migrationBuilder.AddColumn<string>("TechnicalFeedbackError", "InterviewSession", "nvarchar(100)", maxLength: 100, nullable: true);

            // Preserve completed legacy feedback so upgrading does not regenerate it.
            migrationBuilder.Sql("""
                UPDATE [BehaviourRoundResult]
                SET [FinalFeedbackStatus] = 2
                WHERE NULLIF(LTRIM(RTRIM([AiExecutiveSummary])), '') IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE [InterviewSession]
                SET [TechnicalFeedbackStatus] = 2
                WHERE NULLIF(LTRIM(RTRIM([TechnicalSummaryJson])), '') IS NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_BehaviourAnswer_SubmissionIdempotencyKey", "BehaviourAnswer");
            foreach (var column in new[]
            {
                "SubmissionIdempotencyKey", "EvaluationStatus", "EvaluationModel", "EvaluationPromptVersion",
                "EvaluationInputTokens", "EvaluationOutputTokens", "EvaluationLatencyMs", "EvaluationError",
                "ProcessingStartedAt", "ProcessingCompletedAt"
            }) migrationBuilder.DropColumn(column, "BehaviourAnswer");

            foreach (var column in new[]
            {
                "FinalFeedbackStatus", "FinalFeedbackModel", "FinalFeedbackPromptVersion", "FeedbackInputTokens",
                "FeedbackOutputTokens", "FeedbackLatencyMs", "FeedbackError"
            }) migrationBuilder.DropColumn(column, "BehaviourRoundResult");

            foreach (var column in new[]
            {
                "TechnicalFeedbackStatus", "TechnicalFeedbackModel", "TechnicalFeedbackPromptVersion",
                "TechnicalFeedbackInputTokens", "TechnicalFeedbackOutputTokens", "TechnicalFeedbackLatencyMs",
                "TechnicalFeedbackError"
            }) migrationBuilder.DropColumn(column, "InterviewSession");
        }
    }
}
