using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class SavedScriptsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<SavedScriptsController> _logger;

    public SavedScriptsController(
        IDataService dataService,
        ILogger<SavedScriptsController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<SavedScript>>> GetScripts()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // Get the user ID to include their private scripts
        string? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (userResult.IsSuccess)
            {
                userId = userResult.Value.Id;
            }
        }
        else
        {
            // For API key authentication, get the first admin user from the organization
            var users = await _dataService.GetAllUsersInOrganization(orgId);
            var adminUser = users.FirstOrDefault(u => u.IsAdministrator);
            if (adminUser != null)
            {
                userId = adminUser.Id;
            }
        }

        var scripts = await _dataService.GetSavedScriptsForOrganization(orgId, userId);
        return Ok(scripts);
    }

    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [HttpGet("{scriptId}")]
    public async Task<ActionResult<SavedScript>> GetScript(Guid scriptId)
    {
        var result =  await _dataService.GetSavedScript(scriptId);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        return result.Value;
    }

    [HttpGet("{scriptId}/content")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<SavedScript>> GetScriptContent(Guid scriptId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var result = await _dataService.GetSavedScript(scriptId);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        // Validate the script belongs to the requesting organization
        if (result.Value.OrganizationID != orgId)
        {
            return NotFound(); // Return 404 instead of 403 to avoid information leakage
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Creates a new saved script.
    /// </summary>
    /// <param name="dto">The script data to create.</param>
    /// <returns>The created script entity.</returns>
    [HttpPost]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<SavedScript>> CreateScript([FromBody] CreateSavedScriptDto dto)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get the user ID - either from authenticated user or API key
        string? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }
            userId = userResult.Value.Id;
        }
        else
        {
            // For API key authentication, we need to get a user from the organization
            // Use the first admin user or create a system user reference
            var users = await _dataService.GetAllUsersInOrganization(orgId);
            var adminUser = users.FirstOrDefault(u => u.IsAdministrator);
            if (adminUser == null)
            {
                return BadRequest("No administrator user found in the organization.");
            }
            userId = adminUser.Id;
        }

        var script = new SavedScript
        {
            Name = dto.Name,
            Content = dto.Content,
            Shell = dto.Shell,
            FolderPath = dto.FolderPath,
            IsPublic = dto.IsPublic,
            IsQuickScript = dto.IsQuickScript,
            GenerateAlertOnError = dto.GenerateAlertOnError,
            SendEmailOnError = dto.SendEmailOnError,
            SendErrorEmailTo = dto.SendErrorEmailTo,
            CreatorId = userId,
            OrganizationID = orgId
        };

        var result = await _dataService.AddOrUpdateSavedScript(script, userId);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to create script: {Error}", result.Reason);
            return BadRequest(result.Reason);
        }

        return CreatedAtAction(nameof(GetScriptContent), new { scriptId = script.Id }, script);
    }

    /// <summary>
    /// Updates an existing saved script.
    /// </summary>
    /// <param name="scriptId">The ID of the script to update.</param>
    /// <param name="dto">The updated script data.</param>
    /// <returns>The updated script entity.</returns>
    [HttpPut("{scriptId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<SavedScript>> UpdateScript(Guid scriptId, [FromBody] UpdateSavedScriptDto dto)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get the existing script
        var scriptResult = await _dataService.GetSavedScript(scriptId);
        if (!scriptResult.IsSuccess)
        {
            return NotFound("Script not found.");
        }

        var script = scriptResult.Value;

        // Verify the script belongs to the requesting organization
        if (script.OrganizationID != orgId)
        {
            return NotFound("Script not found.");
        }

        // Check user permissions - must be owner or administrator
        string? userId = null;
        bool isAdmin = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }
            userId = userResult.Value.Id;
            isAdmin = userResult.Value.IsAdministrator;

            // Check if user is owner or admin
            if (script.CreatorId != userId && !isAdmin)
            {
                return Forbid();
            }
        }
        // API key authentication is considered admin-level access

        // Update only the provided fields, preserving CreatorId and OrganizationID
        if (dto.Name != null)
            script.Name = dto.Name;
        if (dto.Content != null)
            script.Content = dto.Content;
        if (dto.Shell.HasValue)
            script.Shell = dto.Shell.Value;
        if (dto.FolderPath != null)
            script.FolderPath = dto.FolderPath;
        if (dto.IsPublic.HasValue)
            script.IsPublic = dto.IsPublic.Value;
        if (dto.IsQuickScript.HasValue)
            script.IsQuickScript = dto.IsQuickScript.Value;
        if (dto.GenerateAlertOnError.HasValue)
            script.GenerateAlertOnError = dto.GenerateAlertOnError.Value;
        if (dto.SendEmailOnError.HasValue)
            script.SendEmailOnError = dto.SendEmailOnError.Value;
        if (dto.SendErrorEmailTo != null)
            script.SendErrorEmailTo = dto.SendErrorEmailTo;

        // Preserve original CreatorId and OrganizationID (already set, no changes needed)

        var result = await _dataService.AddOrUpdateSavedScript(script, script.CreatorId);
        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to update script: {Error}", result.Reason);
            return BadRequest(result.Reason);
        }

        return Ok(script);
    }

    /// <summary>
    /// Deletes a saved script.
    /// </summary>
    /// <param name="scriptId">The ID of the script to delete.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{scriptId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteScript(Guid scriptId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // Get the existing script
        var scriptResult = await _dataService.GetSavedScript(scriptId);
        if (!scriptResult.IsSuccess)
        {
            return NotFound("Script not found.");
        }

        var script = scriptResult.Value;

        // Verify the script belongs to the requesting organization
        if (script.OrganizationID != orgId)
        {
            return NotFound("Script not found.");
        }

        // Check user permissions - must be owner or administrator
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }
            var userId = userResult.Value.Id;
            var isAdmin = userResult.Value.IsAdministrator;

            // Check if user is owner or admin
            if (script.CreatorId != userId && !isAdmin)
            {
                return Forbid();
            }
        }
        // API key authentication is considered admin-level access

        // Delete the script (this also deletes associated schedules but preserves results)
        await _dataService.DeleteSavedScript(scriptId);

        return NoContent();
    }

    /// <summary>
    /// Gets the quick scripts for the current user.
    /// </summary>
    /// <returns>A list of quick scripts with content.</returns>
    [HttpGet("quick")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<SavedScript>>> GetQuickScripts()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // Get the user ID
        string? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }
            userId = userResult.Value.Id;
        }
        else
        {
            // For API key authentication, we need to get a user from the organization
            var users = await _dataService.GetAllUsersInOrganization(orgId);
            var adminUser = users.FirstOrDefault(u => u.IsAdministrator);
            if (adminUser == null)
            {
                return Ok(Array.Empty<SavedScript>());
            }
            userId = adminUser.Id;
        }

        var quickScripts = await _dataService.GetQuickScripts(userId);
        return Ok(quickScripts);
    }
}
