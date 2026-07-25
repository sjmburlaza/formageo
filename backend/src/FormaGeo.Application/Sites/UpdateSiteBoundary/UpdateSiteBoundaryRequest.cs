using FormaGeo.Application.Sites.Contracts;

namespace FormaGeo.Application.Sites.UpdateSiteBoundary;

public sealed record UpdateSiteBoundaryRequest(
    GeoJsonPolygonRequest? Boundary);
