using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;

namespace Remotely.Server.API;

/// <summary>
/// Controller for retrieving script run history via API.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class ScriptRunsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<ScriptRunsController> _logger;

    public ScriptRunsController(
        IDataService dataService,
        ILogger<ScriptRunsController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of script runs for the organization.
    /// </summary>
    /// <param name="savedScriptId">Optional filter by saved script ID.</param>
    /// <param name="deviceId">Optional filter by device ID.</param>
    /// <param name="startDate">Optional filter by start date (inclusive).</param>
    /// <param name="endDate">Optional filter by end date (inclusive).</param>
    /// <param name="page">Page number (1-based, default: 1).</param>
    /// <param name="pageSize">Number of items per page (default: 20).</param>
    /// <returns>A paginated list of script runs with related data.</returns>
    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<PagedResultDto<ScriptRunDto>>> GetRuns(
        [FromQuery] Guid? savedScriptId = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // Validate pagination parameters
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 20;
        }
        else if (pageSize > 100)
        {
            pageSize = 100; // Cap at 100 items per page
        }

        var filter = new ScriptRunFilterDto
        {
            SavedScriptId = savedScriptId,
            DeviceId = deviceId,
            StartDate = startDate,
            EndDate = endDate,
            Page = page,
            PageSize = pageSize
        };

        var result = await _dataService.GetScriptRuns(orgId, filter);
        
        return Ok(result);
    }

    /// <summary>
    /// Gets the details of a specific script run including all results.
    /// </summary>
    /// <param name="runId">The ID of the script run to retrieve.</param>
    /// <returns>The script run details with all script results.</returns>
    [HttpGet("{runId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<ScriptRunDetailDto>> GetRun(int runId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var result = await _dataService.GetScriptRun(runId, orgId);
        
        if (!result.IsSuccess)
        {
            return NotFound("Script run not found.");
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Deletes a specific script run and all its results.
    /// </summary>
    /// <param name="runId">The ID of the script run to delete.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{runId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteRun(int runId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var result = await _dataService.DeleteScriptRun(runId, orgId);
        
        if (!result.IsSuccess)
        {
            return NotFound("Script run not found.");
        }

        _logger.LogInformation("Script run {RunId} deleted by organization {OrgId}", runId, orgId);
        return NoContent();
    }

    /// <summary>
    /// Deletes multiple script runs.
    /// </summary>
    /// <param name="runIds">The IDs of the script runs to delete.</param>
    /// <returns>The number of deleted runs.</returns>
    [HttpPost("delete-batch")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<int>> DeleteRuns([FromBody] List<int> runIds)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (runIds == null || runIds.Count == 0)
        {
            return BadRequest("No run IDs provided.");
        }

        var deletedCount = await _dataService.DeleteScriptRuns(runIds, orgId);
        
        _logger.LogInformation("Deleted {Count} script runs for organization {OrgId}", deletedCount, orgId);
        return Ok(deletedCount);
    }

    /// <summary>
    /// Deletes all script runs for the organization.
    /// </summary>
    /// <returns>The number of deleted runs.</returns>
    [HttpDelete("clear-all")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<int>> ClearAllRuns()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var deletedCount = await _dataService.ClearAllScriptRuns(orgId);
        
        _logger.LogInformation("Cleared all ({Count}) script runs for organization {OrgId}", deletedCount, orgId);
        return Ok(deletedCount);
    }
}
