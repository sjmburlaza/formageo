using FormaGeo.Application.Projects;
using FormaGeo.Application.Sites;
using FormaGeo.Domain.Scoring;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormaGeo.Application.Scoring;

public sealed class ScoringService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    private readonly IScoringRepository _scoringRepository;
    private readonly IScoringValueProvider _valueProvider;
    private readonly IProjectRepository _projectRepository;
    private readonly ISiteRepository _siteRepository;

    public ScoringService(
        IScoringRepository scoringRepository,
        IScoringValueProvider valueProvider,
        IProjectRepository projectRepository,
        ISiteRepository siteRepository)
    {
        _scoringRepository = scoringRepository;
        _valueProvider = valueProvider;
        _projectRepository = projectRepository;
        _siteRepository = siteRepository;
    }

    public async Task<IReadOnlyList<ScoringScenarioResponse>>
        GetForProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
    {
        if (!await _projectRepository.ExistsAsync(
            projectId,
            cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        var scenarios =
            await _scoringRepository.GetScenariosByProjectAsync(
                projectId,
                cancellationToken);
        var models = await _scoringRepository.GetLatestModelsAsync(
            scenarios.Select(scenario => scenario.Id),
            cancellationToken);

        return scenarios
            .Where(scenario => models.ContainsKey(scenario.Id))
            .Select(scenario => ToResponse(
                scenario,
                models[scenario.Id]))
            .ToArray();
    }

    public async Task<ScoringScenarioResponse?> GetAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _scoringRepository.GetScenarioAsync(
            scenarioId,
            false,
            cancellationToken);

        if (scenario is null)
        {
            return null;
        }

        var model = await _scoringRepository.GetLatestModelAsync(
            scenarioId,
            cancellationToken);

        return model is null
            ? null
            : ToResponse(scenario, model);
    }

    public async Task<ScoringScenarioResponse> CreateAsync(
        Guid projectId,
        SaveScoringScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _projectRepository.ExistsAsync(
            projectId,
            cancellationToken))
        {
            throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");
        }

        var scenario = ScoringScenario.Create(
            projectId,
            request.Name);
        var model = BuildModel(
            scenario.Id,
            1,
            request);

        await _scoringRepository.AddScenarioAsync(
            scenario,
            model,
            cancellationToken);

        return ToResponse(scenario, model);
    }

    public async Task<ScoringScenarioResponse> UpdateAsync(
        Guid scenarioId,
        SaveScoringScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenario = await _scoringRepository.GetScenarioAsync(
            scenarioId,
            true,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' was not found.");
        var latestModel =
            await _scoringRepository.GetLatestModelAsync(
                scenarioId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' has no saved model.");
        var model = BuildModel(
            scenario.Id,
            latestModel.Version + 1,
            request);

        scenario.Rename(request.Name);
        await _scoringRepository.AddModelAsync(
            model,
            cancellationToken);

        return ToResponse(scenario, model);
    }

    public async Task<ScoringScenarioResponse> DuplicateAsync(
        Guid scenarioId,
        CancellationToken cancellationToken = default)
    {
        var source = await _scoringRepository.GetScenarioAsync(
            scenarioId,
            false,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' was not found.");
        var sourceModel =
            await _scoringRepository.GetLatestModelAsync(
                scenarioId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' has no saved model.");
        var scenario = ScoringScenario.Create(
            source.ProjectId,
            $"{source.Name} — copy");
        var criteria = sourceModel.Criteria
            .OrderBy(criterion => criterion.SortOrder)
            .Select(CloneCriterion)
            .ToArray();
        var model = ScoringModel.Create(
            scenario.Id,
            1,
            scenario.Name,
            criteria);

        await _scoringRepository.AddScenarioAsync(
            scenario,
            model,
            cancellationToken);

        return ToResponse(scenario, model);
    }

    public async Task<IReadOnlyList<ScoringResultResponse>>
        RunAsync(
            Guid scenarioId,
            RunScoringRequest request,
            CancellationToken cancellationToken = default)
    {
        if (request.SiteIds.Count == 0)
        {
            throw new ScoringValidationException(
                "siteIds",
                "Select at least one site to score.");
        }

        if (request.SiteIds.Count > 50)
        {
            throw new ScoringValidationException(
                "siteIds",
                "No more than 50 sites can be scored at once.");
        }

        var scenario = await _scoringRepository.GetScenarioAsync(
            scenarioId,
            false,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' was not found.");
        var model = request.ModelVersion.HasValue
            ? await _scoringRepository.GetModelVersionAsync(
                scenarioId,
                request.ModelVersion.Value,
                cancellationToken)
            : await _scoringRepository.GetLatestModelAsync(
                scenarioId,
                cancellationToken);

        if (model is null)
        {
            throw new KeyNotFoundException(
                request.ModelVersion.HasValue
                    ? $"Scoring model version {request.ModelVersion} was not found."
                    : $"Scoring scenario '{scenarioId}' has no saved model.");
        }

        var responses = new List<ScoringResultResponse>();
        var results = new List<ScoringResult>();

        foreach (var siteId in request.SiteIds.Distinct())
        {
            var site = await _siteRepository.GetByIdAsync(
                siteId,
                cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Site '{siteId}' was not found.");

            if (site.ProjectId != scenario.ProjectId)
            {
                throw new ScoringValidationException(
                    "siteIds",
                    $"Site '{siteId}' does not belong to the scenario's project.");
            }

            var observations = await _valueProvider.GetValuesAsync(
                site,
                model.Criteria,
                cancellationToken);
            var calculation =
                SuitabilityScoreCalculator.Calculate(
                    model.Criteria,
                    observations);
            var snapshot = Snapshot(
                site.Name,
                calculation);
            var result = ScoringResult.Create(
                site.Id,
                scenario.Id,
                model.Id,
                calculation.OverallScore,
                calculation.Rating,
                calculation.IsScoreable,
                JsonSerializer.Serialize(
                    snapshot,
                    SnapshotJsonOptions));

            results.Add(result);
            responses.Add(ToResponse(
                result,
                model,
                snapshot));
        }

        await _scoringRepository.AddResultsAsync(
            results,
            cancellationToken);

        return responses;
    }

    public async Task<IReadOnlyList<ScoringResultResponse>>
        GetResultsAsync(
            Guid scenarioId,
            CancellationToken cancellationToken = default)
    {
        var scenario = await _scoringRepository.GetScenarioAsync(
            scenarioId,
            false,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Scoring scenario '{scenarioId}' was not found.");
        var results = await _scoringRepository.GetResultsAsync(
            scenario.Id,
            cancellationToken);
        var models = new Dictionary<Guid, ScoringModel>();
        var responses = new List<ScoringResultResponse>();

        foreach (var result in results)
        {
            if (!models.TryGetValue(
                result.ScoringModelId,
                out var model))
            {
                model = await FindModelAsync(
                    scenarioId,
                    result.ScoringModelId,
                    cancellationToken);
                models[result.ScoringModelId] = model;
            }

            var snapshot =
                JsonSerializer.Deserialize<ScoringBreakdownSnapshot>(
                    result.BreakdownJson,
                    SnapshotJsonOptions)
                ?? throw new InvalidOperationException(
                    $"Scoring result '{result.Id}' has no readable breakdown.");
            responses.Add(ToResponse(
                result,
                model,
                snapshot));
        }

        return responses;
    }

    private async Task<ScoringModel> FindModelAsync(
        Guid scenarioId,
        Guid modelId,
        CancellationToken cancellationToken)
    {
        var latest = await _scoringRepository.GetLatestModelAsync(
            scenarioId,
            cancellationToken);

        if (latest?.Id == modelId)
        {
            return latest;
        }

        for (var version = 1;
             latest is not null && version < latest.Version;
             version++)
        {
            var model = await _scoringRepository.GetModelVersionAsync(
                scenarioId,
                version,
                cancellationToken);
            if (model?.Id == modelId)
            {
                return model;
            }
        }

        throw new InvalidOperationException(
            $"Saved scoring model '{modelId}' was not found.");
    }

    private static ScoringModel BuildModel(
        Guid scenarioId,
        int version,
        SaveScoringScenarioRequest request)
    {
        if (request.Criteria is null)
        {
            throw new ScoringValidationException(
                "criteria",
                "Scoring criteria are required.");
        }

        var criteria = request.Criteria
            .Select((criterion, index) =>
                ScoringCriterion.Create(
                    criterion.Key,
                    criterion.Name,
                    criterion.Weight,
                    criterion.Direction,
                    criterion.NormalizationMethod,
                    criterion.DataSource,
                    criterion.Unit,
                    criterion.LowerThreshold,
                    criterion.UpperThreshold,
                    criterion.MissingDataBehavior,
                    index))
            .ToArray();

        return ScoringModel.Create(
            scenarioId,
            version,
            request.Name,
            criteria);
    }

    private static ScoringCriterion CloneCriterion(
        ScoringCriterion criterion)
    {
        return ScoringCriterion.Create(
            criterion.Key,
            criterion.Name,
            criterion.Weight,
            criterion.Direction,
            criterion.NormalizationMethod,
            criterion.DataSource,
            criterion.Unit,
            criterion.LowerThreshold,
            criterion.UpperThreshold,
            criterion.MissingDataBehavior,
            criterion.SortOrder);
    }

    private static ScoringBreakdownSnapshot Snapshot(
        string siteName,
        SuitabilityScoreCalculation calculation)
    {
        return new ScoringBreakdownSnapshot(
            siteName,
            calculation.Criteria.Sum(
                criterion => criterion.ConfiguredWeight),
            calculation.Criteria.Sum(
                criterion => criterion.EffectiveWeight),
            calculation.Criteria
                .Select(criterion =>
                    new CriterionScoreResponse(
                        criterion.CriterionKey,
                        criterion.Name,
                        criterion.RawValue,
                        criterion.Unit,
                        criterion.NormalizedScore,
                        criterion.ConfiguredWeight,
                        criterion.EffectiveWeight,
                        criterion.Contribution,
                        criterion.DataSource,
                        criterion.DataVersion,
                        criterion.IsMissing,
                        criterion.MissingDataBehavior,
                        criterion.Explanation))
                .ToArray(),
            calculation.Strengths,
            calculation.Weaknesses,
            calculation.MissingInformation,
            calculation.ValidationMessages);
    }

    private static ScoringScenarioResponse ToResponse(
        ScoringScenario scenario,
        ScoringModel model)
    {
        return new ScoringScenarioResponse(
            scenario.Id,
            scenario.ProjectId,
            scenario.Name,
            scenario.CreatedAtUtc,
            scenario.UpdatedAtUtc,
            ScoringModelResponse.FromDomain(model));
    }

    private static ScoringResultResponse ToResponse(
        ScoringResult result,
        ScoringModel model,
        ScoringBreakdownSnapshot snapshot)
    {
        return new ScoringResultResponse(
            result.Id,
            result.SiteId,
            snapshot.SiteName,
            result.ScoringScenarioId,
            result.ScoringModelId,
            model.Version,
            model.Name,
            result.OverallScore,
            result.Rating,
            result.IsScoreable,
            result.CalculatedAtUtc,
            snapshot.ConfiguredWeightTotal,
            snapshot.EffectiveWeightTotal,
            snapshot.Criteria,
            snapshot.Strengths,
            snapshot.Weaknesses,
            snapshot.MissingInformation,
            snapshot.ValidationMessages);
    }
}
