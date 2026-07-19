using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalInterviewModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TechnicalAiModel",
                table: "InterviewSession",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalAiProvider",
                table: "InterviewSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalCompletedAt",
                table: "InterviewSession",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalCompletedMainQuestionCount",
                table: "InterviewSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalConcurrencyVersion",
                table: "InterviewSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalExperienceLevel",
                table: "InterviewSession",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TechnicalFinalScore",
                table: "InterviewSession",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalJobRole",
                table: "InterviewSession",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLanguage",
                table: "InterviewSession",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalPerformanceBand",
                table: "InterviewSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalRubricVersion",
                table: "InterviewSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalScoringPolicyVersion",
                table: "InterviewSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSelectedSkillsJson",
                table: "InterviewSession",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalStartedAt",
                table: "InterviewSession",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalState",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSummaryJson",
                table: "InterviewSession",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TechnicalQuestionAttempt",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: true),
                    ParentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RootMainAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    QuestionContentSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    MainQuestionIndex = table.Column<int>(type: "int", nullable: false),
                    AnswerTranscript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AudioId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmissionIdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SkillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubskillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DifficultySnapshot = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalQuestionAttempt", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_TechnicalQuestionAttempt_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicalQuestionAttempt_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalQuestionAttempt_TechnicalQuestionAttempt_ParentAttemptId",
                        column: x => x.ParentAttemptId,
                        principalTable: "TechnicalQuestionAttempt",
                        principalColumn: "AttemptId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIInteractionLog",
                columns: table => new
                {
                    AIInteractionLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OperationType = table.Column<int>(type: "int", nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RubricVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    InputTokenCount = table.Column<int>(type: "int", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "int", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIInteractionLog", x => x.AIInteractionLogId);
                    table.ForeignKey(
                        name: "FK_AIInteractionLog_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIInteractionLog_TechnicalQuestionAttempt_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TechnicalQuestionAttempt",
                        principalColumn: "AttemptId");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalAnswerEvaluation",
                columns: table => new
                {
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RootMainAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AiSuggestedOverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FinalOverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DimensionEvaluationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoringBreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StrengthsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingPointsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncorrectClaimsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImprovementSuggestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    PromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsFinalForMainQuestion = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalAnswerEvaluation", x => x.EvaluationId);
                    table.ForeignKey(
                        name: "FK_TechnicalAnswerEvaluation_TechnicalQuestionAttempt_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "TechnicalQuestionAttempt",
                        principalColumn: "AttemptId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIInteractionLog_AttemptId",
                table: "AIInteractionLog",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInteractionLog_InterviewSessionId_CreatedAt",
                table: "AIInteractionLog",
                columns: new[] { "InterviewSessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalAnswerEvaluation_AttemptId",
                table: "TechnicalAnswerEvaluation",
                column: "AttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalAnswerEvaluation_RootMainAttemptId",
                table: "TechnicalAnswerEvaluation",
                column: "RootMainAttemptId",
                unique: true,
                filter: "[IsFinalForMainQuestion] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_SequenceNumber",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "InterviewSessionId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_Status",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "InterviewSessionId", "Status" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_SubmissionIdempotencyKey",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "InterviewSessionId", "SubmissionIdempotencyKey" },
                unique: true,
                filter: "[SubmissionIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_ParentAttemptId",
                table: "TechnicalQuestionAttempt",
                column: "ParentAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_QuestionId",
                table: "TechnicalQuestionAttempt",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIInteractionLog");

            migrationBuilder.DropTable(
                name: "TechnicalAnswerEvaluation");

            migrationBuilder.DropTable(
                name: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "TechnicalAiModel",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalAiProvider",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalCompletedAt",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalCompletedMainQuestionCount",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalConcurrencyVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalExperienceLevel",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalFinalScore",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalJobRole",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalLanguage",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPerformanceBand",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalRubricVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalScoringPolicyVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalSelectedSkillsJson",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalStartedAt",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalState",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalSummaryJson",
                table: "InterviewSession");
        }
    }
}
