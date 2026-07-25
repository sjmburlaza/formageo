using System.Data;
using System.Data.Common;
using FormaGeo.Application.Sites.Summaries;
using Microsoft.EntityFrameworkCore;

namespace FormaGeo.Infrastructure.Persistence.Queries;

public sealed class PostGisSiteSummaryReader
    : ISiteSummaryReader
{
    private const string SummarySql = """
        WITH site_geometry AS (
            SELECT
                boundary,
                ST_IsValid(boundary) AS is_valid,
                ST_IsValidReason(boundary) AS validity_reason
            FROM sites
            WHERE id = @siteId
        ),
        measured AS (
            SELECT
                ST_GeometryType(boundary) AS geometry_type,
                ST_SRID(boundary) AS srid,
                is_valid,
                validity_reason,
                ST_NumInteriorRings(boundary) + 1 AS ring_count,
                ST_NPoints(boundary)
                    - ST_NumInteriorRings(boundary)
                    - 1 AS vertex_count,
                CASE
                    WHEN is_valid
                    THEN ST_Area(boundary::geography, true)
                END AS area_square_metres,
                CASE
                    WHEN is_valid
                    THEN ST_Perimeter(boundary::geography, true)
                END AS perimeter_metres,
                CASE
                    WHEN is_valid
                    THEN ST_Centroid(boundary::geography, true)::geometry
                END AS centroid,
                Box3D(boundary) AS bounding_box
            FROM site_geometry
        )
        SELECT
            geometry_type,
            srid,
            is_valid,
            validity_reason,
            ring_count,
            vertex_count,
            area_square_metres,
            perimeter_metres,
            ST_X(centroid) AS centroid_longitude,
            ST_Y(centroid) AS centroid_latitude,
            ST_XMin(bounding_box) AS bounding_box_west,
            ST_YMin(bounding_box) AS bounding_box_south,
            ST_XMax(bounding_box) AS bounding_box_east,
            ST_YMax(bounding_box) AS bounding_box_north
        FROM measured;
        """;

    private readonly FormaGeoDbContext _dbContext;

    public PostGisSiteSummaryReader(
        FormaGeoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SiteSpatialSummary?> GetAsync(
        Guid siteId,
        CancellationToken cancellationToken = default)
    {
        var connection =
            _dbContext.Database.GetDbConnection();
        var shouldClose =
            connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await _dbContext.Database.OpenConnectionAsync(
                cancellationToken);
        }

        try
        {
            await using var command =
                connection.CreateCommand();
            command.CommandText = SummarySql;

            var siteIdParameter =
                command.CreateParameter();
            siteIdParameter.ParameterName = "siteId";
            siteIdParameter.Value = siteId;
            command.Parameters.Add(siteIdParameter);

            await using var reader =
                await command.ExecuteReaderAsync(
                    CommandBehavior.SingleRow,
                    cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new SiteSpatialSummary(
                reader.GetString(
                    reader.GetOrdinal("geometry_type")),
                reader.GetInt32(
                    reader.GetOrdinal("srid")),
                reader.GetBoolean(
                    reader.GetOrdinal("is_valid")),
                reader.GetString(
                    reader.GetOrdinal("validity_reason")),
                reader.GetInt32(
                    reader.GetOrdinal("ring_count")),
                reader.GetInt32(
                    reader.GetOrdinal("vertex_count")),
                GetNullableDouble(
                    reader,
                    "area_square_metres"),
                GetNullableDouble(
                    reader,
                    "perimeter_metres"),
                GetNullableDouble(
                    reader,
                    "centroid_longitude"),
                GetNullableDouble(
                    reader,
                    "centroid_latitude"),
                reader.GetDouble(
                    reader.GetOrdinal("bounding_box_west")),
                reader.GetDouble(
                    reader.GetOrdinal("bounding_box_south")),
                reader.GetDouble(
                    reader.GetOrdinal("bounding_box_east")),
                reader.GetDouble(
                    reader.GetOrdinal("bounding_box_north")));
        }
        finally
        {
            if (shouldClose)
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    private static double? GetNullableDouble(
        DbDataReader reader,
        string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDouble(ordinal);
    }
}
