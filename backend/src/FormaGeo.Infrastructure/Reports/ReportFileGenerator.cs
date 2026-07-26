using FormaGeo.Application.Reports;
using FormaGeo.Domain.Reports;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FormaGeo.Infrastructure.Reports;

public sealed class ReportFileGenerator
    : IReportFileGenerator
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    public GeneratedReportFile Generate(
        ReportFormat format,
        ReportPreviewResponse snapshot)
    {
        var slug = Slug(snapshot.Title);

        return format switch
        {
            ReportFormat.Pdf =>
                new GeneratedReportFile(
                    FeasibilityPdfWriter.Write(snapshot),
                    $"{slug}.pdf",
                    "application/pdf"),
            ReportFormat.MetricsCsv =>
                CsvFile(
                    SiteMetricsCsv(snapshot),
                    $"{slug}-metrics.csv"),
            ReportFormat.GeometryGeoJson =>
                JsonFile(
                    SiteGeometryGeoJson(snapshot),
                    $"{slug}-geometry.geojson",
                    "application/geo+json"),
            ReportFormat.AnalysisGeoJson =>
                JsonFile(
                    AnalysisGeoJson(snapshot),
                    $"{slug}-analysis.geojson",
                    "application/geo+json"),
            ReportFormat.ComparisonCsv =>
                CsvFile(
                    ComparisonCsv(snapshot),
                    $"{slug}.csv"),
            ReportFormat.ProjectArchive =>
                new GeneratedReportFile(
                    ProjectArchive(snapshot),
                    $"{slug}.zip",
                    "application/zip"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(format),
                format,
                "Unsupported report format.")
        };
    }

    private static GeneratedReportFile CsvFile(
        string csv,
        string fileName)
    {
        return new GeneratedReportFile(
            new UTF8Encoding(true).GetBytes(csv),
            fileName,
            "text/csv; charset=utf-8");
    }

    private static GeneratedReportFile JsonFile(
        byte[] content,
        string fileName,
        string contentType)
    {
        return new GeneratedReportFile(
            content,
            fileName,
            contentType);
    }

    private static string SiteMetricsCsv(
        ReportPreviewResponse snapshot)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "record_type",
                "site_id",
                "site_name",
                "metric",
                "value",
                "unit",
                "source",
                "version",
                "generated_at_utc"
            }
        };

        foreach (var measurement in snapshot.Measurements)
        {
            rows.Add(MetricRow(
                measurement,
                "Area",
                measurement.AreaHectares,
                "ha",
                "PostGIS geography",
                "WGS 84",
                snapshot.GenerationDateUtc));
            rows.Add(MetricRow(
                measurement,
                "Perimeter",
                measurement.PerimeterMetres,
                "m",
                "PostGIS geography",
                "WGS 84",
                snapshot.GenerationDateUtc));
            rows.Add(MetricRow(
                measurement,
                "Vertex count",
                measurement.VertexCount,
                "vertices",
                "Saved site boundary",
                measurement.CoordinateSystem,
                snapshot.GenerationDateUtc));
        }

        if (snapshot.SuitabilityScore is { } score)
        {
            rows.Add(
                new object?[]
                {
                    "suitability",
                    score.SiteId,
                    score.SiteName,
                    "Overall suitability score",
                    score.OverallScore,
                    "score",
                    score.ModelName,
                    $"v{score.ModelVersion}",
                    snapshot.GenerationDateUtc
                });

            rows.AddRange(
                score.Metrics.Select(metric =>
                    (IReadOnlyList<object?>)new object?[]
                    {
                        "suitability_metric",
                        score.SiteId,
                        score.SiteName,
                        metric.Name,
                        metric.RawValue,
                        metric.Unit,
                        metric.DataSource,
                        metric.DataVersion,
                        snapshot.GenerationDateUtc
                    }));
        }

        return ToCsv(rows);
    }

    private static IReadOnlyList<object?> MetricRow(
        ReportMeasurementResponse measurement,
        string metric,
        object? value,
        string unit,
        string source,
        string version,
        DateTimeOffset generationDate)
    {
        return new object?[]
        {
            "geometry",
            measurement.SiteId,
            measurement.SiteName,
            metric,
            value,
            unit,
            source,
            version,
            generationDate
        };
    }

    private static string ComparisonCsv(
        ReportPreviewResponse snapshot)
    {
        var comparison = snapshot.Comparison
            ?? throw new InvalidOperationException(
                "A comparison CSV requires a comparison snapshot.");
        var metricNames = comparison.Sites
            .SelectMany(site => site.Metrics)
            .Select(metric => metric.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "rank",
                "site_id",
                "site_name",
                "overall_score",
                "rating",
                "scenario",
                "model_version"
            }
            .Concat(metricNames.Cast<object?>())
            .ToArray()
        };

        rows.AddRange(
            comparison.Sites.Select(site =>
                (IReadOnlyList<object?>)new object?[]
                {
                    site.Rank,
                    site.SiteId,
                    site.SiteName,
                    site.OverallScore,
                    site.Rating,
                    comparison.ScenarioName,
                    comparison.ModelVersion
                }
                .Concat(
                    metricNames.Select(name =>
                        (object?)site.Metrics
                            .FirstOrDefault(metric =>
                                metric.Name.Equals(
                                    name,
                                    StringComparison.OrdinalIgnoreCase))
                            ?.RawValue))
                .ToArray()));

        return ToCsv(rows);
    }

    private static string FindingsCsv(
        ReportPreviewResponse snapshot)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "site_id",
                "site_name",
                "category",
                "finding",
                "severity",
                "classification",
                "site_percent",
                "summary",
                "analysis_version"
            }
        };
        rows.AddRange(
            snapshot.Findings.Select(finding =>
                (IReadOnlyList<object?>)new object?[]
                {
                    finding.SiteId,
                    finding.SiteName,
                    finding.Category,
                    finding.Name,
                    finding.Severity,
                    finding.Classification,
                    finding.SitePercent,
                    finding.Summary,
                    finding.AnalysisVersion
                }));
        return ToCsv(rows);
    }

    private static string SourcesCsv(
        ReportPreviewResponse snapshot)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "dataset",
                "organization",
                "version",
                "published_date",
                "license",
                "source_url",
                "attribution"
            }
        };
        rows.AddRange(
            snapshot.DataSources.Select(source =>
                (IReadOnlyList<object?>)new object?[]
                {
                    source.Dataset,
                    source.Organization,
                    source.Version,
                    source.PublishedDate,
                    source.License,
                    source.SourceUrl,
                    source.Attribution
                }));
        return ToCsv(rows);
    }

    private static byte[] SiteGeometryGeoJson(
        ReportPreviewResponse snapshot)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true
            });

        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WritePropertyName("features");
        writer.WriteStartArray();
        foreach (var site in snapshot.MapSites)
        {
            WriteSiteFeature(writer, site, snapshot);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteSiteFeature(
        Utf8JsonWriter writer,
        ReportMapSiteResponse site,
        ReportPreviewResponse snapshot)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");
        writer.WriteString("id", site.SiteId);
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("siteId", site.SiteId);
        writer.WriteString("name", site.SiteName);
        writer.WriteString("projectId", snapshot.ProjectId);
        writer.WriteString("projectName", snapshot.ProjectName);
        writer.WriteString(
            "generatedAtUtc",
            snapshot.GenerationDateUtc);
        writer.WriteEndObject();
        writer.WritePropertyName("geometry");
        writer.WriteStartObject();
        writer.WriteString("type", "Polygon");
        writer.WritePropertyName("coordinates");
        writer.WriteStartArray();
        writer.WriteStartArray();
        foreach (var coordinate in site.Boundary)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(coordinate[0]);
            writer.WriteNumberValue(coordinate[1]);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static byte[] AnalysisGeoJson(
        ReportPreviewResponse snapshot)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions
            {
                Indented = true
            });
        writer.WriteStartObject();
        writer.WriteString("type", "FeatureCollection");
        writer.WritePropertyName("metadata");
        writer.WriteStartObject();
        writer.WriteString("projectId", snapshot.ProjectId);
        writer.WriteString("sourceId", snapshot.SourceId);
        writer.WriteString(
            "generatedAtUtc",
            snapshot.GenerationDateUtc);
        writer.WriteEndObject();
        writer.WritePropertyName("features");
        writer.WriteStartArray();

        foreach (var feature in snapshot.AnalysisFeatures)
        {
            WriteAnalysisGeometry(writer, feature);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteAnalysisGeometry(
        Utf8JsonWriter writer,
        AnalysisGeoJsonFeature feature)
    {
        var geometry = feature.Geometry;
        var type = geometry.TryGetProperty(
                "type",
                out var typeProperty)
            ? typeProperty.GetString()
            : null;

        if (type == "FeatureCollection" &&
            geometry.TryGetProperty(
                "features",
                out var features) &&
            features.ValueKind == JsonValueKind.Array)
        {
            foreach (var nested in features.EnumerateArray())
            {
                WriteFeature(
                    writer,
                    feature,
                    nested.TryGetProperty(
                        "geometry",
                        out var nestedGeometry)
                        ? nestedGeometry
                        : default,
                    nested.TryGetProperty(
                        "properties",
                        out var nestedProperties)
                        ? nestedProperties
                        : default);
            }
            return;
        }

        if (type == "Feature")
        {
            WriteFeature(
                writer,
                feature,
                geometry.TryGetProperty(
                    "geometry",
                    out var featureGeometry)
                    ? featureGeometry
                    : default,
                geometry.TryGetProperty(
                    "properties",
                    out var featureProperties)
                    ? featureProperties
                    : default);
            return;
        }

        WriteFeature(
            writer,
            feature,
            geometry,
            default);
    }

    private static void WriteFeature(
        Utf8JsonWriter writer,
        AnalysisGeoJsonFeature feature,
        JsonElement geometry,
        JsonElement nestedProperties)
    {
        if (geometry.ValueKind is JsonValueKind.Undefined
            or JsonValueKind.Null)
        {
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("type", "Feature");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        writer.WriteString("analysisId", feature.AnalysisId);
        writer.WriteString("analysisType", feature.AnalysisType);
        writer.WriteString(
            "analysisVersion",
            feature.AnalysisVersion);
        foreach (var property in feature.Properties)
        {
            writer.WritePropertyName(property.Key);
            JsonSerializer.Serialize(
                writer,
                property.Value,
                JsonOptions);
        }
        if (nestedProperties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in nestedProperties
                .EnumerateObject())
            {
                if (feature.Properties.ContainsKey(property.Name))
                {
                    continue;
                }
                property.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
        writer.WritePropertyName("geometry");
        geometry.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static byte[] ProjectArchive(
        ReportPreviewResponse snapshot)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Create,
            true,
            Encoding.UTF8))
        {
            AddEntry(
                archive,
                "README.txt",
                Encoding.UTF8.GetBytes(
                    $"{snapshot.Title}\n" +
                    $"Generated: {snapshot.GenerationDateUtc:O}\n\n" +
                    "This archive contains the project snapshot, site boundaries, " +
                    "measurements, analysis findings, analysis geometries, and data-source citations.\n\n" +
                    string.Join(
                        "\n",
                        snapshot.Limitations.Select(
                            limitation => $"- {limitation}"))));
            AddEntry(
                archive,
                "project.json",
                JsonSerializer.SerializeToUtf8Bytes(
                    snapshot,
                    JsonOptions));
            AddEntry(
                archive,
                "sites.geojson",
                SiteGeometryGeoJson(snapshot));
            AddEntry(
                archive,
                "measurements.csv",
                new UTF8Encoding(true).GetBytes(
                    SiteMetricsCsv(snapshot)));
            AddEntry(
                archive,
                "analysis-findings.csv",
                new UTF8Encoding(true).GetBytes(
                    FindingsCsv(snapshot)));
            AddEntry(
                archive,
                "analysis-results.geojson",
                AnalysisGeoJson(snapshot));
            AddEntry(
                archive,
                "data-sources.csv",
                new UTF8Encoding(true).GetBytes(
                    SourcesCsv(snapshot)));
        }

        return stream.ToArray();
    }

    private static void AddEntry(
        ZipArchive archive,
        string name,
        byte[] content)
    {
        var entry = archive.CreateEntry(
            name,
            CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(
            1980,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static string ToCsv(
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        return string.Join(
            "\r\n",
            rows.Select(row =>
                string.Join(
                    ",",
                    row.Select(value =>
                        EscapeCsv(FormatValue(value)))))) +
            "\r\n";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTimeOffset date => date.ToString("O"),
            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string EscapeCsv(string value)
    {
        return value.IndexOfAny(
                [',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string Slug(string value)
    {
        var characters = value
            .Trim()
            .ToLowerInvariant()
            .Select(character =>
                char.IsAsciiLetterOrDigit(character)
                    ? character
                    : '-')
            .ToArray();
        var slug = string.Join(
            '-',
            new string(characters)
                .Split(
                    '-',
                    StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(slug)
            ? "formageo-report"
            : slug[..Math.Min(slug.Length, 100)];
    }
}
