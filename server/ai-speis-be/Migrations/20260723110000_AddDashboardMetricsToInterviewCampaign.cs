using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardMetricsToInterviewCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder builder)
        {
            builder.Sql(@"
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'DashboardMetricsJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [DashboardMetricsJson] nvarchar(max) NULL;
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'OverallScore') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [OverallScore] decimal(5,2) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder builder)
        {
            builder.Sql(@"
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'DashboardMetricsJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [DashboardMetricsJson];
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'OverallScore') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [OverallScore];
                END
            ");
        }
    }
}
