using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Utilities;

namespace Remotely.Server.API;

/// <summary>
/// Controller for managing script schedules via API.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class ScriptSchedulesController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<ScriptSchedulesController> _logger;

    public ScriptSchedulesController(
        IDataService dataService,
        ILogger<ScriptSchedulesController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all script schedules for the organization.
    /// </summary>
    /// <returns>A list of script schedules with related data.</returns>
    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<ScriptScheduleDto>>> GetSchedules()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var schedules = await _dataService.GetScriptSchedules(orgId);
        
        var dtos = new List<ScriptScheduleDto>();
        foreach (var schedule in schedules)
        {
            var dto = MapToDto(schedule);
            // Fetch the script name
            var scriptResult = await _dataService.GetSavedScript(schedule.SavedScriptId);
            if (scriptResult.IsSuccess)
            {
                dto.SavedScriptName = scriptResult.Value.Name;
            }
            dtos.Add(dto);
        }
        
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a specific script schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The ID of the schedule to retrieve.</param>
    /// <returns>The script schedule with related data.</returns>
    [HttpGet("{scheduleId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<ScriptScheduleDto>> GetSchedule(int scheduleId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var result = await _dataService.GetScriptSchedule(scheduleId, orgId);
        
        if (!result.IsSuccess)
        {
            return NotFound("Script schedule not found.");
        }

        var dto = MapToDto(result.Value);
        
        // Fetch the script name
        var scriptResult = await _dataService.GetSavedScript(result.Value.SavedScriptId);
        if (scriptResult.IsSuccess)
        {
            dto.SavedScriptName = scriptResult.Value.Name;
        }
        
        return Ok(dto);
    }


    /// <summary>
    /// Creates a new script schedule.
    /// </summary>
    /// <param name="dto">The schedule data to create.</param>
    /// <returns>The created schedule.</returns>
    [HttpPost]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<ScriptScheduleDto>> CreateSchedule([FromBody] CreateScriptScheduleDto dto)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate that the SavedScript exists
        var scriptResult = await _dataService.GetSavedScript(dto.SavedScriptId);
        if (!scriptResult.IsSuccess)
        {
            return BadRequest("The specified SavedScriptId does not exist.");
        }

        // Verify the script belongs to the same organization
        if (scriptResult.Value.OrganizationID != orgId)
        {
            return BadRequest("The specified SavedScriptId does not exist.");
        }

        // Get the user ID
        string? userId = await GetCurrentUserId(orgId);
        if (userId == null)
        {
            return Unauthorized();
        }

        // Get devices and device groups
        var devices = new List<Device>();
        var deviceGroups = new List<DeviceGroup>();

        if (dto.DeviceIds?.Any() == true)
        {
            devices = _dataService.GetDevices(dto.DeviceIds);
            // Filter to only devices in the same organization
            devices = devices.Where(d => d.OrganizationID == orgId).ToList();
        }

        if (dto.DeviceGroupIds?.Any() == true)
        {
            foreach (var groupId in dto.DeviceGroupIds)
            {
                var groupResult = await _dataService.GetDeviceGroup(groupId);
                if (groupResult.IsSuccess && groupResult.Value.OrganizationID == orgId)
                {
                    deviceGroups.Add(groupResult.Value);
                }
            }
        }

        // Calculate NextRun based on StartAt
        var nextRun = CalculateNextRun(dto.StartAt, dto.Interval);

        var schedule = new ScriptSchedule
        {
            Name = dto.Name,
            SavedScriptId = dto.SavedScriptId,
            StartAt = dto.StartAt,
            Interval = dto.Interval,
            RunOnNextConnect = dto.RunOnNextConnect,
            CreatorId = userId,
            OrganizationID = orgId,
            CreatedAt = Time.Now,
            NextRun = nextRun,
            Devices = devices,
            DeviceGroups = deviceGroups
        };

        await _dataService.AddOrUpdateScriptSchedule(schedule);

        // Retrieve the created schedule with related data
        var createdResult = await _dataService.GetScriptSchedule(schedule.Id, orgId);
        if (!createdResult.IsSuccess)
        {
            _logger.LogError("Failed to retrieve created schedule with ID {ScheduleId}", schedule.Id);
            return StatusCode(500, "Failed to retrieve created schedule.");
        }

        var responseDto = MapToDto(createdResult.Value);
        responseDto.SavedScriptName = scriptResult.Value.Name;
        
        return CreatedAtAction(nameof(GetSchedule), new { scheduleId = schedule.Id }, responseDto);
    }


    /// <summary>
    /// Updates an existing script schedule.
    /// </summary>
    /// <param name="scheduleId">The ID of the schedule to update.</param>
    /// <param name="dto">The updated schedule data.</param>
    /// <returns>The updated schedule.</returns>
    [HttpPut("{scheduleId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult<ScriptScheduleDto>> UpdateSchedule(int scheduleId, [FromBody] UpdateScriptScheduleDto dto)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get the existing schedule
        var scheduleResult = await _dataService.GetScriptSchedule(scheduleId, orgId);
        if (!scheduleResult.IsSuccess)
        {
            return NotFound("Script schedule not found.");
        }

        var schedule = scheduleResult.Value;

        // Check user permissions - must be owner or administrator
        var permissionResult = await CheckUserPermission(schedule.CreatorId, orgId);
        if (!permissionResult.HasPermission)
        {
            return Forbid();
        }

        // Validate SavedScriptId if provided
        if (dto.SavedScriptId.HasValue)
        {
            var scriptResult = await _dataService.GetSavedScript(dto.SavedScriptId.Value);
            if (!scriptResult.IsSuccess || scriptResult.Value.OrganizationID != orgId)
            {
                return BadRequest("The specified SavedScriptId does not exist.");
            }
        }

        // Update only the provided fields
        if (dto.Name != null)
            schedule.Name = dto.Name;
        if (dto.SavedScriptId.HasValue)
            schedule.SavedScriptId = dto.SavedScriptId.Value;
        if (dto.StartAt.HasValue)
            schedule.StartAt = dto.StartAt.Value;
        if (dto.Interval.HasValue)
            schedule.Interval = dto.Interval.Value;
        if (dto.RunOnNextConnect.HasValue)
            schedule.RunOnNextConnect = dto.RunOnNextConnect.Value;

        // Update devices if provided
        if (dto.DeviceIds != null)
        {
            schedule.Devices = new List<Device>();
            if (dto.DeviceIds.Any())
            {
                var devices = _dataService.GetDevices(dto.DeviceIds);
                schedule.Devices = devices.Where(d => d.OrganizationID == orgId).ToList();
            }
        }

        // Update device groups if provided
        if (dto.DeviceGroupIds != null)
        {
            schedule.DeviceGroups = new List<DeviceGroup>();
            if (dto.DeviceGroupIds.Any())
            {
                foreach (var groupId in dto.DeviceGroupIds)
                {
                    var groupResult = await _dataService.GetDeviceGroup(groupId);
                    if (groupResult.IsSuccess && groupResult.Value.OrganizationID == orgId)
                    {
                        schedule.DeviceGroups.Add(groupResult.Value);
                    }
                }
            }
        }

        // Recalculate NextRun if StartAt or Interval changed
        if (dto.StartAt.HasValue || dto.Interval.HasValue)
        {
            schedule.NextRun = CalculateNextRun(schedule.StartAt, schedule.Interval);
        }

        await _dataService.AddOrUpdateScriptSchedule(schedule);

        // Retrieve the updated schedule with related data
        var updatedResult = await _dataService.GetScriptSchedule(schedule.Id, orgId);
        if (!updatedResult.IsSuccess)
        {
            _logger.LogError("Failed to retrieve updated schedule with ID {ScheduleId}", schedule.Id);
            return StatusCode(500, "Failed to retrieve updated schedule.");
        }

        var responseDto = MapToDto(updatedResult.Value);
        
        // Fetch the script name
        var updatedScriptResult = await _dataService.GetSavedScript(schedule.SavedScriptId);
        if (updatedScriptResult.IsSuccess)
        {
            responseDto.SavedScriptName = updatedScriptResult.Value.Name;
        }
        
        return Ok(responseDto);
    }


    /// <summary>
    /// Deletes a script schedule.
    /// </summary>
    /// <param name="scheduleId">The ID of the schedule to delete.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{scheduleId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<ActionResult> DeleteSchedule(int scheduleId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // Get the existing schedule
        var scheduleResult = await _dataService.GetScriptSchedule(scheduleId, orgId);
        if (!scheduleResult.IsSuccess)
        {
            return NotFound("Script schedule not found.");
        }

        var schedule = scheduleResult.Value;

        // Check user permissions - must be owner or administrator
        var permissionResult = await CheckUserPermission(schedule.CreatorId, orgId);
        if (!permissionResult.HasPermission)
        {
            return Forbid();
        }

        await _dataService.DeleteScriptSchedule(scheduleId);

        return NoContent();
    }

    #region Helper Methods

    private async Task<string?> GetCurrentUserId(string orgId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (userResult.IsSuccess)
            {
                return userResult.Value.Id;
            }
        }
        else
        {
            // For API key authentication, get the first admin user from the organization
            var users = await _dataService.GetAllUsersInOrganization(orgId);
            var adminUser = users.FirstOrDefault(u => u.IsAdministrator);
            if (adminUser != null)
            {
                return adminUser.Id;
            }
        }
        return null;
    }

    private async Task<(bool HasPermission, bool IsAdmin)> CheckUserPermission(string creatorId, string orgId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return (false, false);
            }
            var userId = userResult.Value.Id;
            var isAdmin = userResult.Value.IsAdministrator;

            // Check if user is owner or admin
            if (creatorId != userId && !isAdmin)
            {
                return (false, isAdmin);
            }
            return (true, isAdmin);
        }
        // API key authentication is considered admin-level access
        return (true, true);
    }

    private static DateTimeOffset CalculateNextRun(DateTimeOffset startAt, RepeatInterval interval)
    {
        var now = Time.Now;
        
        // If start time is in the future, use it as next run
        if (startAt > now)
        {
            return startAt;
        }

        // Calculate the next run based on interval
        var nextRun = startAt;
        while (nextRun <= now)
        {
            nextRun = interval switch
            {
                RepeatInterval.Hourly => nextRun.AddHours(1),
                RepeatInterval.Daily => nextRun.AddDays(1),
                RepeatInterval.Weekly => nextRun.AddDays(7),
                RepeatInterval.Monthly => nextRun.AddMonths(1),
                _ => nextRun.AddDays(1)
            };
        }

        return nextRun;
    }

    private ScriptScheduleDto MapToDto(ScriptSchedule schedule)
    {
        return new ScriptScheduleDto
        {
            Id = schedule.Id,
            Name = schedule.Name,
            SavedScriptId = schedule.SavedScriptId,
            SavedScriptName = null, // Will be populated below if available
            CreatedAt = schedule.CreatedAt,
            StartAt = schedule.StartAt,
            NextRun = schedule.NextRun,
            LastRun = schedule.LastRun,
            Interval = schedule.Interval,
            RunOnNextConnect = schedule.RunOnNextConnect,
            DeviceIds = schedule.Devices?.Select(d => d.ID).ToList() ?? new List<string>(),
            DeviceGroupIds = schedule.DeviceGroups?.Select(dg => dg.ID).ToList() ?? new List<string>(),
            CreatorName = schedule.Creator?.UserName
        };
    }

    #endregion
}
