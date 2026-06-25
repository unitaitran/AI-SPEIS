using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ai_speis_be.Services.BackgroundWorker
{
    public class CvParseQueue : ICvParseQueue
    {
        private readonly Channel<CvParseRequest> _queue;

        public CvParseQueue(int capacity = 100)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<CvParseRequest>(options);
        }

        public async ValueTask QueueCvParseAsync(CvParseRequest request, CancellationToken cancellationToken = default)
        {
            await _queue.Writer.WriteAsync(request, cancellationToken);
        }

        public async ValueTask<CvParseRequest> DequeueAsync(CancellationToken cancellationToken)
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
