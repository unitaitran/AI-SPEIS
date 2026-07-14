using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
    public sealed class TextToSpeechRequestDto
    {
        [Required(ErrorMessage = "Text is required.")]
        [StringLength(5000, MinimumLength = 1, ErrorMessage = "Text must be between 1 and 5000 characters.")]
        public string Text { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "LanguageCode is too long.")]
        public string LanguageCode { get; set; } = "vi-VN";

        [StringLength(120, ErrorMessage = "VoiceName is too long.")]
        public string? VoiceName { get; set; }

        [Range(0.25, 4.0, ErrorMessage = "SpeakingRate must be between 0.25 and 4.0.")]
        public double SpeakingRate { get; set; } = 1.0;

        [Range(-20.0, 20.0, ErrorMessage = "Pitch must be between -20.0 and 20.0.")]
        public double Pitch { get; set; } = 0.0;
    }
}
