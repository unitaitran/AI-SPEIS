using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBehaviouralDbSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BehaviourQuestionSet",
                columns: table => new
                {
                    BehaviourQuestionSetId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    SelectionSource = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiExecutionRunId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    CoveredSkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConstraintsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourQuestionSet", x => x.BehaviourQuestionSetId);
                    table.ForeignKey(
                        name: "FK_BehaviourQuestionSet_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BehaviourRoundResult",
                columns: table => new
                {
                    BehaviourRoundResultId = table.Column<int>(type: "int", nullable: false)
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
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourRoundResult", x => x.BehaviourRoundResultId);
                    table.ForeignKey(
                        name: "FK_BehaviourRoundResult_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BehaviourSessionQuestion",
                columns: table => new
                {
                    BehaviourSessionQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BehaviourQuestionSetId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    QuestionOrder = table.Column<int>(type: "int", nullable: false),
                    QuestionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentQuestionId = table.Column<int>(type: "int", nullable: true),
                    QuestionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluationGoal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AskedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourSessionQuestion", x => x.BehaviourSessionQuestionId);
                    table.ForeignKey(
                        name: "FK_BehaviourSessionQuestion_BehaviourQuestionSet_BehaviourQuestionSetId",
                        column: x => x.BehaviourQuestionSetId,
                        principalTable: "BehaviourQuestionSet",
                        principalColumn: "BehaviourQuestionSetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BehaviourSessionQuestion_BehaviourSessionQuestion_ParentQuestionId",
                        column: x => x.ParentQuestionId,
                        principalTable: "BehaviourSessionQuestion",
                        principalColumn: "BehaviourSessionQuestionId");
                    table.ForeignKey(
                        name: "FK_BehaviourSessionQuestion_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BehaviourAnswer",
                columns: table => new
                {
                    BehaviourAnswerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BehaviourSessionQuestionId = table.Column<int>(type: "int", nullable: false),
                    Transcript = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SttConfidence = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiSituationScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiActionScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiResultScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiCompetencyScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiCommunicationScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AiCriteriaDetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiOverallRubricScore = table.Column<int>(type: "int", nullable: true),
                    AiAnswerQuality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiMissingPoints = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiRecommendedAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputedScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BehaviourAnswer", x => x.BehaviourAnswerId);
                    table.ForeignKey(
                        name: "FK_BehaviourAnswer_BehaviourSessionQuestion_BehaviourSessionQuestionId",
                        column: x => x.BehaviourSessionQuestionId,
                        principalTable: "BehaviourSessionQuestion",
                        principalColumn: "BehaviourSessionQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourAnswer_BehaviourSessionQuestionId",
                table: "BehaviourAnswer",
                column: "BehaviourSessionQuestionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourQuestionSet_InterviewSessionId",
                table: "BehaviourQuestionSet",
                column: "InterviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourRoundResult_InterviewSessionId",
                table: "BehaviourRoundResult",
                column: "InterviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourSessionQuestion_BehaviourQuestionSetId",
                table: "BehaviourSessionQuestion",
                column: "BehaviourQuestionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourSessionQuestion_ParentQuestionId",
                table: "BehaviourSessionQuestion",
                column: "ParentQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_BehaviourSessionQuestion_QuestionId",
                table: "BehaviourSessionQuestion",
                column: "QuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BehaviourAnswer");

            migrationBuilder.DropTable(
                name: "BehaviourRoundResult");

            migrationBuilder.DropTable(
                name: "BehaviourSessionQuestion");

            migrationBuilder.DropTable(
                name: "BehaviourQuestionSet");
        }
    }
}
