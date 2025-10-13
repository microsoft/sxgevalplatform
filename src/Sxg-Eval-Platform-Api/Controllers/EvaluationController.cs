//using Microsoft.AspNetCore.Mvc;
//using SxgEvalPlatformApi.Models;
//using SxgEvalPlatformApi.Services;

//namespace SxgEvalPlatformApi.Controllers;

///// <summary>
///// Controller for evaluation operations
///// </summary>
//[ApiController]
//[Route("api/v1/[controller]")]
//public class EvaluationController : BaseController
//{
//    private readonly IEvaluationService _evaluationService;

//    public EvaluationController(IEvaluationService evaluationService, ILogger<EvaluationController> logger)
//        : base(logger)
//    {
//        _evaluationService = evaluationService;
//    }

//    /// <summary>
//    /// Get all evaluations
//    /// </summary>
//    /// <returns>List of evaluations</returns>
//    [HttpGet]
//    [ProducesResponseType(typeof(IEnumerable<EvaluationDto>), StatusCodes.Status200OK)]
//    public async Task<ActionResult<IEnumerable<EvaluationDto>>> GetEvaluations()
//    {
//        try
//        {
//            var evaluations = await _evaluationService.GetAllEvaluationsAsync();
//            return Ok(evaluations);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while fetching evaluations");
//            return CreateErrorResponse<IEnumerable<EvaluationDto>>("Failed to retrieve evaluations");
//        }
//    }

//    /// <summary>
//    /// Get evaluation by ID
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <returns>Evaluation details</returns>
//    [HttpGet("{id}")]
//    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    public async Task<ActionResult<EvaluationDto>> GetEvaluation(int id)
//    {
//        try
//        {
//            var evaluation = await _evaluationService.GetEvaluationByIdAsync(id);
//            if (evaluation == null)
//            {
//                return NotFound($"Evaluation with ID {id} not found");
//            }
//            return Ok(evaluation);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while fetching evaluation {Id}", id);
//            return CreateErrorResponse<EvaluationDto>($"Failed to retrieve evaluation with ID {id}");
//        }
//    }

//    /// <summary>
//    /// Create a new evaluation
//    /// </summary>
//    /// <param name="createEvaluationDto">Evaluation creation data</param>
//    /// <returns>Created evaluation</returns>
//    [HttpPost]
//    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status201Created)]
//    [ProducesResponseType(StatusCodes.Status400BadRequest)]
//    public async Task<ActionResult<EvaluationDto>> CreateEvaluation([FromBody] CreateEvaluationDto createEvaluationDto)
//    {
//        try
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var evaluation = await _evaluationService.CreateEvaluationAsync(createEvaluationDto);
//            return CreatedAtAction(nameof(GetEvaluation), new { id = evaluation.Id }, evaluation);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while creating evaluation");
//            return CreateErrorResponse<EvaluationDto>("Failed to create evaluation");
//        }
//    }

//    /// <summary>
//    /// Update an existing evaluation
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <param name="updateEvaluationDto">Evaluation update data</param>
//    /// <returns>Updated evaluation</returns>
//    [HttpPut("{id}")]
//    [ProducesResponseType(typeof(EvaluationDto), StatusCodes.Status200OK)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    [ProducesResponseType(StatusCodes.Status400BadRequest)]
//    public async Task<ActionResult<EvaluationDto>> UpdateEvaluation(int id, [FromBody] UpdateEvaluationDto updateEvaluationDto)
//    {
//        try
//        {
//            if (!ModelState.IsValid)
//            {
//                return BadRequest(ModelState);
//            }

//            var evaluation = await _evaluationService.UpdateEvaluationAsync(id, updateEvaluationDto);
//            if (evaluation == null)
//            {
//                return NotFound($"Evaluation with ID {id} not found");
//            }
//            return Ok(evaluation);
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while updating evaluation {Id}", id);
//            return CreateErrorResponse<EvaluationDto>($"Failed to update evaluation with ID {id}");
//        }
//    }

//    /// <summary>
//    /// Delete an evaluation
//    /// </summary>
//    /// <param name="id">Evaluation ID</param>
//    /// <returns>No content</returns>
//    [HttpDelete("{id}")]
//    [ProducesResponseType(StatusCodes.Status204NoContent)]
//    [ProducesResponseType(StatusCodes.Status404NotFound)]
//    public async Task<IActionResult> DeleteEvaluation(int id)
//    {
//        try
//        {
//            var result = await _evaluationService.DeleteEvaluationAsync(id);
//            if (!result)
//            {
//                return NotFound($"Evaluation with ID {id} not found");
//            }
//            return NoContent();
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, "Error occurred while deleting evaluation {Id}", id);
//            return CreateErrorResponse($"Failed to delete evaluation with ID {id}");
//        }
//    }
//}