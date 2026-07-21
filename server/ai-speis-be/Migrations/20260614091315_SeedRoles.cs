using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use idempotent SQL to avoid primary key violations when roles already exist
            // IDENTITY_INSERT must be ON to insert explicit values into an IDENTITY column
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [Role] ON;
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleId] = 1)
    INSERT INTO [Role] ([RoleId], [Description], [RoleName], [Status])
    VALUES (1, N'Quản trị viên', N'admin', CAST(1 AS bit));
IF NOT EXISTS (SELECT 1 FROM [Role] WHERE [RoleId] = 2)
    INSERT INTO [Role] ([RoleId], [Description], [RoleName], [Status])
    VALUES (2, N'Người dùng', N'user', CAST(1 AS bit));
SET IDENTITY_INSERT [Role] OFF;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Role",
                keyColumn: "RoleId",
                keyValue: 2);
        }
    }
}
