namespace ai_speis_be.Models.DTOs
{
    public sealed class SpeechToTextRequestDto
    {
        public IFormFile AudioFile { get; set; } = null!;
        public string LanguageCode { get; set; } = "vi-VN";
    }
}