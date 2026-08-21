using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Configuration;
using ai_speis_be.BehaviouralInterviews.Validation;

namespace ai_speis_be.BehaviouralInterviews.Selection
{
    public sealed class BehaviouralSelectionContext
    {
        public string Language { get; init; } = string.Empty;
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public string? CompanyCategory { get; init; }
        public QuestionDifficultyEnum? Difficulty { get; init; }
        public int NumberOfQuestions { get; init; } = 3;
        public string? AiProvider { get; init; }
        public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> NiceToHaveSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CvHighlights { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CvSkills { get; init; } = Array.Empty<string>();
        public IReadOnlySet<int> AskedQuestionIds { get; init; } = new HashSet<int>();

        /// <summary>CV-JD Match Score (0–100) từ Fast Check; null → coi như band trung bình.</summary>
        public int? CvJdMatchScore { get; init; }
    }

    public sealed class BehaviouralSelectedQuestion
    {
        public required Question Question { get; init; }
        public int Order { get; init; }
    }

    public sealed class BehaviouralQuestionSelectionResult
    {
        public IReadOnlyList<BehaviouralSelectedQuestion> Questions { get; init; } = Array.Empty<BehaviouralSelectedQuestion>();
        public BehaviouralAIProviderResult<BehaviouralAISelectionResponse>? AIResult { get; init; }
        public bool FallbackUsed { get; init; }
        public string? ErrorCode { get; init; }
        public string Relaxation { get; init; } = "none";
        public IReadOnlyList<string> CoveredSkills { get; init; } = Array.Empty<string>();
    }

    public sealed class BehaviouralQuestionSelectionService : IBehaviouralQuestionSelectionService
    {
        private readonly IQuestionRepoitory _questionRepository;
        private readonly IBehaviouralInterviewAIProviderResolver _providerResolver;
        private readonly IBehaviouralAIResponseValidator _validator;
        private readonly BehaviouralInterviewOptions _options;
        private readonly ILogger<BehaviouralQuestionSelectionService> _logger;
        private readonly Services.RagService.IRagQuestionRetrievalClient? _ragClient;

        public BehaviouralQuestionSelectionService(
            IQuestionRepoitory questionRepository,
            IBehaviouralInterviewAIProviderResolver providerResolver,
            IBehaviouralAIResponseValidator validator,
            BehaviouralInterviewOptions options,
            ILogger<BehaviouralQuestionSelectionService> logger,
            Services.RagService.IRagQuestionRetrievalClient? ragClient = null)
        {
            _questionRepository = questionRepository;
            _providerResolver = providerResolver;
            _validator = validator;
            _options = options;
            _logger = logger;
            _ragClient = ragClient;
        }

        private static bool IsOllamaProvider(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return false;
            var name = provider.Trim().ToLowerInvariant();
            return name.Contains("ollama") || name.Contains("qwen") || name.Equals("local", StringComparison.OrdinalIgnoreCase) || name.Equals("aispeis", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<BehaviouralQuestionSelectionResult> SelectAsync(
            BehaviouralSelectionContext context,
            CancellationToken cancellationToken = default)
        {
            var (candidates, relaxation, poolErrorCode) = await BuildCandidatePoolAsync(context, cancellationToken);
            if (candidates.Count == 0)
            {
                return new BehaviouralQuestionSelectionResult
                {
                    ErrorCode = poolErrorCode ?? "NO_BEHAVIOURAL_CANDIDATE",
                    Relaxation = relaxation
                };
            }

            var questionCount = Math.Min(context.NumberOfQuestions, candidates.Count);
            var (cvFocusCount, jdFocusCount) = ComputeSourceSplit(context.CvJdMatchScore, questionCount);
            var constraints = new BehaviouralAISelectionConstraints
            {
                RequiredQuestionCount = questionCount,
                MaximumQuestionsPerSkill = 1,
                MinimumCoveredSkills = Math.Min(3, questionCount),
                DifficultyDistribution = BuildDifficultyDistribution(context.CvJdMatchScore, questionCount),
                CvFocusQuestionCount = cvFocusCount,
                JdFocusQuestionCount = jdFocusCount
            };

            var aiRequest = new BehaviouralAISelectionRequest
            {
                Language = context.Language,
                JobRole = context.JobRole,
                ExperienceLevel = context.ExperienceLevel,
                CompanyCategory = context.CompanyCategory,
                CvJdMatchScore = context.CvJdMatchScore,
                RequiredSkills = context.RequiredSkills,
                NiceToHaveSkills = context.NiceToHaveSkills,
                CvSkills = context.CvSkills,
                CvHighlights = context.CvHighlights,
                Constraints = constraints,
                Candidates = candidates.Select(question => new BehaviouralAIQuestionCandidate(
                    question.QuestionId,
                    question.QuestionContent,
                    question.Skill,
                    question.Difficulty.ToString(),
                    question.ExperienceLevel)).ToList()
            };

            var candidatesById = candidates.ToDictionary(question => question.QuestionId);
            var candidateSkillsById = candidates.ToDictionary(
                question => question.QuestionId,
                question => question.Skill);

            var aiProvider = _providerResolver.Resolve(context.AiProvider ?? _options.Provider);
            BehaviouralAIProviderResult<BehaviouralAISelectionResponse>? aiResult = null;

            // Retry 1 lần khi AI trả kết quả không hợp lệ (brief §21)
            for (var attempt = 0; attempt < 1; attempt++)
            {
                aiResult = await aiProvider.SelectQuestionsAsync(aiRequest, cancellationToken);
                if (!aiResult.Success || aiResult.Data is null)
                {
                    continue;
                }

                var validation = _validator.ValidateSelection(aiResult.Data, constraints, candidateSkillsById);
                if (validation.IsValid)
                {
                    var ordered = aiResult.Data.SelectedQuestions
                        .OrderBy(item => item.Order)
                        .Select((item, index) => new BehaviouralSelectedQuestion
                        {
                            Question = candidatesById[item.QuestionId],
                            Order = index + 1
                        })
                        .ToList();

                    return new BehaviouralQuestionSelectionResult
                    {
                        Questions = ordered,
                        AIResult = aiResult,
                        FallbackUsed = false,
                        Relaxation = relaxation,
                        CoveredSkills = aiResult.Data.CoveredSkills
                    };
                }

                _logger.LogWarning(
                    "Behavioural AI selection invalid ({ErrorCode}), attempt {Attempt}.",
                    validation.ErrorCode,
                    attempt + 1);
            }

            _logger.LogWarning(
                "Behavioural AI selection failed ({ErrorCode}); using rule-based fallback.",
                aiResult?.ErrorCode ?? "INVALID_SELECTION");

            var fallback = SelectFallback(candidates, context, questionCount);
            return new BehaviouralQuestionSelectionResult
            {
                Questions = fallback,
                AIResult = aiResult,
                FallbackUsed = true,
                Relaxation = relaxation,
                CoveredSkills = fallback
                    .Select(item => item.Question.Skill)
                    .Where(skill => !string.IsNullOrWhiteSpace(skill))
                    .Select(skill => skill!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private async Task<(IReadOnlyList<Question> Candidates, string Relaxation, string? ErrorCode)> BuildCandidatePoolAsync(
            BehaviouralSelectionContext context,
            CancellationToken cancellationToken)
        {
            var effectiveProvider = context.AiProvider ?? _options.Provider;
            if (IsOllamaProvider(effectiveProvider))
            {
                if (_ragClient is null)
                {
                    _logger.LogWarning("Ollama/Qwen provider selected for Behavioral interview, but IRagQuestionRetrievalClient is null.");
                    return (Array.Empty<Question>(), "none", "RAG_SERVICE_UNAVAILABLE");
                }

                var ragResult = await _ragClient.RetrieveQuestionsAsync(
                    context.JobRole,
                    context.ExperienceLevel,
                    context.RequiredSkills,
                    context.Language,
                    50,
                    "behavioral",
                    cancellationToken);

                if (!ragResult.Success)
                {
                    _logger.LogWarning("RAG Retrieval failed for Behavioral interview: ErrorCode={ErrorCode}, Detail={Detail}", ragResult.ErrorCode, ragResult.ErrorDetail);
                    return (Array.Empty<Question>(), "none", ragResult.ErrorCode ?? "RAG_SERVICE_UNAVAILABLE");
                }

                if (ragResult.Questions.Count == 0)
                {
                    _logger.LogWarning("RAG Retrieval returned 0 behavioral candidates for role '{JobRole}'", context.JobRole);
                    return (Array.Empty<Question>(), "none", "NO_ELIGIBLE_RAG_QUESTION");
                }

                return (ragResult.Questions, "rag", null);
            }

            var roleTargets = new List<string> { context.JobRole };
            var experienceLevels = new List<string> { context.ExperienceLevel };

            var candidates = await QueryAsync(context, roleTargets, experienceLevels, context.RequiredSkills, cancellationToken);
            if (candidates.Count >= context.NumberOfQuestions)
            {
                return (candidates, "none", null);
            }

            // Nới lỏng dần khi pool quá nhỏ so với số câu cần chọn
            candidates = await QueryAsync(context, roleTargets, experienceLevels, Array.Empty<string>(), cancellationToken);
            if (candidates.Count >= context.NumberOfQuestions)
            {
                return (candidates, "skill", null);
            }

            candidates = await QueryAsync(context, roleTargets, Array.Empty<string>(), Array.Empty<string>(), cancellationToken);
            if (candidates.Count >= context.NumberOfQuestions)
            {
                return (candidates, "skill,experience", null);
            }

            candidates = await QueryAsync(context, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), cancellationToken);
            return (candidates, "skill,experience,role", null);
        }

        private Task<IReadOnlyList<Question>> QueryAsync(
            BehaviouralSelectionContext context,
            IReadOnlyCollection<string> roleTargets,
            IReadOnlyCollection<string> experienceLevels,
            IReadOnlyCollection<string> skills,
            CancellationToken cancellationToken)
        {
            return _questionRepository.GetBehaviouralCandidatesAsync(
                new BehaviouralQuestionCandidateQuery
                {
                    Language = context.Language,
                    RoleTargets = roleTargets,
                    ExperienceLevels = experienceLevels,
                    Skills = skills,
                    Difficulty = null,
                    ExcludedQuestionIds = context.AskedQuestionIds,
                    MaximumResults = 50
                },
                cancellationToken);
        }

        /// <summary>
        /// Phân bổ nguồn câu hỏi CV/JD theo Match Score (Adaptive Question Generation Rubric §5):
        /// 0–39 → trọng tâm CV (70% CV); 40–69 → cân bằng; 70–100 → trọng tâm JD (70% JD).
        /// Với 3 câu: 2 CV + 1 JD / cân bằng / 1 CV + 2 JD. Match Score null → coi như band giữa.
        /// </summary>
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

        private static IReadOnlyDictionary<string, int> BuildDifficultyDistribution(int? matchScore, int questionCount)
        {
            // Adaptive Question Generation Rubric §3: match thấp → Easy→Medium,
            // trung bình → Medium, cao → Medium→Hard.
            if (questionCount <= 1)
            {
                return new Dictionary<string, int> { ["Medium"] = questionCount };
            }

            return (matchScore ?? 50) switch
            {
                < 40 => new Dictionary<string, int> { ["Easy"] = 1, ["Medium"] = questionCount - 1 },
                < 70 => new Dictionary<string, int> { ["Medium"] = questionCount },
                _ => new Dictionary<string, int> { ["Medium"] = questionCount - 1, ["Hard"] = 1 }
            };
        }

        private static List<BehaviouralSelectedQuestion> SelectFallback(
            IReadOnlyList<Question> candidates,
            BehaviouralSelectionContext context,
            int questionCount)
        {
            var requiredSkills = new HashSet<string>(context.RequiredSkills, StringComparer.OrdinalIgnoreCase);
            var niceToHaveSkills = new HashSet<string>(context.NiceToHaveSkills, StringComparer.OrdinalIgnoreCase);
            var cvSkills = new HashSet<string>(context.CvSkills, StringComparer.OrdinalIgnoreCase);

            var ranked = candidates
                .OrderBy(question => RankSkillPriority(question.Skill, requiredSkills, niceToHaveSkills))
                .ThenBy(question => question.Difficulty)
                .ThenBy(question => question.QuestionId)
                .ToList();

            // Phân bổ nguồn CV/JD theo match score: lấp quota bucket CV (skill có trong CV)
            // và bucket JD (required skill trong JD) trước, thiếu mới lấy phần còn lại.
            var (cvQuota, jdQuota) = ComputeSourceSplit(context.CvJdMatchScore, questionCount);
            var selected = new List<Question>();
            var usedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cvTaken = 0;
            var jdTaken = 0;

            bool TryTake(Question question)
            {
                var skill = question.Skill?.Trim() ?? string.Empty;
                if (skill.Length > 0 && !usedSkills.Add(skill))
                {
                    return false;
                }

                selected.Add(question);
                return true;
            }

            foreach (var question in ranked)
            {
                if (selected.Count == questionCount)
                {
                    break;
                }

                var skill = question.Skill?.Trim() ?? string.Empty;
                var isCvFocus = skill.Length > 0 && cvSkills.Contains(skill);
                if (isCvFocus && cvTaken < cvQuota && TryTake(question))
                {
                    cvTaken++;
                }
                else if (!isCvFocus && jdTaken < jdQuota && TryTake(question))
                {
                    jdTaken++;
                }
            }

            // Chưa đủ quota (pool lệch về một nguồn) → lấp bằng câu tốt nhất còn lại, ưu tiên đa dạng skill
            foreach (var question in ranked)
            {
                if (selected.Count == questionCount)
                {
                    break;
                }

                if (!selected.Contains(question))
                {
                    TryTake(question);
                }
            }

            foreach (var question in ranked)
            {
                if (selected.Count == questionCount)
                {
                    break;
                }

                if (!selected.Contains(question))
                {
                    selected.Add(question);
                }
            }

            return selected
                .OrderBy(question => question.Difficulty)
                .ThenBy(question => question.QuestionId)
                .Select((question, index) => new BehaviouralSelectedQuestion
                {
                    Question = question,
                    Order = index + 1
                })
                .ToList();
        }

        private static int RankSkillPriority(
            string? skill,
            IReadOnlySet<string> requiredSkills,
            IReadOnlySet<string> niceToHaveSkills)
        {
            if (skill is null)
            {
                return 2;
            }

            if (requiredSkills.Contains(skill))
            {
                return 0;
            }

            return niceToHaveSkills.Contains(skill) ? 1 : 2;
        }
    }
}
