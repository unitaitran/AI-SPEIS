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
  


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure soft delete query filters
            modelBuilder.Entity<InterviewCampaign>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<InterviewSession>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<CodingQuestion>().HasQueryFilter(q => !q.InterviewSession.IsDeleted);
            modelBuilder.Entity<CodingSubmission>().HasQueryFilter(sub => !sub.InterviewSession.IsDeleted);
            modelBuilder.Entity<CodingQuestionTemplate>().HasQueryFilter(t => !t.CodingQuestion.InterviewSession.IsDeleted);
            modelBuilder.Entity<TestCase>().HasQueryFilter(tc => !tc.CodingQuestion.InterviewSession.IsDeleted);
            modelBuilder.Entity<SubmissionTestCaseResult>().HasQueryFilter(r => !r.CodingSubmission.InterviewSession.IsDeleted);

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
                    "[DurationMinutes] IN (10, 15, 20)"));

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

            // CodingQuestion Relationships
            modelBuilder.Entity<CodingQuestion>()
                .HasOne(q => q.InterviewSession)
                .WithMany()
                .HasForeignKey(q => q.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);

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
        }
    }
}
