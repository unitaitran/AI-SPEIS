using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddBehaviouralFieldsToQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClarificationQuestion",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCategory",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySubcategory",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingText",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedKeyPoints",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceLevel",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUp1",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUp2",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordTags",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelTags",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QdrantPayloadJson",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "Question",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScoringRubricJson",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Skill",
                table: "Question",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitSeconds",
                table: "Question",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClarificationQuestion",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "CompanyCategory",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "CompanySubcategory",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "EmbeddingText",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "ExpectedKeyPoints",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "FollowUp1",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "FollowUp2",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "KeywordTags",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "LevelTags",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "QdrantPayloadJson",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "ScoringRubricJson",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "Skill",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "TimeLimitSeconds",
                table: "Question");
        }
    }
}
