using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatusFromAiEvaluationFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiEvaluationFeedback_User_ReviewedByUserId",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropIndex(
                name: "IX_AiEvaluationFeedback_ReviewedByUserId",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropIndex(
                name: "IX_AiEvaluationFeedback_Status_CreatedAt",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "AdminReviewNote",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "AiEvaluationFeedback");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AiEvaluationFeedback");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminReviewNote",
                table: "AiEvaluationFeedback",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "AiEvaluationFeedback",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "AiEvaluationFeedback",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AiEvaluationFeedback",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_ReviewedByUserId",
                table: "AiEvaluationFeedback",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiEvaluationFeedback_Status_CreatedAt",
                table: "AiEvaluationFeedback",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiEvaluationFeedback_User_ReviewedByUserId",
                table: "AiEvaluationFeedback",
                column: "ReviewedByUserId",
                principalTable: "User",
                principalColumn: "UserId");
        }
    }
}
