using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class MakeCodingQuestionGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CodingQuestion_InterviewSession_InterviewSessionId",
                table: "CodingQuestion");

            migrationBuilder.DropIndex(
                name: "IX_CodingQuestion_InterviewSessionId",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "InterviewSessionId",
                table: "CodingQuestion");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CodingQuestion",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCategory",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanySubcategory",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Constraints",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CodingQuestion",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CodingQuestion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedBy",
                table: "CodingQuestion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingText",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationCriteria",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Examples",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedSpaceComplexity",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExpectedTimeComplexity",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceLevel",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionName",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionParameters",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FunctionSignature",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenTestCases",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputDescription",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CodingQuestion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CodingQuestion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "JobRole",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeywordTags",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelTags",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputDescription",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicTestCases",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QdrantPayloadJson",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuestionType",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceSolution",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnType",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Skill",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolutionExplanation",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StarterCode",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subskill",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportedProgrammingLanguages",
                table: "CodingQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "CodingQuestion",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyCategory",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "CompanySubcategory",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Constraints",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "EmbeddingText",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "EvaluationCriteria",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Examples",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "ExpectedSpaceComplexity",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "ExpectedTimeComplexity",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "FunctionName",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "FunctionParameters",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "FunctionSignature",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "HiddenTestCases",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "InputDescription",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "JobRole",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "KeywordTags",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "LevelTags",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "OutputDescription",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "PublicTestCases",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "QdrantPayloadJson",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "QuestionType",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "ReferenceSolution",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "ReturnType",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Skill",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "SolutionExplanation",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "StarterCode",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "Subskill",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "SupportedProgrammingLanguages",
                table: "CodingQuestion");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "CodingQuestion");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "CodingQuestion",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<int>(
                name: "InterviewSessionId",
                table: "CodingQuestion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CodingQuestion_InterviewSessionId",
                table: "CodingQuestion",
                column: "InterviewSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CodingQuestion_InterviewSession_InterviewSessionId",
                table: "CodingQuestion",
                column: "InterviewSessionId",
                principalTable: "InterviewSession",
                principalColumn: "InterviewSessionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
