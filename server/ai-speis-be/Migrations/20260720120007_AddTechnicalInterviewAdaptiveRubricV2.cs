using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ai_speis_be.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalInterviewAdaptiveRubricV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdaptiveStage",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedBonus",
                table: "TechnicalQuestionAttempt",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BonusCalculationVersion",
                table: "TechnicalQuestionAttempt",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedClarificationCount",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompletedFollowUpCount",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CumulativeFollowUpBonus",
                table: "TechnicalQuestionAttempt",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "EvaluationObjective",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalMainScore",
                table: "TechnicalQuestionAttempt",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GenerationReason",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialMainScore",
                table: "TechnicalQuestionAttempt",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlanDeviation",
                table: "TechnicalQuestionAttempt",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PlanDeviationReason",
                table: "TechnicalQuestionAttempt",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RawScore",
                table: "TechnicalQuestionAttempt",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredClarificationCount",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredFollowUpCount",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SequenceWithinMain",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "TechnicalQuestionAttempt",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetSkillSnapshot",
                table: "TechnicalQuestionAttempt",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetSubskillSnapshot",
                table: "TechnicalQuestionAttempt",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiSuggestedAction",
                table: "TechnicalAnswerEvaluation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BackendResolvedAction",
                table: "TechnicalAnswerEvaluation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OverrideReason",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScoringPolicyVersion",
                table: "TechnicalAnswerEvaluation",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TechnicalAdaptiveRuleVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalBonusCalculationVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMatchBand",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalMatchScoreSnapshot",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalPlannedCvQuestionCount",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicalPlannedJdQuestionCount",
                table: "InterviewSession",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalQuestionPlanJson",
                table: "InterviewSession",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalQuestionPlanVersion",
                table: "InterviewSession",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE evaluation
                SET evaluation.BackendResolvedAction = evaluation.Decision,
                    evaluation.ScoringPolicyVersion = COALESCE(session.TechnicalScoringPolicyVersion, 'technical-scoring-v1')
                FROM TechnicalAnswerEvaluation AS evaluation
                INNER JOIN TechnicalQuestionAttempt AS attempt ON attempt.AttemptId = evaluation.AttemptId
                INNER JOIN InterviewSession AS session ON session.InterviewSessionId = attempt.InterviewSessionId;
                """);

            migrationBuilder.Sql("""
                WITH RankedAttempts AS
                (
                    SELECT AttemptId,
                           ROW_NUMBER() OVER (
                               PARTITION BY RootMainAttemptId
                               ORDER BY SequenceNumber, AttemptId) - 1 AS SequenceWithinMain
                    FROM TechnicalQuestionAttempt
                )
                UPDATE attempt
                SET attempt.SequenceWithinMain = ranked.SequenceWithinMain
                FROM TechnicalQuestionAttempt AS attempt
                INNER JOIN RankedAttempts AS ranked ON ranked.AttemptId = attempt.AttemptId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalQuestionAttempt_RootMainAttemptId_QuestionType_SequenceWithinMain",
                table: "TechnicalQuestionAttempt",
                columns: new[] { "RootMainAttemptId", "QuestionType", "SequenceWithinMain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TechnicalQuestionAttempt_RootMainAttemptId_QuestionType_SequenceWithinMain",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "AdaptiveStage",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "AppliedBonus",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "BonusCalculationVersion",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "CompletedClarificationCount",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "CompletedFollowUpCount",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "CumulativeFollowUpBonus",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "EvaluationObjective",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "FinalMainScore",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "GenerationReason",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "InitialMainScore",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "PlanDeviation",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "PlanDeviationReason",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "RawScore",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "RequiredClarificationCount",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "RequiredFollowUpCount",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "SequenceWithinMain",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "TargetSkillSnapshot",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "TargetSubskillSnapshot",
                table: "TechnicalQuestionAttempt");

            migrationBuilder.DropColumn(
                name: "AiSuggestedAction",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "BackendResolvedAction",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "OverrideReason",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "ScoringPolicyVersion",
                table: "TechnicalAnswerEvaluation");

            migrationBuilder.DropColumn(
                name: "TechnicalAdaptiveRuleVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalBonusCalculationVersion",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalMatchBand",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalMatchScoreSnapshot",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPlannedCvQuestionCount",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalPlannedJdQuestionCount",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalQuestionPlanJson",
                table: "InterviewSession");

            migrationBuilder.DropColumn(
                name: "TechnicalQuestionPlanVersion",
                table: "InterviewSession");
        }
    }
}
