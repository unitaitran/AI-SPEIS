using Xunit;
using ai_speis_be.Services.BackgroundWorker;

namespace ai_speis_be.Tests.Services
{
    public class CvParseQueueTests
    {
        // U14: Enqueue → Dequeue trả đúng item gốc
        [Fact]
        public async Task Enqueue_Dequeue_ReturnsSameRequest()
        {
            // Arrange
            var queue = new CvParseQueue();
            var request = new CvParseRequest(42, "/path/to/cv.pdf");

            // Act
            await queue.QueueCvParseAsync(request);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var dequeued = await queue.DequeueAsync(cts.Token);

            // Assert
            Assert.Equal(request.CVFileId, dequeued.CVFileId);
            Assert.Equal(request.FilePath, dequeued.FilePath);
        }

        [Fact]
        public async Task Dequeue_EmptyQueue_BlocksUntilEnqueue()
        {
            // Arrange
            var queue = new CvParseQueue();
            var request = new CvParseRequest(99, "/another/cv.pdf");

            // Act: Dequeue starts first (blocks), then enqueue after 100ms
            var dequeueTask = Task.Run(async () =>
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                return await queue.DequeueAsync(cts.Token);
            });

            await Task.Delay(100);
            await queue.QueueCvParseAsync(request);

            var result = await dequeueTask;

            // Assert
            Assert.Equal(99, result.CVFileId);
        }

        [Fact]
        public async Task Dequeue_CancelledToken_ThrowsOperationCancelledException()
        {
            // Arrange
            var queue = new CvParseQueue();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => queue.DequeueAsync(cts.Token).AsTask());
        }
    }
}
