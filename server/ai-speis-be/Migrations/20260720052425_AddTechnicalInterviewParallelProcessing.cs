using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalInterviewParallelProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CriticalPathLatencyMs",
                table: "TechnicalQuestionAttempt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EvaluationFallbackUsed",
                table: "TechnicalQuestionAttempt",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationTaskStatus",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "FeedbackFallbackUsed",
                table: "TechnicalQuestionAttempt",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FeedbackTaskStatus",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ParallelLatencySavingMs",
                table: "TechnicalQuestionAttempt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingCompletedAt",
                table: "TechnicalQuestionAttempt",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "TechnicalQuestionAttempt",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuestionFallbackUsed",
                table: "TechnicalQuestionAttempt",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionGenerationTaskStatus",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SequentialEstimatedLatencyMs",
                table: "TechnicalQuestionAttempt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalProcessingLatencyMs",
                table: "TechnicalQuestionAttempt",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FeedbackFallbackUsed",
                table: "TechnicalAnswerEvaluation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackModelName",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FeedbackPromptVersion",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FeedbackSummary",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "AIInteractionLog",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "AIInteractionLog",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "AIInteractionLog",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriticalPathLatencyMs",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "EvaluationFallbackUsed",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "EvaluationTaskStatus",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "FeedbackFallbackUsed",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "FeedbackTaskStatus",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "ParallelLatencySavingMs",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "ProcessingCompletedAt",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "QuestionFallbackUsed",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "QuestionGenerationTaskStatus",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "SequentialEstimatedLatencyMs",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "TotalProcessingLatencyMs",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "FeedbackFallbackUsed",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "FeedbackModelName",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "FeedbackPromptVersion",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "FeedbackSummary",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AIInteractionLog");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "AIInteractionLog");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "AIInteractionLog");
        }
    }
}
