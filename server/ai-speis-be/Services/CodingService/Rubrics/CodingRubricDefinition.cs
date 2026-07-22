using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ai_speis_be.Services.CodingService.Rubrics
{
    public sealed class CodingRubricDefinition
    {
        public string Version { get; set; } = "coding-rubric-v1";
        public int TotalQuestionCount { get; set; } = 3;
        public List<CodingRubricBand> Bands { get; set; } = new();

        public CodingRubricBand GetBand(int? matchScore)
        {
            var score = Math.Clamp(matchScore ?? 50, 0, 100);
            var band = Bands.FirstOrDefault(b => score >= b.MinimumScore && score <= b.MaximumScore);
            return band ?? Bands.FirstOrDefault(b => b.Code == "MEDIUM_FIT") ?? Bands.First();
        }

        public static CodingRubricDefinition LoadDefault()
        {
            try
            {
                var basePath = AppContext.BaseDirectory;
                var jsonPath = Path.Combine(basePath, "Services", "CodingService", "Rubrics", "coding-rubric-v1.json");
                
                if (!File.Exists(jsonPath))
                {
                    // Fallback to relative path from execution dir
                    jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Services", "CodingService", "Rubrics", "coding-rubric-v1.json");
                }

                if (File.Exists(jsonPath))
                {
                    var json = File.ReadAllText(jsonPath);
                    var parsed = JsonSerializer.Deserialize<CodingRubricDefinition>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (parsed != null && parsed.Bands.Count > 0)
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                // Fallback code-based definition
            }

            return GetFallbackDefinition();
        }

        private static CodingRubricDefinition GetFallbackDefinition()
        {
            return new CodingRubricDefinition
            {
                Version = "coding-rubric-v1",
                TotalQuestionCount = 3,
                Bands = new List<CodingRubricBand>
                {
                    new CodingRubricBand
                    {
                        Code = "LOW_FIT",
                        Name = "Thấp",
                        MinimumScore = 0,
                        MaximumScore = 39,
                        CvRatio = 0.70,
                        JdRatio = 0.30,
                        CvQuestionCount = 2,
                        JdQuestionCount = 1,
                        AllowedDifficulties = new List<string> { "Easy", "Medium" }
                    },
                    new CodingRubricBand
                    {
                        Code = "MEDIUM_FIT",
                        Name = "Trung bình",
                        MinimumScore = 40,
                        MaximumScore = 69,
                        CvRatio = 0.50,
                        JdRatio = 0.50,
                        CvQuestionCount = 2,
                        JdQuestionCount = 1,
                        AllowedDifficulties = new List<string> { "Medium" }
                    },
                    new CodingRubricBand
                    {
                        Code = "HIGH_FIT",
                        Name = "Cao",
                        MinimumScore = 70,
                        MaximumScore = 100,
                        CvRatio = 0.30,
                        JdRatio = 0.70,
                        CvQuestionCount = 1,
                        JdQuestionCount = 2,
                        AllowedDifficulties = new List<string> { "Medium", "Hard" }
                    }
                }
            };
        }
    }

    public sealed class CodingRubricBand
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MinimumScore { get; set; }
        public int MaximumScore { get; set; }
        public double CvRatio { get; set; }
        public double JdRatio { get; set; }
        public int CvQuestionCount { get; set; }
        public int JdQuestionCount { get; set; }
        public List<string> AllowedDifficulties { get; set; } = new();

        public (int CvCount, int JdCount) GetQuestionCounts(int totalCount)
        {
            if (totalCount <= 0) totalCount = 3;
            int cvCount = (int)Math.Round(totalCount * CvRatio, MidpointRounding.AwayFromZero);
            int jdCount = totalCount - cvCount;
            if (jdCount < 0) { jdCount = 0; cvCount = totalCount; }
            return (cvCount, jdCount);
        }
    }
}
