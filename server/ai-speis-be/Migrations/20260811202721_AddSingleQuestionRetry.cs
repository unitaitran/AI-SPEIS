using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleQuestionRetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SingleQuestionRetry",
                columns: table => new
                {
                    RetryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    OriginalSessionId = table.Column<int>(type: "int", nullable: true),
                    RoundType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuestionSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Skill = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Transcript = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    AiCriteriaDetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiStrengths = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiMissingPoints = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EvaluationStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvaluationModel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EvaluationInputTokens = table.Column<int>(type: "int", nullable: true),
                    EvaluationOutputTokens = table.Column<int>(type: "int", nullable: true),
                    EvaluationLatencyMs = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingleQuestionRetry", x => x.RetryId);
                    table.ForeignKey(
                        name: "FK_SingleQuestionRetry_Question_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Question",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SingleQuestionRetry_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SingleQuestionRetry_QuestionId",
                table: "SingleQuestionRetry",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_SingleQuestionRetry_UserId_QuestionId_CreatedAt",
                table: "SingleQuestionRetry",
                columns: new[] { "UserId", "QuestionId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SingleQuestionRetry");
        }
    }
}
