using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewSessionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterviewSession",
                columns: table => new
                {
                    InterviewSessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CVExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    JDExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    InterviewRoundType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSession", x => x.InterviewSessionId);
                    table.ForeignKey(
                        name: "FK_InterviewSession_CVExtractedProfile_CVExtractedProfileId",
                        column: x => x.CVExtractedProfileId,
                        principalTable: "CVExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewSession_JDExtractedProfile_JDExtractedProfileId",
                        column: x => x.JDExtractedProfileId,
                        principalTable: "JDExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewSession_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSession_CVExtractedProfileId",
                table: "InterviewSession",
                column: "CVExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSession_JDExtractedProfileId",
                table: "InterviewSession",
                column: "JDExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSession_UserId",
                table: "InterviewSession",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewSession");
        }
    }
}
