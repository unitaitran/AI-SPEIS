using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIncorrectClaimsFromTechnicalAnswerEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InterviewCampaign_DurationMinutes",
                table: "InterviewCampaign");

            migrationBuilder.DropColumn(
                name: "IncorrectClaimsJson",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.Sql(@"
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'DashboardMetricsJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [DashboardMetricsJson] nvarchar(max) NULL;
                END
                IF COL_LENGTH(N'dbo.InterviewCampaign', N'OverallScore') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] ADD [OverallScore] decimal(18,2) NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.UserSkillScore', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[UserSkillScore] (
                        [UserSkillScoreId] int NOT NULL IDENTITY,
                        [UserId] int NOT NULL,
                        [InterviewCampaignId] int NULL,
                        [InterviewSessionId] int NULL,
                        [SkillCode] nvarchar(100) NOT NULL,
                        [SkillName] nvarchar(200) NOT NULL,
                        [Score] decimal(5,2) NOT NULL,
                        [SessionTitle] nvarchar(255) NULL,
                        [EvaluatedAt] datetime2 NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_UserSkillScore] PRIMARY KEY ([UserSkillScoreId]),
                        CONSTRAINT [FK_UserSkillScore_InterviewCampaign_InterviewCampaignId] FOREIGN KEY ([InterviewCampaignId]) REFERENCES [InterviewCampaign] ([InterviewCampaignId]),
                        CONSTRAINT [FK_UserSkillScore_InterviewSession_InterviewSessionId] FOREIGN KEY ([InterviewSessionId]) REFERENCES [InterviewSession] ([InterviewSessionId]),
                        CONSTRAINT [FK_UserSkillScore_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_UserSkillScore_InterviewCampaignId] ON [dbo].[UserSkillScore] ([InterviewCampaignId]);
                    CREATE INDEX [IX_UserSkillScore_InterviewSessionId] ON [dbo].[UserSkillScore] ([InterviewSessionId]);
                    CREATE INDEX [IX_UserSkillScore_UserId] ON [dbo].[UserSkillScore] ([UserId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSkillScore");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InterviewCampaign_DurationMinutes",
                table: "InterviewCampaign");

            migrationBuilder.DropColumn(
                name: "DashboardMetricsJson",
                table: "InterviewCampaign");

            migrationBuilder.DropColumn(
                name: "OverallScore",
                table: "InterviewCampaign");

            migrationBuilder.AddColumn<string>(
                name: "IncorrectClaimsJson",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InterviewCampaign_DurationMinutes",
                table: "InterviewCampaign",
                sql: "[DurationMinutes] IN (10, 15, 20)");
        }
    }
}
