using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    public partial class AddAiJsonRecoveryObservability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RawResponse')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [RawResponse] nvarchar(max) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RecoveryStatus')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [RecoveryStatus] nvarchar(80) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RecoveryFlags')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [RecoveryFlags] nvarchar(1000) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonExceptionType')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [JsonExceptionType] nvarchar(120) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonErrorPath')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [JsonErrorPath] nvarchar(500) NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonErrorOffset')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [JsonErrorOffset] bigint NULL;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'SchemaVersion')
                    ALTER TABLE [dbo].[AIInteractionLog] ADD [SchemaVersion] nvarchar(80) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RawResponse')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [RawResponse];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RecoveryStatus')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [RecoveryStatus];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'RecoveryFlags')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [RecoveryFlags];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonExceptionType')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [JsonExceptionType];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonErrorPath')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [JsonErrorPath];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'JsonErrorOffset')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [JsonErrorOffset];

                IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AIInteractionLog]') AND name = N'SchemaVersion')
                    ALTER TABLE [dbo].[AIInteractionLog] DROP COLUMN [SchemaVersion];
            ");
        }
    }
}
