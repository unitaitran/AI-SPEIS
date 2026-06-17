using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class RemoveQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Question_QuestionBank_QuestionBankId",
                table: "Question");

            migrationBuilder.DropTable(
                name: "QuestionBank");

            migrationBuilder.DropIndex(
                name: "IX_Question_QuestionBankId",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "IsAIGenerated",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "QuestionBankId",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Question");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Question",
                newName: "IsDeleted");

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "Question",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoleTarget",
                table: "Question",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Major",
                table: "Question");

            migrationBuilder.DropColumn(
                name: "RoleTarget",
                table: "Question");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Question",
                newName: "Status");

            migrationBuilder.AddColumn<bool>(
                name: "IsAIGenerated",
                table: "Question",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QuestionBankId",
                table: "Question",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Question",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuestionBank",
                columns: table => new
                {
                    QuestionBankId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    QuestionBankName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleTarget = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<bool>(type: "bit", nullable: false),
                    TechStack = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBank", x => x.QuestionBankId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Question_QuestionBankId",
                table: "Question",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBank_QuestionBankId",
                table: "QuestionBank",
                column: "QuestionBankId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Question_QuestionBank_QuestionBankId",
                table: "Question",
                column: "QuestionBankId",
                principalTable: "QuestionBank",
                principalColumn: "QuestionBankId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
