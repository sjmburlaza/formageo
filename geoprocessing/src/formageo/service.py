"""HTTP entry point for FormaGeo geoprocessing analyses."""

from __future__ import annotations

from typing import Any
from uuid import UUID

from fastapi import FastAPI
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from formageo.analysis.engine import (
    ANALYSIS_VERSION,
    AnalysisValidationError,
    DatasetLoadError,
    run_analysis,
)


class AnalysisRequest(BaseModel):
    analysisId: UUID
    siteId: UUID
    analysisType: str = Field(min_length=1)
    analysisVersion: str = Field(min_length=1)
    siteGeometry: dict[str, Any]
    inputParameters: dict[str, Any] = Field(default_factory=dict)


app = FastAPI(
    title="FormaGeo Geoprocessing Service",
    version=ANALYSIS_VERSION,
)


@app.get("/health")
def health() -> dict[str, str]:
    return {
        "status": "ok",
        "analysisVersion": ANALYSIS_VERSION,
    }


@app.post("/analyses/run")
def execute_analysis(request: AnalysisRequest) -> dict[str, Any]:
    if request.analysisVersion != ANALYSIS_VERSION:
        return JSONResponse(
            status_code=409,
            content={
                "error": {
                    "code": "analysis_version_mismatch",
                    "field": "analysisVersion",
                    "message": (
                        f"Requested analysis version '{request.analysisVersion}' "
                        f"is not available. Current version is '{ANALYSIS_VERSION}'."
                    ),
                }
            },
        )

    try:
        result = run_analysis(
            request.analysisType,
            request.siteGeometry,
            request.inputParameters,
        )
    except AnalysisValidationError as exception:
        return JSONResponse(
            status_code=422,
            content={
                "error": {
                    "code": "validation_error",
                    "field": exception.field,
                    "message": str(exception),
                }
            },
        )
    except DatasetLoadError as exception:
        return JSONResponse(
            status_code=503,
            content={
                "error": {
                    "code": "dataset_unavailable",
                    "message": str(exception),
                }
            },
        )
    except Exception:
        return JSONResponse(
            status_code=500,
            content={
                "error": {
                    "code": "analysis_failed",
                    "message": (
                        "The spatial operation failed unexpectedly. "
                        "Review service logs using the analysis ID."
                    ),
                }
            },
        )

    return {
        "analysisId": str(request.analysisId),
        "siteId": str(request.siteId),
        "analysisType": request.analysisType,
        "analysisVersion": ANALYSIS_VERSION,
        "result": result,
    }


def main() -> None:
    import uvicorn

    uvicorn.run(
        "formageo.service:app",
        host="0.0.0.0",
        port=8000,
    )


if __name__ == "__main__":
    main()
