using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    public partial class AddQuestionTrashAndPurgeLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // History keeps QuestionId only as an audit identifier; snapshots are durable.
            migrationBuilder.DropForeignKey(name: "FK_BehaviourSessionQuestion_Question_QuestionId", table: "BehaviourSessionQuestion");
            migrationBuilder.DropForeignKey(name: "FK_SingleQuestionRetry_Question_QuestionId", table: "SingleQuestionRetry");
            migrationBuilder.DropForeignKey(name: "FK_TechnicalSessionQuestion_Question_QuestionId", table: "TechnicalSessionQuestion");

            migrationBuilder.AddColumn<string>(name: "LastPurgeError", table: "Question", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PurgeAttemptCount", table: "Question", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<DateTime>(name: "PurgeRequestedAt", table: "Question", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PurgeRequestedBy", table: "Question", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "PurgeStatus", table: "Question", type: "int", nullable: false, defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuestionPurgeAudit",
                columns: table => new
                {
                    QuestionPurgeAuditId = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<int>(type: "int", nullable: true),
                    SoftDeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurgedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_QuestionPurgeAudit", x => x.QuestionPurgeAuditId));

            migrationBuilder.CreateIndex(name: "IX_Question_PurgeCandidates", table: "Question", columns: new[] { "IsDeleted", "PurgeStatus", "DeletedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Question_PurgeCandidates", table: "Question");
            migrationBuilder.DropTable(name: "QuestionPurgeAudit");
            migrationBuilder.DropColumn(name: "LastPurgeError", table: "Question");
            migrationBuilder.DropColumn(name: "PurgeAttemptCount", table: "Question");
            migrationBuilder.DropColumn(name: "PurgeRequestedAt", table: "Question");
            migrationBuilder.DropColumn(name: "PurgeRequestedBy", table: "Question");
            migrationBuilder.DropColumn(name: "PurgeStatus", table: "Question");

            migrationBuilder.AddForeignKey(name: "FK_BehaviourSessionQuestion_Question_QuestionId", table: "BehaviourSessionQuestion", column: "QuestionId", principalTable: "Question", principalColumn: "QuestionId", onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(name: "FK_SingleQuestionRetry_Question_QuestionId", table: "SingleQuestionRetry", column: "QuestionId", principalTable: "Question", principalColumn: "QuestionId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_TechnicalSessionQuestion_Question_QuestionId", table: "TechnicalSessionQuestion", column: "QuestionId", principalTable: "Question", principalColumn: "QuestionId", onDelete: ReferentialAction.Restrict);
        }
    }
}
