using System.Threading;
using System.Threading.Tasks;

namespace ai_speis_be.Services.BackgroundWorker
{
    public interface ICvParseQueue
    {
        ValueTask QueueCvParseAsync(CvParseRequest request, CancellationToken cancellationToken = default);
        ValueTask<CvParseRequest> DequeueAsync(CancellationToken cancellationToken);
    }
}
