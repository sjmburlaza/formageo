using System.Text;
using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites;
using FormaGeo.Application.Sites.Contracts;
using FormaGeo.Domain.Sites;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace FormaGeo.Application.SiteImports;

public sealed class SiteImportService
{
    public const int MaximumFileSizeBytes = 1_048_576;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".geojson",
            ".json"
        };

    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public SiteImportService(
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<SiteImportResult> HandleAsync(
        Guid projectId,
        SiteImportRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateRequestAsync(
            projectId,
            request,
            cancellationToken);

        var fallbackName =
            Path.GetFileNameWithoutExtension(request.FileName).Trim();
        var parsed = GeoJsonImportParser.Parse(
            request.Content,
            string.IsNullOrWhiteSpace(fallbackName)
                ? "Imported site"
                : fallbackName);
        var selectedIndexes =
            request.SelectedFeatureIndexes is null
                ? null
                : request.SelectedFeatureIndexes.ToHashSet();
        var selectedFeatures = parsed.Features
            .Where(feature =>
                selectedIndexes is null ||
                selectedIndexes.Contains(feature.Index))
            .ToArray();

        if (selectedFeatures.Length == 0)
        {
            throw Invalid(
                "selectedFeatureIndexes",
                "no_features_selected",
                "Select at least one feature to import.");
        }

        var skipped = selectedFeatures
            .Where(feature => feature.Error is not null)
            .Select(feature => new SiteImportSkippedFeature(
                feature.Index,
                feature.Name,
                feature.Error!))
            .ToList();
        var warnings = selectedFeatures
            .SelectMany(feature => feature.Warnings)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var candidates = request.Mode == SiteImportMode.Merge
            ? MergeCandidates(
                selectedFeatures,
                fallbackName,
                skipped)
            : SeparateCandidates(selectedFeatures);
        var existingSites =
            await _siteRepository.GetByProjectIdAsync(
                projectId,
                cancellationToken);
        var acceptedSites = new List<Site>();
        var candidateNumber = 0;

        foreach (var candidate in candidates)
        {
            candidateNumber++;

            if (IsDuplicate(
                candidate.Polygon,
                existingSites.Select(site => site.Boundary)
                    .Concat(acceptedSites.Select(site => site.Boundary))))
            {
                skipped.Add(new SiteImportSkippedFeature(
                    candidate.FeatureIndex,
                    candidate.FeatureName,
                    "This geometry duplicates a Site already in the target project."));
                continue;
            }

            var name = ApplyNamePattern(
                request.NamePattern,
                candidate.FeatureName,
                candidateNumber);

            acceptedSites.Add(
                Site.Create(
                    projectId,
                    name,
                    candidate.Polygon));
        }

        if (acceptedSites.Count > 0)
        {
            await _siteRepository.AddRangeAsync(
                acceptedSites,
                cancellationToken);
        }

        if (parsed.Features.Count > selectedFeatures.Length)
        {
            warnings.Add(
                $"{parsed.Features.Count - selectedFeatures.Length} " +
                "unselected feature(s) were not imported.");
        }

        return new SiteImportResult(
            Guid.NewGuid(),
            projectId,
            parsed.Features.Count,
            parsed.Features.Count(feature =>
                feature.Error is not null),
            parsed.DetectedCoordinateSystem,
            acceptedSites
                .Select(SiteResponse.FromDomain)
                .ToArray(),
            skipped,
            warnings);
    }

    private async Task ValidateRequestAsync(
        Guid projectId,
        SiteImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _projectRepository.ExistsAsync(
                projectId,
                cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw Invalid(
                "file",
                "file_required",
                "Choose a GeoJSON file to import.");
        }

        var extension = Path.GetExtension(request.FileName);

        if (!AllowedExtensions.Contains(extension))
        {
            throw Invalid(
                "file",
                "unsupported_file_type",
                "Only .geojson and .json files are supported.");
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw Invalid(
                "file",
                "empty_file",
                "The selected GeoJSON file is empty.");
        }

        if (Encoding.UTF8.GetByteCount(request.Content) >
            MaximumFileSizeBytes)
        {
            throw Invalid(
                "file",
                "file_too_large",
                "The GeoJSON file cannot exceed 1 MB.");
        }
    }

    private static IReadOnlyList<ImportCandidate>
        SeparateCandidates(
            IEnumerable<ParsedGeoJsonFeature> features)
    {
        var candidates = new List<ImportCandidate>();

        foreach (var feature in features.Where(feature =>
                     feature.Error is null))
        {
            for (var index = 0;
                 index < feature.Polygons.Count;
                 index++)
            {
                var featureName = feature.Polygons.Count == 1
                    ? feature.Name
                    : $"{feature.Name} {index + 1}";

                candidates.Add(new ImportCandidate(
                    feature.Index,
                    featureName,
                    feature.Polygons[index]));
            }
        }

        return candidates;
    }

    private static IReadOnlyList<ImportCandidate> MergeCandidates(
        IReadOnlyList<ParsedGeoJsonFeature> features,
        string fallbackName,
        List<SiteImportSkippedFeature> skipped)
    {
        var polygons = features
            .Where(feature => feature.Error is null)
            .SelectMany(feature => feature.Polygons)
            .Cast<Geometry>()
            .ToArray();

        if (polygons.Length == 0)
        {
            return [];
        }

        var merged = UnaryUnionOp.Union(polygons);

        if (merged is not Polygon polygon ||
            polygon.IsEmpty ||
            !polygon.IsValid)
        {
            skipped.Add(new SiteImportSkippedFeature(
                null,
                null,
                "The selected polygons are disjoint or incompatible and " +
                "cannot be merged into one Site. Import them separately instead."));

            return [];
        }

        polygon.SRID = 4326;

        return
        [
            new ImportCandidate(
                null,
                string.IsNullOrWhiteSpace(fallbackName)
                    ? "Merged import"
                    : fallbackName,
                polygon)
        ];
    }

    private static bool IsDuplicate(
        Polygon candidate,
        IEnumerable<Polygon> existingPolygons)
    {
        return existingPolygons.Any(existing =>
            existing.EqualsTopologically(candidate));
    }

    private static string ApplyNamePattern(
        string? pattern,
        string featureName,
        int number)
    {
        var template = string.IsNullOrWhiteSpace(pattern)
            ? "{feature}"
            : pattern.Trim();
        var name = template
            .Replace(
                "{feature}",
                featureName,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "{n}",
                number.ToString(),
                StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            name = featureName;
        }

        return name.Length <= 200
            ? name
            : name[..200].TrimEnd();
    }

    private static SiteImportException Invalid(
        string field,
        string problem,
        string message)
    {
        return new SiteImportException(
            field,
            problem,
            message);
    }

    private sealed record ImportCandidate(
        int? FeatureIndex,
        string FeatureName,
        Polygon Polygon);
}
