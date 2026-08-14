using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddBehaviourAnswerEvaluationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiErrorCode",
                table: "BehaviourAnswer",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationStatus",
                table: "BehaviourAnswer",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiErrorCode",
                table: "BehaviourAnswer");

            migrationBuilder.DropColumn(
                name: "EvaluationStatus",
                table: "BehaviourAnswer");
        }
    }
}
