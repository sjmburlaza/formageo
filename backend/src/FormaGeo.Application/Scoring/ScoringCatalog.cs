using FormaGeo.Domain.Scoring;

namespace FormaGeo.Application.Scoring;

public static class ScoringCatalog
{
    private static readonly IReadOnlyList<
        ScoringCriterionDefinitionResponse> Definitions =
        [
            new(
                "flood-risk",
                "Flood risk",
                "Percentage of the site intersecting mapped flood susceptibility areas.",
                ScoringDirection.LowerIsBetter,
                NormalizationMethod.Linear,
                "Hazard exposure · flood susceptibility",
                "%",
                0,
                60,
                MissingDataBehavior.CannotScore,
                "Hazard Exposure"),
            new(
                "slope",
                "Slope",
                "Area-weighted average terrain slope across the site.",
                ScoringDirection.LowerIsBetter,
                NormalizationMethod.Linear,
                "Terrain summary grid",
                "°",
                0,
                25,
                MissingDataBehavior.ExcludeAndReweight,
                "Terrain"),
            new(
                "road-access",
                "Road access",
                "Shortest boundary distance to a mapped transport corridor.",
                ScoringDirection.LowerIsBetter,
                NormalizationMethod.Linear,
                "Transport corridors",
                " m",
                0,
                5000,
                MissingDataBehavior.ExcludeAndReweight,
                "Accessibility"),
            new(
                "distance-schools",
                "Distance to schools",
                "Shortest distance to a mapped education facility in the search radius.",
                ScoringDirection.LowerIsBetter,
                NormalizationMethod.Linear,
                "Community facilities · Education",
                " m",
                0,
                4000,
                MissingDataBehavior.ExcludeAndReweight,
                "Nearby Facilities"),
            new(
                "distance-hospitals",
                "Distance to hospitals",
                "Shortest distance to a mapped health facility in the search radius.",
                ScoringDirection.LowerIsBetter,
                NormalizationMethod.Linear,
                "Community facilities · Health",
                " m",
                0,
                5000,
                MissingDataBehavior.ExcludeAndReweight,
                "Nearby Facilities"),
            new(
                "population-reach",
                "Population reach",
                "Estimated population reachable within the configured service area.",
                ScoringDirection.HigherIsBetter,
                NormalizationMethod.Linear,
                "Population reach analysis",
                " people",
                0,
                50000,
                MissingDataBehavior.CannotScore,
                "Population Reach"),
            new(
                "land-use-compatibility",
                "Land-use compatibility",
                "Compatibility index derived from the dominant mapped land-use classification.",
                ScoringDirection.HigherIsBetter,
                NormalizationMethod.Linear,
                "Land-use zoning",
                "%",
                0,
                100,
                MissingDataBehavior.CannotScore,
                "Zoning"),
            new(
                "developable-area",
                "Developable area",
                "Share of the site without mapped development restrictions.",
                ScoringDirection.HigherIsBetter,
                NormalizationMethod.Linear,
                "Development restrictions",
                "%",
                0,
                100,
                MissingDataBehavior.CannotScore,
                "Zoning")
        ];

    private static readonly IReadOnlyList<ScoringPresetResponse> Presets =
        [
            new(
                "balanced",
                "Balanced screening",
                "Balances resilience, access, planning compatibility, and developable area.",
                [
                    new("flood-risk", 25),
                    new("slope", 15),
                    new("road-access", 20),
                    new("land-use-compatibility", 20),
                    new("developable-area", 20)
                ]),
            new(
                "resilience",
                "Resilience first",
                "Prioritizes lower flood exposure and gentler terrain.",
                [
                    new("flood-risk", 40),
                    new("slope", 30),
                    new("land-use-compatibility", 15),
                    new("developable-area", 15)
                ]),
            new(
                "community",
                "Community access",
                "Prioritizes transport, education, health, and population reach.",
                [
                    new("road-access", 25),
                    new("distance-schools", 25),
                    new("distance-hospitals", 25),
                    new("population-reach", 25)
                ])
        ];

    public static ScoringCatalogResponse Get()
    {
        return new ScoringCatalogResponse(
            100,
            Definitions,
            Presets);
    }
}
