using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ai_speis_be.Services.SpeechToTextService
{
    public interface ISpeechToTextService
    {
        Task<string> RecognizeSpeechAsync(
            IFormFile audioFile,
            string languageCode = "vi-VN",
            CancellationToken cancellationToken = default);
    }
}
