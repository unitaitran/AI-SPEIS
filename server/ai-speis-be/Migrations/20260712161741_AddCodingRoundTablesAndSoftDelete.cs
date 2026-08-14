using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddCodingRoundTablesAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
         
            // Create CodingQuestion table
            migrationBuilder.CreateTable(
                name: "CodingQuestion",
                columns: table => new
                {
                    CodingQuestionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeLimit = table.Column<double>(type: "float", nullable: false),
                    MemoryLimit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodingQuestion", x => x.CodingQuestionId);
                    table.ForeignKey(
                        name: "FK_CodingQuestion_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create CodingQuestionTemplate table
            migrationBuilder.CreateTable(
                name: "CodingQuestionTemplate",
                columns: table => new
                {
                    TemplateId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodingQuestionId = table.Column<int>(type: "int", nullable: false),
                    LanguageId = table.Column<int>(type: "int", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodingQuestionTemplate", x => x.TemplateId);
                    table.ForeignKey(
                        name: "FK_CodingQuestionTemplate_CodingQuestion_CodingQuestionId",
                        column: x => x.CodingQuestionId,
                        principalTable: "CodingQuestion",
                        principalColumn: "CodingQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create CodingSubmission table
            migrationBuilder.CreateTable(
                name: "CodingSubmission",
                columns: table => new
                {
                    CodingSubmissionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false),
                    CodingQuestionId = table.Column<int>(type: "int", nullable: false),
                    SourceCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LanguageId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalTestCases = table.Column<int>(type: "int", nullable: false),
                    PassedTestCases = table.Column<int>(type: "int", nullable: false),
                    MaxTimeMs = table.Column<double>(type: "float", nullable: false),
                    MaxMemoryKb = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodingSubmission", x => x.CodingSubmissionId);
                    table.ForeignKey(
                        name: "FK_CodingSubmission_CodingQuestion_CodingQuestionId",
                        column: x => x.CodingQuestionId,
                        principalTable: "CodingQuestion",
                        principalColumn: "CodingQuestionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodingSubmission_InterviewSession_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSession",
                        principalColumn: "InterviewSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create TestCase table
            migrationBuilder.CreateTable(
                name: "TestCase",
                columns: table => new
                {
                    TestCaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodingQuestionId = table.Column<int>(type: "int", nullable: false),
                    Input = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedOutput = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSample = table.Column<bool>(type: "bit", nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCase", x => x.TestCaseId);
                    table.ForeignKey(
                        name: "FK_TestCase_CodingQuestion_CodingQuestionId",
                        column: x => x.CodingQuestionId,
                        principalTable: "CodingQuestion",
                        principalColumn: "CodingQuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create SubmissionTestCaseResult table
            migrationBuilder.CreateTable(
                name: "SubmissionTestCaseResult",
                columns: table => new
                {
                    ResultId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodingSubmissionId = table.Column<int>(type: "int", nullable: false),
                    TestCaseId = table.Column<int>(type: "int", nullable: false),
                    ActualOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stderr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompileOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeMs = table.Column<double>(type: "float", nullable: false),
                    MemoryKb = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionTestCaseResult", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_SubmissionTestCaseResult_CodingSubmission_CodingSubmissionId",
                        column: x => x.CodingSubmissionId,
                        principalTable: "CodingSubmission",
                        principalColumn: "CodingSubmissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionTestCaseResult_TestCase_TestCaseId",
                        column: x => x.TestCaseId,
                        principalTable: "TestCase",
                        principalColumn: "TestCaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create Indexes
            migrationBuilder.CreateIndex(
                name: "IX_CodingQuestion_InterviewSessionId",
                table: "CodingQuestion",
                column: "InterviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CodingQuestionTemplate_CodingQuestionId",
                table: "CodingQuestionTemplate",
                column: "CodingQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CodingSubmission_CodingQuestionId",
                table: "CodingSubmission",
                column: "CodingQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CodingSubmission_InterviewSessionId",
                table: "CodingSubmission",
                column: "InterviewSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionTestCaseResult_CodingSubmissionId",
                table: "SubmissionTestCaseResult",
                column: "CodingSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionTestCaseResult_TestCaseId",
                table: "SubmissionTestCaseResult",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCase_CodingQuestionId",
                table: "TestCase",
                column: "CodingQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodingQuestionTemplate");

            migrationBuilder.DropTable(
                name: "SubmissionTestCaseResult");

            migrationBuilder.DropTable(
                name: "CodingSubmission");

            migrationBuilder.DropTable(
                name: "TestCase");

            migrationBuilder.DropTable(
                name: "CodingQuestion");

          
        }
    }
}
