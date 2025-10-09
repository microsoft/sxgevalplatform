using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Implementation of evaluation service
/// </summary>
public class EvaluationService : IEvaluationService
{
    private readonly ILogger<EvaluationService> _logger;
    private static readonly List<EvaluationDto> _evaluations = new();
    private static int _nextId = 1;

    public EvaluationService(ILogger<EvaluationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EvaluationDto>> GetAllEvaluationsAsync()
    {
        _logger.LogInformation("Retrieving all evaluations");
        return await Task.FromResult(_evaluations.AsEnumerable());
    }

    /// <inheritdoc />
    public async Task<EvaluationDto?> GetEvaluationByIdAsync(int id)
    {
        _logger.LogInformation("Retrieving evaluation with ID: {Id}", id);
        var evaluation = _evaluations.FirstOrDefault(e => e.Id == id);
        return await Task.FromResult(evaluation);
    }

    /// <inheritdoc />
    public async Task<EvaluationDto> CreateEvaluationAsync(CreateEvaluationDto createEvaluationDto)
    {
        _logger.LogInformation("Creating new evaluation: {Name}", createEvaluationDto.Name);
        
        var evaluation = new EvaluationDto
        {
            Id = _nextId++,
            Name = createEvaluationDto.Name,
            Description = createEvaluationDto.Description,
            CreatedBy = createEvaluationDto.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending",
            Metadata = createEvaluationDto.Metadata
        };

        _evaluations.Add(evaluation);
        
        _logger.LogInformation("Created evaluation with ID: {Id}", evaluation.Id);
        return await Task.FromResult(evaluation);
    }

    /// <inheritdoc />
    public async Task<EvaluationDto?> UpdateEvaluationAsync(int id, UpdateEvaluationDto updateEvaluationDto)
    {
        _logger.LogInformation("Updating evaluation with ID: {Id}", id);
        
        var evaluation = _evaluations.FirstOrDefault(e => e.Id == id);
        if (evaluation == null)
        {
            _logger.LogWarning("Evaluation with ID {Id} not found for update", id);
            return null;
        }

        // Update properties if provided
        if (!string.IsNullOrEmpty(updateEvaluationDto.Name))
            evaluation.Name = updateEvaluationDto.Name;
        
        if (updateEvaluationDto.Description is not null)
            evaluation.Description = updateEvaluationDto.Description;
        
        if (!string.IsNullOrEmpty(updateEvaluationDto.Status))
            evaluation.Status = updateEvaluationDto.Status;
        
        if (updateEvaluationDto.Score.HasValue)
            evaluation.Score = updateEvaluationDto.Score;
        
        if (updateEvaluationDto.Metadata is not null)
            evaluation.Metadata = updateEvaluationDto.Metadata;

        evaluation.UpdatedAt = DateTime.UtcNow;
        
        _logger.LogInformation("Updated evaluation with ID: {Id}", id);
        return await Task.FromResult(evaluation);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEvaluationAsync(int id)
    {
        _logger.LogInformation("Deleting evaluation with ID: {Id}", id);
        
        var evaluation = _evaluations.FirstOrDefault(e => e.Id == id);
        if (evaluation == null)
        {
            _logger.LogWarning("Evaluation with ID {Id} not found for deletion", id);
            return false;
        }

        _evaluations.Remove(evaluation);
        
        _logger.LogInformation("Deleted evaluation with ID: {Id}", id);
        return await Task.FromResult(true);
    }
}