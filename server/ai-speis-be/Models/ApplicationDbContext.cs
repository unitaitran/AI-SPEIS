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
        public DbSet<QuestionPurgeAudit> QuestionPurgeAudits { get; set; } = null!;
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
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; } = null!;
        public DbSet<SubscriptionPrice> SubscriptionPrices { get; set; } = null!;
        public DbSet<UserSubscription> UserSubscriptions { get; set; } = null!;
        public DbSet<SubscriptionTerm> SubscriptionTerms { get; set; } = null!;
        public DbSet<QuotaPeriod> QuotaPeriods { get; set; } = null!;
        public DbSet<QuotaTransaction> QuotaTransactions { get; set; } = null!;
        public DbSet<RewardAccount> RewardAccounts { get; set; } = null!;
        public DbSet<RewardTransaction> RewardTransactions { get; set; } = null!;
        public DbSet<RewardRule> RewardRules { get; set; } = null!;
        public DbSet<FastCheckResult> FastCheckResults { get; set; } = null!;

        // Behavioural Round Models
        public DbSet<BehaviourQuestionSet> BehaviourQuestionSets { get; set; } = null!;
        public DbSet<BehaviourSessionQuestion> BehaviourSessionQuestions { get; set; } = null!;
        public DbSet<BehaviourAnswer> BehaviourAnswers { get; set; } = null!;
        public DbSet<BehaviourRoundResult> BehaviourRoundResults { get; set; } = null!;

        public DbSet<TechnicalQuestionSet> TechnicalQuestionSets { get; set; } = null!;
        public DbSet<TechnicalSessionQuestion> TechnicalSessionQuestions { get; set; } = null!;
        public DbSet<TechnicalAnswer> TechnicalAnswers { get; set; } = null!;
        public DbSet<TechnicalRoundResult> TechnicalRoundResults { get; set; } = null!;
        public DbSet<AIInteractionLog> AIInteractionLogs { get; set; } = null!;
        public DbSet<UserSkillScore> UserSkillScores { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<SingleQuestionRetry> SingleQuestionRetries { get; set; } = null!;



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
            modelBuilder.Entity<AIInteractionLog>().HasQueryFilter(a => !a.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalQuestionSet>().HasQueryFilter(s => !s.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalSessionQuestion>().HasQueryFilter(q => !q.TechnicalQuestionSet.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalAnswer>().HasQueryFilter(a => !a.TechnicalSessionQuestion.TechnicalQuestionSet.InterviewSession.IsDeleted);
            modelBuilder.Entity<TechnicalRoundResult>().HasQueryFilter(r => !r.InterviewSession.IsDeleted);

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

            modelBuilder.Entity<Notification>()
                .HasOne(notification => notification.Recipient)
                .WithMany()
                .HasForeignKey(notification => notification.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(notification => new { notification.DeliveryChannel, notification.DeliveryStatus, notification.NextRetryAt })
                .HasDatabaseName("IX_Notification_EmailRetry");

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

            modelBuilder.Entity<User>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_User_FreeInterviewQuotaRemaining",
                    "[FreeInterviewQuotaRemaining] >= 0 AND [FreeInterviewQuotaRemaining] <= 3"));

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

            modelBuilder.Entity<AIInteractionLog>()
                .HasOne(log => log.InterviewSession)
                .WithMany(session => session.AIInteractionLogs)
                .HasForeignKey(log => log.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TechnicalQuestionSet>()
                .HasOne(set => set.InterviewSession)
                .WithOne(session => session.TechnicalQuestionSet)
                .HasForeignKey<TechnicalQuestionSet>(set => set.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SingleQuestionRetry>()
                .HasOne(retry => retry.User)
                .WithMany()
                .HasForeignKey(retry => retry.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SingleQuestionRetry>()
                .HasIndex(retry => new { retry.UserId, retry.QuestionId, retry.CreatedAt });
            modelBuilder.Entity<SingleQuestionRetry>()
                .HasIndex(retry => retry.QuestionId);
            modelBuilder.Entity<TechnicalQuestionSet>()
                .HasIndex(set => set.InterviewSessionId)
                .IsUnique();
            modelBuilder.Entity<TechnicalQuestionSet>()
                .Property(set => set.SelectionSource)
                .HasConversion<string>();
            modelBuilder.Entity<TechnicalQuestionSet>()
                .Property(set => set.Status)
                .HasConversion<string>();

            modelBuilder.Entity<TechnicalSessionQuestion>()
                .HasOne(question => question.TechnicalQuestionSet)
                .WithMany(set => set.Questions)
                .HasForeignKey(question => question.TechnicalQuestionSetId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TechnicalSessionQuestion>()
                .HasOne(question => question.ParentQuestion)
                .WithMany(question => question.ChildQuestions)
                .HasForeignKey(question => question.ParentQuestionId)
                .OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<TechnicalSessionQuestion>()
                .Property(question => question.QuestionType)
                .HasConversion<string>();
            modelBuilder.Entity<TechnicalSessionQuestion>()
                .Property(question => question.Status)
                .HasConversion<string>();
            modelBuilder.Entity<TechnicalSessionQuestion>()
                .HasIndex(question => new { question.TechnicalQuestionSetId, question.QuestionOrder })
                .IsUnique();
            modelBuilder.Entity<TechnicalSessionQuestion>()
                .HasIndex(question => question.QuestionId);

            modelBuilder.Entity<TechnicalAnswer>()
                .HasOne(answer => answer.TechnicalSessionQuestion)
                .WithOne(question => question.Answer)
                .HasForeignKey<TechnicalAnswer>(answer => answer.TechnicalSessionQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TechnicalAnswer>()
                .HasIndex(answer => answer.TechnicalSessionQuestionId)
                .IsUnique();
            modelBuilder.Entity<TechnicalAnswer>()
                .Property(answer => answer.EvaluationStatus)
                .HasConversion<string>();
            modelBuilder.Entity<TechnicalAnswer>()
                .HasIndex(answer => new { answer.TechnicalSessionQuestionId, answer.SubmissionIdempotencyKey })
                .IsUnique()
                .HasFilter("[SubmissionIdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<TechnicalRoundResult>()
                .HasOne(result => result.InterviewSession)
                .WithOne(session => session.TechnicalRoundResult)
                .HasForeignKey<TechnicalRoundResult>(result => result.InterviewSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<TechnicalRoundResult>()
                .HasIndex(result => result.InterviewSessionId)
                .IsUnique();

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

            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.SubscriptionPrice)
                .WithMany()
                .HasForeignKey(payment => payment.PriceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.ProviderTransactionId)
                .IsUnique()
                .HasFilter("[ProviderTransactionId] IS NOT NULL");

            modelBuilder.Entity<SubscriptionPlan>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_SubscriptionPlan_InterviewQuota",
                    "[InterviewQuota] >= 0"));

            modelBuilder.Entity<SubscriptionPlan>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_SubscriptionPlan_QuotaResetDays",
                    "[QuotaResetDays] IS NULL OR [QuotaResetDays] > 0"));

            modelBuilder.Entity<SubscriptionPlan>()
                .Property(plan => plan.AiTier)
                .HasDefaultValue("ADVANCED")
                .HasMaxLength(20);

            modelBuilder.Entity<SubscriptionPrice>()
                .HasOne(price => price.Plan)
                .WithMany(plan => plan.Prices)
                .HasForeignKey(price => price.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionPrice>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_SubscriptionPrice_Amount",
                    "[Amount] >= 0"));

            modelBuilder.Entity<UserSubscription>()
                .HasOne(subscription => subscription.User)
                .WithOne()
                .HasForeignKey<UserSubscription>(subscription => subscription.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSubscription>()
                .HasOne(subscription => subscription.Plan)
                .WithMany()
                .HasForeignKey(subscription => subscription.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionTerm>()
                .HasOne(term => term.UserSubscription)
                .WithMany(subscription => subscription.Terms)
                .HasForeignKey(term => term.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SubscriptionTerm>()
                .HasOne(term => term.Price)
                .WithMany()
                .HasForeignKey(term => term.PriceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SubscriptionTerm>()
                .HasIndex(term => term.SourcePaymentId)
                .IsUnique()
                .HasFilter("[SourcePaymentId] IS NOT NULL");

            modelBuilder.Entity<QuotaPeriod>()
                .HasOne(period => period.UserSubscription)
                .WithMany(subscription => subscription.QuotaPeriods)
                .HasForeignKey(period => period.UserSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuotaPeriod>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_QuotaPeriod_Values",
                    "[QuotaLimit] >= 0 AND [UsedQuota] >= 0 AND [ReservedQuota] >= 0 AND [UsedQuota] + [ReservedQuota] <= [QuotaLimit]"));

            modelBuilder.Entity<QuotaTransaction>()
                .HasOne(transaction => transaction.QuotaPeriod)
                .WithMany(period => period.Transactions)
                .HasForeignKey(transaction => transaction.QuotaPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RewardAccount>()
                .HasOne(account => account.User)
                .WithOne()
                .HasForeignKey<RewardAccount>(account => account.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RewardAccount>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_RewardAccount_Points",
                    "[AvailablePoints] >= 0 AND [ReservedPoints] >= 0 AND [LifetimeEarnedPoints] >= 0"));

            modelBuilder.Entity<RewardTransaction>()
                .HasOne(transaction => transaction.Account)
                .WithMany(account => account.Transactions)
                .HasForeignKey(transaction => transaction.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<SubscriptionPlan>().HasData(
                new SubscriptionPlan
                {
                    PlanId = 1,
                    Code = "FREE",
                    Name = "Gói Cơ Bản",
                    Description = "3 lượt phỏng vấn miễn phí.",
                    InterviewQuota = 3,
                    QuotaResetDays = null,
                    IsFree = true,
                    DisplayOrder = 1,
                    IsActive = true,
                    AiTier = "STANDARD",
                    AdvancedAnalyticsEnabled = false,
                    IsPopular = false,
                    CreatedAt = seedDate
                },
                new SubscriptionPlan
                {
                    PlanId = 2,
                    Code = "PREMIUM",
                    Name = "Premium",
                    Description = "15 lượt phỏng vấn, làm mới sau mỗi 30 ngày.",
                    InterviewQuota = 15,
                    QuotaResetDays = 30,
                    IsFree = false,
                    DisplayOrder = 2,
                    IsActive = true,
                    AiTier = "ADVANCED",
                    AdvancedAnalyticsEnabled = true,
                    IsPopular = true,
                    CreatedAt = seedDate
                });

            modelBuilder.Entity<SubscriptionPrice>().HasData(
                new SubscriptionPrice
                {
                    PriceId = 1,
                    PlanId = 2,
                    BillingCycle = Enums.BillingCycle.Monthly,
                    BillingCycleCount = 1,
                    Amount = 59000m,
                    Currency = "VND",
                    EffectiveFrom = seedDate,
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new SubscriptionPrice
                {
                    PriceId = 2,
                    PlanId = 2,
                    BillingCycle = Enums.BillingCycle.Yearly,
                    BillingCycleCount = 1,
                    Amount = 599000m,
                    Currency = "VND",
                    EffectiveFrom = seedDate,
                    IsActive = true,
                    CreatedAt = seedDate
                });

            modelBuilder.Entity<RewardRule>().HasData(new RewardRule
            {
                RewardRuleId = 1,
                PointValueVnd = 1,
                PointsExpire = false,
                AllowFullPaymentByPoints = true,
                IsActive = true,
                EffectiveFrom = seedDate
            });

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
            modelBuilder.Entity<BehaviourSessionQuestion>()
                .HasIndex(question => question.QuestionId);

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
