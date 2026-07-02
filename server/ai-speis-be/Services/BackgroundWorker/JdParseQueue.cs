using System.Threading.Channels;
using System.Threading.Tasks;

namespace ai_speis_be.Services.BackgroundWorker
{
    public interface IJdParseQueue
    {
        ValueTask QueueJdForParsingAsync(int jdFileId);
        ValueTask<int> DequeueAsync(CancellationToken cancellationToken);
    }

    public class JdParseQueue : IJdParseQueue
    {
        private readonly Channel<int> _queue;

        public JdParseQueue()
        {
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<int>(options);
        }

        public async ValueTask QueueJdForParsingAsync(int jdFileId)
        {
            await _queue.Writer.WriteAsync(jdFileId);
        }

        public async ValueTask<int> DequeueAsync(CancellationToken cancellationToken)
        {
            var jdFileId = await _queue.Reader.ReadAsync(cancellationToken);
            return jdFileId;
        }
    }
}
