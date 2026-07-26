using FormaGeo.Application.Reports;
using FormaGeo.Domain.Reports;
using System.Globalization;
using System.Text;

namespace FormaGeo.Infrastructure.Reports;

internal static class FeasibilityPdfWriter
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 54;
    private const double ContentWidth = PageWidth - (Margin * 2);

    public static byte[] Write(
        ReportPreviewResponse report)
    {
        var document = new PdfDocument(
            report.Branding.AccentColor);
        var includedSections = report.Sections
            .Where(section => section.Included)
            .OrderBy(section => section.SortOrder)
            .ToArray();

        foreach (var section in includedSections)
        {
            switch (section.Key)
            {
                case ReportSectionKey.Cover:
                    Cover(document, report);
                    break;
                case ReportSectionKey.ExecutiveSummary:
                    ExecutiveSummary(document, report);
                    break;
                case ReportSectionKey.SiteLocation:
                    Location(document, report);
                    break;
                case ReportSectionKey.GeometrySummary:
                    Geometry(document, report);
                    break;
                case ReportSectionKey.HazardFindings:
                    Findings(
                        document,
                        report,
                        "Hazards",
                        "Hazard findings");
                    break;
                case ReportSectionKey.PlanningFindings:
                    Findings(
                        document,
                        report,
                        "Planning",
                        "Planning findings");
                    break;
                case ReportSectionKey.SuitabilityScore:
                    Score(document, report);
                    break;
                case ReportSectionKey.SiteComparison:
                    Comparison(document, report);
                    break;
                case ReportSectionKey.DataSources:
                    Sources(document, report);
                    break;
                case ReportSectionKey.Disclaimer:
                    Disclaimer(document, report);
                    break;
            }
        }

        if (document.PageCount == 0)
        {
            Cover(document, report);
        }

        return document.Build(
            report.Title,
            report.Branding.OrganizationName,
            report.Branding.FooterText);
    }

    private static void Cover(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = document.NewPage(false);
        page.FillRect(
            0,
            0,
            PageWidth,
            PageHeight,
            0.96,
            0.97,
            0.97);
        page.FillRect(
            0,
            0,
            20,
            PageHeight,
            document.Accent);
        page.FillRect(
            54,
            80,
            72,
            6,
            document.Accent);
        page.Text(
            report.Branding.OrganizationName.ToUpperInvariant(),
            54,
            112,
            10,
            true,
            document.Accent);
        var y = page.WrappedText(
            report.Title,
            54,
            175,
            ContentWidth,
            30,
            37,
            true,
            (0.08, 0.13, 0.16));
        y += 24;
        page.Text(
            report.SourceType == ReportSourceType.Comparison
                ? "SITE COMPARISON FEASIBILITY REPORT"
                : "SITE FEASIBILITY REPORT",
            54,
            y,
            10,
            true,
            document.Accent);
        y += 52;
        page.Rule(54, y, 541, y, 0.82, 0.85, 0.85);
        y += 30;
        page.Text(
            "PROJECT",
            54,
            y,
            8,
            true,
            (0.38, 0.43, 0.45));
        page.Text(
            report.ProjectName,
            170,
            y,
            12,
            true,
            (0.08, 0.13, 0.16));
        y += 31;
        page.Text(
            "REPORT SOURCE",
            54,
            y,
            8,
            true,
            (0.38, 0.43, 0.45));
        page.Text(
            report.SourceName,
            170,
            y,
            11,
            false,
            (0.08, 0.13, 0.16));
        y += 31;
        page.Text(
            "PREPARED BY",
            54,
            y,
            8,
            true,
            (0.38, 0.43, 0.45));
        page.Text(
            report.Branding.PreparedBy,
            170,
            y,
            11,
            false,
            (0.08, 0.13, 0.16));
        y += 31;
        page.Text(
            "GENERATED",
            54,
            y,
            8,
            true,
            (0.38, 0.43, 0.45));
        page.Text(
            report.GenerationDateUtc
                .ToString(
                    "dd MMMM yyyy, HH:mm 'UTC'",
                    CultureInfo.InvariantCulture),
            170,
            y,
            11,
            false,
            (0.08, 0.13, 0.16));

        page.FillRect(
            54,
            690,
            ContentWidth,
            78,
            0.91,
            0.95,
            0.94);
        page.Text(
            "DECISION-SUPPORT SNAPSHOT",
            72,
            716,
            8,
            true,
            document.Accent);
        page.WrappedText(
            "Measurements, spatial findings, scoring, citations, and limitations are frozen as of the generation date.",
            72,
            739,
            ContentWidth - 36,
            10,
            14,
            false,
            (0.16, 0.25, 0.25));
    }

    private static void ExecutiveSummary(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Executive summary",
            "A concise decision snapshot of the selected site evidence.");
        var y = 150d;
        var notable = report.Findings.Count(finding =>
            finding.Severity.Equals(
                "High",
                StringComparison.OrdinalIgnoreCase) ||
            finding.Severity.Equals(
                "Moderate",
                StringComparison.OrdinalIgnoreCase));
        var scoreText = report.SuitabilityScore is { } score
            ? score.IsScoreable
                ? $"{score.OverallScore:0.#}"
                : "N/A"
            : report.Comparison?.Sites
                .FirstOrDefault()?.OverallScore
                ?.ToString("0.#", CultureInfo.InvariantCulture)
                ?? "N/A";

        page.MetricCard(
            54,
            y,
            150,
            78,
            report.MapSites.Count.ToString(
                CultureInfo.InvariantCulture),
            "Sites reviewed");
        page.MetricCard(
            222,
            y,
            150,
            78,
            notable.ToString(
                CultureInfo.InvariantCulture),
            "Notable findings");
        page.MetricCard(
            390,
            y,
            151,
            78,
            scoreText,
            report.SourceType == ReportSourceType.Comparison
                    ? "Leading score"
                    : "Suitability score");
        y += 112;
        page.Subheading("Assessment overview", 54, y);
        y += 26;
        var overview =
            $"This report evaluates {report.SourceName} within the {report.ProjectName} project. " +
            $"It consolidates {report.Measurements.Count} geometry record(s), " +
            $"{report.Findings.Count} spatial finding(s), and " +
            $"{report.DataSources.Count} attributed data source(s).";
        y = page.WrappedText(
            overview,
            54,
            y,
            ContentWidth,
            11,
            16,
            false,
            (0.18, 0.22, 0.24));
        y += 28;
        page.Subheading("Key observations", 54, y);
        y += 25;

        var observations = report.Findings
            .OrderBy(finding =>
                SeverityOrder(finding.Severity))
            .Take(5)
            .Select(finding =>
                $"{finding.SiteName}: {finding.Name} - {finding.Classification}.")
            .ToList();
        if (observations.Count == 0)
        {
            observations.Add(
                "No completed hazard or planning findings were available at generation time.");
        }

        foreach (var observation in observations)
        {
            y = page.Bullet(
                observation,
                54,
                y,
                ContentWidth);
            y += 8;
        }
    }

    private static void Location(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Site location",
            "Consistent WGS 84 framing for every boundary in this report.");
        page.Map(
            report.MapSites,
            54,
            150,
            ContentWidth,
            390);
        var y = 566d;
        page.Text(
            "MAP NOTES",
            54,
            y,
            8,
            true,
            document.Accent);
        y += 22;
        page.WrappedText(
            "Boundary outlines are normalized to a shared viewport with a fixed margin. " +
            "This report map is for orientation and is not a cadastral or survey plan.",
            54,
            y,
            ContentWidth,
            10,
            14,
            false,
            (0.25, 0.3, 0.32));
    }

    private static void Geometry(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Geometry summary",
            "Geodesic measurements and boundary-quality metadata.");
        var y = 150d;
        foreach (var measurement in report.Measurements)
        {
            if (y > 680)
            {
                page = SectionPage(
                    document,
                    "Geometry summary (continued)",
                    "Geodesic measurements and boundary-quality metadata.");
                y = 150;
            }

            page.Card(
                54,
                y,
                ContentWidth,
                126,
                0.98,
                0.99,
                0.99);
            page.Text(
                measurement.SiteName,
                72,
                y + 26,
                13,
                true,
                (0.08, 0.13, 0.16));
            page.LabelValue(
                "Area",
                measurement.AreaHectares.HasValue
                    ? $"{measurement.AreaHectares:N2} ha"
                    : "Unavailable",
                72,
                y + 58);
            page.LabelValue(
                "Perimeter",
                measurement.PerimeterMetres.HasValue
                    ? $"{measurement.PerimeterMetres:N0} m"
                    : "Unavailable",
                240,
                y + 58);
            page.LabelValue(
                "Vertices",
                measurement.VertexCount.ToString("N0"),
                408,
                y + 58);
            page.LabelValue(
                "Centroid",
                measurement.CentroidLongitude.HasValue &&
                measurement.CentroidLatitude.HasValue
                    ? $"{measurement.CentroidLatitude:F6}, {measurement.CentroidLongitude:F6}"
                    : "Unavailable",
                72,
                y + 94);
            page.LabelValue(
                "Coordinate system",
                measurement.CoordinateSystem,
                300,
                y + 94);
            y += 142;
        }

        if (report.Measurements.Count == 0)
        {
            page.EmptyState(
                54,
                y,
                "No geometry measurements were available.");
        }
    }

    private static void Findings(
        PdfDocument document,
        ReportPreviewResponse report,
        string category,
        string heading)
    {
        var matching = report.Findings
            .Where(finding =>
                finding.Category.Equals(
                    category,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(finding =>
                SeverityOrder(finding.Severity))
            .ThenBy(finding => finding.SiteName)
            .ToArray();
        var page = SectionPage(
            document,
            heading,
            category == "Hazards"
                ? "Mapped exposure and proximity findings from completed analyses."
                : "Mapped zoning, jurisdiction, and development-constraint findings.");
        var y = 150d;

        foreach (var finding in matching)
        {
            var height = 126d;
            if (y + height > 760)
            {
                page = SectionPage(
                    document,
                    $"{heading} (continued)",
                    "Additional evidence records.");
                y = 150;
            }

            page.FindingCard(
                finding,
                54,
                y,
                ContentWidth,
                height);
            y += height + 14;
        }

        if (matching.Length == 0)
        {
            page.EmptyState(
                54,
                y,
                $"No completed {category.ToLowerInvariant()} findings were available at generation time.");
        }
    }

    private static void Score(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Suitability score",
            "Saved scoring output with criterion-level traceability.");
        var score = report.SuitabilityScore;
        if (score is null)
        {
            page.EmptyState(
                54,
                150,
                "No saved site suitability result was available.");
            return;
        }

        page.ScorePanel(
            score,
            54,
            150,
            ContentWidth,
            112);
        var y = 294d;
        page.TableHeader(
            ["Criterion", "Value", "Score", "Contribution"],
            [54d, 270d, 370d, 455d],
            y);
        y += 28;

        foreach (var metric in score.Metrics)
        {
            if (y > 732)
            {
                page = SectionPage(
                    document,
                    "Suitability score (continued)",
                    "Criterion-level scoring evidence.");
                y = 150;
                page.TableHeader(
                    ["Criterion", "Value", "Score", "Contribution"],
                    [54d, 270d, 370d, 455d],
                    y);
                y += 28;
            }

            page.TableRow(
                [
                    metric.Name,
                    metric.RawValue.HasValue
                        ? $"{metric.RawValue:0.##} {metric.Unit}".Trim()
                        : "No data",
                    metric.NormalizedScore.HasValue
                        ? $"{metric.NormalizedScore:0.#}"
                        : "N/A",
                    $"{metric.Contribution:0.##}"
                ],
                [54d, 270d, 370d, 455d],
                [205d, 90d, 75d, 86d],
                y,
                34);
            y += 34;
        }
    }

    private static void Comparison(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Site comparison",
            "Ranked snapshot produced with one scoring model version.");
        var comparison = report.Comparison;
        if (comparison is null)
        {
            page.EmptyState(
                54,
                150,
                "No comparison snapshot is attached to this report.");
            return;
        }

        var y = 150d;
        page.Text(
            $"{comparison.ScenarioName} - model v{comparison.ModelVersion}",
            54,
            y,
            11,
            true,
            document.Accent);
        y += 31;
        page.TableHeader(
            ["Rank", "Candidate site", "Overall", "Rating"],
            [54d, 112d, 388d, 458d],
            y);
        y += 30;
        foreach (var site in comparison.Sites)
        {
            page.TableRow(
                [
                    $"#{site.Rank}",
                    site.SiteName,
                    site.OverallScore.HasValue
                        ? $"{site.OverallScore:0.##}"
                        : "N/A",
                    site.Rating
                ],
                [54d, 112d, 388d, 458d],
                [48d, 266d, 60d, 83d],
                y,
                40);
            y += 40;
        }

        y += 24;
        page.Subheading("Ranking method", 54, y);
        y += 24;
        page.WrappedText(
            comparison.RankingExplanation,
            54,
            y,
            ContentWidth,
            10,
            14,
            false,
            (0.22, 0.27, 0.29));
    }

    private static void Sources(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Data sources",
            "Human-readable attribution for evidence used in this snapshot.");
        var y = 150d;

        foreach (var source in report.DataSources)
        {
            if (y > 704)
            {
                page = SectionPage(
                    document,
                    "Data sources (continued)",
                    "Additional source attributions.");
                y = 150;
            }

            page.Text(
                source.Dataset,
                54,
                y,
                11,
                true,
                (0.08, 0.13, 0.16));
            y += 17;
            var details = string.Join(
                " | ",
                new[]
                {
                    source.Organization,
                    source.Version is null
                        ? null
                        : $"Version {source.Version}",
                    source.PublishedDate is null
                        ? null
                        : $"Published {source.PublishedDate}",
                    source.License
                }.Where(value =>
                    !string.IsNullOrWhiteSpace(value)));
            page.Text(
                details,
                54,
                y,
                9,
                false,
                (0.35, 0.4, 0.42));
            y += 16;
            y = page.WrappedText(
                source.Attribution,
                54,
                y,
                ContentWidth,
                9,
                13,
                false,
                (0.2, 0.25, 0.27));
            if (!string.IsNullOrWhiteSpace(source.SourceUrl))
            {
                y += 2;
                y = page.WrappedText(
                    source.SourceUrl,
                    54,
                    y,
                    ContentWidth,
                    8,
                    11,
                    false,
                    document.Accent);
            }
            y += 16;
            page.Rule(
                54,
                y,
                541,
                y,
                0.88,
                0.9,
                0.9);
            y += 20;
        }

        if (report.DataSources.Count == 0)
        {
            page.EmptyState(
                54,
                y,
                "No external data-source metadata was available.");
        }
    }

    private static void Disclaimer(
        PdfDocument document,
        ReportPreviewResponse report)
    {
        var page = SectionPage(
            document,
            "Disclaimer and limitations",
            "Scope, interpretation, and appropriate decision use.");
        var y = 150d;

        foreach (var limitation in report.Limitations)
        {
            if (y > 715)
            {
                page = SectionPage(
                    document,
                    "Disclaimer and limitations (continued)",
                    "Additional decision-use statements.");
                y = 150;
            }

            y = page.NumberedItem(
                Array.IndexOf(
                    report.Limitations.ToArray(),
                    limitation) + 1,
                limitation,
                54,
                y,
                ContentWidth);
            y += 18;
        }
    }

    private static PdfPage SectionPage(
        PdfDocument document,
        string heading,
        string subtitle)
    {
        var page = document.NewPage(true);
        page.Text(
            heading,
            Margin,
            74,
            23,
            true,
            (0.08, 0.13, 0.16));
        page.WrappedText(
            subtitle,
            Margin,
            106,
            ContentWidth,
            10,
            14,
            false,
            (0.38, 0.43, 0.45));
        page.FillRect(
            Margin,
            128,
            54,
            3,
            document.Accent);
        return page;
    }

    private static int SeverityOrder(string severity)
    {
        return severity.Trim().ToLowerInvariant() switch
        {
            "high" => 0,
            "moderate" => 1,
            "low" => 2,
            "none" => 3,
            _ => 4
        };
    }

    private sealed class PdfDocument
    {
        private readonly List<PdfPage> _pages = [];

        public PdfDocument(string accentColor)
        {
            Accent = ParseHex(accentColor);
        }

        public (double R, double G, double B) Accent { get; }

        public int PageCount => _pages.Count;

        public PdfPage NewPage(bool standardHeader)
        {
            var page = new PdfPage(Accent, standardHeader);
            _pages.Add(page);
            return page;
        }

        public byte[] Build(
            string title,
            string author,
            string? footerText)
        {
            var objectCount = 4 + (_pages.Count * 2) + 1;
            var objects = new string[objectCount + 1];
            objects[1] = "<< /Type /Catalog /Pages 2 0 R /Metadata " +
                objectCount + " 0 R >>";
            var kids = string.Join(
                " ",
                Enumerable.Range(0, _pages.Count)
                    .Select(index =>
                        $"{5 + (index * 2)} 0 R"));
            objects[2] =
                $"<< /Type /Pages /Count {_pages.Count} /Kids [{kids}] >>";
            objects[3] =
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";
            objects[4] =
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>";

            for (var index = 0; index < _pages.Count; index++)
            {
                var pageObject = 5 + (index * 2);
                var contentObject = pageObject + 1;
                var page = _pages[index];
                page.Footer(
                    index + 1,
                    _pages.Count,
                    footerText);
                var commands = page.Commands;
                objects[pageObject] =
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] " +
                    $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> " +
                    $"/Contents {contentObject} 0 R >>";
                objects[contentObject] =
                    $"<< /Length {Encoding.ASCII.GetByteCount(commands)} >>\nstream\n{commands}\nendstream";
            }

            var metadata =
                $"<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" +
                "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">" +
                "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">" +
                "<rdf:Description xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
                $"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{Xml(title)}</rdf:li></rdf:Alt></dc:title>" +
                $"<dc:creator><rdf:Seq><rdf:li>{Xml(author)}</rdf:li></rdf:Seq></dc:creator>" +
                "</rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
            metadata = PdfPage.Ascii(metadata);
            objects[objectCount] =
                $"<< /Type /Metadata /Subtype /XML /Length {Encoding.ASCII.GetByteCount(metadata)} >>\nstream\n{metadata}\nendstream";

            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.7\n%FORMAGEO\n");
            var offsets = new long[objectCount + 1];
            for (var index = 1; index <= objectCount; index++)
            {
                offsets[index] = stream.Position;
                WriteAscii(
                    stream,
                    $"{index} 0 obj\n{objects[index]}\nendobj\n");
            }

            var xrefOffset = stream.Position;
            WriteAscii(
                stream,
                $"xref\n0 {objectCount + 1}\n");
            WriteAscii(
                stream,
                "0000000000 65535 f \n");
            for (var index = 1; index <= objectCount; index++)
            {
                WriteAscii(
                    stream,
                    $"{offsets[index]:D10} 00000 n \n");
            }
            WriteAscii(
                stream,
                $"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\n" +
                $"startxref\n{xrefOffset}\n%%EOF");
            return stream.ToArray();
        }

        private static (double R, double G, double B)
            ParseHex(string value)
        {
            if (value.Length != 7 || value[0] != '#')
            {
                return (0.059, 0.463, 0.431);
            }

            return (
                Convert.ToInt32(value.Substring(1, 2), 16) / 255d,
                Convert.ToInt32(value.Substring(3, 2), 16) / 255d,
                Convert.ToInt32(value.Substring(5, 2), 16) / 255d);
        }

        private static void WriteAscii(
            Stream stream,
            string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes);
        }

        private static string Xml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }

    private sealed class PdfPage
    {
        private readonly StringBuilder _commands =
            new();
        private readonly (
            double R,
            double G,
            double B) _accent;

        public PdfPage(
            (double R, double G, double B) accent,
            bool standardHeader)
        {
            _accent = accent;
            if (standardHeader)
            {
                Text(
                    "FORMAGEO FEASIBILITY REPORT",
                    54,
                    32,
                    7,
                    true,
                    accent);
                Rule(
                    54,
                    44,
                    541,
                    44,
                    0.86,
                    0.88,
                    0.88);
            }
        }

        public string Commands => _commands.ToString();

        public void Footer(
            int pageNumber,
            int pageCount,
            string? footerText)
        {
            Rule(
                54,
                800,
                541,
                800,
                0.86,
                0.88,
                0.88);
            Text(
                footerText ??
                "Prepared with FormaGeo spatial decision support",
                54,
                818,
                7,
                false,
                (0.42, 0.46, 0.47));
            Text(
                $"{pageNumber} / {pageCount}",
                506,
                818,
                7,
                true,
                _accent);
        }

        public void Text(
            string value,
            double x,
            double top,
            double size,
            bool bold,
            (double R, double G, double B) color)
        {
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "BT /{0} {1:0.##} Tf {2:0.###} {3:0.###} {4:0.###} rg 1 0 0 1 {5:0.##} {6:0.##} Tm ({7}) Tj ET\n",
                bold ? "F2" : "F1",
                size,
                color.R,
                color.G,
                color.B,
                x,
                PageHeight - top,
                Escape(value));
        }

        public double WrappedText(
            string value,
            double x,
            double top,
            double width,
            double size,
            double lineHeight,
            bool bold,
            (double R, double G, double B) color)
        {
            var lines = Wrap(value, width, size);
            foreach (var line in lines)
            {
                Text(
                    line,
                    x,
                    top,
                    size,
                    bold,
                    color);
                top += lineHeight;
            }
            return top;
        }

        public void Subheading(
            string value,
            double x,
            double top)
        {
            Text(
                value,
                x,
                top,
                14,
                true,
                (0.08, 0.13, 0.16));
        }

        public double Bullet(
            string value,
            double x,
            double top,
            double width)
        {
            FillCircle(
                x + 4,
                top - 3,
                2.5,
                _accent);
            return WrappedText(
                value,
                x + 18,
                top,
                width - 18,
                10,
                14,
                false,
                (0.2, 0.25, 0.27));
        }

        public double NumberedItem(
            int number,
            string value,
            double x,
            double top,
            double width)
        {
            FillCircle(
                x + 11,
                top - 4,
                11,
                _accent);
            Text(
                number.ToString(
                    CultureInfo.InvariantCulture),
                x + (number < 10 ? 8 : 5),
                top,
                8,
                true,
                (1, 1, 1));
            return WrappedText(
                value,
                x + 34,
                top,
                width - 34,
                10,
                15,
                false,
                (0.2, 0.25, 0.27));
        }

        public void MetricCard(
            double x,
            double top,
            double width,
            double height,
            string value,
            string label)
        {
            Card(
                x,
                top,
                width,
                height,
                0.96,
                0.98,
                0.98);
            Text(
                value,
                x + 16,
                top + 34,
                22,
                true,
                _accent);
            Text(
                label,
                x + 16,
                top + 59,
                8,
                true,
                (0.35, 0.4, 0.42));
        }

        public void Card(
            double x,
            double top,
            double width,
            double height,
            double r,
            double g,
            double b)
        {
            FillRect(x, top, width, height, r, g, b);
            StrokeRect(
                x,
                top,
                width,
                height,
                0.86,
                0.89,
                0.89);
        }

        public void LabelValue(
            string label,
            string value,
            double x,
            double top)
        {
            Text(
                label.ToUpperInvariant(),
                x,
                top,
                7,
                true,
                (0.42, 0.46, 0.47));
            Text(
                value,
                x,
                top + 17,
                10,
                false,
                (0.1, 0.16, 0.18));
        }

        public void FindingCard(
            ReportFindingResponse finding,
            double x,
            double top,
            double width,
            double height)
        {
            Card(
                x,
                top,
                width,
                height,
                0.985,
                0.988,
                0.988);
            var color = finding.Severity.ToLowerInvariant() switch
            {
                "high" => (0.75, 0.12, 0.12),
                "moderate" => (0.72, 0.38, 0.03),
                "low" => (0.10, 0.45, 0.55),
                _ => _accent
            };
            FillRect(
                x,
                top,
                5,
                height,
                color.Item1,
                color.Item2,
                color.Item3);
            Text(
                finding.Name,
                x + 18,
                top + 24,
                12,
                true,
                (0.08, 0.13, 0.16));
            Text(
                $"{finding.Severity.ToUpperInvariant()} | {finding.SiteName}",
                x + width - 150,
                top + 24,
                8,
                true,
                color);
            Text(
                finding.Classification,
                x + 18,
                top + 48,
                10,
                true,
                (0.18, 0.23, 0.25));
            WrappedText(
                finding.Summary,
                x + 18,
                top + 70,
                width - 36,
                9,
                13,
                false,
                (0.32, 0.36, 0.38));
            if (finding.SitePercent.HasValue)
            {
                Text(
                    $"{finding.SitePercent:0.##}% of site",
                    x + width - 100,
                    top + 104,
                    8,
                    true,
                    color);
            }
        }

        public void ScorePanel(
            ReportScoreResponse score,
            double x,
            double top,
            double width,
            double height)
        {
            Card(
                x,
                top,
                width,
                height,
                0.93,
                0.97,
                0.96);
            Text(
                score.IsScoreable
                    ? $"{score.OverallScore:0.#}"
                    : "N/A",
                x + 22,
                top + 48,
                30,
                true,
                _accent);
            Text(
                score.Rating,
                x + 22,
                top + 76,
                10,
                true,
                (0.12, 0.2, 0.21));
            Text(
                score.SiteName,
                x + 130,
                top + 34,
                15,
                true,
                (0.08, 0.13, 0.16));
            Text(
                $"{score.ModelName} | model v{score.ModelVersion}",
                x + 130,
                top + 58,
                9,
                false,
                (0.35, 0.4, 0.42));
            Text(
                $"Calculated {score.CalculatedAtUtc:dd MMM yyyy HH:mm} UTC",
                x + 130,
                top + 78,
                8,
                false,
                (0.42, 0.46, 0.47));
        }

        public void TableHeader(
            IReadOnlyList<string> values,
            IReadOnlyList<double> x,
            double top)
        {
            FillRect(
                54,
                top - 17,
                ContentWidth,
                28,
                0.94,
                0.95,
                0.95);
            for (var index = 0; index < values.Count; index++)
            {
                Text(
                    values[index].ToUpperInvariant(),
                    x[index],
                    top,
                    7,
                    true,
                    (0.35, 0.4, 0.42));
            }
        }

        public void TableRow(
            IReadOnlyList<string> values,
            IReadOnlyList<double> x,
            IReadOnlyList<double> widths,
            double top,
            double height)
        {
            for (var index = 0; index < values.Count; index++)
            {
                var text = Truncate(
                    values[index],
                    widths[index],
                    9);
                Text(
                    text,
                    x[index],
                    top + 21,
                    9,
                    index == 0,
                    (0.13, 0.18, 0.2));
            }
            Rule(
                54,
                top + height,
                541,
                top + height,
                0.88,
                0.9,
                0.9);
        }

        public void EmptyState(
            double x,
            double top,
            string message)
        {
            Card(
                x,
                top,
                ContentWidth,
                100,
                0.97,
                0.98,
                0.98);
            Text(
                "NO SAVED EVIDENCE",
                x + 22,
                top + 34,
                8,
                true,
                _accent);
            WrappedText(
                message,
                x + 22,
                top + 58,
                ContentWidth - 44,
                10,
                14,
                false,
                (0.3, 0.35, 0.37));
        }

        public void Map(
            IReadOnlyList<ReportMapSiteResponse> sites,
            double x,
            double top,
            double width,
            double height)
        {
            FillRect(
                x,
                top,
                width,
                height,
                0.95,
                0.97,
                0.96);
            StrokeRect(
                x,
                top,
                width,
                height,
                0.78,
                0.82,
                0.81);

            for (var step = 1; step < 8; step++)
            {
                var gridX = x + (width * step / 8);
                var gridY = top + (height * step / 8);
                Rule(
                    gridX,
                    top,
                    gridX,
                    top + height,
                    0.88,
                    0.9,
                    0.89);
                Rule(
                    x,
                    gridY,
                    x + width,
                    gridY,
                    0.88,
                    0.9,
                    0.89);
            }

            var coordinates = sites
                .SelectMany(site => site.Boundary)
                .Where(point => point.Count >= 2)
                .ToArray();
            if (coordinates.Length == 0)
            {
                Text(
                    "No boundary coordinates available",
                    x + 20,
                    top + 40,
                    10,
                    false,
                    (0.4, 0.44, 0.45));
                return;
            }

            var minX = coordinates.Min(point => point[0]);
            var maxX = coordinates.Max(point => point[0]);
            var minY = coordinates.Min(point => point[1]);
            var maxY = coordinates.Max(point => point[1]);
            var rangeX = Math.Max(maxX - minX, 0.000001);
            var rangeY = Math.Max(maxY - minY, 0.000001);
            var padding = 32d;
            var drawWidth = width - (padding * 2);
            var drawHeight = height - (padding * 2);
            var scale = Math.Min(
                drawWidth / rangeX,
                drawHeight / rangeY);
            var offsetX =
                x + ((width - (rangeX * scale)) / 2);
            var offsetY =
                top + ((height - (rangeY * scale)) / 2);

            foreach (var site in sites)
            {
                var points = site.Boundary
                    .Where(point => point.Count >= 2)
                    .Select(point =>
                        (
                            X: offsetX +
                                ((point[0] - minX) * scale),
                            Y: offsetY +
                                ((maxY - point[1]) * scale)))
                    .ToArray();
                Polygon(
                    points,
                    _accent);
            }

            FillRect(
                x + 14,
                top + 14,
                112,
                24,
                1,
                1,
                1);
            Text(
                $"WGS 84 | {sites.Count} site(s)",
                x + 23,
                top + 30,
                7,
                true,
                _accent);
        }

        public void FillRect(
            double x,
            double top,
            double width,
            double height,
            double r,
            double g,
            double b)
        {
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {6:0.##} re f\n",
                r,
                g,
                b,
                x,
                PageHeight - top - height,
                width,
                height);
        }

        public void FillRect(
            double x,
            double top,
            double width,
            double height,
            (double R, double G, double B) color)
        {
            FillRect(
                x,
                top,
                width,
                height,
                color.R,
                color.G,
                color.B);
        }

        public void StrokeRect(
            double x,
            double top,
            double width,
            double height,
            double r,
            double g,
            double b)
        {
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###} RG 0.6 w {3:0.##} {4:0.##} {5:0.##} {6:0.##} re S\n",
                r,
                g,
                b,
                x,
                PageHeight - top - height,
                width,
                height);
        }

        public void Rule(
            double x1,
            double top1,
            double x2,
            double top2,
            double r,
            double g,
            double b)
        {
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###} RG 0.6 w {3:0.##} {4:0.##} m {5:0.##} {6:0.##} l S\n",
                r,
                g,
                b,
                x1,
                PageHeight - top1,
                x2,
                PageHeight - top2);
        }

        private void FillCircle(
            double x,
            double top,
            double radius,
            (double R, double G, double B) color)
        {
            var y = PageHeight - top;
            var k = radius * 0.5522847498;
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.###} {1:0.###} {2:0.###} rg " +
                "{3:0.##} {4:0.##} m " +
                "{5:0.##} {6:0.##} {7:0.##} {8:0.##} {9:0.##} {10:0.##} c " +
                "{11:0.##} {12:0.##} {13:0.##} {14:0.##} {15:0.##} {16:0.##} c " +
                "{17:0.##} {18:0.##} {19:0.##} {20:0.##} {21:0.##} {22:0.##} c " +
                "{23:0.##} {24:0.##} {25:0.##} {26:0.##} {27:0.##} {28:0.##} c f\n",
                color.R,
                color.G,
                color.B,
                x + radius,
                y,
                x + radius,
                y + k,
                x + k,
                y + radius,
                x,
                y + radius,
                x - k,
                y + radius,
                x - radius,
                y + k,
                x - radius,
                y,
                x - radius,
                y - k,
                x - k,
                y - radius,
                x,
                y - radius,
                x + k,
                y - radius,
                x + radius,
                y - k,
                x + radius,
                y);
        }

        private void Polygon(
            IReadOnlyList<(double X, double Y)> points,
            (double R, double G, double B) color)
        {
            if (points.Count < 3)
            {
                return;
            }

            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "q {0:0.###} {1:0.###} {2:0.###} rg {3:0.###} {4:0.###} {5:0.###} RG 1.8 w ",
                0.72 + (color.R * 0.28),
                0.72 + (color.G * 0.28),
                0.72 + (color.B * 0.28),
                color.R,
                color.G,
                color.B);
            _commands.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.##} {1:0.##} m ",
                points[0].X,
                PageHeight - points[0].Y);
            foreach (var point in points.Skip(1))
            {
                _commands.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0:0.##} {1:0.##} l ",
                    point.X,
                    PageHeight - point.Y);
            }
            _commands.Append("h B Q\n");
        }

        private static IReadOnlyList<string> Wrap(
            string value,
            double width,
            double size)
        {
            var words = Sanitize(value)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = new StringBuilder();

            foreach (var word in words)
            {
                var candidate = current.Length == 0
                    ? word
                    : $"{current} {word}";
                if (EstimateWidth(candidate, size) <= width)
                {
                    current.Clear();
                    current.Append(candidate);
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (EstimateWidth(word, size) <= width)
                {
                    current.Append(word);
                    continue;
                }

                var maximumCharacters = Math.Max(
                    1,
                    (int)(width / (size * 0.52)));
                for (var index = 0;
                     index < word.Length;
                     index += maximumCharacters)
                {
                    lines.Add(
                        word.Substring(
                            index,
                            Math.Min(
                                maximumCharacters,
                                word.Length - index)));
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }

            return lines.Count == 0 ? [""] : lines;
        }

        private static string Truncate(
            string value,
            double width,
            double size)
        {
            var sanitized = Sanitize(value);
            if (EstimateWidth(sanitized, size) <= width)
            {
                return sanitized;
            }

            var maximumCharacters = Math.Max(
                1,
                (int)(width / (size * 0.52)) - 3);
            return sanitized[..Math.Min(
                maximumCharacters,
                sanitized.Length)] + "...";
        }

        private static double EstimateWidth(
            string value,
            double size)
        {
            return value.Length * size * 0.52;
        }

        private static string Escape(string value)
        {
            return Sanitize(value)
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)");
        }

        private static string Sanitize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value
                .Replace('\u2011', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('\u2018', '\'')
                .Replace('\u2019', '\'')
                .Replace('\u201C', '"')
                .Replace('\u201D', '"')
                .Replace('\u2026', '.'))
            {
                builder.Append(
                    character is >= ' ' and <= '~'
                        ? character
                        : '?');
            }
            return builder.ToString();
        }

        public static string Ascii(string value)
        {
            return Sanitize(value);
        }
    }
}
