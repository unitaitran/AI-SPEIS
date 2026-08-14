using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ai_speis_be.Models;

#nullable disable

namespace ai_speis_be.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260723090000_OptimizeInterviewEvaluationFeedback")]
    public partial class OptimizeInterviewEvaluationFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An earlier branch shipped
            // 20260722190000_OptimizeInterviewEvaluationAndRoundFeedback and was
            // later replaced without a compensating migration. Some databases
            // therefore already contain a subset of these columns and indexes.
            // Reconcile both schemas so this migration also works on a clean DB.
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'AnswerVersion') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [AnswerVersion] int NOT NULL
                        CONSTRAINT [DF_BehaviourAnswer_AnswerVersion] DEFAULT (1);
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'AudioId') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [AudioId] nvarchar(200) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'SubmissionIdempotencyKey') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [SubmissionIdempotencyKey] nvarchar(128) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'AiEvidenceJson') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [AiEvidenceJson] nvarchar(max) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'AiMissingAspectsJson') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [AiMissingAspectsJson] nvarchar(max) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'FinalQuestionScore') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [FinalQuestionScore] decimal(18,2) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationModel') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationModel] nvarchar(120) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationPromptVersion') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationPromptVersion] nvarchar(80) NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationInputTokens') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationInputTokens] int NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationOutputTokens') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationOutputTokens] int NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationLatencyMs') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationLatencyMs] bigint NULL;
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationRetryCount') IS NULL
                    ALTER TABLE [dbo].[BehaviourAnswer] ADD [EvaluationRetryCount] int NOT NULL
                        CONSTRAINT [DF_BehaviourAnswer_EvaluationRetryCount] DEFAULT (0);

                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackJson') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackJson] nvarchar(max) NULL;

                IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_InterviewCampaign_DurationMinutes')
                BEGIN
                    ALTER TABLE [dbo].[InterviewCampaign] DROP CONSTRAINT [CK_InterviewCampaign_DurationMinutes];
                    ALTER TABLE [dbo].[InterviewCampaign] ADD CONSTRAINT [CK_InterviewCampaign_DurationMinutes]
                        CHECK ([DurationMinutes] >= 5 AND [DurationMinutes] <= 120);
                END

                -- The superseded migration stored this status as an enum/int.
                -- Replace it with the string status expected by the current model.
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackStatus') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackStatus] nvarchar(30) NOT NULL
                        CONSTRAINT [DF_BehaviourRoundResult_FinalFeedbackStatus] DEFAULT (N'NOT_STARTED');
                END
                ELSE IF EXISTS (
                    SELECT 1
                    FROM sys.columns c
                    WHERE c.object_id = OBJECT_ID(N'dbo.BehaviourRoundResult')
                      AND c.name = N'FinalFeedbackStatus'
                      AND TYPE_NAME(c.user_type_id) <> N'nvarchar')
                BEGIN
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackStatus_New] nvarchar(30) NULL;
                    EXEC(N'UPDATE [dbo].[BehaviourRoundResult]
                           SET [FinalFeedbackStatus_New] = CASE [FinalFeedbackStatus]
                               WHEN 1 THEN N''PROCESSING''
                               WHEN 2 THEN N''COMPLETED''
                               WHEN 3 THEN N''FAILED''
                               ELSE N''NOT_STARTED''
                           END');

                    DECLARE @FinalStatusDefault sysname;
                    SELECT @FinalStatusDefault = dc.name
                    FROM sys.default_constraints dc
                    JOIN sys.columns c
                      ON c.object_id = dc.parent_object_id
                     AND c.column_id = dc.parent_column_id
                    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.BehaviourRoundResult')
                      AND c.name = N'FinalFeedbackStatus';
                    IF @FinalStatusDefault IS NOT NULL
                    BEGIN
                        DECLARE @DropFinalStatusDefault nvarchar(max) =
                            N'ALTER TABLE [dbo].[BehaviourRoundResult] DROP CONSTRAINT ' +
                            QUOTENAME(@FinalStatusDefault);
                        EXEC sp_executesql @DropFinalStatusDefault;
                    END;

                    ALTER TABLE [dbo].[BehaviourRoundResult] DROP COLUMN [FinalFeedbackStatus];
                    EXEC sp_rename
                        N'dbo.BehaviourRoundResult.FinalFeedbackStatus_New',
                        N'FinalFeedbackStatus',
                        N'COLUMN';
                    ALTER TABLE [dbo].[BehaviourRoundResult]
                        ALTER COLUMN [FinalFeedbackStatus] nvarchar(30) NOT NULL;
                    ALTER TABLE [dbo].[BehaviourRoundResult]
                        ADD CONSTRAINT [DF_BehaviourRoundResult_FinalFeedbackStatus]
                        DEFAULT (N'NOT_STARTED') FOR [FinalFeedbackStatus];
                END;

                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackStartedAt') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackStartedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackConcurrencyVersion') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FeedbackConcurrencyVersion] int NOT NULL
                        CONSTRAINT [DF_BehaviourRoundResult_FeedbackConcurrencyVersion] DEFAULT (0);
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackModel') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackModel] nvarchar(120) NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackPromptVersion') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackPromptVersion] nvarchar(80) NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackInputTokens') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FeedbackInputTokens] int NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackOutputTokens') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FeedbackOutputTokens] int NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackLatencyMs') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FeedbackLatencyMs] bigint NULL;
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackRetryCount') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FeedbackRetryCount] int NOT NULL
                        CONSTRAINT [DF_BehaviourRoundResult_FeedbackRetryCount] DEFAULT (0);
                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FinalFeedbackError') IS NULL
                    ALTER TABLE [dbo].[BehaviourRoundResult] ADD [FinalFeedbackError] nvarchar(100) NULL;

                IF COL_LENGTH(N'dbo.BehaviourRoundResult', N'FeedbackError') IS NOT NULL
                    EXEC(N'UPDATE [dbo].[BehaviourRoundResult]
                           SET [FinalFeedbackError] =
                               COALESCE([FinalFeedbackError], LEFT([FeedbackError], 100))');

                IF COL_LENGTH(N'dbo.InterviewSession', N'TechnicalFinalFeedbackStatus') IS NULL
                    ALTER TABLE [dbo].[InterviewSession] ADD [TechnicalFinalFeedbackStatus] nvarchar(30) NOT NULL
                        CONSTRAINT [DF_InterviewSession_TechnicalFinalFeedbackStatus] DEFAULT (N'NOT_STARTED');
                IF COL_LENGTH(N'dbo.InterviewSession', N'TechnicalFinalFeedbackStartedAt') IS NULL
                    ALTER TABLE [dbo].[InterviewSession] ADD [TechnicalFinalFeedbackStartedAt] datetime2 NULL;
                IF COL_LENGTH(N'dbo.InterviewSession', N'TechnicalFinalFeedbackError') IS NULL
                    ALTER TABLE [dbo].[InterviewSession] ADD [TechnicalFinalFeedbackError] nvarchar(100) NULL;

                IF COL_LENGTH(N'dbo.InterviewSession', N'TechnicalFeedbackStatus') IS NOT NULL
                    EXEC(N'UPDATE [dbo].[InterviewSession]
                           SET [TechnicalFinalFeedbackStatus] =
                               CASE [TechnicalFeedbackStatus]
                                   WHEN 1 THEN N''PROCESSING''
                                   WHEN 2 THEN N''COMPLETED''
                                   WHEN 3 THEN N''FAILED''
                                   ELSE N''NOT_STARTED''
                               END');
                IF COL_LENGTH(N'dbo.InterviewSession', N'TechnicalFeedbackError') IS NOT NULL
                    EXEC(N'UPDATE [dbo].[InterviewSession]
                           SET [TechnicalFinalFeedbackError] =
                               COALESCE([TechnicalFinalFeedbackError],
                                        LEFT([TechnicalFeedbackError], 100))');
                IF COL_LENGTH(N'dbo.BehaviourAnswer', N'EvaluationError') IS NOT NULL
                   AND COL_LENGTH(N'dbo.BehaviourAnswer', N'AiErrorCode') IS NOT NULL
                    EXEC(N'UPDATE [dbo].[BehaviourAnswer]
                           SET [AiErrorCode] =
                               COALESCE([AiErrorCode], [EvaluationError])');

                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.BehaviourRoundResult')
                      AND name = N'IX_BehaviourRoundResult_InterviewSessionId'
                      AND is_unique = 0)
                    EXEC(N'DROP INDEX [IX_BehaviourRoundResult_InterviewSessionId]
                        ON [dbo].[BehaviourRoundResult]');
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.BehaviourRoundResult')
                      AND name = N'IX_BehaviourRoundResult_InterviewSessionId')
                    EXEC(N'CREATE UNIQUE INDEX [IX_BehaviourRoundResult_InterviewSessionId]
                        ON [dbo].[BehaviourRoundResult] ([InterviewSessionId])');

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'dbo.BehaviourAnswer')
                      AND name = N'IX_BehaviourAnswer_SubmissionIdempotencyKey')
                    EXEC(N'CREATE UNIQUE INDEX [IX_BehaviourAnswer_SubmissionIdempotencyKey]
                        ON [dbo].[BehaviourAnswer] ([SubmissionIdempotencyKey])
                        WHERE [SubmissionIdempotencyKey] IS NOT NULL');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_BehaviourAnswer_SubmissionIdempotencyKey", table: "BehaviourAnswer");
            migrationBuilder.DropIndex(name: "IX_BehaviourRoundResult_InterviewSessionId", table: "BehaviourRoundResult");
            migrationBuilder.CreateIndex(
                name: "IX_BehaviourRoundResult_InterviewSessionId",
                table: "BehaviourRoundResult",
                column: "InterviewSessionId");

            foreach (var column in new[]
            {
                "AnswerVersion", "AudioId", "SubmissionIdempotencyKey", "AiEvidenceJson",
                "AiMissingAspectsJson", "FinalQuestionScore", "EvaluationModel",
                "EvaluationPromptVersion", "EvaluationInputTokens", "EvaluationOutputTokens",
                "EvaluationLatencyMs", "EvaluationRetryCount"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "BehaviourAnswer");
            }

            foreach (var column in new[]
            {
                "FinalFeedbackJson", "FinalFeedbackStatus", "FinalFeedbackModel",
                "FinalFeedbackPromptVersion", "FeedbackInputTokens", "FeedbackOutputTokens",
                "FeedbackLatencyMs", "FeedbackRetryCount", "FinalFeedbackError",
                "FinalFeedbackStartedAt", "FeedbackConcurrencyVersion"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "BehaviourRoundResult");
            }

            foreach (var column in new[]
            {
                "TechnicalFinalFeedbackStatus", "TechnicalFinalFeedbackStartedAt", "TechnicalFinalFeedbackError"
            })
            {
                migrationBuilder.DropColumn(name: column, table: "InterviewSession");
            }
        }
    }
}
