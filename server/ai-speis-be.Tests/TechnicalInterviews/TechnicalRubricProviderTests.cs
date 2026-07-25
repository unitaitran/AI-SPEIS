using ai_speis_be.TechnicalInterviews.Rubrics;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalRubricProviderTests
{
    [Fact]
    public void GetRequired_LoadsZeroToTenRubricWithStablePerformanceBands()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(Path.GetFullPath("..\\ai-speis-be"));
        var provider = new TechnicalRubricProvider(environment.Object);

        var rubric = provider.GetRequired("technical-rubric-v2");

        Assert.Equal(5, rubric.Dimensions.Count);
        Assert.Equal(1m, rubric.Dimensions.Sum(item => item.Weight));
        Assert.Equal(10m, rubric.MaximumScore);
    }

    [Fact]
    public void GetRequired_LoadsZeroToTenRubricWithStablePerformanceBands()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(Path.GetFullPath("..\\ai-speis-be"));
        var provider = new TechnicalRubricProvider(environment.Object);

        var rubric = provider.GetRequired("technical-rubric-v2");

        Assert.Equal(10m, rubric.MaximumScore);
        Assert.Equal(
            new[] { "ACCURACY", "TECHNICAL_DEPTH", "REASONING", "APPLICATION", "COMMUNICATION" },
            rubric.Dimensions.Select(item => item.Code));
        Assert.Equal("EXCELLENT", rubric.GetPerformanceBand(9m).Code);
        Assert.Equal("VERY_GOOD", rubric.GetPerformanceBand(8m).Code);
        Assert.Equal("GOOD", rubric.GetPerformanceBand(6.5m).Code);
        Assert.Equal("MINIMUM_REQUIREMENT_MET", rubric.GetPerformanceBand(5m).Code);
        Assert.Equal("WEAK", rubric.GetPerformanceBand(3m).Code);
        Assert.Equal("VERY_WEAK", rubric.GetPerformanceBand(2.99m).Code);
        Assert.Equal("EXCELLENT", rubric.GetPerformanceBandCode(10m));
        Assert.Equal("VERY_GOOD", rubric.GetPerformanceBandCode(8.99m));
        Assert.Equal("GOOD", rubric.GetPerformanceBandCode(7m));
        Assert.Equal("MINIMUM_REQUIREMENT_MET", rubric.GetPerformanceBandCode(5m));
        Assert.Equal("WEAK", rubric.GetPerformanceBandCode(3m));
        Assert.Equal("VERY_WEAK", rubric.GetPerformanceBandCode(0m));
    }
}
