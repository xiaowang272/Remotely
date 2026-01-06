using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Remotely.Shared.Utilities;
using Remotely.Shared.Enums;
using Remotely.Server.Auth;
using Remotely.Shared.Helpers;
using Remotely.Shared;
using Remotely.Server.Extensions;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Dtos;

namespace Remotely.Server.API;

[ApiController]
[Route("api/[controller]")]
public class ScriptingController : ControllerBase
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;

    private readonly IDataService _dataService;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IExpiringTokenService _expiringTokenService;
    private readonly ILogger<ScriptingController> _logger;

    private readonly UserManager<RemotelyUser> _userManager;

    public ScriptingController(UserManager<RemotelyUser> userManager,
        IDataService dataService,
        IAgentHubSessionCache serviceSessionCache,
        IExpiringTokenService expiringTokenService,
        IHubContext<AgentHub, IAgentHubClient> agentHub,
        ILogger<ScriptingController> logger)
    {
        _dataService = dataService;
        _serviceSessionCache = serviceSessionCache;
        _expiringTokenService = expiringTokenService;
        _userManager = userManager;
        _agentHubContext = agentHub;
        _logger = logger;
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpPost("[action]/{mode}/{deviceID}")]
    public async Task<ActionResult<ScriptResult>> ExecuteCommand(string mode, string deviceID)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!Enum.TryParse<ScriptingShell>(mode, true, out var shell))
        {
            return BadRequest("Unable to parse shell type.  Use either PSCore, WinPS, Bash, or CMD.");
        }

        var command = string.Empty;
        using (var sr = new StreamReader(Request.Body))
        {
            command = await sr.ReadToEndAsync();
        }

        if (Request.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var username = Request.HttpContext.User.Identity.Name;
            var userResult = await _dataService.GetUserByName($"{username}");

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_dataService.DoesUserHaveAccessToDevice(deviceID, userResult.Value))
            {
                return Unauthorized();
            }
        }

        if (!_serviceSessionCache.TryGetByDeviceId(deviceID, out var device))
        {
            return NotFound();
        }

        if (!_serviceSessionCache.TryGetConnectionId(deviceID, out var connectionId))
        {
            return NotFound();
        }

        if (device.OrganizationID != orgId)
        {
            return Unauthorized();
        }

        var requestID = Guid.NewGuid().ToString();
        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes));

        // TODO: Replace with new invoke capability in .NET 7.
        await _agentHubContext.Clients.Client(connectionId).ExecuteCommandFromApi(
            shell, 
            authToken, 
            requestID, 
            command,
            User?.Identity?.Name ?? "API Key");

        var success = await WaitHelper.WaitForAsync(() => AgentHub.ApiScriptResults.TryGetValue(requestID, out _), TimeSpan.FromSeconds(30));
        if (!success)
        {
            return NotFound();
        }
        AgentHub.ApiScriptResults.TryGetValue(requestID, out var commandId);
        AgentHub.ApiScriptResults.Remove(requestID);

        var scriptResult = await _dataService.GetScriptResult($"{commandId}", orgId);
        if (!scriptResult.IsSuccess)
        {
            return NotFound();
        }
        return scriptResult.Value;
    }

    /// <summary>
    /// Executes a saved script on multiple devices.
    /// </summary>
    /// <param name="dto">The execution request containing SavedScriptId and DeviceIds.</param>
    /// <returns>The ScriptRun ID and execution status.</returns>
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpPost("[action]")]
    public async Task<ActionResult<ExecuteSavedScriptResponse>> ExecuteSavedScript([FromBody] ExecuteSavedScriptDto dto)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate the script exists
        var scriptResult = await _dataService.GetSavedScript(dto.SavedScriptId);
        if (!scriptResult.IsSuccess)
        {
            return NotFound("The specified script was not found.");
        }

        var script = scriptResult.Value;

        // Verify the script belongs to the requesting organization
        if (script.OrganizationID != orgId)
        {
            return NotFound("The specified script was not found.");
        }

        // Get the initiator name
        string initiator = "API Key";
        if (User.Identity?.IsAuthenticated == true)
        {
            initiator = User.Identity.Name ?? "API Key";
        }

        // Filter devices that are accessible and online
        var accessibleDeviceIds = new List<string>();
        var onlineDevices = new List<Device>();

        foreach (var deviceId in dto.DeviceIds)
        {
            // Check if device exists and is accessible
            if (_serviceSessionCache.TryGetByDeviceId(deviceId, out var device))
            {
                // Verify device belongs to the same organization
                if (device.OrganizationID == orgId)
                {
                    accessibleDeviceIds.Add(deviceId);
                    onlineDevices.Add(device);
                }
            }
        }

        // If no accessible devices, return appropriate response
        if (accessibleDeviceIds.Count == 0)
        {
            _logger.LogWarning("ExecuteSavedScript: No accessible online devices found for script {ScriptId}", dto.SavedScriptId);
            return Ok(new ExecuteSavedScriptResponse
            {
                ScriptRunId = 0,
                DeviceCount = 0,
                Status = "NoDevicesAvailable"
            });
        }

        // Create ScriptRun record
        var scriptRun = new ScriptRun
        {
            OrganizationID = orgId,
            SavedScriptId = dto.SavedScriptId,
            Initiator = initiator,
            InputType = ScriptInputType.Api,
            RunAt = DateTimeOffset.UtcNow,
            Devices = onlineDevices,
            RunOnNextConnect = false
        };

        await _dataService.AddScriptRun(scriptRun);

        _logger.LogInformation(
            "Created ScriptRun {ScriptRunId} for script {ScriptId} on {DeviceCount} devices by {Initiator}",
            scriptRun.Id, dto.SavedScriptId, accessibleDeviceIds.Count, initiator);

        // Generate auth token for script execution
        var authToken = _expiringTokenService.GetToken(Time.Now.AddMinutes(AppConstants.ScriptRunExpirationMinutes));

        // Execute script on all accessible devices asynchronously
        var executionTasks = new List<Task>();
        foreach (var deviceId in accessibleDeviceIds)
        {
            if (_serviceSessionCache.TryGetConnectionId(deviceId, out var connectionId))
            {
                var task = _agentHubContext.Clients.Client(connectionId).RunScript(
                    dto.SavedScriptId,
                    scriptRun.Id,
                    initiator,
                    ScriptInputType.Api,
                    authToken);
                executionTasks.Add(task);
            }
        }

        // Fire and forget - don't wait for all executions to complete
        // The results will be collected asynchronously via the agent hub
        _ = Task.WhenAll(executionTasks).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogError(t.Exception, "Error executing script {ScriptId} on devices", dto.SavedScriptId);
            }
        });

        return Ok(new ExecuteSavedScriptResponse
        {
            ScriptRunId = scriptRun.Id,
            DeviceCount = accessibleDeviceIds.Count,
            Status = "Queued"
        });
    }
}
