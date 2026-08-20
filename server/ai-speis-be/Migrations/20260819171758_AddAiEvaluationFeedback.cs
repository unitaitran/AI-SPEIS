using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddAiEvaluationFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiEvaluationFeedback",
                columns: table => new
                {
                    AiEvaluationFeedbackId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    SessionQuestionId = table.Column<int>(type: "int", nullable: false),
                    EvaluationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuestionSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TranscriptSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoreSnapshot = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EvaluationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminReviewNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiEvaluationFeedback", x => x.AiEvaluationFeedbackId);
                    table.ForeignKey(
                        name: "FK_AiEvaluationFeedback_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId");
                    table.ForeignKey(
                        name: "FK_AiEvaluationFeedback_User_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "User",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_AiEvaluationFeedback_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_InterviewSessionId",
                table: "AiEvaluationFeedback",
                column: "InterviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_ReviewedByUserId",
                table: "AiEvaluationFeedback",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_Status_CreatedAt",
                table: "AiEvaluationFeedback",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_UserId_CreatedAt",
                table: "AiEvaluationFeedback",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiEvaluationFeedback");

        }
    }
}
