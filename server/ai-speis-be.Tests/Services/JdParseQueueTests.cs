using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Services.BackgroundWorker;
using Xunit;

namespace ai_speis_be.Tests.Services
{
    public class JdParseQueueTests
    {
        [Fact]
        public async Task QueueJdForParsingAsync_ShouldEnqueueAndDequeueSuccessfully()
        {
            // Arrange
            var queue = new JdParseQueue();
            int expectedJdFileId = 42;
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            await queue.QueueJdForParsingAsync(expectedJdFileId);
            var dequeuedId = await queue.DequeueAsync(cancellationTokenSource.Token);

            // Assert
            Assert.Equal(expectedJdFileId, dequeuedId);
        }

        [Fact]
        public async Task DequeueAsync_ShouldBlockUntilItemIsEnqueued()
        {
            // Arrange
            var queue = new JdParseQueue();
            int expectedJdFileId = 99;
            var cancellationTokenSource = new CancellationTokenSource();

            // Act
            // Bắt đầu dequeue ngầm (chưa có item nên sẽ block)
            var dequeueTask = queue.DequeueAsync(cancellationTokenSource.Token).AsTask();

            // Đảm bảo task chưa hoàn thành
            Assert.False(dequeueTask.IsCompleted);

            // Queue item vào
            await queue.QueueJdForParsingAsync(expectedJdFileId);

            // Chờ task hoàn thành
            var dequeuedId = await dequeueTask;

            // Assert
            Assert.True(dequeueTask.IsCompletedSuccessfully);
            Assert.Equal(expectedJdFileId, dequeuedId);
        }
    }
}
