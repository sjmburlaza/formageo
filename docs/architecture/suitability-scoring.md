# Suitability scoring

FormaGeo suitability scoring is a versioned weighted-sum model. A
`ScoringScenario` is the user-facing scenario identity. Every save creates a
new immutable `ScoringModel` version containing its own `ScoringCriterion`
records. Each `ScoringResult` points to the exact model ID and stores the full
calculation breakdown.

## Calculation

Each available raw criterion value is normalized to a score from 0 to 100.

- `Linear` interpolates between the lower and upper thresholds and clamps
  values outside that range.
- `Threshold` assigns 0, 50, or 100 according to the thresholds.
- `Boolean` maps false/true to 0/100.
- `HigherIsBetter` preserves the scale direction.
- `LowerIsBetter` reverses the scale direction.

For every scoreable criterion:

```text
contribution = normalized score × effective weight / 100
overall score = sum of displayed criterion contributions
```

Contributions are rounded to two decimals. A final rounding remainder is
assigned to the last contributing criterion so that the returned contributions
always sum exactly to the returned overall score.

## Missing data

Missing observations are always present in the breakdown and use the behavior
saved with the model:

- `CannotScore` makes the site result unscoreable.
- `ScoreZero` retains the criterion weight and contributes zero points.
- `ExcludeAndReweight` contributes no points directly and proportionally
  redistributes its weight across the remaining criteria.

The result records missing-information explanations and any redistributed
weight. Missing data is never interpreted as a favorable raw value.

## Analysis adapters

The scoring value provider reads the latest completed, versioned analysis for
each site:

| Criterion | Source value |
| --- | --- |
| Flood risk | Flood evidence site percentage from Hazard Exposure |
| Slope | Area-weighted average slope from Terrain |
| Road access | Nearest corridor distance from Accessibility |
| Schools / hospitals | Nearest matching facility from Nearby Facilities |
| Land-use compatibility | Index derived from Zoning's dominant land use |
| Developable area | 100 minus Zoning's mapped restricted percentage |
| Population reach | Explicitly missing until a population-reach adapter exists |

Every extracted value includes its analysis or dataset version in the score
breakdown.

## Reproduction

`POST /api/scoring-scenarios/{scenarioId}/score` accepts an optional
`modelVersion`. Passing the `modelVersion` returned with an earlier result
re-runs the same saved criteria, directions, thresholds, weights, and
missing-data rules against the current versioned site evidence. The original
result remains stored for audit and comparison.
