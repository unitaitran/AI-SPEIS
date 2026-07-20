using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.TechnicalInterviews.Selection
{
    public sealed class TechnicalSelectionContext
    {
        public string Language { get; init; } = string.Empty;
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public QuestionDifficultyEnum Difficulty { get; init; }
        public IReadOnlyList<string> SelectedSkills { get; init; } = Array.Empty<string>();
        public IReadOnlySet<int> AskedQuestionIds { get; init; } = new HashSet<int>();
        public IReadOnlyDictionary<string, int> SkillUsage { get; init; } = new Dictionary<string, int>();
        public IReadOnlySet<string> AskedSubskills { get; init; } = new HashSet<string>();
    }

    public sealed class TechnicalQuestionSelectionResult
    {
        public Question? Question { get; init; }
        public AIProviderResult<TechnicalAISelectionResponse>? AIResult { get; init; }
        public bool FallbackUsed { get; init; }
        public string? ErrorCode { get; init; }
        public string Relaxation { get; init; } = "none";
    }

    public sealed class TechnicalQuestionPoolResult
    {
        public IReadOnlyList<Question> Candidates { get; init; } = Array.Empty<Question>();
        public string? ErrorCode { get; init; }
        public string Relaxation { get; init; } = "none";
    }

    public interface ITechnicalQuestionSelectionService
    {
        Task<TechnicalQuestionSelectionResult> SelectAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken);

        Task<TechnicalQuestionPoolResult> PreparePoolAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken);
    }

    public sealed class TechnicalQuestionSelectionService : ITechnicalQuestionSelectionService
    {
        private readonly IQuestionRepoitory _questionRepository;
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalQuestionSelectionService(
            IQuestionRepoitory questionRepository,
            ITechnicalInterviewAIProviderResolver providerResolver,
            ITechnicalAIResponseValidator validator,
            TechnicalInterviewOptions options)
        {
            _questionRepository = questionRepository;
            _providerResolver = providerResolver;
            _validator = validator;
            _options = options;
        }

        public async Task<TechnicalQuestionSelectionResult> SelectAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken)
        {
            var pool = await PreparePoolAsync(context, cancellationToken);
            if (pool.Candidates.Count == 0)
            {
                return new TechnicalQuestionSelectionResult
                {
                    ErrorCode = pool.ErrorCode ?? "NO_TECHNICAL_CANDIDATE",
                    Relaxation = pool.Relaxation
                };
            }

            var ranked = pool.Candidates;
            var aiRequest = new TechnicalAISelectionRequest
            {
                Language = context.Language,
                JobRole = context.JobRole,
                ExperienceLevel = context.ExperienceLevel,
                SelectedSkills = context.SelectedSkills,
                AskedSkills = context.SkillUsage.Keys.ToList(),
                Candidates = ranked.Select(question => new TechnicalAIQuestionCandidate(
                    question.QuestionId,
                    question.QuestionContent,
                    question.Skill ?? string.Empty,
                    TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson),
                    question.Difficulty.ToString(),
                    question.ExperienceLevel ?? string.Empty)).ToList()
            };

            var aiResult = await _providerResolver.Resolve()
                .SelectQuestionAsync(aiRequest, cancellationToken);
            var candidateIds = ranked.Select(question => question.QuestionId).ToHashSet();
            var validAiSelection = aiResult.Success
                && aiResult.Data is not null
                && _validator.IsValidSelection(aiResult.Data.SelectedQuestionId, candidateIds);
            var selectedId = validAiSelection
                ? aiResult.Data!.SelectedQuestionId
                : ranked[0].QuestionId;
            var fallbackUsed = !validAiSelection;

            var selected = await _questionRepository.GetQuestionByIdAsync(selectedId, cancellationToken);
            if (selected is null
                || selected.IsDeleted
                || !string.Equals(selected.QuestionType, "Technical", StringComparison.OrdinalIgnoreCase)
                || !candidateIds.Contains(selected.QuestionId))
            {
                selected = ranked[0];
                fallbackUsed = true;
            }

            return new TechnicalQuestionSelectionResult
            {
                Question = selected,
                AIResult = aiResult,
                FallbackUsed = fallbackUsed,
                Relaxation = pool.Relaxation
            };
        }

        public async Task<TechnicalQuestionPoolResult> PreparePoolAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken)
        {
            var roleTargets = TechnicalQuestionMetadata.ResolveRoleAliases(context.JobRole);
            if (roleTargets.Count == 0)
            {
                return new TechnicalQuestionPoolResult { ErrorCode = "UNSUPPORTED_JOB_ROLE" };
            }

            var experienceLevels = TechnicalQuestionMetadata.ResolveExperienceAliases(context.ExperienceLevel);
            var candidates = await QueryAsync(
                context,
                roleTargets,
                experienceLevels,
                context.SelectedSkills,
                context.Difficulty,
                cancellationToken);
            var relaxation = "none";

            if (candidates.Count == 0 && context.SelectedSkills.Count > 0)
            {
                candidates = await QueryAsync(
                    context,
                    roleTargets,
                    experienceLevels,
                    Array.Empty<string>(),
                    context.Difficulty,
                    cancellationToken);
                relaxation = "skill";
            }

            if (candidates.Count == 0 && experienceLevels.Count > 0)
            {
                candidates = await QueryAsync(
                    context,
                    roleTargets,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    context.Difficulty,
                    cancellationToken);
                relaxation = "skill,experience";
            }

            if (candidates.Count == 0)
            {
                candidates = await QueryAsync(
                    context,
                    roleTargets,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    null,
                    cancellationToken);
                relaxation = "skill,experience,difficulty";
            }

            if (candidates.Count == 0)
            {
                return new TechnicalQuestionPoolResult
                {
                    ErrorCode = "NO_TECHNICAL_CANDIDATE",
                    Relaxation = relaxation
                };
            }

            var ranked = candidates
                .OrderBy(question => GetSkillUsage(context, question.Skill))
                .ThenBy(question => IsRepeatedSubskill(context, question) ? 1 : 0)
                .ThenBy(question => question.Difficulty == context.Difficulty ? 0 : 1)
                .ThenBy(question => question.QuestionId)
                .Take(_options.CandidatePoolSize)
                .ToList();

            return new TechnicalQuestionPoolResult
            {
                Candidates = ranked,
                Relaxation = relaxation
            };
        }

        private Task<IReadOnlyList<Question>> QueryAsync(
            TechnicalSelectionContext context,
            IReadOnlyCollection<string> roleTargets,
            IReadOnlyCollection<string> experienceLevels,
            IReadOnlyCollection<string> skills,
            QuestionDifficultyEnum? difficulty,
            CancellationToken cancellationToken)
        {
            return _questionRepository.GetTechnicalCandidatesAsync(
                new TechnicalQuestionCandidateQuery
                {
                    Language = context.Language,
                    RoleTargets = roleTargets,
                    ExperienceLevels = experienceLevels,
                    Skills = skills,
                    Difficulty = difficulty,
                    ExcludedQuestionIds = context.AskedQuestionIds,
                    MaximumResults = Math.Max(_options.CandidatePoolSize * 5, 50)
                },
                cancellationToken);
        }

        private static int GetSkillUsage(TechnicalSelectionContext context, string? skill)
        {
            return skill is not null && context.SkillUsage.TryGetValue(skill, out var count) ? count : 0;
        }

        private static bool IsRepeatedSubskill(TechnicalSelectionContext context, Question question)
        {
            var subskill = TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson);
            return subskill is not null && context.AskedSubskills.Contains(subskill);
        }
    }
}
