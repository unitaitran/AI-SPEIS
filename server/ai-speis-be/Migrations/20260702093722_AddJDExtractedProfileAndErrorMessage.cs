using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddJDExtractedProfileAndErrorMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "JDFile",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JDExtractedProfile",
                columns: table => new
                {
                    ExtractedProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JDFileId = table.Column<int>(type: "int", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ExperienceLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequiredSkills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NiceToHaveSkills = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsibilities = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyCharacteristics = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawAiOutput = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmedBy = table.Column<int>(type: "int", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JDExtractedProfile", x => x.ExtractedProfileId);
                    table.ForeignKey(
                        name: "FK_JDExtractedProfile_JDFile_JDFileId",
                        column: x => x.JDFileId,
                        principalTable: "JDFile",
                        principalColumn: "JDFileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JDExtractedProfile_User_ConfirmedBy",
                        column: x => x.ConfirmedBy,
                        principalTable: "User",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JDExtractedProfile_ConfirmedBy",
                table: "JDExtractedProfile",
                column: "ConfirmedBy");

            migrationBuilder.CreateIndex(
                name: "IX_JDExtractedProfile_JDFileId",
                table: "JDExtractedProfile",
                column: "JDFileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JDExtractedProfile");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "JDFile");
        }
    }
}
