using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;

namespace ai_speis_be.Services.CodingService.Selection
{
    public interface ICodingQuestionSelectionService
    {
        Task<List<CodingQuestion>> SelectCodingQuestionsAsync(
            InterviewSession session,
            CancellationToken cancellationToken = default);
    }
}
