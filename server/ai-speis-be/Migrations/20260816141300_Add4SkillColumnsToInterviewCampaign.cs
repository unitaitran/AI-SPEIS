using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class Add4SkillColumnsToInterviewCampaign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder builder)
        {
            builder.Sql(@"
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'ProfessionalKnowledge') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [ProfessionalKnowledge] decimal(5,2) NULL;
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'CommunicationSkill') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [CommunicationSkill] decimal(5,2) NULL;
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'CvUnderstanding') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [CvUnderstanding] decimal(5,2) NULL;
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'ProblemSolving') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [ProblemSolving] decimal(5,2) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder builder)
        {
            builder.Sql(@"
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'ProfessionalKnowledge') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [ProfessionalKnowledge];
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'CommunicationSkill') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [CommunicationSkill];
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'CvUnderstanding') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [CvUnderstanding];
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'ProblemSolving') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP COLUMN [ProblemSolving];
                END
            ");
        }
    }
}
