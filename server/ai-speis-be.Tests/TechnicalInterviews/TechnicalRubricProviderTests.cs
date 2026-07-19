using ai_speis_be.TechnicalInterviews.Rubrics;
using Microsoft.AspNetCore.Hosting;
using Moq;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalRubricProviderTests
{
    [Fact]
    public void GetRequired_LoadsVersionedDocumentRubricConfiguration()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.ContentRootPath).Returns(Path.GetFullPath("..\\ai-speis-be"));
        var provider = new TechnicalRubricProvider(environment.Object);

        var rubric = provider.GetRequired("technical-rubric-v1");

        Assert.Equal(5, rubric.Dimensions.Count);
        Assert.Equal(1m, rubric.Dimensions.Sum(item => item.Weight));
        Assert.Equal("Xuất sắc", rubric.GetPerformanceBand(4.50m).Name);
        Assert.Equal("Kém", rubric.GetPerformanceBand(1.49m).Name);
    }
}
