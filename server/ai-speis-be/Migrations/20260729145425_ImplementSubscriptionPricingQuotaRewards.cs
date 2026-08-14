using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class ImplementSubscriptionPricingQuotaRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FreeInterviewQuotaRemaining",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payment",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Payment",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiredAt",
                table: "Payment",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Payment",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "Payment",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PriceId",
                table: "Payment",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "Payment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RewardPointsUsed",
                table: "Payment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RewardAccount",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AvailablePoints = table.Column<int>(type: "int", nullable: false),
                    ReservedPoints = table.Column<int>(type: "int", nullable: false),
                    LifetimeEarnedPoints = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardAccount", x => x.UserId);
                    table.CheckConstraint("CK_RewardAccount_Points", "[AvailablePoints] >= 0 AND [ReservedPoints] >= 0 AND [LifetimeEarnedPoints] >= 0");
                    table.ForeignKey(
                        name: "FK_RewardAccount_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RewardRule",
                columns: table => new
                {
                    RewardRuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PointValueVnd = table.Column<int>(type: "int", nullable: false),
                    PointsExpire = table.Column<bool>(type: "bit", nullable: false),
                    AllowFullPaymentByPoints = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRule", x => x.RewardRuleId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlan",
                columns: table => new
                {
                    PlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InterviewQuota = table.Column<int>(type: "int", nullable: false),
                    QuotaResetDays = table.Column<int>(type: "int", nullable: true),
                    IsFree = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlan", x => x.PlanId);
                    table.CheckConstraint("CK_SubscriptionPlan_InterviewQuota", "[InterviewQuota] >= 0");
                    table.CheckConstraint("CK_SubscriptionPlan_QuotaResetDays", "[QuotaResetDays] IS NULL OR [QuotaResetDays] > 0");
                });

            migrationBuilder.CreateTable(
                name: "RewardTransaction",
                columns: table => new
                {
                    RewardTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardTransaction", x => x.RewardTransactionId);
                    table.ForeignKey(
                        name: "FK_RewardTransaction_RewardAccount_UserId",
                        column: x => x.UserId,
                        principalTable: "RewardAccount",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeature",
                columns: table => new
                {
                    PlanFeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeature", x => x.PlanFeatureId);
                    table.ForeignKey(
                        name: "FK_PlanFeature_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPrice",
                columns: table => new
                {
                    PriceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<int>(type: "int", nullable: false),
                    BillingCycleCount = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPrice", x => x.PriceId);
                    table.CheckConstraint("CK_SubscriptionPrice_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPrice_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSubscription",
                columns: table => new
                {
                    UserSubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubscription", x => x.UserSubscriptionId);
                    table.ForeignKey(
                        name: "FK_UserSubscription_SubscriptionPlan_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlan",
                        principalColumn: "PlanId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSubscription_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotaPeriod",
                columns: table => new
                {
                    QuotaPeriodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuotaLimit = table.Column<int>(type: "int", nullable: false),
                    UsedQuota = table.Column<int>(type: "int", nullable: false),
                    ReservedQuota = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaPeriod", x => x.QuotaPeriodId);
                    table.CheckConstraint("CK_QuotaPeriod_Values", "[QuotaLimit] >= 0 AND [UsedQuota] >= 0 AND [ReservedQuota] >= 0 AND [UsedQuota] + [ReservedQuota] <= [QuotaLimit]");
                    table.ForeignKey(
                        name: "FK_QuotaPeriod_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionTerm",
                columns: table => new
                {
                    SubscriptionTermId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserSubscriptionId = table.Column<int>(type: "int", nullable: false),
                    PriceId = table.Column<int>(type: "int", nullable: false),
                    SourcePaymentId = table.Column<int>(type: "int", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTerm", x => x.SubscriptionTermId);
                    table.ForeignKey(
                        name: "FK_SubscriptionTerm_SubscriptionPrice_PriceId",
                        column: x => x.PriceId,
                        principalTable: "SubscriptionPrice",
                        principalColumn: "PriceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionTerm_UserSubscription_UserSubscriptionId",
                        column: x => x.UserSubscriptionId,
                        principalTable: "UserSubscription",
                        principalColumn: "UserSubscriptionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuotaTransaction",
                columns: table => new
                {
                    QuotaTransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuotaPeriodId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotaTransaction", x => x.QuotaTransactionId);
                    table.ForeignKey(
                        name: "FK_QuotaTransaction_QuotaPeriod_QuotaPeriodId",
                        column: x => x.QuotaPeriodId,
                        principalTable: "QuotaPeriod",
                        principalColumn: "QuotaPeriodId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RewardRule",
                columns: new[] { "RewardRuleId", "AllowFullPaymentByPoints", "EffectiveFrom", "EffectiveTo", "IsActive", "PointValueVnd", "PointsExpire" },
                values: new object[] { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 1, false });

            migrationBuilder.InsertData(
                table: "SubscriptionPlan",
                columns: new[] { "PlanId", "Code", "CreatedAt", "Description", "DisplayOrder", "InterviewQuota", "IsActive", "IsFree", "Name", "QuotaResetDays", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "FREE", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "3 lượt phỏng vấn miễn phí.", 1, 3, true, true, "Gói Cơ Bản", null, null },
                    { 2, "PREMIUM", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "15 lượt phỏng vấn, làm mới sau mỗi 30 ngày.", 2, 15, true, false, "Premium", 30, null }
                });

            migrationBuilder.InsertData(
                table: "PlanFeature",
                columns: new[] { "PlanFeatureId", "DisplayOrder", "FeatureCode", "IsEnabled", "LimitValue", "PlanId" },
                values: new object[,]
                {
                    { 1, 1, "BASIC_AI_INTERVIEW", true, null, 1 },
                    { 2, 2, "GENERAL_SKILL_ASSESSMENT", true, null, 1 },
                    { 3, 1, "COMPREHENSIVE_AI_INTERVIEW", true, null, 2 },
                    { 4, 2, "ADVANCED_ANALYSIS", true, null, 2 },
                    { 5, 3, "QUOTA_REFRESH_30_DAYS", true, null, 2 }
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPrice",
                columns: new[] { "PriceId", "Amount", "BillingCycle", "BillingCycleCount", "CreatedAt", "Currency", "EffectiveFrom", "EffectiveTo", "IsActive", "PlanId", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 59000m, 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 2, null },
                    { 2, 599000m, 2, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "VND", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, 2, null }
                });

            // Backfill legacy users and payments. The previous schema did not preserve a
            // separate free balance for premium users, so the only recoverable value is
            // the legacy remaining quota clamped to the new lifetime free allowance.
            migrationBuilder.Sql(@"
                UPDATE [User]
                SET [FreeInterviewQuotaRemaining] = CASE
                    WHEN [RemainingInterviewQuota] < 0 THEN 0
                    WHEN [RemainingInterviewQuota] > 3 THEN 3
                    ELSE [RemainingInterviewQuota]
                END;

                UPDATE [User]
                SET [RemainingInterviewQuota] = [FreeInterviewQuotaRemaining]
                WHERE [IsPremium] = 0 OR [PremiumExpireAt] IS NULL OR [PremiumExpireAt] <= SYSUTCDATETIME();

                UPDATE [Payment]
                SET [OriginalAmount] = [Amount],
                    [Currency] = 'VND',
                    [PriceId] = CASE WHEN [PackageId] IN (1, 2) THEN [PackageId] ELSE NULL END;

                INSERT INTO [RewardAccount] ([UserId], [AvailablePoints], [ReservedPoints], [LifetimeEarnedPoints])
                SELECT [UserId], 0, 0, 0 FROM [User];

                INSERT INTO [UserSubscription]
                    ([UserId], [PlanId], [Status], [StartedAt], [ExpiresAt], [CreatedAt], [UpdatedAt])
                SELECT
                    [UserId],
                    CASE WHEN [IsPremium] = 1 AND [PremiumExpireAt] > SYSUTCDATETIME() THEN 2 ELSE 1 END,
                    2,
                    CASE
                        WHEN [IsPremium] = 1 AND [PremiumExpireAt] > SYSUTCDATETIME()
                            THEN COALESCE([LastQuotaResetAt], [CreatedAt])
                        ELSE [CreatedAt]
                    END,
                    CASE WHEN [IsPremium] = 1 AND [PremiumExpireAt] > SYSUTCDATETIME() THEN [PremiumExpireAt] ELSE NULL END,
                    [CreatedAt],
                    SYSUTCDATETIME()
                FROM [User];

                INSERT INTO [QuotaPeriod]
                    ([UserSubscriptionId], [PeriodStart], [PeriodEnd], [QuotaLimit], [UsedQuota], [ReservedQuota])
                SELECT
                    s.[UserSubscriptionId],
                    s.[StartedAt],
                    CASE WHEN s.[PlanId] = 2 THEN DATEADD(day, 30, s.[StartedAt]) ELSE NULL END,
                    CASE WHEN s.[PlanId] = 2 THEN 15 ELSE 3 END,
                    CASE
                        WHEN s.[PlanId] = 2 THEN 15 - CASE
                            WHEN u.[RemainingInterviewQuota] < 0 THEN 0
                            WHEN u.[RemainingInterviewQuota] > 15 THEN 15
                            ELSE u.[RemainingInterviewQuota]
                        END
                        ELSE 3 - u.[FreeInterviewQuotaRemaining]
                    END,
                    0
                FROM [UserSubscription] s
                INNER JOIN [User] u ON u.[UserId] = s.[UserId];
            ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_FreeInterviewQuotaRemaining",
                table: "User",
                sql: "[FreeInterviewQuotaRemaining] >= 0 AND [FreeInterviewQuotaRemaining] <= 3");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_PriceId",
                table: "Payment",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ProviderTransactionId",
                table: "Payment",
                column: "ProviderTransactionId",
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeature_PlanId_FeatureCode",
                table: "PlanFeature",
                columns: new[] { "PlanId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotaPeriod_UserSubscriptionId_PeriodStart",
                table: "QuotaPeriod",
                columns: new[] { "UserSubscriptionId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotaTransaction_QuotaPeriodId_Type_ReferenceType_ReferenceId",
                table: "QuotaTransaction",
                columns: new[] { "QuotaPeriodId", "Type", "ReferenceType", "ReferenceId" },
                unique: true,
                filter: "[ReferenceType] IS NOT NULL AND [ReferenceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RewardTransaction_UserId_Type_ReferenceType_ReferenceId",
                table: "RewardTransaction",
                columns: new[] { "UserId", "Type", "ReferenceType", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlan_Code",
                table: "SubscriptionPlan",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPrice_PlanId_BillingCycle_IsActive",
                table: "SubscriptionPrice",
                columns: new[] { "PlanId", "BillingCycle", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTerm_PriceId",
                table: "SubscriptionTerm",
                column: "PriceId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTerm_SourcePaymentId",
                table: "SubscriptionTerm",
                column: "SourcePaymentId",
                unique: true,
                filter: "[SourcePaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTerm_UserSubscriptionId_StartsAt_EndsAt",
                table: "SubscriptionTerm",
                columns: new[] { "UserSubscriptionId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscription_PlanId",
                table: "UserSubscription",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscription_UserId",
                table: "UserSubscription",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payment_SubscriptionPrice_PriceId",
                table: "Payment",
                column: "PriceId",
                principalTable: "SubscriptionPrice",
                principalColumn: "PriceId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payment_SubscriptionPrice_PriceId",
                table: "Payment");

            migrationBuilder.DropTable(
                name: "PlanFeature");

            migrationBuilder.DropTable(
                name: "QuotaTransaction");

            migrationBuilder.DropTable(
                name: "RewardRule");

            migrationBuilder.DropTable(
                name: "RewardTransaction");

            migrationBuilder.DropTable(
                name: "SubscriptionTerm");

            migrationBuilder.DropTable(
                name: "QuotaPeriod");

            migrationBuilder.DropTable(
                name: "RewardAccount");

            migrationBuilder.DropTable(
                name: "SubscriptionPrice");

            migrationBuilder.DropTable(
                name: "UserSubscription");

            migrationBuilder.DropTable(
                name: "SubscriptionPlan");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_FreeInterviewQuotaRemaining",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Payment_PriceId",
                table: "Payment");

            migrationBuilder.DropIndex(
                name: "IX_Payment_ProviderTransactionId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "FreeInterviewQuotaRemaining",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "PriceId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "Payment");

            migrationBuilder.DropColumn(
                name: "RewardPointsUsed",
                table: "Payment");

        }
    }
}
