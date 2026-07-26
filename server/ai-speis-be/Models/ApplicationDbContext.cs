using Microsoft.EntityFrameworkCore;
using ai_speis_be.Models;
namespace ai_speis_be.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<UserProfile> UserProfiles { get; set; } = null!;
        public DbSet<CVFile> CVFiles { get; set; } = null!;
        public DbSet<CVExtractedProfile> CVExtractedProfiles { get; set; } = null!;
        public DbSet<CVSkill> CVSkills { get; set; } = null!;
        public DbSet<CVProject> CVProjects { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<SavedQuestion> SavedQuestion { get; set; } = null!;
        public DbSet<JDFile> JDFiles { get; set; } = null!;
        public DbSet<JDExtractedProfile> JDExtractedProfiles { get; set; } = null!;
        public DbSet<InterviewSession> InterviewSessions { get; set; } = null!;
        public DbSet<InterviewCampaign> InterviewCampaigns { get; set; } = null!;
        public DbSet<CodingQuestion> CodingQuestions { get; set; } = null!;
        public DbSet<CodingQuestionTemplate> CodingQuestionTemplates { get; set; } = null!;
        public DbSet<TestCase> TestCases { get; set; } = null!;
        public DbSet<CodingSubmission> CodingSubmissions { get; set; } = null!;
        public DbSet<SubmissionTestCaseResult> SubmissionTestCaseResults { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<FastCheckResult> FastCheckResults { get; set; } = null!;

        // Behavioural Round Models
        public DbSet<BehaviourQuestionSet> BehaviourQuestionSets { get; set; } = null!;
        public DbSet<BehaviourSessionQuestion> BehaviourSessionQuestions { get; set; } = null!;
        public DbSet<BehaviourAnswer> BehaviourAnswers { get; set; } = null!;
        public DbSet<BehaviourRoundResult> BehaviourRoundResults { get; set; } = null!;

        public DbSet<TechnicalQuestionAttempt> TechnicalQuestionAttempts { get; set; } = null!;
        public DbSet<TechnicalAnswerEvaluation> TechnicalAnswerEvaluations { get; set; } = null!;
        public DbSet<AIInteractionLog> AIInteractionLogs { get; set; } = null!;
        public DbSet<UserSkillScore> UserSkillScores { get; set; } = null!;



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure soft delete query filters
            modelBuilder.Entity<InterviewCampaign>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<InterviewSession>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<CodingQuestion>().HasQueryFilter(q => !q.IsDeleted);
            modelBuilder.Entity<CodingSubmission>().HasQueryFilter(sub => !sub.InterviewSession.IsDeleted);
            modelBuilder.Entity<CodingQuestionTemplate>().HasQueryFilter(t => !t.CodingQuestion.IsDeleted);
            modelBuilder.Entity<TestCase>().HasQueryFilter(tc => !tc.CodingQuestion.IsDeleted);
            modelBuilder.Entity<SubmissionTestCaseResult>().HasQueryFilter(r => !r.CodingSubmission.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalQuestionAttempt>().HasQueryFilter(a => !a.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalAnswerEvaluation>().HasQueryFilter(e => !e.Attempt.InterviewSession.IsDeleted);
            modelBuilder.Entity<AIInteractionLog>().HasQueryFilter(a => !a.InterviewSession.IsDeleted);

            // Seed default roles
            modelBuilder.Entity<Role>().HasData(
                new Role { RoleId = 1, RoleName = "admin", Description = "Quản trị viên", Status = true },
                new Role { RoleId = 2, RoleName = "user", Description = "Người dùng", Status = true }
            );

            // Configure relationships and constraints if needed
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserProfile>()
                .HasOne(up => up.User)
                .WithOne()
                .HasForeignKey<UserProfile>(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CVFile>()
                .HasOne(cv => cv.User)
                .WithMany(u => u.CVFiles)
                .HasForeignKey(cv => cv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JDFile>()
                .HasOne(jd => jd.User)
                .WithMany(u => u.JDFiles)
                .HasForeignKey(jd => jd.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // CVExtractedProfile — 1:1 with CVFile
            modelBuilder.Entity<CVExtractedProfile>()
                .HasOne(ep => ep.CVFile)
                .WithOne()
                .HasForeignKey<CVExtractedProfile>(ep => ep.CVFileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CVExtractedProfile>()
                .HasOne(ep => ep.ConfirmedByUser)
                .WithMany()
                .HasForeignKey(ep => ep.ConfirmedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // CVSkill — N:1 with CVExtractedProfile
            modelBuilder.Entity<CVSkill>()
                .HasOne(s => s.ExtractedProfile)
                .WithMany(ep => ep.Skills)
                .HasForeignKey(s => s.ExtractedProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CVSkill>()
                .HasIndex(s => new { s.ExtractedProfileId, s.SkillName })
                .IsUnique();

            // CVProject — N:1 with CVExtractedProfile
            modelBuilder.Entity<CVProject>()
                .HasOne(p => p.ExtractedProfile)
                .WithMany(ep => ep.Projects)
                .HasForeignKey(p => p.ExtractedProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.User)
                .WithMany() // Nếu bảng User không có danh sách Questions điều hướng ngược lại thì để trống WithMany()
                .HasForeignKey(q => q.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SavedQuestion>()
                .HasIndex(usq => new { usq.UserId, usq.QuestionId })
                .IsUnique(); // Đảm bảo duy nhất

            modelBuilder.Entity<SavedQuestion>()
                .HasOne(usq => usq.User)
                .WithMany()
                .HasForeignKey(usq => usq.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SavedQuestion>()
                .HasOne(usq => usq.Question)
                .WithMany()
                .HasForeignKey(usq => usq.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // FastCheckResult Relationships
            modelBuilder.Entity<FastCheckResult>()
                .HasOne(fc => fc.User)
                .WithMany()
                .HasForeignKey(fc => fc.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FastCheckResult>()
                .HasOne(fc => fc.CVFile)
                .WithMany()
                .HasForeignKey(fc => fc.CVFileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<FastCheckResult>()
                .HasOne(fc => fc.JDFile)
                .WithMany()
                .HasForeignKey(fc => fc.JDFileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<FastCheckResult>()
                .HasIndex(fc => new { fc.UserId, fc.CVFileId, fc.JDFileId })
                .IsUnique()
                .HasDatabaseName("IX_FastCheckResult_User_CV_JD");

            // InterviewCampaign Relationships
            modelBuilder.Entity<InterviewCampaign>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InterviewCampaign>()
                .HasIndex(c => new { c.UserId, c.Status })
                .HasDatabaseName("IX_InterviewCampaign_UserId_Status");

            modelBuilder.Entity<InterviewCampaign>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_InterviewCampaign_DurationMinutes",
                    "[DurationMinutes] >= 5 AND [DurationMinutes] <= 120"));

            modelBuilder.Entity<User>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_User_RemainingInterviewQuota",
                    "[RemainingInterviewQuota] >= 0"));

            modelBuilder.Entity<InterviewCampaign>()
                .HasOne(c => c.CVExtractedProfile)
                .WithMany()
                .HasForeignKey(c => c.CVExtractedProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InterviewCampaign>()
                .HasOne(c => c.JDExtractedProfile)
                .WithMany()
                .HasForeignKey(c => c.JDExtractedProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // InterviewSession Relationships
            modelBuilder.Entity<InterviewSession>()
                .HasOne(s => s.InterviewCampaign)
                .WithMany(c => c.InterviewSessions)
                .HasForeignKey(s => s.InterviewCampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InterviewSession>()
                .Property(s => s.TechnicalConcurrencyVersion)
                .IsConcurrencyToken();

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasOne(a => a.InterviewSession)
                .WithMany(s => s.TechnicalQuestionAttempts)
                .HasForeignKey(a => a.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasOne(a => a.ParentAttempt)
                .WithMany(a => a.ChildAttempts)
                .HasForeignKey(a => a.ParentAttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasIndex(a => new { a.InterviewSessionId, a.SubmissionIdempotencyKey })
                .IsUnique()
                .HasFilter("[SubmissionIdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasIndex(a => new { a.InterviewSessionId, a.Status })
                .IsUnique()
                .HasFilter("[Status] = 0");

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasIndex(a => new { a.RootMainAttemptId, a.QuestionType, a.SequenceWithinMain })
                .IsUnique();

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasIndex(a => new { a.InterviewSessionId, a.MainQuestionIndex })
                .IsUnique()
                .HasFilter("[QuestionType] = 0");

            modelBuilder.Entity<TechnicalQuestionAttempt>()
                .HasIndex(a => new { a.InterviewSessionId, a.QuestionId })
                .IsUnique()
                .HasFilter("[QuestionType] = 0 AND [QuestionId] IS NOT NULL");

            modelBuilder.Entity<TechnicalAnswerEvaluation>()
                .HasOne(e => e.Attempt)
                .WithMany(a => a.Evaluations)
                .HasForeignKey(e => e.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TechnicalAnswerEvaluation>()
                .HasIndex(e => e.RootMainAttemptId)
                .IsUnique()
                .HasFilter("[IsFinalForMainQuestion] = 1");

            modelBuilder.Entity<AIInteractionLog>()
                .HasOne(log => log.InterviewSession)
                .WithMany(session => session.AIInteractionLogs)
                .HasForeignKey(log => log.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIInteractionLog>()
                .HasOne(log => log.Attempt)
                .WithMany()
                .HasForeignKey(log => log.AttemptId)
                .OnDelete(DeleteBehavior.NoAction);

            // CodingQuestion Relationships (No longer tied to InterviewSession)

            // CodingQuestionTemplate Relationships
            modelBuilder.Entity<CodingQuestionTemplate>()
                .HasOne(t => t.CodingQuestion)
                .WithMany(q => q.CodingQuestionTemplates)
                .HasForeignKey(t => t.CodingQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // TestCase Relationships
            modelBuilder.Entity<TestCase>()
                .HasOne(tc => tc.CodingQuestion)
                .WithMany(q => q.TestCases)
                .HasForeignKey(tc => tc.CodingQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // CodingSubmission Relationships
            modelBuilder.Entity<CodingSubmission>()
                .HasOne(s => s.InterviewSession)
                .WithMany()
                .HasForeignKey(s => s.InterviewSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CodingSubmission>()
                .HasOne(s => s.CodingQuestion)
                .WithMany(q => q.CodingSubmissions)
                .HasForeignKey(s => s.CodingQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // SubmissionTestCaseResult Relationships
            modelBuilder.Entity<SubmissionTestCaseResult>()
                .HasOne(r => r.CodingSubmission)
                .WithMany(s => s.SubmissionTestCaseResults)
                .HasForeignKey(r => r.CodingSubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubmissionTestCaseResult>()
                .HasOne(r => r.TestCase)
                .WithMany(tc => tc.SubmissionTestCaseResults)
                .HasForeignKey(r => r.TestCaseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.User)
                .WithMany()
                .HasForeignKey(payment => payment.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==========================================
            // Behavioural Round Configurations
            // ==========================================
            
            // Enum Conversions for BehaviourQuestionSet
            modelBuilder.Entity<BehaviourQuestionSet>()
                .Property(s => s.SelectionSource)
                .HasConversion<string>();
            modelBuilder.Entity<BehaviourQuestionSet>()
                .Property(s => s.Status)
                .HasConversion<string>();

            // Enum Conversions for BehaviourSessionQuestion
            modelBuilder.Entity<BehaviourSessionQuestion>()
                .Property(q => q.QuestionType)
                .HasConversion<string>();
            modelBuilder.Entity<BehaviourSessionQuestion>()
                .Property(q => q.Status)
                .HasConversion<string>();

            // Enum Conversions for BehaviourAnswer
            modelBuilder.Entity<BehaviourAnswer>()
                .Property(a => a.ResolvedAction)
                .HasConversion<string>();

            modelBuilder.Entity<BehaviourAnswer>()
                .HasIndex(a => a.SubmissionIdempotencyKey)
                .IsUnique()
                .HasFilter("[SubmissionIdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<BehaviourRoundResult>()
                .HasIndex(result => result.InterviewSessionId)
                .IsUnique();

            // Relationships
            modelBuilder.Entity<BehaviourSessionQuestion>()
                .HasOne(q => q.ParentQuestion)
                .WithMany()
                .HasForeignKey(q => q.ParentQuestionId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BehaviourSessionQuestion>()
                .HasOne(q => q.BehaviourAnswerAnswer)
                .WithOne(a => a.BehaviourSessionQuestion)
                .HasForeignKey<BehaviourAnswer>(a => a.BehaviourSessionQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
