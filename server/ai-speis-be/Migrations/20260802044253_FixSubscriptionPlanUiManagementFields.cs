using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class FixSubscriptionPlanUiManagementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdvancedAnalyticsEnabled",
                table: "SubscriptionPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AiTier",
                table: "SubscriptionPlan",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ADVANCED");

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                table: "SubscriptionPlan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "SubscriptionPlan",
                keyColumn: "PlanId",
                keyValue: 1,
                columns: new[] { "AdvancedAnalyticsEnabled", "AiTier", "IsPopular" },
                values: new object[] { false, "STANDARD", false });

            migrationBuilder.UpdateData(
                table: "SubscriptionPlan",
                keyColumn: "PlanId",
                keyValue: 2,
                columns: new[] { "AdvancedAnalyticsEnabled", "AiTier", "IsPopular" },
                values: new object[] { true, "ADVANCED", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvancedAnalyticsEnabled",
                table: "SubscriptionPlan");

            migrationBuilder.DropColumn(
                name: "AiTier",
                table: "SubscriptionPlan");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                table: "SubscriptionPlan");
        }
    }
}
