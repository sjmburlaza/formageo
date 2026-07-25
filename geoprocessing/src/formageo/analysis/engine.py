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

ANALYSIS_VERSION = "2.0.0"
SUPPORTED_ANALYSES = {
    "SiteGeometry",
    "HazardExposure",
    "Zoning",
    "Terrain",
    "Accessibility",
    "NearbyFacilities",
    "Suitability",
}

DATA_SOURCES = {
    "flood": {
        "id": "fg-demo-flood",
        "dataset": "Flood susceptibility demonstration areas",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/flood-susceptibility.geojson",
        "license": "CC0 1.0",
    },
    "landslide": {
        "id": "fg-demo-landslide",
        "dataset": "Landslide susceptibility demonstration areas",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/landslide-susceptibility.geojson",
        "license": "CC0 1.0",
    },
    "stormSurge": {
        "id": "fg-demo-storm-surge",
        "dataset": "Storm-surge demonstration zones",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/storm-surge-zones.geojson",
        "license": "CC0 1.0",
    },
    "faultLines": {
        "id": "fg-demo-fault-lines",
        "dataset": "Fault-line demonstration traces",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/fault-lines.geojson",
        "license": "CC0 1.0",
    },
    "protectedAreas": {
        "id": "fg-demo-protected-areas",
        "dataset": "Protected-area demonstration boundaries",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/protected-areas.geojson",
        "license": "CC0 1.0",
    },
    "zoning": {
        "id": "fg-demo-zoning",
        "dataset": "Land-use zoning demonstration areas",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/land-use-zones.geojson",
        "license": "CC0 1.0",
    },
    "jurisdictions": {
        "id": "fg-demo-jurisdictions",
        "dataset": "Planning district demonstration boundaries",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/planning-districts.geojson",
        "license": "CC0 1.0",
    },
    "restrictions": {
        "id": "fg-demo-restrictions",
        "dataset": "Development restriction demonstration areas",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/development-restrictions.geojson",
        "license": "CC0 1.0",
    },
    "terrain": {
        "id": "fg-demo-terrain",
        "dataset": "Terrain summary demonstration grid",
        "organization": "FormaGeo",
        "dataVersion": "2026.07",
        "publishedDate": "2026-07-26",
        "coordinateSystem": "EPSG:4326",
        "sourceUrl": "/layers/terrain-summary-grid.geojson",
        "license": "CC0 1.0",
    },
}

DEMONSTRATION_LIMITATION = (
    "This is synthetic demonstration data for workflow validation and must not "
    "be used for permitting, engineering design, emergency planning, or risk decisions."
)


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
        "Terrain": _terrain,
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
    _reject_unknown(
        parameters,
        {"minimumOverlapPercent", "faultSearchDistanceMetres"},
    )
    minimum_overlap = _number(
        parameters,
        "minimumOverlapPercent",
        default=0,
        minimum=0,
        maximum=100,
    )
    fault_search_distance = _number(
        parameters,
        "faultSearchDistanceMetres",
        default=5000,
        minimum=100,
        maximum=100_000,
    )
    site_metric, forward, _ = _metric_geometry(site)
    evidence: list[dict[str, Any]] = []
    affected_geometries = []

    hazard_datasets = [
        (
            "flood-zone",
            "Flood-zone intersection",
            "flood-susceptibility.geojson",
            "flood",
            "susceptibility",
        ),
        (
            "landslide",
            "Landslide exposure",
            "landslide-susceptibility.geojson",
            "landslide",
            "susceptibility",
        ),
        (
            "storm-surge",
            "Storm-surge exposure",
            "storm-surge-zones.geojson",
            "stormSurge",
            "exposureLevel",
        ),
        (
            "protected-area",
            "Protected-area overlap",
            "protected-areas.geojson",
            "protectedAreas",
            "designation",
        ),
    ]

    for result_id, name, filename, source_key, classification_property in (
        hazard_datasets
    ):
        result, intersection = _polygon_evidence(
            site,
            site_metric.area,
            forward,
            datasets.load(filename),
            result_id=result_id,
            name=name,
            category="Hazards",
            source_key=source_key,
            classification_property=classification_property,
            minimum_overlap_percent=minimum_overlap,
            limitation=(
                f"{DEMONSTRATION_LIMITATION} Mapped boundaries are generalized "
                "and do not model event probability, depth, velocity, or local mitigation."
            ),
        )
        evidence.append(result)
        if intersection is not None and not intersection.is_empty:
            affected_geometries.append(intersection)

    fault_features = datasets.load("fault-lines.geojson")
    if not fault_features:
        raise DatasetLoadError("The fault-line dataset contains no features.")
    fault_metric = unary_union(
        [transform(forward, feature["geometry"]) for feature in fault_features]
    )
    site_point, fault_point = nearest_points(site_metric, fault_metric)
    fault_distance = site_point.distance(fault_point)
    nearest_fault = min(
        fault_features,
        key=lambda feature: site_metric.distance(
            transform(forward, feature["geometry"])
        ),
    )
    fault_geometry_features = [
        _feature(
            nearest_fault["geometry"],
            {
                **nearest_fault["properties"],
                "role": "nearestFaultTrace",
            },
        )
    ]
    if fault_distance > 0:
        inverse = _metric_geometry(site)[2]
        connector = transform(
            inverse,
            LineString([site_point.coords[0], fault_point.coords[0]]),
        )
        fault_geometry_features.append(
            _feature(
                connector,
                {
                    "role": "faultDistance",
                    "distanceMetres": _rounded(fault_distance, 2),
                },
            )
        )

    fault_severity = (
        "High"
        if fault_distance <= 500
        else "Moderate"
        if fault_distance <= 2000
        else "Low"
        if fault_distance <= fault_search_distance
        else "None"
    )
    fault_classification = (
        "Intersects mapped trace"
        if fault_distance == 0
        else f"{_rounded(fault_distance / 1000, 2)} km from nearest mapped trace"
    )
    fault_source = _source("faultLines")
    evidence.append(
        {
            "id": "fault-line-proximity",
            "name": "Fault-line proximity",
            "category": "Hazards",
            "summary": (
                f"The nearest mapped fault trace is {_rounded(fault_distance, 0):.0f} "
                f"metres from the site ({fault_severity.lower()} proximity exposure)."
            ),
            "severity": fault_severity,
            "classification": fault_classification,
            "intersectionAreaSquareMetres": 0,
            "sitePercent": 0,
            "resultGeometry": {
                "type": "FeatureCollection",
                "features": fault_geometry_features,
            },
            "source": fault_source,
            "dataVersion": fault_source["dataVersion"],
            "methodology": (
                "Shortest planar distance from the site boundary to the nearest "
                "mapped fault trace, measured in the site's local UTM zone."
            ),
            "limitations": [
                DEMONSTRATION_LIMITATION,
                "Distance to a generalized mapped trace is not a site-specific fault investigation.",
            ],
            "details": {
                "distanceMetres": _rounded(fault_distance, 2),
                "searchDistanceMetres": fault_search_distance,
                "nearestFeature": nearest_fault["properties"],
            },
        }
    )

    affected = (
        unary_union(affected_geometries)
        if affected_geometries
        else None
    )
    affected_area = (
        transform(forward, affected).area
        if affected is not None and not affected.is_empty
        else 0
    )
    highest_severity = _highest_severity(evidence)
    result_geometry = _combined_result_geometry(evidence)

    return {
        "summary": (
            f"{len(evidence)} hazard and environmental checks completed. "
            f"{_rounded(_percent(affected_area, site_metric.area), 2)}% of the "
            f"site intersects at least one mapped polygon exposure."
        ),
        "category": "Hazards",
        "severity": highest_severity,
        "classification": f"{highest_severity} overall mapped exposure",
        "intersectionAreaSquareMetres": _rounded(affected_area, 2),
        "sitePercent": _rounded(_percent(affected_area, site_metric.area), 2),
        "metrics": {
            "affectedAreaSquareMetres": _rounded(affected_area, 2),
            "affectedPercent": _rounded(
                _percent(affected_area, site_metric.area), 2
            ),
            "checkCount": len(evidence),
            "mappedExposureCount": sum(
                1 for result in evidence if result["severity"] != "None"
            ),
            "highestSeverity": highest_severity,
        },
        "results": evidence,
        "exposures": evidence,
        "methodology": (
            "Polygon exposures are clipped to the site and measured using a local "
            "UTM projection. Fault proximity uses the shortest boundary-to-trace distance."
        ),
        "limitations": [DEMONSTRATION_LIMITATION],
        "sourceMetadata": _unique_sources(evidence),
        "dataNotice": DEMONSTRATION_LIMITATION,
        "resultGeometry": result_geometry,
    }


def _zoning(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"includeUnzoned"})
    include_unzoned = _boolean(parameters, "includeUnzoned", default=True)
    site_metric, forward, _ = _metric_geometry(site)
    zoning_features = datasets.load("land-use-zones.geojson")
    zoning_result, covered = _polygon_evidence(
        site,
        site_metric.area,
        forward,
        zoning_features,
        result_id="zoning-classification",
        name="Zoning and land-use classification",
        category="Planning",
        source_key="zoning",
        classification_property="landUse",
        limitation=(
            f"{DEMONSTRATION_LIMITATION} Zone boundaries and labels may be "
            "generalized and do not replace the legally adopted zoning map."
        ),
    )
    jurisdiction_result, jurisdiction_geometry = _polygon_evidence(
        site,
        site_metric.area,
        forward,
        datasets.load("planning-districts.geojson"),
        result_id="administrative-jurisdiction",
        name="Administrative jurisdiction",
        category="Planning",
        source_key="jurisdictions",
        classification_property="name",
        severity_label="Informational",
        limitation=(
            f"{DEMONSTRATION_LIMITATION} Administrative boundaries may not "
            "reflect cadastral, barangay, municipal, or agency-specific jurisdiction."
        ),
    )
    restriction_result, restriction_geometry = _polygon_evidence(
        site,
        site_metric.area,
        forward,
        datasets.load("development-restrictions.geojson"),
        result_id="development-restrictions",
        name="Development restrictions",
        category="Planning",
        source_key="restrictions",
        classification_property="restriction",
        limitation=(
            f"{DEMONSTRATION_LIMITATION} Only mapped demonstration restrictions "
            "are checked; easements, title conditions, and agency clearances are excluded."
        ),
    )
    planning_results = [
        zoning_result,
        jurisdiction_result,
        restriction_result,
    ]
    unzoned = (
        site.difference(covered)
        if covered is not None and not covered.is_empty
        else site
    )
    unzoned_area = transform(forward, unzoned).area if include_unzoned else 0
    overlaps = zoning_result["details"]["intersections"]
    planning_geometries = [
        geometry
        for geometry in [covered, jurisdiction_geometry, restriction_geometry]
        if geometry is not None and not geometry.is_empty
    ]
    planning_coverage = (
        unary_union(planning_geometries)
        if planning_geometries
        else None
    )
    planning_area = (
        transform(forward, site.intersection(planning_coverage)).area
        if planning_coverage is not None
        else 0
    )
    primary_classification = zoning_result["classification"]

    return {
        "summary": (
            f"The site overlaps {len(overlaps)} mapped "
            f"{'zone' if len(overlaps) == 1 else 'zones'} and "
            f"{len(jurisdiction_result['details']['intersections'])} planning "
            f"{'jurisdiction' if len(jurisdiction_result['details']['intersections']) == 1 else 'jurisdictions'}."
        ),
        "category": "Planning",
        "severity": restriction_result["severity"],
        "classification": primary_classification,
        "intersectionAreaSquareMetres": _rounded(planning_area, 2),
        "sitePercent": _rounded(_percent(planning_area, site_metric.area), 2),
        "metrics": {
            "zoneCount": len(overlaps),
            "primaryLandUse": primary_classification,
            "jurisdictionCount": len(
                jurisdiction_result["details"]["intersections"]
            ),
            "restrictedAreaSquareMetres": restriction_result[
                "intersectionAreaSquareMetres"
            ],
            "restrictedPercent": restriction_result["sitePercent"],
            "unzonedAreaSquareMetres": (
                _rounded(unzoned_area, 2) if include_unzoned else None
            ),
            "unzonedPercent": (
                _rounded(_percent(unzoned_area, site_metric.area), 2)
                if include_unzoned
                else None
            ),
        },
        "results": planning_results,
        "zones": overlaps,
        "methodology": (
            "Each planning layer is clipped to the site in WGS 84, then measured "
            "in the site's local UTM zone. The dominant class is the class with "
            "the greatest intersected area."
        ),
        "limitations": [DEMONSTRATION_LIMITATION],
        "sourceMetadata": _unique_sources(planning_results),
        "dataNotice": DEMONSTRATION_LIMITATION,
        "resultGeometry": _combined_result_geometry(planning_results),
    }


def _terrain(
    site: Any,
    parameters: dict[str, Any],
    datasets: DatasetRepository,
) -> dict[str, Any]:
    _reject_unknown(parameters, {"steepSlopeThresholdDegrees"})
    steep_threshold = _number(
        parameters,
        "steepSlopeThresholdDegrees",
        default=15,
        minimum=1,
        maximum=60,
    )
    site_metric, forward, _ = _metric_geometry(site)
    covered_geometries = []
    cell_results = []
    total_weighted_elevation = 0.0
    total_weighted_slope = 0.0
    estimated_steep_area = 0.0
    minimum_elevations = []
    maximum_elevations = []

    for feature in datasets.load("terrain-summary-grid.geojson"):
        intersection = site.intersection(feature["geometry"])
        if intersection.is_empty:
            continue

        properties = feature["properties"]
        area = transform(forward, intersection).area
        covered_geometries.append(intersection)
        mean_elevation = _required_dataset_number(
            properties,
            "meanElevationMetres",
            "terrain-summary-grid.geojson",
        )
        mean_slope = _required_dataset_number(
            properties,
            "meanSlopeDegrees",
            "terrain-summary-grid.geojson",
        )
        source_steep_percent = _required_dataset_number(
            properties,
            "steepAreaPercent",
            "terrain-summary-grid.geojson",
        )
        minimum_elevations.append(
            _required_dataset_number(
                properties,
                "minimumElevationMetres",
                "terrain-summary-grid.geojson",
            )
        )
        maximum_elevations.append(
            _required_dataset_number(
                properties,
                "maximumElevationMetres",
                "terrain-summary-grid.geojson",
            )
        )
        total_weighted_elevation += mean_elevation * area
        total_weighted_slope += mean_slope * area
        estimated_steep_area += area * source_steep_percent / 100
        cell_results.append(
            _feature(
                intersection,
                {
                    **properties,
                    "role": "terrainEvidence",
                    "intersectionAreaSquareMetres": _rounded(area, 2),
                    "sitePercent": _rounded(_percent(area, site_metric.area), 2),
                },
            )
        )

    covered = unary_union(covered_geometries) if covered_geometries else None
    covered_area = (
        transform(forward, covered).area
        if covered is not None and not covered.is_empty
        else 0
    )
    if covered_area <= 0:
        raise DatasetLoadError(
            "The terrain dataset does not cover the submitted site."
        )

    average_elevation = total_weighted_elevation / covered_area
    average_slope = total_weighted_slope / covered_area
    steep_percent = _percent(estimated_steep_area, site_metric.area)
    classification = (
        "Steep terrain"
        if average_slope >= steep_threshold
        else "Moderately sloping terrain"
        if average_slope >= 8
        else "Gently sloping terrain"
        if average_slope >= 3
        else "Near-level terrain"
    )
    severity = (
        "High"
        if steep_percent >= 30
        else "Moderate"
        if steep_percent >= 10
        else "Low"
    )
    source = _source("terrain")
    result_geometry = {
        "type": "FeatureCollection",
        "features": cell_results,
    }
    evidence = {
        "id": "terrain-profile",
        "name": "Terrain profile",
        "category": "Terrain",
        "summary": (
            f"Average elevation is {_rounded(average_elevation, 1)} m and "
            f"average slope is {_rounded(average_slope, 1)}°; "
            f"{_rounded(steep_percent, 1)}% of the site is estimated steep."
        ),
        "severity": severity,
        "classification": classification,
        "intersectionAreaSquareMetres": _rounded(covered_area, 2),
        "sitePercent": _rounded(_percent(covered_area, site_metric.area), 2),
        "resultGeometry": result_geometry,
        "source": source,
        "dataVersion": source["dataVersion"],
        "methodology": (
            "Terrain cell summaries are clipped to the site. Mean elevation and "
            "slope are area-weighted; minimum and maximum elevation are the extrema "
            "of intersecting cells. Steep area uses each cell's summarized proportion."
        ),
        "limitations": [
            DEMONSTRATION_LIMITATION,
            (
                "Statistics are aggregated from generalized terrain cells and are "
                "not a substitute for a topographic or geotechnical survey."
            ),
            (
                f"The source grid defines steep terrain using a 15° threshold; the "
                f"requested {steep_threshold:g}° threshold affects classification only."
            ),
        ],
        "details": {
            "minimumElevationMetres": _rounded(min(minimum_elevations), 1),
            "maximumElevationMetres": _rounded(max(maximum_elevations), 1),
            "averageElevationMetres": _rounded(average_elevation, 1),
            "averageSlopeDegrees": _rounded(average_slope, 1),
            "steepAreaSquareMetres": _rounded(estimated_steep_area, 2),
            "steepAreaPercent": _rounded(steep_percent, 2),
            "steepSlopeThresholdDegrees": steep_threshold,
            "coveredCellCount": len(cell_results),
        },
    }

    return {
        "summary": evidence["summary"],
        "category": "Terrain",
        "severity": severity,
        "classification": classification,
        "intersectionAreaSquareMetres": _rounded(covered_area, 2),
        "sitePercent": _rounded(_percent(covered_area, site_metric.area), 2),
        "metrics": {
            **evidence["details"],
            "coveragePercent": _rounded(
                _percent(covered_area, site_metric.area), 2
            ),
        },
        "results": [evidence],
        "methodology": evidence["methodology"],
        "limitations": evidence["limitations"],
        "sourceMetadata": [source],
        "dataNotice": DEMONSTRATION_LIMITATION,
        "resultGeometry": result_geometry,
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


def _polygon_evidence(
    site: Any,
    site_area: float,
    forward: Any,
    features: list[dict[str, Any]],
    *,
    result_id: str,
    name: str,
    category: str,
    source_key: str,
    classification_property: str,
    limitation: str,
    minimum_overlap_percent: float = 0,
    severity_label: str | None = None,
) -> tuple[dict[str, Any], Any | None]:
    intersections = []
    details = []
    result_features = []

    for feature in features:
        intersection = site.intersection(feature["geometry"])
        if intersection.is_empty:
            continue

        area = transform(forward, intersection).area
        site_percent = _percent(area, site_area)
        if site_percent < minimum_overlap_percent:
            continue

        properties = feature["properties"]
        classification = str(
            properties.get(classification_property)
            or properties.get("name")
            or "Mapped area"
        )
        intersections.append(intersection)
        details.append(
            {
                "name": properties.get("name", "Mapped area"),
                "classification": classification,
                "areaSquareMetres": _rounded(area, 2),
                "sitePercent": _rounded(site_percent, 2),
                "properties": properties,
            }
        )
        result_features.append(
            _feature(
                intersection,
                {
                    **properties,
                    "role": result_id,
                    "classification": classification,
                    "intersectionAreaSquareMetres": _rounded(area, 2),
                    "sitePercent": _rounded(site_percent, 2),
                },
            )
        )

    intersection_geometry = (
        unary_union(intersections) if intersections else None
    )
    intersection_area = (
        transform(forward, intersection_geometry).area
        if intersection_geometry is not None
        and not intersection_geometry.is_empty
        else 0
    )
    site_percent = _percent(intersection_area, site_area)
    details.sort(key=lambda item: item["areaSquareMetres"], reverse=True)
    primary_classification = (
        details[0]["classification"] if details else "No mapped overlap"
    )
    severity = (
        severity_label
        if severity_label is not None and details
        else "None"
        if not details
        else "High"
        if site_percent >= 30
        else "Moderate"
        if site_percent >= 10
        else "Low"
    )
    source = _source(source_key)
    result_geometry = (
        {
            "type": "FeatureCollection",
            "features": result_features,
        }
        if result_features
        else None
    )

    return (
        {
            "id": result_id,
            "name": name,
            "category": category,
            "summary": (
                f"{_rounded(site_percent, 2)}% of the site intersects "
                f"{len(details)} mapped "
                f"{'feature' if len(details) == 1 else 'features'}; "
                f"the dominant classification is {primary_classification}."
                if details
                else "No mapped overlap was found in the source dataset."
            ),
            "severity": severity,
            "classification": primary_classification,
            "intersectionAreaSquareMetres": _rounded(intersection_area, 2),
            "sitePercent": _rounded(site_percent, 2),
            "resultGeometry": result_geometry,
            "source": source,
            "dataVersion": source["dataVersion"],
            "methodology": (
                "Source polygons are intersected with the site in WGS 84. "
                "Area is measured in the site's local UTM projection and divided "
                "by total site area. The dominant classification has the largest overlap."
            ),
            "limitations": [limitation],
            "details": {
                "featureCount": len(details),
                "intersections": details,
            },
        },
        intersection_geometry,
    )


def _source(source_key: str) -> dict[str, Any]:
    return dict(DATA_SOURCES[source_key])


def _unique_sources(results: list[dict[str, Any]]) -> list[dict[str, Any]]:
    sources: dict[str, dict[str, Any]] = {}
    for result in results:
        source = result.get("source")
        if isinstance(source, dict) and source.get("id"):
            sources[str(source["id"])] = source
    return list(sources.values())


def _combined_result_geometry(
    results: list[dict[str, Any]],
) -> dict[str, Any] | None:
    features = []
    for result in results:
        geometry = result.get("resultGeometry")
        if not isinstance(geometry, dict):
            continue
        if geometry.get("type") == "Feature":
            features.append(geometry)
        elif geometry.get("type") == "FeatureCollection":
            features.extend(geometry.get("features") or [])
    return (
        {"type": "FeatureCollection", "features": features}
        if features
        else None
    )


def _highest_severity(results: list[dict[str, Any]]) -> str:
    ranking = {
        "None": 0,
        "Informational": 0,
        "Low": 1,
        "Moderate": 2,
        "High": 3,
        "Critical": 4,
    }
    return max(
        (str(result.get("severity", "None")) for result in results),
        key=lambda value: ranking.get(value, 0),
        default="None",
    )


def _required_dataset_number(
    properties: dict[str, Any],
    name: str,
    filename: str,
) -> float:
    value = properties.get(name)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise DatasetLoadError(
            f"Dataset '{filename}' requires numeric property '{name}'."
        )
    number = float(value)
    if not math.isfinite(number):
        raise DatasetLoadError(
            f"Dataset '{filename}' property '{name}' must be finite."
        )
    return number


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
