import pytest

from formageo.analysis import (
    ANALYSIS_VERSION,
    AnalysisValidationError,
    run_analysis,
)


SITE = {
    "type": "Polygon",
    "coordinates": [
        [
            [121.005, 14.56],
            [121.025, 14.56],
            [121.025, 14.575],
            [121.005, 14.575],
            [121.005, 14.56],
        ]
    ],
}


@pytest.mark.parametrize(
    "analysis_type,parameters",
    [
        ("SiteGeometry", {}),
        ("HazardExposure", {"minimumOverlapPercent": 0}),
        ("Zoning", {"includeUnzoned": True}),
        ("Terrain", {"steepSlopeThresholdDegrees": 15}),
        ("Accessibility", {"maximumDistanceMetres": 5000}),
        ("NearbyFacilities", {"radiusMetres": 5000, "facilityTypes": ""}),
        (
            "Suitability",
            {
                "hazardWeight": 35,
                "accessWeight": 25,
                "facilitiesWeight": 20,
                "greenSpaceWeight": 20,
            },
        ),
    ],
)
def test_supported_analysis_returns_structured_result(
    analysis_type, parameters
):
    result = run_analysis(analysis_type, SITE, parameters)

    assert result["summary"]
    assert isinstance(result["metrics"], dict)
    assert "resultGeometry" in result
    assert ANALYSIS_VERSION == "2.0.0"


@pytest.mark.parametrize(
    "analysis_type,expected_category",
    [
        ("HazardExposure", "Hazards"),
        ("Zoning", "Planning"),
        ("Terrain", "Terrain"),
    ],
)
def test_intelligence_analysis_returns_explainable_evidence(
    analysis_type, expected_category
):
    result = run_analysis(analysis_type, SITE, {})

    assert result["category"] == expected_category
    assert result["classification"]
    assert result["severity"]
    assert result["methodology"]
    assert result["limitations"]
    assert result["sourceMetadata"]
    assert result["resultGeometry"]["type"] == "FeatureCollection"

    for evidence in result["results"]:
        assert evidence["summary"]
        assert evidence["classification"]
        assert evidence["severity"]
        assert evidence["intersectionAreaSquareMetres"] >= 0
        assert 0 <= evidence["sitePercent"] <= 100
        assert evidence["source"]["dataset"]
        assert evidence["source"]["sourceUrl"]
        assert evidence["dataVersion"]
        assert evidence["methodology"]
        assert evidence["limitations"]
        assert "resultGeometry" in evidence


def test_hazard_exposure_checks_multiple_hazards_and_proximity():
    result = run_analysis("HazardExposure", SITE, {})
    names = {evidence["name"] for evidence in result["results"]}

    assert names == {
        "Flood-zone intersection",
        "Landslide exposure",
        "Storm-surge exposure",
        "Protected-area overlap",
        "Fault-line proximity",
    }
    fault = next(
        evidence
        for evidence in result["results"]
        if evidence["id"] == "fault-line-proximity"
    )
    assert fault["details"]["distanceMetres"] > 0
    assert fault["resultGeometry"]["features"]


def test_planning_analysis_reports_zone_jurisdiction_and_restriction():
    result = run_analysis("Zoning", SITE, {})
    result_ids = {evidence["id"] for evidence in result["results"]}

    assert result_ids == {
        "zoning-classification",
        "administrative-jurisdiction",
        "development-restrictions",
    }
    assert result["metrics"]["primaryLandUse"] == "Mixed use"
    assert result["metrics"]["jurisdictionCount"] == 1
    assert result["metrics"]["restrictedPercent"] > 0


def test_terrain_analysis_reports_elevation_slope_and_steep_area():
    result = run_analysis("Terrain", SITE, {})
    metrics = result["metrics"]

    assert metrics["minimumElevationMetres"] == 8
    assert metrics["maximumElevationMetres"] == 74
    assert metrics["averageElevationMetres"] > 0
    assert metrics["averageSlopeDegrees"] > 0
    assert 0 < metrics["steepAreaPercent"] < 100
    assert metrics["coveragePercent"] == pytest.approx(100, abs=0.01)


def test_rejects_invalid_parameter_with_field_context():
    with pytest.raises(AnalysisValidationError) as exception:
        run_analysis(
            "NearbyFacilities",
            SITE,
            {"radiusMetres": -1},
        )

    assert exception.value.field == "inputParameters.radiusMetres"
    assert "between" in str(exception.value)


def test_rejects_invalid_site_geometry():
    with pytest.raises(AnalysisValidationError) as exception:
        run_analysis(
            "SiteGeometry",
            {"type": "Point", "coordinates": [121, 14]},
            {},
        )

    assert exception.value.field == "siteGeometry"
