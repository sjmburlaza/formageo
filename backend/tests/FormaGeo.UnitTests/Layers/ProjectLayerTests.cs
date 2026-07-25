using FormaGeo.Domain.Layers;

namespace FormaGeo.UnitTests.Layers;

public sealed class ProjectLayerTests
{
    [Fact]
    public void Create_NormalizesAndStoresProjectPreferences()
    {
        var projectId = Guid.NewGuid();
        var layerId = Guid.NewGuid();

        var projectLayer = ProjectLayer.Create(
            projectId,
            layerId,
            true,
            0.65m,
            2,
            "  health  ");

        Assert.Equal(projectId, projectLayer.ProjectId);
        Assert.Equal(layerId, projectLayer.LayerDefinitionId);
        Assert.True(projectLayer.IsVisible);
        Assert.Equal(0.65m, projectLayer.Opacity);
        Assert.Equal(2, projectLayer.SortOrder);
        Assert.Equal("health", projectLayer.Filter);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_RejectsOpacityOutsideSupportedRange(
        double opacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectLayer.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                (decimal)opacity,
                0));
    }

    [Fact]
    public void Create_RejectsNegativeLayerOrder()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectLayer.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                1m,
                -1));
    }

    [Fact]
    public void Create_DropsAnEmptyFilter()
    {
        var projectLayer = ProjectLayer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            false,
            1m,
            0,
            "  ");

        Assert.Null(projectLayer.Filter);
    }
}
