using System.Collections.Concurrent;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.Orchestration;
using ai_speis_be.Services.CodingService;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.TechnicalInterviews.PreGeneration
{
    /// <summary>
    /// Service Singleton quản lý tiến trình tạo trước câu hỏi Technical chạy ngầm.
    /// Sử dụng ConcurrentDictionary để đảm bảo thread-safe, tránh race condition.
    /// </summary>
    public sealed class TechnicalPreGenerationService : ITechnicalPreGenerationService
    {
        private const int MaxRetryCount = 3;

        /// <summary>
        /// Lưu trạng thái & CancellationTokenSource cho mỗi session đang được tạo ngầm.
        /// </summary>
        private sealed class PreGenerationEntry
        {
            public TechnicalPreGenerationStatus Status { get; set; } = TechnicalPreGenerationStatus.Idle;
            public string? ErrorMessage { get; set; }
            public int RetryCount { get; set; }
            public int UserId { get; set; }
            public CancellationTokenSource? Cts { get; set; }
            public readonly object Lock = new();
        }

        private readonly ConcurrentDictionary<int, PreGenerationEntry> _entries = new();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TechnicalPreGenerationService> _logger;

        public TechnicalPreGenerationService(
            IServiceScopeFactory scopeFactory,
            ILogger<TechnicalPreGenerationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<TechnicalPreGenerationStatusDto> PreGenerateAsync(
            int userId,
            int technicalSessionId,
            CancellationToken cancellationToken)
        {
            // 1. Kiểm tra xem session Technical đã được khởi tạo trong DB chưa (idempotent check)
            using (var checkScope = _scopeFactory.CreateScope())
            {
                var db = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var session = await db.InterviewSessions
                    .AsNoTracking()
                    .Include(s => s.InterviewCampaign)
                    .Where(s => s.InterviewSessionId == technicalSessionId
                        && s.InterviewCampaign.UserId == userId
                        && s.InterviewRoundType == InterviewRoundType.Technical)
                    .Select(s => new { s.TechnicalState })
                    .FirstOrDefaultAsync(cancellationToken);

                if (session is null)
                {
                    return new TechnicalPreGenerationStatusDto
                    {
                        Status = TechnicalPreGenerationStatus.Failed,
                        TechnicalSessionId = technicalSessionId,
                        ErrorMessage = "Technical session not found.",
                    };
                }

                // Nếu session đã được khởi tạo rồi → trả Completed ngay
                if (session.TechnicalState.HasValue)
                {
                    return new TechnicalPreGenerationStatusDto
                    {
                        Status = TechnicalPreGenerationStatus.Completed,
                        TechnicalSessionId = technicalSessionId,
                    };
                }
            }

            // 2. Thêm/lấy entry thread-safe
            var entry = _entries.GetOrAdd(technicalSessionId, _ => new PreGenerationEntry());

            lock (entry.Lock)
            {
                // Đã hoàn thành hoặc đang chạy → trả trạng thái ngay
                if (entry.Status == TechnicalPreGenerationStatus.Completed
                    || entry.Status == TechnicalPreGenerationStatus.Generating)
                {
                    return ToDto(technicalSessionId, entry);
                }

                // Đánh dấu bắt đầu
                entry.Status = TechnicalPreGenerationStatus.Generating;
                entry.UserId = userId;
                entry.ErrorMessage = null;
                entry.RetryCount = 0;
                entry.Cts?.Dispose();
                entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

            // 3. Chạy background task (fire-and-forget) – KHÔNG block caller
            _ = Task.Run(() => ExecutePreGenerationAsync(technicalSessionId, entry));

            return ToDto(technicalSessionId, entry);
        }

        public TechnicalPreGenerationStatusDto GetStatus(int technicalSessionId)
        {
            if (_entries.TryGetValue(technicalSessionId, out var entry))
            {
                lock (entry.Lock)
                {
                    return ToDto(technicalSessionId, entry);
                }
            }

            return new TechnicalPreGenerationStatusDto
            {
                Status = TechnicalPreGenerationStatus.Idle,
                TechnicalSessionId = technicalSessionId,
            };
        }

        public void CancelPreGeneration(int technicalSessionId)
        {
            if (_entries.TryRemove(technicalSessionId, out var entry))
            {
                lock (entry.Lock)
                {
                    try { entry.Cts?.Cancel(); } catch { /* best-effort */ }
                    entry.Cts?.Dispose();
                    entry.Status = TechnicalPreGenerationStatus.Idle;
                }
                _logger.LogInformation(
                    "[PreGen] Cancelled pre-generation for Technical session {SessionId}.",
                    technicalSessionId);
            }
        }

        /// <summary>
        /// Thực thi background generation với retry logic.
        /// </summary>
        private async Task ExecutePreGenerationAsync(int technicalSessionId, PreGenerationEntry entry)
        {
            CancellationToken token;
            int userId;
            lock (entry.Lock)
            {
                token = entry.Cts?.Token ?? CancellationToken.None;
                userId = entry.UserId;
            }

            for (var attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "[PreGen] Pre-generation cancelled for session {SessionId}.", technicalSessionId);
                    lock (entry.Lock) { entry.Status = TechnicalPreGenerationStatus.Idle; }
                    _entries.TryRemove(technicalSessionId, out _);
                    return;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider
                        .GetRequiredService<ITechnicalInterviewOrchestrator>();

                    var result = await orchestrator.InitializeAsync(
                        userId,
                        new InitializeTechnicalInterviewRequest
                        {
                            InterviewSessionId = technicalSessionId,
                        },
                        token);

                    if (result.Status == TechnicalOperationStatus.Ok
                        || result.Status == TechnicalOperationStatus.Created
                        || (result.Status == TechnicalOperationStatus.Conflict
                            && result.ErrorCode is "SESSION_CONCURRENCY_CONFLICT"
                                or "INITIALIZE_CONCURRENCY_CONFLICT"
                                or "SESSION_ALREADY_ENDED"))
                    {
                        lock (entry.Lock)
                        {
                            entry.Status = TechnicalPreGenerationStatus.Completed;
                            entry.ErrorMessage = null;
                            entry.RetryCount = attempt;
                        }
                        _logger.LogInformation(
                            "[PreGen] Technical session {SessionId} pre-generated successfully (attempt {Attempt}).",
                            technicalSessionId, attempt + 1);
                        return;
                    }

                    // Other business errors → ghi log nhưng có thể retry
                    var errorMsg = $"{result.ErrorCode}: {result.Message}";
                    _logger.LogWarning(
                        "[PreGen] Attempt {Attempt} for session {SessionId} returned {Status}: {Error}",
                        attempt + 1, technicalSessionId, result.Status, errorMsg);

                    lock (entry.Lock)
                    {
                        entry.ErrorMessage = errorMsg;
                        entry.RetryCount = attempt + 1;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation(
                        "[PreGen] Pre-generation cancelled for session {SessionId}.", technicalSessionId);
                    lock (entry.Lock) { entry.Status = TechnicalPreGenerationStatus.Idle; }
                    _entries.TryRemove(technicalSessionId, out _);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[PreGen] Attempt {Attempt} for session {SessionId} threw an exception.",
                        attempt + 1, technicalSessionId);
                    lock (entry.Lock)
                    {
                        entry.ErrorMessage = ex.Message;
                        entry.RetryCount = attempt + 1;
                    }
                }

                // Exponential backoff: 2s, 4s, 8s
                if (attempt < MaxRetryCount)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                    try { await Task.Delay(delay, token); }
                    catch (OperationCanceledException) { break; }
                }
            }

            // Hết retry → đánh dấu Failed
            lock (entry.Lock)
            {
                entry.Status = TechnicalPreGenerationStatus.Failed;
            }
            _logger.LogError(
                "[PreGen] Pre-generation for session {SessionId} FAILED after {MaxRetry} retries. Last error: {Error}",
                technicalSessionId, MaxRetryCount, entry.ErrorMessage);
        }

        private static TechnicalPreGenerationStatusDto ToDto(int sessionId, PreGenerationEntry entry)
        {
            return new TechnicalPreGenerationStatusDto
            {
                Status = entry.Status,
                TechnicalSessionId = sessionId,
                ErrorMessage = entry.ErrorMessage,
                RetryCount = entry.RetryCount,
            };
        }
    }
}
