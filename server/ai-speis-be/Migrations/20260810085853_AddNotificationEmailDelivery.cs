using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeliveryChannel",
                table: "Notification",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryLeaseExpiresAt",
                table: "Notification",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryLeaseToken",
                table: "Notification",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryStatus",
                table: "Notification",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmailBody",
                table: "Notification",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailSubject",
                table: "Notification",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "Notification",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "Notification",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "Notification",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Notification",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_EmailRetry",
                table: "Notification",
                columns: new[] { "DeliveryChannel", "DeliveryStatus", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notification_EmailRetry",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "DeliveryChannel",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "DeliveryLeaseExpiresAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "DeliveryLeaseToken",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailBody",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "EmailSubject",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "Notification");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Notification");
        }
    }
}
