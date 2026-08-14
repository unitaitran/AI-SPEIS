using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewCampaignTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSession_CVExtractedProfile_CVExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSession_JDExtractedProfile_JDExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSession_User_UserId",
                table: "InterviewSession");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSession_CVExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropIndex(
                name: "IX_InterviewSession_JDExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "CVExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "JDExtractedProfileId",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "InterviewSession");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "InterviewSession",
                newName: "InterviewCampaignId");

            migrationBuilder.RenameIndex(
                name: "IX_InterviewSession_UserId",
                table: "InterviewSession",
                newName: "IX_InterviewSession_InterviewCampaignId");

            migrationBuilder.CreateTable(
                name: "InterviewCampaign",
                columns: table => new
                {
                    InterviewCampaignId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CVExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    JDExtractedProfileId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewCampaign", x => x.InterviewCampaignId);
                    table.ForeignKey(
                        name: "FK_InterviewCampaign_CVExtractedProfile_CVExtractedProfileId",
                        column: x => x.CVExtractedProfileId,
                        principalTable: "CVExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewCampaign_JDExtractedProfile_JDExtractedProfileId",
                        column: x => x.JDExtractedProfileId,
                        principalTable: "JDExtractedProfile",
                        principalColumn: "ExtractedProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewCampaign_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewCampaign_CVExtractedProfileId",
                table: "InterviewCampaign",
                column: "CVExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewCampaign_JDExtractedProfileId",
                table: "InterviewCampaign",
                column: "JDExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewCampaign_UserId",
                table: "InterviewCampaign",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSession_InterviewCampaign_InterviewCampaignId",
                table: "InterviewSession",
                column: "InterviewCampaignId",
                principalTable: "InterviewCampaign",
                principalColumn: "InterviewCampaignId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InterviewSession_InterviewCampaign_InterviewCampaignId",
                table: "InterviewSession");

            migrationBuilder.DropTable(
                name: "InterviewCampaign");

            migrationBuilder.RenameColumn(
                name: "InterviewCampaignId",
                table: "InterviewSession",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_InterviewSession_InterviewCampaignId",
                table: "InterviewSession",
                newName: "IX_InterviewSession_UserId");

            migrationBuilder.AddColumn<int>(
                name: "CVExtractedProfileId",
                table: "InterviewSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "JDExtractedProfileId",
                table: "InterviewSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "InterviewSession",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "InterviewSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSession_CVExtractedProfileId",
                table: "InterviewSession",
                column: "CVExtractedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSession_JDExtractedProfileId",
                table: "InterviewSession",
                column: "JDExtractedProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSession_CVExtractedProfile_CVExtractedProfileId",
                table: "InterviewSession",
                column: "CVExtractedProfileId",
                principalTable: "CVExtractedProfile",
                principalColumn: "ExtractedProfileId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSession_JDExtractedProfile_JDExtractedProfileId",
                table: "InterviewSession",
                column: "JDExtractedProfileId",
                principalTable: "JDExtractedProfile",
                principalColumn: "ExtractedProfileId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InterviewSession_User_UserId",
                table: "InterviewSession",
                column: "UserId",
                principalTable: "User",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
