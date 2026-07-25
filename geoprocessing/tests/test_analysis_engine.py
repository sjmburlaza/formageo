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
    assert ANALYSIS_VERSION == "1.0.0"


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
