using FormaGeo.Application.Analyses;
using FormaGeo.Domain.Analyses;
using System.Text.Json;

namespace FormaGeo.UnitTests.Analyses;

public sealed class AnalysisCatalogTests
{
    [Fact]
    public void Catalog_ContainsAllSupportedAnalysisTypes()
    {
        var definitions = AnalysisCatalog.GetAll();

        Assert.Equal(
            Enum.GetValues<AnalysisType>().Length,
            definitions.Count);
        Assert.All(
            definitions,
            definition =>
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        definition.AnalysisVersion)));
    }

    [Fact]
    public void ValidateAndNormalizeParameters_AppliesDefaults()
    {
        var parameters =
            AnalysisCatalog.ValidateAndNormalizeParameters(
                AnalysisType.NearbyFacilities,
                JsonSerializer.SerializeToElement(
                    new Dictionary<string, object?>()));

        Assert.Equal(
            2000,
            parameters
                .GetProperty("radiusMetres")
                .GetDouble());
        Assert.Equal(
            string.Empty,
            parameters
                .GetProperty("facilityTypes")
                .GetString());
    }

    [Fact]
    public void ValidateAndNormalizeParameters_RejectsUnknownInput()
    {
        var parameters = JsonSerializer.SerializeToElement(
            new { unsupported = true });

        var exception =
            Assert.Throws<AnalysisValidationException>(
                () =>
                    AnalysisCatalog
                        .ValidateAndNormalizeParameters(
                            AnalysisType.SiteGeometry,
                            parameters));

        Assert.Equal(
            "inputParameters.unsupported",
            exception.Field);
    }
}
