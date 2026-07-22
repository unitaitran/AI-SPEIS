using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260721143000_LockTechnicalMainQuestionsAndAdaptiveDecision")]
    public partial class LockTechnicalMainQuestionsAndAdaptiveDecision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdaptiveRuleVersion",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionReason",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackUsed",
                table: "TechnicalAnswerEvaluation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetRubricCodesJson",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "TechnicalReliabilityFailureReason",
                table: "InterviewSession",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalLegacyUpgradeFailureReason",
                table: "InterviewSession",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_MainQuestionIndex",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropIndex(
                name: "IX_TechnicalQuestionAttempt_InterviewSessionId_QuestionId",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(name: "AdaptiveRuleVersion", table: "TechnicalAnswerEvaluation");
            migrationBuilder.DropColumn(name: "DecisionReason", table: "TechnicalAnswerEvaluation");
            migrationBuilder.DropColumn(name: "FallbackUsed", table: "TechnicalAnswerEvaluation");
            migrationBuilder.DropColumn(name: "TargetRubricCodesJson", table: "TechnicalAnswerEvaluation");
            migrationBuilder.DropColumn(name: "TechnicalReliabilityFailureReason", table: "InterviewSession");
            migrationBuilder.DropColumn(name: "TechnicalLegacyUpgradeFailureReason", table: "InterviewSession");
        }
    }
}
