using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalV2RuntimeFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TechnicalRuntimeVersion",
                table: "InterviewSession",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [InterviewSession] SET [TechnicalRuntimeVersion] = N'LEGACY' WHERE [TechnicalRuntimeVersion] IS NULL AND [InterviewRoundType] = 0");

            migrationBuilder.CreateTable(
                name: "TechnicalQuestionSet",
                columns: table => new
                {
                    TechnicalQuestionSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    SelectionSource = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiExecutionRunId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    CoveredSkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConstraintsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalQuestionSet", x => x.TechnicalQuestionSetId);
                    table.ForeignKey(
                        name: "FK_TechnicalQuestionSet_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalRoundResult",
                columns: table => new
                {
                    TechnicalRoundResultId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    OverallScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SkillScoresJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriteriaAveragesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiExecutiveSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiGaps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiLevelAssessment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiRecommendations = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalFeedbackJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinalFeedbackStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FinalFeedbackStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FeedbackConcurrencyVersion = table.Column<int>(type: "int", nullable: false),
                    FinalFeedbackModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FinalFeedbackPromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FeedbackInputTokens = table.Column<int>(type: "int", nullable: true),
                    FeedbackOutputTokens = table.Column<int>(type: "int", nullable: true),
                    FeedbackLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    FeedbackRetryCount = table.Column<int>(type: "int", nullable: false),
                    FinalFeedbackError = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalRoundResult", x => x.TechnicalRoundResultId);
                    table.ForeignKey(
                        name: "FK_TechnicalRoundResult_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalSessionQuestion",
                columns: table => new
                {
                    TechnicalSessionQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicalQuestionSetId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    QuestionOrder = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentQuestionId = table.Column<int>(type: "int", nullable: true),
                    QuestionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AskedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Skill = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Subskill = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EvaluationObjective = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DifficultySnapshot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalSessionQuestion", x => x.TechnicalSessionQuestionId);
                    table.ForeignKey(
                        name: "FK_TechnicalSessionQuestion_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicalSessionQuestion_TechnicalQuestionSet_TechnicalQuestionSetId",
                        column: x => x.TechnicalQuestionSetId,
                        principalTable: "TechnicalQuestionSet",
                        principalColumn: "TechnicalQuestionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicalSessionQuestion_TechnicalSessionQuestion_ParentQuestionId",
                        column: x => x.ParentQuestionId,
                        principalTable: "TechnicalSessionQuestion",
                        principalColumn: "TechnicalSessionQuestionId");
                });

            migrationBuilder.CreateTable(
                name: "TechnicalAnswer",
                columns: table => new
                {
                    TechnicalAnswerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicalSessionQuestionId = table.Column<int>(type: "int", nullable: false),
                    Transcript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AudioId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmissionIdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AnswerVersion = table.Column<int>(type: "int", nullable: false),
                    SttConfidence = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiProfessionalKnowledgeScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiTechnicalAccuracyScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiProblemSolvingReasoningScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiCommunicationExplanationScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiCriteriaDetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiMissingPoints = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputedScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FinalQuestionScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvaluationModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EvaluationPromptVersion = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EvaluationInputTokens = table.Column<int>(type: "int", nullable: true),
                    EvaluationOutputTokens = table.Column<int>(type: "int", nullable: true),
                    EvaluationLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    EvaluationRetryCount = table.Column<int>(type: "int", nullable: false),
                    AiProvider = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalAnswer", x => x.TechnicalAnswerId);
                    table.ForeignKey(
                        name: "FK_TechnicalAnswer_TechnicalSessionQuestion_TechnicalSessionQuestionId",
                        column: x => x.TechnicalSessionQuestionId,
                        principalTable: "TechnicalSessionQuestion",
                        principalColumn: "TechnicalSessionQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalAnswer_TechnicalSessionQuestionId",
                table: "TechnicalAnswer",
                column: "TechnicalSessionQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalAnswer_TechnicalSessionQuestionId_SubmissionIdempotencyKey",
                table: "TechnicalAnswer",
                columns: new[] { "TechnicalSessionQuestionId", "SubmissionIdempotencyKey" },
                unique: true,
                filter: "[SubmissionIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionSet_InterviewSessionId",
                table: "TechnicalQuestionSet",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalRoundResult_InterviewSessionId",
                table: "TechnicalRoundResult",
                column: "InterviewSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSessionQuestion_ParentQuestionId",
                table: "TechnicalSessionQuestion",
                column: "ParentQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSessionQuestion_QuestionId",
                table: "TechnicalSessionQuestion",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalSessionQuestion_TechnicalQuestionSetId_QuestionOrder",
                table: "TechnicalSessionQuestion",
                columns: new[] { "TechnicalQuestionSetId", "QuestionOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalAnswer");

            migrationBuilder.DropTable(
                name: "TechnicalRoundResult");

            migrationBuilder.DropTable(
                name: "TechnicalSessionQuestion");

            migrationBuilder.DropTable(
                name: "TechnicalQuestionSet");

            migrationBuilder.DropColumn(
                name: "TechnicalRuntimeVersion",
                table: "InterviewSession");
        }
    }
}
