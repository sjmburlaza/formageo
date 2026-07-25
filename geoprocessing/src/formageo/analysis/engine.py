"""Versioned, dataset-backed spatial analyses for FormaGeo sites."""

from __future__ import annotations

import json
import math
import os
from pathlib import Path
from typing import Any

from pyproj import Transformer
from shapely.geometry import LineString, mapping, shape
from shapely.ops import nearest_points, transform, unary_union

ANALYSIS_VERSION = "1.0.0"
SUPPORTED_ANALYSES = {
    "SiteGeometry",
    "HazardExposure",
    "Zoning",
    "Accessibility",
    "NearbyFacilities",
    "Suitability",
}


class AnalysisValidationError(ValueError):
    """Raised when an analysis request contains invalid user input."""

    def __init__(self, field: str, message: str) -> None:
        super().__init__(message)
        self.field = field


class DatasetLoadError(RuntimeError):
    """Raised when a required contextual dataset cannot be loaded."""


class DatasetRepository:
    """Loads GeoJSON feature collections from a configurable directory."""

    def __init__(self, dataset_directory: str | Path | None = None) -> None:
        configured_directory = (
            Path(dataset_directory)
            if dataset_directory is not None
            else Path(
                os.environ.get(
                    "FORMAGEO_DATASET_DIR",
                    _default_dataset_directory(),
                )
            )
        )
        self.dataset_directory = configured_directory.resolve()
        self._cache: dict[str, tuple[int, list[dict[str, Any]]]] = {}

    def load(self, filename: str) -> list[dict[str, Any]]:
        path = self.dataset_directory / filename

        try:
            modified_time = path.stat().st_mtime_ns
        except FileNotFoundError as exception:
            raise DatasetLoadError(
                f"Required dataset '{filename}' was not found in "
                f"'{self.dataset_directory}'."
            ) from exception

        cached = self._cache.get(filename)
        if cached is not None and cached[0] == modified_time:
            return cached[1]

        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exception:
            raise DatasetLoadError(
                f"Required dataset '{filename}' could not be read: {exception}."
            ) from exception

        if document.get("type") != "FeatureCollection":
            raise DatasetLoadError(
                f"Required dataset '{filename}' is not a GeoJSON FeatureCollection."
            )

        features: list[dict[str, Any]] = []
        for index, feature in enumerate(document.get("features", [])):
            geometry_document = feature.get("geometry")
            if not geometry_document:
                continue

            try:
                geometry = shape(geometry_document)
            except (TypeError, ValueError) as exception:
                raise DatasetLoadError(
                    f"Feature {index} in '{filename}' has invalid geometry."
                ) from exception

            if geometry.is_empty or not geometry.is_valid:
                raise DatasetLoadError(
                    f"Feature {index} in '{filename}' has invalid geometry."
                )

            features.append(
                {
                    "properties": dict(feature.get("properties") or {}),
                    "geometry": geometry,
                }
            )

        self._cache[filename] = (modified_time, features)
        return features


def run_analysis(
    analysis_type: str,
    site_geometry: dict[str, Any],
    input_parameters: dict[str, Any] | None = None,
    *,
    datasets: DatasetRepository | None = None,
) -> dict[str, Any]:
    """Validate and execute one supported analysis."""

    if analysis_type not in SUPPORTED_ANALYSES:
        raise AnalysisValidationError(
            "analysisType",
            f"Analysis type '{analysis_type}' is not supported.",
        )

    site = _validated_site(site_geometry)
    parameters = input_parameters or {}
    if not isinstance(parameters, dict):
        raise AnalysisValidationError(
            "inputParameters",
            "Analysis input parameters must be an object.",
        )

    repository = datasets or DatasetRepository()
    operations = {
        "SiteGeometry": _site_geometry,
        "HazardExposure": _hazard_exposure,
        "Zoning": _zoning,
        "Accessibility": _accessibility,
        "NearbyFacilities": _nearby_facilities,
        "Suitability": _suitability,
    }
    return operations[analysis_type](site, parameters, repository)


def _site_geometry(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, set())
    site_metric, _, inverse = _metric_geometry(site)
    centroid = site.centroid
    minimum_x, minimum_y, maximum_x, maximum_y = site.bounds
    ring_count = 1 + len(getattr(site, "interiors", []))

    return {
        "summary": "Site geometry measurements completed.",
        "metrics": {
            "areaSquareMetres": _rounded(site_metric.area, 2),
            "areaHectares": _rounded(site_metric.area / 10_000, 4),
            "perimeterMetres": _rounded(site_metric.length, 2),
            "vertexCount": _vertex_count(site),
            "ringCount": ring_count,
        },
        "centroid": {
            "longitude": _rounded(centroid.x, 7),
            "latitude": _rounded(centroid.y, 7),
        },
        "boundingBox": {
            "west": minimum_x,
            "south": minimum_y,
            "east": maximum_x,
            "north": maximum_y,
        },
        "coordinateSystem": "EPSG:4326",
        "measurementMethod": "Local UTM projection",
        "resultGeometry": _feature(centroid, {"role": "siteCentroid"}),
    }


def _hazard_exposure(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"minimumOverlapPercent"})
    minimum_overlap = _number(
        parameters,
        "minimumOverlapPercent",
        default=0,
        minimum=0,
        maximum=100,
    )
    site_metric, forward, _ = _metric_geometry(site)
    exposures: list[dict[str, Any]] = []
    intersections = []

    for feature in datasets.load("flood-susceptibility.geojson"):
        intersection = site.intersection(feature["geometry"])
        if intersection.is_empty:
            continue

        area = transform(forward, intersection).area
        overlap_percent = _percent(area, site_metric.area)
        if overlap_percent < minimum_overlap:
            continue

        intersections.append(intersection)
        exposures.append(
            {
                "name": feature["properties"].get("name", "Mapped hazard area"),
                "susceptibility": feature["properties"].get("susceptibility"),
                "overlapSquareMetres": _rounded(area, 2),
                "overlapPercent": _rounded(overlap_percent, 2),
            }
        )

    affected = unary_union(intersections) if intersections else None
    affected_area = (
        transform(forward, affected).area
        if affected is not None and not affected.is_empty
        else 0
    )

    return {
        "summary": (
            f"{_rounded(_percent(affected_area, site_metric.area), 2)}% of the "
            "site overlaps mapped hazard areas."
            if affected_area
            else "No mapped hazard overlap was found."
        ),
        "metrics": {
            "affectedAreaSquareMetres": _rounded(affected_area, 2),
            "affectedPercent": _rounded(
                _percent(affected_area, site_metric.area), 2
            ),
            "exposureCount": len(exposures),
        },
        "exposures": exposures,
        "dataNotice": "Demonstration hazard data; not suitable for risk decisions.",
        "resultGeometry": (
            _feature(affected, {"role": "hazardExposure"})
            if affected is not None and not affected.is_empty
            else None
        ),
    }


def _zoning(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"includeUnzoned"})
    include_unzoned = _boolean(parameters, "includeUnzoned", default=True)
    site_metric, forward, _ = _metric_geometry(site)
    overlaps: list[dict[str, Any]] = []
    covered_geometries = []

    for feature in datasets.load("land-use-zones.geojson"):
        intersection = site.intersection(feature["geometry"])
        if intersection.is_empty:
            continue

        area = transform(forward, intersection).area
        covered_geometries.append(intersection)
        overlaps.append(
            {
                "name": feature["properties"].get("name", "Mapped zone"),
                "landUse": feature["properties"].get("landUse", "Unclassified"),
                "areaSquareMetres": _rounded(area, 2),
                "sitePercent": _rounded(_percent(area, site_metric.area), 2),
            }
        )

    covered = unary_union(covered_geometries) if covered_geometries else None
    unzoned = (
        site.difference(covered)
        if covered is not None and not covered.is_empty
        else site
    )
    unzoned_area = transform(forward, unzoned).area if include_unzoned else 0

    return {
        "summary": (
            f"The site overlaps {len(overlaps)} mapped "
            f"{'zone' if len(overlaps) == 1 else 'zones'}."
        ),
        "metrics": {
            "zoneCount": len(overlaps),
            "unzonedAreaSquareMetres": (
                _rounded(unzoned_area, 2) if include_unzoned else None
            ),
            "unzonedPercent": (
                _rounded(_percent(unzoned_area, site_metric.area), 2)
                if include_unzoned
                else None
            ),
        },
        "zones": overlaps,
        "dataNotice": "Illustrative zoning only.",
        "resultGeometry": (
            _feature(covered, {"role": "zoningOverlap"})
            if covered is not None and not covered.is_empty
            else None
        ),
    }


def _accessibility(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"maximumDistanceMetres"})
    maximum_distance = _number(
        parameters,
        "maximumDistanceMetres",
        default=5000,
        minimum=100,
        maximum=100_000,
    )
    site_metric, forward, inverse = _metric_geometry(site)
    corridors = datasets.load("transport-corridors.geojson")
    if not corridors:
        raise DatasetLoadError("The transport corridor dataset contains no features.")

    corridor_metric = unary_union(
        [transform(forward, feature["geometry"]) for feature in corridors]
    )
    site_point, corridor_point = nearest_points(site_metric, corridor_metric)
    distance = site_point.distance(corridor_point)
    nearest_feature = min(
        corridors,
        key=lambda feature: site_metric.distance(
            transform(forward, feature["geometry"])
        ),
    )
    connector = transform(
        inverse,
        LineString([site_point.coords[0], corridor_point.coords[0]]),
    )

    return {
        "summary": (
            f"The nearest mapped transport corridor is "
            f"{_rounded(distance, 0):.0f} metres from the site."
        ),
        "metrics": {
            "nearestDistanceMetres": _rounded(distance, 2),
            "withinThreshold": distance <= maximum_distance,
            "distanceThresholdMetres": maximum_distance,
        },
        "nearestCorridor": nearest_feature["properties"],
        "resultGeometry": _feature(
            connector,
            {
                "role": "nearestTransportConnection",
                "distanceMetres": _rounded(distance, 2),
            },
        ),
    }


def _nearby_facilities(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"radiusMetres", "facilityTypes"})
    radius = _number(
        parameters,
        "radiusMetres",
        default=2000,
        minimum=100,
        maximum=50_000,
    )
    facility_types = _facility_types(parameters.get("facilityTypes", ""))
    site_metric, forward, _ = _metric_geometry(site)
    facilities: list[dict[str, Any]] = []
    result_features: list[dict[str, Any]] = []

    for feature in datasets.load("community-facilities.geojson"):
        properties = feature["properties"]
        facility_type = str(properties.get("facilityType", "Other"))
        if facility_types and facility_type.casefold() not in facility_types:
            continue

        distance = site_metric.distance(transform(forward, feature["geometry"]))
        if distance > radius:
            continue

        item = {
            **properties,
            "distanceMetres": _rounded(distance, 2),
        }
        facilities.append(item)
        result_features.append(_feature(feature["geometry"], item))

    facilities.sort(key=lambda item: item["distanceMetres"])
    result_features.sort(
        key=lambda feature: feature["properties"]["distanceMetres"]
    )

    return {
        "summary": (
            f"Found {len(facilities)} mapped "
            f"{'facility' if len(facilities) == 1 else 'facilities'} "
            f"within {_rounded(radius, 0):.0f} metres."
        ),
        "metrics": {
            "facilityCount": len(facilities),
            "searchRadiusMetres": radius,
            "nearestDistanceMetres": (
                facilities[0]["distanceMetres"] if facilities else None
            ),
        },
        "facilities": facilities,
        "resultGeometry": {
            "type": "FeatureCollection",
            "features": result_features,
        },
    }


def _suitability(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    allowed = {
        "hazardWeight",
        "accessWeight",
        "facilitiesWeight",
        "greenSpaceWeight",
    }
    _reject_unknown(parameters, allowed)
    weights = {
        name: _number(
            parameters,
            name,
            default=default,
            minimum=0,
            maximum=100,
        )
        for name, default in {
            "hazardWeight": 35,
            "accessWeight": 25,
            "facilitiesWeight": 20,
            "greenSpaceWeight": 20,
        }.items()
    }
    weight_total = sum(weights.values())
    if weight_total <= 0:
        raise AnalysisValidationError(
            "inputParameters",
            "At least one suitability weight must be greater than zero.",
        )

    site_metric, forward, _ = _metric_geometry(site)
    hazard_area = _intersection_area(
        site,
        datasets.load("flood-susceptibility.geojson"),
        forward,
    )
    green_area = _intersection_area(
        site,
        datasets.load("green-areas.geojson"),
        forward,
    )

    corridors = datasets.load("transport-corridors.geojson")
    corridor_distance = (
        site_metric.distance(
            unary_union(
                [
                    transform(forward, feature["geometry"])
                    for feature in corridors
                ]
            )
        )
        if corridors
        else 5000
    )
    facilities = datasets.load("community-facilities.geojson")
    facility_count = sum(
        1
        for feature in facilities
        if site_metric.distance(transform(forward, feature["geometry"])) <= 2000
    )

    component_scores = {
        "hazard": max(
            0,
            100 - _percent(hazard_area, site_metric.area),
        ),
        "access": max(0, 100 - min(100, corridor_distance / 5000 * 100)),
        "facilities": min(100, facility_count / 3 * 100),
        "greenSpace": min(
            100,
            _percent(green_area, site_metric.area),
        ),
    }
    weighted_score = (
        component_scores["hazard"] * weights["hazardWeight"]
        + component_scores["access"] * weights["accessWeight"]
        + component_scores["facilities"] * weights["facilitiesWeight"]
        + component_scores["greenSpace"] * weights["greenSpaceWeight"]
    ) / weight_total
    classification = (
        "Highly suitable"
        if weighted_score >= 75
        else "Moderately suitable"
        if weighted_score >= 50
        else "Lower suitability"
    )

    return {
        "summary": (
            f"The site scored {_rounded(weighted_score, 1)} out of 100 "
            f"({classification.lower()})."
        ),
        "metrics": {
            "score": _rounded(weighted_score, 1),
            "classification": classification,
            "hazardExposurePercent": _rounded(
                _percent(hazard_area, site_metric.area), 2
            ),
            "nearestTransportMetres": _rounded(corridor_distance, 2),
            "nearbyFacilityCount": facility_count,
            "greenSpaceOverlapPercent": _rounded(
                _percent(green_area, site_metric.area), 2
            ),
        },
        "componentScores": {
            key: _rounded(value, 1) for key, value in component_scores.items()
        },
        "weights": weights,
        "methodology": (
            "Weighted score using analysis version "
            f"{ANALYSIS_VERSION} and demonstration contextual datasets."
        ),
        "resultGeometry": None,
    }


def _validated_site(document: dict[str, Any]) -> Any:
    if not isinstance(document, dict):
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry must be a GeoJSON object.",
        )

    try:
        geometry = shape(document)
    except (TypeError, ValueError, KeyError) as exception:
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry is not valid GeoJSON.",
        ) from exception

    if geometry.geom_type != "Polygon":
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry must be a Polygon.",
        )
    if geometry.is_empty:
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry cannot be empty.",
        )
    if not geometry.is_valid:
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry is topologically invalid.",
        )

    minimum_x, minimum_y, maximum_x, maximum_y = geometry.bounds
    if (
        minimum_x < -180
        or maximum_x > 180
        or minimum_y < -90
        or maximum_y > 90
    ):
        raise AnalysisValidationError(
            "siteGeometry",
            "Site geometry coordinates must use WGS 84 longitude and latitude.",
        )
    return geometry


def _default_dataset_directory() -> str:
    repository_root = Path(__file__).resolve().parents[4]
    return str(repository_root / "backend/src/FormaGeo.Api/wwwroot/layers")


def _metric_geometry(geometry: Any) -> tuple[Any, Any, Any]:
    centroid = geometry.centroid
    zone = max(1, min(60, math.floor((centroid.x + 180) / 6) + 1))
    epsg = (32600 if centroid.y >= 0 else 32700) + zone
    forward = Transformer.from_crs(
        "EPSG:4326",
        f"EPSG:{epsg}",
        always_xy=True,
    ).transform
    inverse = Transformer.from_crs(
        f"EPSG:{epsg}",
        "EPSG:4326",
        always_xy=True,
    ).transform
    return transform(forward, geometry), forward, inverse


def _intersection_area(
    site: Any,
    features: list[dict[str, Any]],
    forward: Any,
) -> float:
    intersections = [
        site.intersection(feature["geometry"])
        for feature in features
        if site.intersects(feature["geometry"])
    ]
    if not intersections:
        return 0
    return transform(forward, unary_union(intersections)).area


def _feature(geometry: Any, properties: dict[str, Any]) -> dict[str, Any]:
    return {
        "type": "Feature",
        "properties": properties,
        "geometry": mapping(geometry),
    }


def _number(
    parameters: dict[str, Any],
    name: str,
    *,
    default: float,
    minimum: float,
    maximum: float,
) -> float:
    value = parameters.get(name, default)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise AnalysisValidationError(
            f"inputParameters.{name}",
            f"Parameter '{name}' must be a number.",
        )
    value = float(value)
    if not math.isfinite(value) or value < minimum or value > maximum:
        raise AnalysisValidationError(
            f"inputParameters.{name}",
            f"Parameter '{name}' must be between {minimum} and {maximum}.",
        )
    return value


def _boolean(
    parameters: dict[str, Any],
    name: str,
    *,
    default: bool,
) -> bool:
    value = parameters.get(name, default)
    if not isinstance(value, bool):
        raise AnalysisValidationError(
            f"inputParameters.{name}",
            f"Parameter '{name}' must be true or false.",
        )
    return value


def _facility_types(value: Any) -> set[str]:
    if value in ("", None):
        return set()
    if isinstance(value, str):
        return {
            item.strip().casefold()
            for item in value.split(",")
            if item.strip()
        }
    if isinstance(value, list) and all(isinstance(item, str) for item in value):
        return {item.strip().casefold() for item in value if item.strip()}
    raise AnalysisValidationError(
        "inputParameters.facilityTypes",
        "Parameter 'facilityTypes' must be comma-separated text.",
    )


def _reject_unknown(
    parameters: dict[str, Any],
    allowed: set[str],
) -> None:
    unknown = sorted(set(parameters) - allowed)
    if unknown:
        raise AnalysisValidationError(
            f"inputParameters.{unknown[0]}",
            f"Parameter '{unknown[0]}' is not supported for this analysis.",
        )


def _vertex_count(geometry: Any) -> int:
    return len(geometry.exterior.coords) + sum(
        len(interior.coords) for interior in geometry.interiors
    )


def _percent(value: float, total: float) -> float:
    return value / total * 100 if total > 0 else 0


def _rounded(value: float, digits: int) -> float:
    return round(float(value), digits)
