using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    public partial class AddAiJsonRecoveryObservability : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "RawResponse", table: "AIInteractionLog", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RecoveryStatus", table: "AIInteractionLog", type: "nvarchar(80)", maxLength: 80, nullable: true);
            migrationBuilder.AddColumn<string>(name: "RecoveryFlags", table: "AIInteractionLog", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
            migrationBuilder.AddColumn<string>(name: "JsonExceptionType", table: "AIInteractionLog", type: "nvarchar(120)", maxLength: 120, nullable: true);
            migrationBuilder.AddColumn<string>(name: "JsonErrorPath", table: "AIInteractionLog", type: "nvarchar(500)", maxLength: 500, nullable: true);
            migrationBuilder.AddColumn<long>(name: "JsonErrorOffset", table: "AIInteractionLog", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SchemaVersion", table: "AIInteractionLog", type: "nvarchar(80)", maxLength: 80, nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RawResponse", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "RecoveryStatus", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "RecoveryFlags", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "JsonExceptionType", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "JsonErrorPath", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "JsonErrorOffset", table: "AIInteractionLog");
            migrationBuilder.DropColumn(name: "SchemaVersion", table: "AIInteractionLog");
        }
    }
}
