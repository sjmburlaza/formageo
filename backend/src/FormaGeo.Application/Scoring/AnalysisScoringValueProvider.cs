using FormaGeo.Application.Analyses;
using FormaGeo.Domain.Analyses;
using FormaGeo.Domain.Scoring;
using FormaGeo.Domain.Sites;
using System.Text.Json;

namespace FormaGeo.Application.Scoring;

public sealed class AnalysisScoringValueProvider
    : IScoringValueProvider
{
    private readonly IAnalysisRunRepository _analysisRunRepository;

    public AnalysisScoringValueProvider(
        IAnalysisRunRepository analysisRunRepository)
    {
        _analysisRunRepository = analysisRunRepository;
    }

    public async Task<IReadOnlyList<CriterionObservation>>
        GetValuesAsync(
            Site site,
            IReadOnlyCollection<ScoringCriterion> criteria,
            CancellationToken cancellationToken = default)
    {
        var runs = await _analysisRunRepository.GetBySiteIdAsync(
            site.Id,
            cancellationToken);
        var latest = runs
            .Where(run =>
                run.Status == AnalysisStatus.Completed &&
                run.ResultJson is not null)
            .GroupBy(run => run.AnalysisType)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(run => run.CompletedAtUtc)
                    .First());
        var documents = new Dictionary<
            AnalysisType,
            JsonDocument>();

        try
        {
            foreach (var run in latest.Values)
            {
                documents[run.AnalysisType] =
                    JsonDocument.Parse(run.ResultJson!);
            }

            return criteria
                .Select(criterion =>
                    ReadObservation(
                        criterion.Key,
                        latest,
                        documents))
                .ToArray();
        }
        finally
        {
            foreach (var document in documents.Values)
            {
                document.Dispose();
            }
        }
    }

    private static CriterionObservation ReadObservation(
        string criterionKey,
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        return criterionKey switch
        {
            "flood-risk" => FloodRisk(runs, documents),
            "slope" => Metric(
                criterionKey,
                AnalysisType.Terrain,
                "averageSlopeDegrees",
                runs,
                documents),
            "road-access" => Metric(
                criterionKey,
                AnalysisType.Accessibility,
                "nearestDistanceMetres",
                runs,
                documents),
            "distance-schools" => FacilityDistance(
                criterionKey,
                "Education",
                runs,
                documents),
            "distance-hospitals" => FacilityDistance(
                criterionKey,
                "Health",
                runs,
                documents),
            "land-use-compatibility" => LandUseCompatibility(
                runs,
                documents),
            "developable-area" => DevelopableArea(
                runs,
                documents),
            "population-reach" => Missing(
                criterionKey,
                "No completed population-reach analysis is available."),
            _ => Missing(
                criterionKey,
                "The selected data source has no scoring adapter.")
        };
    }

    private static CriterionObservation FloodRisk(
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        const string key = "flood-risk";
        if (!TryResult(
            AnalysisType.HazardExposure,
            runs,
            documents,
            out var run,
            out var root))
        {
            return Missing(
                key,
                "Run Hazard Exposure to calculate mapped flood coverage.");
        }

        if (root.TryGetProperty("results", out var results))
        {
            foreach (var result in results.EnumerateArray())
            {
                if (StringProperty(result, "id") == "flood-zone" &&
                    NumberProperty(result, "sitePercent") is { } value)
                {
                    var version =
                        StringProperty(result, "dataVersion") ??
                        run.AnalysisVersion;
                    return new CriterionObservation(
                        key,
                        value,
                        version,
                        null);
                }
            }
        }

        return Missing(
            key,
            "The latest Hazard Exposure result has no flood-coverage value.");
    }

    private static CriterionObservation Metric(
        string key,
        AnalysisType analysisType,
        string propertyName,
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        if (!TryResult(
            analysisType,
            runs,
            documents,
            out var run,
            out var root))
        {
            return Missing(
                key,
                $"Run {AnalysisCatalog.Get(analysisType).Name} to calculate this value.");
        }

        var value = root.TryGetProperty("metrics", out var metrics)
            ? NumberProperty(metrics, propertyName)
            : null;

        return value.HasValue
            ? new CriterionObservation(
                key,
                value,
                run.AnalysisVersion,
                null)
            : Missing(
                key,
                $"The latest {AnalysisCatalog.Get(analysisType).Name} result has no usable value.");
    }

    private static CriterionObservation FacilityDistance(
        string key,
        string facilityType,
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        if (!TryResult(
            AnalysisType.NearbyFacilities,
            runs,
            documents,
            out var run,
            out var root))
        {
            return Missing(
                key,
                "Run Nearby Facilities with all facility types to calculate this distance.");
        }

        if (!root.TryGetProperty("facilities", out var facilities) ||
            facilities.ValueKind != JsonValueKind.Array)
        {
            return Missing(
                key,
                $"The latest Nearby Facilities result has no mapped {facilityType.ToLowerInvariant()} facilities.");
        }

        var distance = facilities
            .EnumerateArray()
            .Where(facility =>
                string.Equals(
                    StringProperty(facility, "facilityType"),
                    facilityType,
                    StringComparison.OrdinalIgnoreCase))
            .Select(facility =>
                NumberProperty(facility, "distanceMetres"))
            .Where(value => value.HasValue)
            .Min();

        return distance.HasValue
            ? new CriterionObservation(
                key,
                distance,
                run.AnalysisVersion,
                null)
            : Missing(
                key,
                $"No mapped {facilityType.ToLowerInvariant()} facility was found in the analyzed radius.");
    }

    private static CriterionObservation LandUseCompatibility(
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        const string key = "land-use-compatibility";
        if (!TryResult(
            AnalysisType.Zoning,
            runs,
            documents,
            out var run,
            out var root))
        {
            return Missing(
                key,
                "Run Zoning to identify the dominant land use.");
        }

        var landUse = root.TryGetProperty(
            "metrics",
            out var metrics)
            ? StringProperty(metrics, "primaryLandUse")
            : null;
        var score = landUse?.Trim().ToLowerInvariant() switch
        {
            "mixed use" => 100m,
            "commercial" => 90m,
            "residential" => 80m,
            "institutional" => 75m,
            "industrial" => 60m,
            "open space" => 40m,
            null or "" => (decimal?)null,
            _ => 50m
        };

        return score.HasValue
            ? new CriterionObservation(
                key,
                score,
                run.AnalysisVersion,
                null)
            : Missing(
                key,
                "The latest Zoning result has no dominant land-use classification.");
    }

    private static CriterionObservation DevelopableArea(
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents)
    {
        const string key = "developable-area";
        if (!TryResult(
            AnalysisType.Zoning,
            runs,
            documents,
            out var run,
            out var root))
        {
            return Missing(
                key,
                "Run Zoning to calculate mapped development restrictions.");
        }

        var restricted = root.TryGetProperty(
            "metrics",
            out var metrics)
            ? NumberProperty(metrics, "restrictedPercent")
            : null;

        return restricted.HasValue
            ? new CriterionObservation(
                key,
                Math.Clamp(100m - restricted.Value, 0m, 100m),
                run.AnalysisVersion,
                null)
            : Missing(
                key,
                "The latest Zoning result has no restricted-area percentage.");
    }

    private static bool TryResult(
        AnalysisType analysisType,
        IReadOnlyDictionary<AnalysisType, AnalysisRun> runs,
        IReadOnlyDictionary<AnalysisType, JsonDocument> documents,
        out AnalysisRun run,
        out JsonElement root)
    {
        if (runs.TryGetValue(analysisType, out run!) &&
            documents.TryGetValue(analysisType, out var document))
        {
            root = document.RootElement;
            return true;
        }

        root = default;
        return false;
    }

    private static decimal? NumberProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDecimal(out var value)
                ? value
                : null;
    }

    private static string? StringProperty(
        JsonElement element,
        string propertyName)
    {
        return element.TryGetProperty(
                propertyName,
                out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static CriterionObservation Missing(
        string key,
        string reason)
    {
        return new CriterionObservation(
            key,
            null,
            null,
            reason);
    }
}
