//using SxgEvalPlatformApi.Models;

//namespace SxgEvalPlatformApi.Services;

///// <summary>
///// Interface for evaluation service operations
///// </summary>
//public interface IEvaluationService
//{
//    /// <summary>
//    /// Get all evaluations
//    /// </summary>
//    /// <returns>List of evaluations</returns>
//    Task<IEnumerable<EvaluationDto>> GetAllEvaluationsAsync();
    
//    /// <summary>
//    /// Get evaluation by ID
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <returns>Evaluation or null if not found</returns>
//    Task<EvaluationDto?> GetEvaluationByIdAsync(int id);
    
//    /// <summary>
//    /// Create a new evaluation
//    /// </summary>
//    /// <param name="createEvaluationDto">Evaluation creation data</param>
//    /// <returns>Created evaluation</returns>
//    Task<EvaluationDto> CreateEvaluationAsync(CreateEvaluationDto createEvaluationDto);
    
//    /// <summary>
//    /// Update an existing evaluation
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <param name="updateEvaluationDto">Evaluation update data</param>
//    /// <returns>Updated evaluation or null if not found</returns>
//    Task<EvaluationDto?> UpdateEvaluationAsync(int id, UpdateEvaluationDto updateEvaluationDto);
    
//    /// <summary>
//    /// Delete an evaluation
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <returns>True if deleted, false if not found</returns>
//    Task<bool> DeleteEvaluationAsync(int id);
//}