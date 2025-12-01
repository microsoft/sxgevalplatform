using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for evaluation run operations
/// Split into partial classes for better organization:
/// - EvalRunsController.cs (base class with dependencies)
/// - EvalRunsController.EvalRuns.cs (CRUD operations for evaluation runs)
/// - EvalRunsController.Status.cs (Status-related operations)
/// - EvalRunsController.EnrichedDataset.cs (Enriched dataset operations)
/// - EvalRunsController.Results.cs (Evaluation results operations)
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/eval/runs")]
public partial class EvalRunsController : BaseController
{
    private readonly IEvalRunRequestHandler _evalRunRequestHandler;
    private readonly IDataSetTableService _dataSetTableService;
    private readonly IMetricsConfigTableService _metricsConfigTableService;
    private readonly IEvalArtifactsRequestHandler _evalArtifactsRequestHandler;
    private readonly IEvaluationResultRequestHandler _evaluationResultRequestHandler;

    public EvalRunsController(
        IEvalRunRequestHandler evalRunRequestHandler,
        IDataSetTableService dataSetTableService,
        IMetricsConfigTableService metricsConfigTableService,
        IEvalArtifactsRequestHandler evalArtifactsRequestHandler,
        IEvaluationResultRequestHandler evaluationResultRequestHandler,
        ICallerIdentificationService callerService,
        ILogger<EvalRunsController> logger,
        IOpenTelemetryService? telemetryService = null)
        : base(logger, callerService, telemetryService)
    {
        _evalRunRequestHandler = evalRunRequestHandler;
        _dataSetTableService = dataSetTableService;
        _metricsConfigTableService = metricsConfigTableService;
        _evalArtifactsRequestHandler = evalArtifactsRequestHandler;
        _evaluationResultRequestHandler = evaluationResultRequestHandler;
    }

    
}