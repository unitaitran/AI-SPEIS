using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ai_speis_be.Models;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260723100000_UpdateCampaignDurationConstraint")]
    public partial class UpdateCampaignDurationConstraint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_InterviewCampaign_DurationMinutes')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP CONSTRAINT [CK_InterviewCampaign_DurationMinutes];
                END

                ALTER TABLE [dbo].[InterviewCampaign] ADD CONSTRAINT [CK_InterviewCampaign_DurationMinutes]
                    CHECK ([DurationMinutes] >= 5 AND [DurationMinutes] <= 120);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_InterviewCampaign_DurationMinutes')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP CONSTRAINT [CK_InterviewCampaign_DurationMinutes];
                END

                ALTER TABLE [dbo].[InterviewCampaign] ADD CONSTRAINT [CK_InterviewCampaign_DurationMinutes]
                    CHECK ([DurationMinutes] IN (10, 15, 20));
            ");
        }
    }
}
