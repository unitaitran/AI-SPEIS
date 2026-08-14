using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTechnicalV1ModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[TechnicalAnswerEvaluation]', N'U') IS NOT NULL DELETE FROM [dbo].[TechnicalAnswerEvaluation];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[AIInteractionLog]', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'AttemptId') DELETE FROM [dbo].[AIInteractionLog] WHERE [AttemptId] IS NOT NULL;");
            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[TechnicalQuestionAttempt]', N'U') IS NOT NULL DELETE FROM [dbo].[TechnicalQuestionAttempt];");
            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[InterviewSession]', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewSession]') AND name = N'TechnicalRuntimeVersion') DELETE FROM [dbo].[InterviewSession] WHERE [InterviewRoundType] = 0 AND ([TechnicalRuntimeVersion] IS NULL OR [TechnicalRuntimeVersion] <> N'V2');");

            migrationBuilder.DropForeignKey(
                name: "FK_AIInteractionLog_TechnicalQuestionAttempt_AttemptId",
                table: "AIInteractionLog");

            migrationBuilder.DropTable(
                name: "TechnicalAnswerEvaluation");

            migrationBuilder.DropTable(
                name: "TechnicalQuestionAttempt");

            migrationBuilder.DropIndex(
                name: "IX_AIInteractionLog_AttemptId",
                table: "AIInteractionLog");

            migrationBuilder.DropColumn(
                name: "AiCommunicationExplanationScore",
                table: "TechnicalAnswer");

            migrationBuilder.DropColumn(
                name: "AiProblemSolvingReasoningScore",
                table: "TechnicalAnswer");

            migrationBuilder.DropColumn(
                name: "AiProfessionalKnowledgeScore",
                table: "TechnicalAnswer");

            migrationBuilder.DropColumn(
                name: "AiTechnicalAccuracyScore",
                table: "TechnicalAnswer");

            migrationBuilder.DropColumn(
                name: "TechnicalAdaptiveRuleVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalAiModel",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalBonusCalculationVersion",
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
                name: "TechnicalFinalFeedbackError",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalFinalFeedbackStartedAt",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalFinalFeedbackStatus",
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
                name: "TechnicalLegacyUpgradeFailureReason",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalMatchBand",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalMatchScoreSnapshot",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPerformanceBand",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPlannedCvQuestionCount",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPlannedJdQuestionCount",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalQuestionPlanJson",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalQuestionPlanVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalReliabilityFailureReason",
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

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "AIInteractionLog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AiCommunicationExplanationScore",
                table: "TechnicalAnswer",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AiProblemSolvingReasoningScore",
                table: "TechnicalAnswer",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AiProfessionalKnowledgeScore",
                table: "TechnicalAnswer",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AiTechnicalAccuracyScore",
                table: "TechnicalAnswer",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalAdaptiveRuleVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalAiModel",
                table: "InterviewSession",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalBonusCalculationVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
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

            migrationBuilder.AddColumn<string>(
                name: "TechnicalFinalFeedbackError",
                table: "InterviewSession",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalFinalFeedbackStartedAt",
                table: "InterviewSession",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalFinalFeedbackStatus",
                table: "InterviewSession",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

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
                name: "TechnicalLegacyUpgradeFailureReason",
                table: "InterviewSession",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMatchBand",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMatchScoreSnapshot",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalPerformanceBand",
                table: "InterviewSession",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalPlannedCvQuestionCount",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalPlannedJdQuestionCount",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalQuestionPlanJson",
                table: "InterviewSession",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalQuestionPlanVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalReliabilityFailureReason",
                table: "InterviewSession",
                type: "nvarchar(500)",
                maxLength: 500,
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

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptId",
                table: "AIInteractionLog",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TechnicalQuestionAttempt",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    ParentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuestionId = table.Column<int>(type: "int", nullable: true),
                    AdaptiveStage = table.Column<int>(type: "int", nullable: true),
                    AnswerTranscript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppliedBonus = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AudioId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BonusCalculationVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedClarificationCount = table.Column<int>(type: "int", nullable: false),
                    CompletedFollowUpCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriticalPathLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    CumulativeFollowUpBonus = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DifficultySnapshot = table.Column<int>(type: "int", nullable: true),
                    EvaluationFallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    EvaluationObjective = table.Column<int>(type: "int", nullable: true),
                    EvaluationTaskStatus = table.Column<int>(type: "int", nullable: false),
                    FeedbackFallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    FeedbackTaskStatus = table.Column<int>(type: "int", nullable: false),
                    FinalMainScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    GenerationReason = table.Column<int>(type: "int", nullable: true),
                    InitialMainScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MainQuestionIndex = table.Column<int>(type: "int", nullable: false),
                    ParallelLatencySavingMs = table.Column<long>(type: "bigint", nullable: true),
                    PlanDeviation = table.Column<bool>(type: "bit", nullable: false),
                    PlanDeviationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessingCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuestionContentSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QuestionFallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    QuestionGenerationTaskStatus = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    RawScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    RequiredClarificationCount = table.Column<int>(type: "int", nullable: false),
                    RequiredFollowUpCount = table.Column<int>(type: "int", nullable: false),
                    RootMainAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    SequenceWithinMain = table.Column<int>(type: "int", nullable: false),
                    SequentialEstimatedLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    SkillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmissionIdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SubskillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetSkillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TargetSubskillSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalProcessingLatencyMs = table.Column<long>(type: "bigint", nullable: true)
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
                name: "TechnicalAnswerEvaluation",
                columns: table => new
                {
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdaptiveRuleVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AiSuggestedOverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    BackendResolvedAction = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DimensionEvaluationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    FeedbackFallbackUsed = table.Column<bool>(type: "bit", nullable: false),
                    FeedbackModelName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FeedbackPromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FeedbackSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalOverallScore = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ImprovementSuggestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFinalForMainQuestion = table.Column<bool>(type: "bit", nullable: false),
                    MissingPointsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RootMainAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScoringBreakdownJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoringPolicyVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StrengthsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetRubricCodesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_MainQuestionIndex",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "InterviewSessionId", "MainQuestionIndex" },
                unique: true,
                filter: "[QuestionType] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_QuestionId",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "InterviewSessionId", "QuestionId" },
                unique: true,
                filter: "[QuestionType] = 0 AND [QuestionId] IS NOT NULL");

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

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_RootMainAttemptId_QuestionType_SequenceWithinMain",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "RootMainAttemptId", "QuestionType", "SequenceWithinMain" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AIInteractionLog_TechnicalQuestionAttempt_AttemptId",
                table: "AIInteractionLog",
                column: "AttemptId",
                principalTable: "TechnicalQuestionAttempt",
                principalColumn: "AttemptId");
        }
    }
}
