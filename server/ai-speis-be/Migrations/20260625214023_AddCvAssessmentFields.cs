using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddCvAssessmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "CVExtractedProfile",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverallAssessment",
                table: "CVExtractedProfile",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Strengths",
                table: "CVExtractedProfile",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Weaknesses",
                table: "CVExtractedProfile",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "CVExtractedProfile");

            migrationBuilder.DropColumn(
                name: "OverallAssessment",
                table: "CVExtractedProfile");

            migrationBuilder.DropColumn(
                name: "Strengths",
                table: "CVExtractedProfile");

            migrationBuilder.DropColumn(
                name: "Weaknesses",
                table: "CVExtractedProfile");
        }
    }
}
