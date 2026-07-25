using FormaGeo.Domain.Analyses;
using System.Text.Json;

namespace FormaGeo.Application.Analyses;

public static class AnalysisCatalog
{
    private const string CurrentVersion = "1.0.0";

    private static readonly IReadOnlyList<AnalysisDefinitionResponse>
        Definitions =
        [
            new(
                AnalysisType.SiteGeometry,
                "Site Geometry",
                "Measures area, perimeter, centroid, bounds, and boundary quality.",
                ["Saved site boundary"],
                "Low",
                CurrentVersion,
                []),
            new(
                AnalysisType.HazardExposure,
                "Hazard Exposure",
                "Calculates the portion of the site intersecting mapped hazard areas.",
                ["Saved site boundary", "Flood susceptibility dataset"],
                "Medium",
                CurrentVersion,
                [
                    new(
                        "minimumOverlapPercent",
                        "Minimum reported overlap",
                        "Ignore mapped overlaps below this percentage.",
                        "number",
                        false,
                        0,
                        0,
                        100)
                ]),
            new(
                AnalysisType.Zoning,
                "Zoning",
                "Summarizes land-use zones that overlap the site boundary.",
                ["Saved site boundary", "Land-use zoning dataset"],
                "Medium",
                CurrentVersion,
                [
                    new(
                        "includeUnzoned",
                        "Include unzoned area",
                        "Report the portion not covered by a mapped zone.",
                        "boolean",
                        false,
                        true)
                ]),
            new(
                AnalysisType.Accessibility,
                "Accessibility",
                "Measures proximity to the nearest mapped transport corridor.",
                ["Saved site boundary", "Transport corridor dataset"],
                "Medium",
                CurrentVersion,
                [
                    new(
                        "maximumDistanceMetres",
                        "Distance threshold",
                        "Distance used to classify the site as accessible.",
                        "number",
                        false,
                        5000,
                        100,
                        100000)
                ]),
            new(
                AnalysisType.NearbyFacilities,
                "Nearby Facilities",
                "Finds mapped community facilities within a configurable radius.",
                ["Saved site boundary", "Community facilities dataset"],
                "Medium",
                CurrentVersion,
                [
                    new(
                        "radiusMetres",
                        "Search radius",
                        "Maximum distance from the site boundary.",
                        "number",
                        false,
                        2000,
                        100,
                        50000),
                    new(
                        "facilityTypes",
                        "Facility types",
                        "Optional comma-separated types, such as Health, Education.",
                        "text",
                        false,
                        "")
                ]),
            new(
                AnalysisType.Suitability,
                "Suitability",
                "Combines hazards, access, facilities, and green-space context into a versioned score.",
                [
                    "Saved site boundary",
                    "Hazard, transport, facilities, and green-space datasets"
                ],
                "High",
                CurrentVersion,
                [
                    new(
                        "hazardWeight",
                        "Hazard weight",
                        "Higher values favor lower mapped hazard exposure.",
                        "number",
                        false,
                        35,
                        0,
                        100),
                    new(
                        "accessWeight",
                        "Access weight",
                        "Higher values favor shorter distance to transport.",
                        "number",
                        false,
                        25,
                        0,
                        100),
                    new(
                        "facilitiesWeight",
                        "Facilities weight",
                        "Higher values favor more nearby facilities.",
                        "number",
                        false,
                        20,
                        0,
                        100),
                    new(
                        "greenSpaceWeight",
                        "Green-space weight",
                        "Higher values favor mapped green-space overlap.",
                        "number",
                        false,
                        20,
                        0,
                        100)
                ])
        ];

    public static IReadOnlyList<AnalysisDefinitionResponse> GetAll()
    {
        return Definitions;
    }

    public static AnalysisDefinitionResponse Get(
        AnalysisType analysisType)
    {
        return Definitions.SingleOrDefault(
                definition =>
                    definition.AnalysisType == analysisType)
            ?? throw new AnalysisValidationException(
                "analysisType",
                $"Analysis type '{analysisType}' is not supported.");
    }

    public static JsonElement ValidateAndNormalizeParameters(
        AnalysisType analysisType,
        JsonElement parameters)
    {
        var definition = Get(analysisType);

        if (parameters.ValueKind is JsonValueKind.Undefined
            or JsonValueKind.Null)
        {
            parameters = JsonSerializer.SerializeToElement(
                new Dictionary<string, object?>());
        }

        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw new AnalysisValidationException(
                "inputParameters",
                "Analysis input parameters must be a JSON object.");
        }

        var normalized = new Dictionary<string, object?>();
        var knownParameters = definition.Parameters.ToDictionary(
            parameter => parameter.Name,
            StringComparer.OrdinalIgnoreCase);

        foreach (var property in parameters.EnumerateObject())
        {
            if (!knownParameters.TryGetValue(
                    property.Name,
                    out var parameter))
            {
                throw new AnalysisValidationException(
                    $"inputParameters.{property.Name}",
                    $"Parameter '{property.Name}' is not supported for " +
                    $"{definition.Name}.");
            }

            normalized[parameter.Name] = ValidateParameter(
                parameter,
                property.Value);
        }

        foreach (var parameter in definition.Parameters)
        {
            if (!normalized.ContainsKey(parameter.Name))
            {
                if (parameter.Required &&
                    parameter.DefaultValue is null)
                {
                    throw new AnalysisValidationException(
                        $"inputParameters.{parameter.Name}",
                        $"Parameter '{parameter.Label}' is required.");
                }

                normalized[parameter.Name] =
                    parameter.DefaultValue;
            }
        }

        if (analysisType == AnalysisType.Suitability)
        {
            var weightTotal = normalized
                .Where(pair =>
                    pair.Key.EndsWith(
                        "Weight",
                        StringComparison.Ordinal))
                .Sum(pair => Convert.ToDouble(pair.Value));

            if (weightTotal <= 0)
            {
                throw new AnalysisValidationException(
                    "inputParameters",
                    "At least one suitability weight must be greater than zero.");
            }
        }

        return JsonSerializer.SerializeToElement(normalized);
    }

    private static object? ValidateParameter(
        AnalysisParameterDefinitionResponse parameter,
        JsonElement value)
    {
        if (parameter.Type == "number")
        {
            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDouble(out var number) ||
                !double.IsFinite(number))
            {
                throw InvalidParameter(
                    parameter,
                    "must be a finite number");
            }

            if (parameter.Minimum.HasValue &&
                number < parameter.Minimum.Value)
            {
                throw InvalidParameter(
                    parameter,
                    $"must be at least {parameter.Minimum.Value}");
            }

            if (parameter.Maximum.HasValue &&
                number > parameter.Maximum.Value)
            {
                throw InvalidParameter(
                    parameter,
                    $"cannot exceed {parameter.Maximum.Value}");
            }

            return number;
        }

        if (parameter.Type == "boolean")
        {
            if (value.ValueKind is not (
                    JsonValueKind.True or JsonValueKind.False))
            {
                throw InvalidParameter(
                    parameter,
                    "must be true or false");
            }

            return value.GetBoolean();
        }

        if (parameter.Type == "text")
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw InvalidParameter(
                    parameter,
                    "must be text");
            }

            return value.GetString()?.Trim() ?? string.Empty;
        }

        throw InvalidParameter(
            parameter,
            "uses an unsupported parameter type");
    }

    private static AnalysisValidationException InvalidParameter(
        AnalysisParameterDefinitionResponse parameter,
        string problem)
    {
        return new AnalysisValidationException(
            $"inputParameters.{parameter.Name}",
            $"Parameter '{parameter.Label}' {problem}.");
    }
}
