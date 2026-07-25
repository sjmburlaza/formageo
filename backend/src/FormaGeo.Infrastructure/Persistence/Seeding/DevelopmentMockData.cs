using FormaGeo.Domain.Projects;
using FormaGeo.Domain.Sites;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace FormaGeo.Infrastructure.Persistence.Seeding;

internal static class DevelopmentMockData
{
    internal const string ProjectName =
        "Metro Manila Site Feasibility Study (Demo)";

    private static readonly IReadOnlyList<MockSiteDefinition>
        SiteDefinitions =
        [
            new(
                "Riverside Mixed-Use Candidate",
                [
                    [121.0015, 14.5650],
                    [121.0030, 14.5585],
                    [121.0125, 14.5585],
                    [121.0135, 14.5685],
                    [121.0060, 14.5710],
                    [121.0015, 14.5650]
                ]),
            new(
                "Central Transit-Oriented Candidate",
                [
                    [121.0160, 14.5660],
                    [121.0240, 14.5660],
                    [121.0250, 14.5740],
                    [121.0180, 14.5760],
                    [121.0160, 14.5660]
                ]),
            new(
                "Eastern Hillside Residential Candidate",
                [
                    [121.0320, 14.5690],
                    [121.0390, 14.5690],
                    [121.0400, 14.5790],
                    [121.0340, 14.5820],
                    [121.0310, 14.5750],
                    [121.0320, 14.5690]
                ]),
            new(
                "Southern Waterfront Candidate",
                [
                    [120.9920, 14.5520],
                    [121.0020, 14.5520],
                    [121.0040, 14.5600],
                    [120.9950, 14.5620],
                    [120.9920, 14.5520]
                ]),
            new(
                "Northern Community Campus Candidate",
                [
                    [121.0080, 14.5835],
                    [121.0180, 14.5835],
                    [121.0200, 14.5910],
                    [121.0120, 14.5940],
                    [121.0070, 14.5890],
                    [121.0080, 14.5835]
                ])
        ];

    internal static Project CreateProject()
    {
        return Project.Create(ProjectName);
    }

    internal static IReadOnlyList<Site> CreateSites(
        Guid projectId)
    {
        return SiteDefinitions
            .Select(definition => Site.Create(
                projectId,
                definition.Name,
                CreatePolygon(definition.Positions)))
            .ToArray();
    }

    private static Polygon CreatePolygon(
        IReadOnlyList<double[]> positions)
    {
        var geometryFactory =
            NtsGeometryServices.Instance
                .CreateGeometryFactory(srid: 4326);
        var coordinates = positions
            .Select(position =>
                new Coordinate(
                    position[0],
                    position[1]))
            .ToArray();

        return geometryFactory.CreatePolygon(coordinates);
    }

    private sealed record MockSiteDefinition(
        string Name,
        IReadOnlyList<double[]> Positions);
}
