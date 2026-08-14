using System;
using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260712143000_AddInterviewCampaignLifecycleAndQuota")]
    public partial class AddInterviewCampaignLifecycleAndQuota : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "InterviewCampaign",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "InterviewCampaign",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "InterviewCampaign",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "QuotaRefunded",
                table: "InterviewCampaign",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "InterviewCampaign",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "InterviewCampaign",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RemainingInterviewQuota",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 5);

            // Campaigns created before quota reservation existed must never refund a quota they did not consume.
            migrationBuilder.Sql(
                "UPDATE [InterviewCampaign] "
                + "SET [ExpiresAt] = DATEADD(MINUTE, 30, [CreatedAt]), [QuotaRefunded] = 1, "
                + "[Status] = CASE WHEN DATEADD(MINUTE, 30, [CreatedAt]) <= GETUTCDATE() THEN 4 ELSE 0 END");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewCampaign_UserId_Status",
                table: "InterviewCampaign",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InterviewCampaign_DurationMinutes",
                table: "InterviewCampaign",
                sql: "[DurationMinutes] IN (10, 15, 20)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_RemainingInterviewQuota",
                table: "User",
                sql: "[RemainingInterviewQuota] >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InterviewCampaign_DurationMinutes",
                table: "InterviewCampaign");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_RemainingInterviewQuota",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_InterviewCampaign_UserId_Status",
                table: "InterviewCampaign");

            migrationBuilder.DropColumn(name: "CancelledAt", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "CompletedAt", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "ExpiresAt", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "QuotaRefunded", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "StartedAt", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "Status", table: "InterviewCampaign");
            migrationBuilder.DropColumn(name: "RemainingInterviewQuota", table: "User");
        }
    }
}
