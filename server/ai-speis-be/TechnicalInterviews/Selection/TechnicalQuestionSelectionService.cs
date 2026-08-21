using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.RagService;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Planning;
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
        public TechnicalQuestionPlanSlot? PlanSlot { get; init; }
        public IReadOnlyList<string> CvSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> JdSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RequiredJdSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<QuestionDifficultyEnum> AllowedDifficulties { get; init; } = Array.Empty<QuestionDifficultyEnum>();
        public int? CvJdMatchScore { get; init; }
        public string? AiProvider { get; init; }
    }

    public sealed class TechnicalQuestionPoolResult
    {
        public IReadOnlyList<Question> Candidates { get; init; } = Array.Empty<Question>();
        public string? ErrorCode { get; init; }
        public string Relaxation { get; init; } = "none";
        public bool PlanDeviation { get; init; }
        public string? PlanDeviationReason { get; init; }
    }

    public sealed record TechnicalBankSubQuestionResult(
        bool IsSuccess,
        int SourceQuestionId,
        string? Content,
        string? ErrorCode);

    public interface ITechnicalQuestionSelectionService
    {
        Task<TechnicalQuestionPoolResult> PreparePoolAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken);

        Task<TechnicalBankSubQuestionResult> SelectBankSubQuestionAsync(
            TechnicalLockedMainQuestionSnapshot lockedMain,
            TechnicalSessionQuestionType attemptType,
            int followUpNumber,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<Question>?> SelectMainQuestionsWithAIAsync(
            TechnicalSelectionContext baseContext,
            IReadOnlyList<Question> candidatePool,
            int targetCount,
            int cvFocusCount,
            int jdFocusCount,
            CancellationToken cancellationToken);
    }

    public sealed class TechnicalQuestionSelectionService : ITechnicalQuestionSelectionService
    {
        private readonly IQuestionRepoitory _questionRepository;
        private readonly ITechnicalInterviewAIProviderResolver _aiProviderResolver;
        private readonly ITechnicalAIResponseValidator _validator;
        private readonly IRagQuestionRetrievalClient _ragClient;
        private readonly TechnicalInterviewOptions _options;
        private readonly ILogger<TechnicalQuestionSelectionService> _logger;

        public TechnicalQuestionSelectionService(
            IQuestionRepoitory questionRepository,
            ITechnicalInterviewAIProviderResolver aiProviderResolver,
            ITechnicalAIResponseValidator validator,
            IRagQuestionRetrievalClient ragClient,
            TechnicalInterviewOptions options,
            ILogger<TechnicalQuestionSelectionService> logger)
        {
            _questionRepository = questionRepository;
            _aiProviderResolver = aiProviderResolver;
            _validator = validator;
            _ragClient = ragClient;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<Question>?> SelectMainQuestionsWithAIAsync(
            TechnicalSelectionContext baseContext,
            IReadOnlyList<Question> candidatePool,
            int targetCount,
            int cvFocusCount,
            int jdFocusCount,
            CancellationToken cancellationToken)
        {
            var providerName = !string.IsNullOrWhiteSpace(baseContext.AiProvider)
                ? TechnicalInterviewAIProviderResolver.Normalize(baseContext.AiProvider)
                : "gemini";
            if (string.Equals(providerName, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                // Qdrant RAG path retrieves existing Question Bank records unchanged without LLM adaptation.
                return candidatePool.Take(targetCount > 0 ? targetCount : 3).ToList();
            }

            var effectiveTargetCount = targetCount > 0 ? targetCount : 3;
            if (candidatePool.Count < effectiveTargetCount)
            {
                return null;
            }

            try
            {
                var candidatesById = candidatePool.ToDictionary(q => q.QuestionId);
                var aiCandidates = candidatePool.Select(q => new TechnicalAIQuestionCandidate(
                    q.QuestionId,
                    q.QuestionContent,
                    q.Skill,
                    TechnicalQuestionMetadata.GetSubskill(q.QdrantPayloadJson),
                    q.Difficulty.ToString(),
                    q.ExperienceLevel
                )).ToList();

                if (cvFocusCount == 0 && jdFocusCount == 0)
                {
                    (cvFocusCount, jdFocusCount) = ComputeSourceSplit(baseContext.CvJdMatchScore, effectiveTargetCount);
                }

                var constraints = new TechnicalAISelectionConstraints
                {
                    RequiredQuestionCount = effectiveTargetCount,
                    MaximumQuestionsPerSkill = 1,
                    MinimumCoveredSkills = Math.Min(effectiveTargetCount, candidatePool.Select(q => q.Skill).Distinct().Count()),
                    CvFocusQuestionCount = cvFocusCount,
                    JdFocusQuestionCount = jdFocusCount
                };

                var request = new TechnicalAISelectionRequest
                {
                    Language = baseContext.Language,
                    JobRole = baseContext.JobRole,
                    ExperienceLevel = baseContext.ExperienceLevel,
                    CvJdMatchScore = baseContext.CvJdMatchScore,
                    RequiredSkills = baseContext.RequiredJdSkills,
                    NiceToHaveSkills = baseContext.JdSkills.Except(baseContext.RequiredJdSkills).ToList(),
                    CvSkills = baseContext.CvSkills,
                    Constraints = constraints,
                    Candidates = aiCandidates
                };

                var provider = _aiProviderResolver.Resolve();
                var aiResult = await provider.SelectQuestionsAsync(request, cancellationToken);

                if (!aiResult.Success || aiResult.Data is null)
                {
                    _logger.LogWarning("Technical AI question selection failed ({ErrorCode}). Falling back to rule-based engine.", aiResult.ErrorCode);
                    return null;
                }

                var validation = _validator.ValidateSelection(
                    aiResult.Data,
                    constraints,
                    candidatesById.Keys.ToHashSet());

                if (!validation.IsValid)
                {
                    _logger.LogWarning("Technical AI question selection invalid ({ErrorCode}). Falling back to rule-based engine.", validation.ErrorCode);
                    return null;
                }

                return aiResult.Data.SelectedQuestions
                    .Where(sq => candidatesById.ContainsKey(sq.QuestionId))
                    .Select(sq => candidatesById[sq.QuestionId])
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Technical AI question selection. Falling back to rule-based engine.");
                return null;
            }
        }

        public async Task<TechnicalQuestionPoolResult> PreparePoolAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken)
        {
            if (context.PlanSlot is not null)
            {
                return await PreparePlannedPoolAsync(context, cancellationToken);
            }

            var providerName = !string.IsNullOrWhiteSpace(context.AiProvider)
                ? TechnicalInterviewAIProviderResolver.Normalize(context.AiProvider)
                : "gemini";
            if (string.Equals(providerName, "ollama", StringComparison.OrdinalIgnoreCase))
            {
                var ragResult = await _ragClient.RetrieveQuestionsAsync(
                    context.JobRole,
                    context.ExperienceLevel,
                    context.SelectedSkills,
                    context.Language,
                    _options.StandardMainQuestionCount,
                    cancellationToken);

                if (!ragResult.Success)
                {
                    _logger.LogWarning("Qwen RAG question retrieval failed: {ErrorCode} - {Detail}", ragResult.ErrorCode, ragResult.ErrorDetail);
                    return new TechnicalQuestionPoolResult
                    {
                        ErrorCode = ragResult.ErrorCode ?? "RAG_SERVICE_UNAVAILABLE"
                    };
                }

                return new TechnicalQuestionPoolResult
                {
                    Candidates = ragResult.Questions,
                    Relaxation = "qdrant-rag"
                };
            }

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

        public async Task<TechnicalBankSubQuestionResult> SelectBankSubQuestionAsync(
            TechnicalLockedMainQuestionSnapshot lockedMain,
            TechnicalSessionQuestionType attemptType,
            int followUpNumber,
            CancellationToken cancellationToken)
        {
            if (attemptType is not TechnicalSessionQuestionType.Clarification
                and not TechnicalSessionQuestionType.FollowUp)
            {
                return new TechnicalBankSubQuestionResult(
                    false,
                    lockedMain.SelectedQuestionId,
                    null,
                    "INVALID_SUBQUESTION_TYPE");
            }

            var content = ResolveSubQuestion(
                lockedMain.ClarificationQuestion,
                lockedMain.FollowUp1,
                lockedMain.FollowUp2,
                attemptType,
                followUpNumber);

            // Legacy locked plans did not snapshot the pre-written probes. Read the
            // same source row from Question Bank; never synthesize replacement text.
            if (string.IsNullOrWhiteSpace(content))
            {
                var source = await _questionRepository.GetQuestionByIdAsync(
                    lockedMain.SelectedQuestionId,
                    cancellationToken);
                if (source is not null)
                {
                    content = ResolveSubQuestion(
                        source.ClarificationQuestion,
                        source.FollowUp1,
                        source.FollowUp2,
                        attemptType,
                        followUpNumber);
                }
            }

            return string.IsNullOrWhiteSpace(content)
                ? new TechnicalBankSubQuestionResult(
                    false,
                    lockedMain.SelectedQuestionId,
                    null,
                    "QUESTION_BANK_SUBQUESTION_UNAVAILABLE")
                : new TechnicalBankSubQuestionResult(
                    true,
                    lockedMain.SelectedQuestionId,
                    content,
                    null);
        }

        private static string? ResolveSubQuestion(
            string? clarification,
            string? followUp1,
            string? followUp2,
            TechnicalSessionQuestionType attemptType,
            int followUpNumber)
        {
            if (attemptType == TechnicalSessionQuestionType.Clarification)
            {
                return clarification;
            }

            return followUpNumber switch
            {
                <= 1 => followUp1,
                2 => followUp2,
                _ => null
            };
        }

        private async Task<TechnicalQuestionPoolResult> PreparePlannedPoolAsync(
            TechnicalSelectionContext context,
            CancellationToken cancellationToken)
        {
            var slot = context.PlanSlot!;
            var roleTargets = TechnicalQuestionMetadata.ResolveRoleAliases(context.JobRole);
            if (roleTargets.Count == 0)
            {
                return new TechnicalQuestionPoolResult { ErrorCode = "UNSUPPORTED_JOB_ROLE" };
            }

            var experienceLevels = TechnicalQuestionMetadata.ResolveExperienceAliases(context.ExperienceLevel);
            var plannedDifficultyCandidates = await QueryAsync(
                context,
                roleTargets,
                experienceLevels,
                Array.Empty<string>(),
                slot.PlannedDifficulty,
                cancellationToken);
            var eligiblePlanned = ExcludeUsedSkills(context, plannedDifficultyCandidates);
            var exactSkill = eligiblePlanned
                .Where(question => SkillMatches(question, slot.TargetSkill))
                .ToList();
            var candidates = exactSkill
                .Where(question => SubskillMatches(question, slot.TargetSubskill))
                .ToList();
            var relaxation = "none";
            var planDeviation = false;
            string? deviationReason = null;

            if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(slot.TargetSubskill))
            {
                candidates = exactSkill;
                relaxation = "subskill";
                planDeviation = candidates.Count > 0;
                deviationReason = candidates.Count > 0 ? "RELAX_SUBSKILL" : null;
            }

            // Question-bank experience labels are advisory metadata and do not form
            // part of the immutable Main plan. If aliases do not match, relax only
            // that filter while preserving role, language, skill and exact difficulty.
            if (candidates.Count == 0 && experienceLevels.Count > 0)
            {
                var experienceRelaxedCandidates = await QueryAsync(
                    context,
                    roleTargets,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    slot.PlannedDifficulty,
                    cancellationToken);
                var experienceRelaxedExactSkill = ExcludeUsedSkills(context, experienceRelaxedCandidates)
                    .Where(question => SkillMatches(question, slot.TargetSkill))
                    .ToList();
                candidates = experienceRelaxedExactSkill
                    .Where(question => SubskillMatches(question, slot.TargetSubskill))
                    .ToList();
                if (candidates.Count > 0)
                {
                    relaxation = "experience";
                }
                else if (!string.IsNullOrWhiteSpace(slot.TargetSubskill)
                    && experienceRelaxedExactSkill.Count > 0)
                {
                    candidates = experienceRelaxedExactSkill;
                    relaxation = "experience,subskill";
                    planDeviation = true;
                    deviationReason = "RELAX_SUBSKILL";
                }
            }

            if (candidates.Count == 0)
            {
                var difficultyBand = context.AllowedDifficulties.Count > 0
                    ? context.AllowedDifficulties
                    : new[] { slot.PlannedDifficulty };
                var bandCandidates = await QueryAsync(
                    context,
                    roleTargets,
                    experienceLevels,
                    Array.Empty<string>(),
                    null,
                    cancellationToken);
                candidates = ExcludeUsedSkills(context, bandCandidates)
                    .Where(question => difficultyBand.Contains(question.Difficulty))
                    .Where(question => SkillMatches(question, slot.TargetSkill))
                    .ToList();
                if (candidates.Count > 0)
                {
                    relaxation = "subskill,difficulty-band";
                    planDeviation = true;
                    deviationReason = "RELAX_DIFFICULTY_WITHIN_BAND";
                }

                if (candidates.Count == 0)
                {
                    candidates = ExcludeUsedSkills(context, bandCandidates)
                        .Where(question => difficultyBand.Contains(question.Difficulty))
                        .OrderBy(question => IsRequiredJdSkill(context, question) ? 0 : 1)
                        .ThenBy(question => IsPlannedSourceSkill(context, question) ? 0 : 1)
                        .ThenBy(question => question.Difficulty == slot.PlannedDifficulty ? 0 : 1)
                        .ThenBy(question => question.QuestionId)
                        .ToList();
                    if (candidates.Count > 0)
                    {
                        relaxation = "subskill,difficulty-band,source";
                        planDeviation = true;
                        deviationReason = "RELAX_SOURCE_CONSTRAINT_REQUIRED_SKILL_PRIORITY";
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return new TechnicalQuestionPoolResult
                {
                    ErrorCode = "NO_PLAN_SLOT_CANDIDATE",
                    Relaxation = relaxation,
                    PlanDeviation = planDeviation,
                    PlanDeviationReason = deviationReason
                };
            }

            var ranked = candidates
                .OrderBy(question => SkillMatches(question, slot.TargetSkill) ? 0 : 1)
                .ThenBy(question => IsRequiredJdSkill(context, question) ? 0 : 1)
                .ThenBy(question => question.Difficulty == slot.PlannedDifficulty ? 0 : 1)
                .ThenBy(question => question.QuestionId)
                .Take(_options.CandidatePoolSize)
                .ToList();
            return new TechnicalQuestionPoolResult
            {
                Candidates = ranked,
                Relaxation = relaxation,
                PlanDeviation = planDeviation,
                PlanDeviationReason = deviationReason
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

        private static List<Question> ExcludeUsedSkills(
            TechnicalSelectionContext context,
            IEnumerable<Question> candidates)
        {
            return candidates
                .Where(question => !context.SkillUsage.Keys.Any(used =>
                    TechnicalQuestionMetadata.FuzzyMatches(used, question.Skill ?? string.Empty)))
                .ToList();
        }

        private static bool SkillMatches(Question question, string targetSkill)
        {
            return !string.IsNullOrWhiteSpace(question.Skill)
                && TechnicalQuestionMetadata.FuzzyMatches(question.Skill, targetSkill);
        }

        private static bool SubskillMatches(Question question, string? targetSubskill)
        {
            return string.IsNullOrWhiteSpace(targetSubskill)
                || TechnicalQuestionMetadata.FuzzyMatches(
                    TechnicalQuestionMetadata.GetSubskill(question.QdrantPayloadJson) ?? string.Empty,
                    targetSubskill);
        }

        private static bool IsRequiredJdSkill(TechnicalSelectionContext context, Question question)
        {
            return context.RequiredJdSkills.Any(skill => SkillMatches(question, skill));
        }

        private static bool IsPlannedSourceSkill(TechnicalSelectionContext context, Question question)
        {
            var sourceSkills = context.PlanSlot?.SourceType == TechnicalQuestionSourceType.CV
                ? context.CvSkills
                : context.JdSkills;
            return sourceSkills.Any(skill => SkillMatches(question, skill));
        }

        private static (int CvFocus, int JdFocus) ComputeSourceSplit(int? matchScore, int questionCount)
        {
            if (questionCount <= 1)
            {
                return (questionCount, 0);
            }

            var cvShare = (matchScore ?? 50) switch
            {
                < 40 => 0.70,
                < 70 => 0.50,
                _ => 0.30
            };

            var cvFocus = Math.Clamp((int)Math.Round(questionCount * cvShare, MidpointRounding.AwayFromZero), 1, questionCount - 1);
            return (cvFocus, questionCount - cvFocus);
        }
    }
}
