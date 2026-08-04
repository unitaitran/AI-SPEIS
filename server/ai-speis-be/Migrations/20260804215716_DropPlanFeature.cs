using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class DropPlanFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanFeature");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanFeature",
                columns: table => new
                {
                    PlanFeatureId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    FeatureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LimitValue = table.Column<int>(type: "int", nullable: true)
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

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeature_PlanId_FeatureCode",
                table: "PlanFeature",
                columns: new[] { "PlanId", "FeatureCode" },
                unique: true);
        }
    }
}
