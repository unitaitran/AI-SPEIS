using System.Collections.Concurrent;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.DTOs;
using ai_speis_be.TechnicalInterviews.V2;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.TechnicalInterviews.PreGeneration
{
    public sealed class TechnicalPreGenerationService : ITechnicalPreGenerationService
    {
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
            using (var checkScope = _scopeFactory.CreateScope())
            {
                var db = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var session = await db.InterviewSessions
                    .AsNoTracking()
                    .Include(s => s.InterviewCampaign)
                    .Where(s => s.InterviewSessionId == technicalSessionId
                        && s.InterviewCampaign.UserId == userId
                        && s.InterviewRoundType == InterviewRoundType.Technical)
                    .Select(s => new { s.TechnicalState, s.TechnicalRuntimeVersion })
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
            }

            var entry = _entries.GetOrAdd(technicalSessionId, _ => new PreGenerationEntry());

            lock (entry.Lock)
            {
                if (entry.Status == TechnicalPreGenerationStatus.Completed
                    || entry.Status == TechnicalPreGenerationStatus.Generating)
                {
                    return ToDto(technicalSessionId, entry);
                }

                entry.Status = TechnicalPreGenerationStatus.Generating;
                entry.UserId = userId;
                entry.ErrorMessage = null;
                entry.RetryCount = 0;
                entry.Cts?.Dispose();
                entry.Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

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

        private async Task ExecutePreGenerationAsync(int technicalSessionId, PreGenerationEntry entry)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ITechnicalV2InterviewOrchestrator>();
                await orchestrator.InitializeAsync(entry.UserId, technicalSessionId, new InitializeTechnicalV2Request(), CancellationToken.None);
                lock (entry.Lock) { entry.Status = TechnicalPreGenerationStatus.Completed; }
            }
            catch (Exception ex)
            {
                lock (entry.Lock)
                {
                    entry.Status = TechnicalPreGenerationStatus.Failed;
                    entry.ErrorMessage = ex.Message;
                }
            }
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
