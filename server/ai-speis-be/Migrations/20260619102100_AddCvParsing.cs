using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddCvParsing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bỏ qua do IsLocked đã có trong DB


            migrationBuilder.CreateTable(
                name: "CVExtractedProfile",
                columns: table => new
                {
                    ExtractedProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CVFileId = table.Column<int>(type: "int", nullable: false),
                    Education = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Experience = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawAiOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVExtractedProfile", x => x.ExtractedProfileId);
                    table.ForeignKey(
                        name: "FK_CVExtractedProfile_CVFile_CVFileId",
                        column: x => x.CVFileId,
                        principalTable: "CVFile",
                        principalColumn: "CVFileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CVExtractedProfile_User_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CVProject",
                columns: table => new
                {
                    CVProjectId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RoleDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TechnologyStack = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVProject", x => x.CVProjectId);
                    table.ForeignKey(
                        name: "FK_CVProject_CVExtractedProfile_ExtractedProfileId",
                        column: x => x.ExtractedProfileId,
                        principalTable: "CVExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CVSkill",
                columns: table => new
                {
                    CVSkillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CVSkill", x => x.CVSkillId);
                    table.ForeignKey(
                        name: "FK_CVSkill_CVExtractedProfile_ExtractedProfileId",
                        column: x => x.ExtractedProfileId,
                        principalTable: "CVExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CVExtractedProfile_ConfirmedBy",
                table: "CVExtractedProfile",
                column: "ConfirmedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CVExtractedProfile_CVFileId",
                table: "CVExtractedProfile",
                column: "CVFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CVProject_ExtractedProfileId",
                table: "CVProject",
                column: "ExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CVSkill_ExtractedProfileId",
                table: "CVSkill",
                column: "ExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CVSkill_ExtractedProfileId_SkillName",
                table: "CVSkill",
                columns: new[] { "ExtractedProfileId", "SkillName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CVProject");

            migrationBuilder.DropTable(
                name: "CVSkill");

            migrationBuilder.DropTable(
                name: "CVExtractedProfile");

            // Bỏ qua drop columns
        }
    }
}
