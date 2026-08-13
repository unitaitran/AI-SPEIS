using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    public partial class AddSingleQuestionRetryEvaluationDiagnostics : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationErrorCode",
                table: "SingleQuestionRetry",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationRawResponse",
                table: "SingleQuestionRetry",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationRetryCount",
                table: "SingleQuestionRetry",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "InvalidCriterionCodesJson",
                table: "SingleQuestionRetry",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EvaluationErrorCode", table: "SingleQuestionRetry");
            migrationBuilder.DropColumn(name: "EvaluationRawResponse", table: "SingleQuestionRetry");
            migrationBuilder.DropColumn(name: "EvaluationRetryCount", table: "SingleQuestionRetry");
            migrationBuilder.DropColumn(name: "InvalidCriterionCodesJson", table: "SingleQuestionRetry");
        }
    }
}
