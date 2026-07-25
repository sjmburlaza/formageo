using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.CreateSite;

public sealed record CreateSiteRequest(
    string? Name,
    GeoJsonPolygonRequest? Boundary);
