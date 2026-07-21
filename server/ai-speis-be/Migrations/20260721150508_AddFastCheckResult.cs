using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddFastCheckResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FastCheckResult",
                columns: table => new
                {
                    FastCheckResultId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CVFileId = table.Column<int>(type: "int", nullable: false),
                    JDFileId = table.Column<int>(type: "int", nullable: false),
                    MatchScore = table.Column<int>(type: "int", nullable: false),
                    SuitabilityLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MatchingSkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MissingSkillsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Advice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawAiResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FastCheckResult", x => x.FastCheckResultId);
                    table.ForeignKey(
                        name: "FK_FastCheckResult_CVFile_CVFileId",
                        column: x => x.CVFileId,
                        principalTable: "CVFile",
                        principalColumn: "CVFileId");
                    table.ForeignKey(
                        name: "FK_FastCheckResult_JDFile_JDFileId",
                        column: x => x.JDFileId,
                        principalTable: "JDFile",
                        principalColumn: "JDFileId");
                    table.ForeignKey(
                        name: "FK_FastCheckResult_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FastCheckResult_CVFileId",
                table: "FastCheckResult",
                column: "CVFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FastCheckResult_JDFileId",
                table: "FastCheckResult",
                column: "JDFileId");

            migrationBuilder.CreateIndex(
                name: "IX_FastCheckResult_User_CV_JD",
                table: "FastCheckResult",
                columns: new[] { "UserId", "CVFileId", "JDFileId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FastCheckResult");
        }
    }
}
