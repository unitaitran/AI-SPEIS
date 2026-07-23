using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class EnsureInterviewCampaignColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'DashboardMetricsJson')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [DashboardMetricsJson] nvarchar(max) NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'OverallScore')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [OverallScore] decimal(5,2) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'DashboardMetricsJson')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [DashboardMetricsJson];
                END

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'OverallScore')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [OverallScore];
                END
            ");
        }
    }
}
