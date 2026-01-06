using Remotely.Shared.Extensions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;

namespace Remotely.Server.API;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<DevicesController> _logger;
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHubContext;
    private readonly IAgentHubSessionCache _serviceSessionCache;

    public DevicesController(
        IDataService dataService,
        ILogger<DevicesController> logger,
        IHubContext<AgentHub, IAgentHubClient> agentHubContext,
        IAgentHubSessionCache serviceSessionCache)
    {
        _dataService = dataService;
        _logger = logger;
        _agentHubContext = agentHubContext;
        _serviceSessionCache = serviceSessionCache;
    }


    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public IEnumerable<Device> Get()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Array.Empty<Device>();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return _dataService.GetDevicesForUser($"{User.Identity.Name}");
        }

        // Authorized with API key.  Return all.
        return _dataService.GetAllDevices(orgId);
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> Get(string id)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_dataService.DoesUserHaveAccessToDevice(id, userResult.Value))
            {
                return Unauthorized();
            }
        }

        var deviceResult = await _dataService.GetDevice(orgId, id);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return NotFound();
        }

        return deviceResult.Value;
    }

    [HttpPut]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Update([FromBody] DeviceSetupOptions deviceOptions)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }
        
        if (string.IsNullOrWhiteSpace(deviceOptions?.DeviceID))
        {
            return BadRequest("DeviceId is required.");
        }


        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_dataService.DoesUserHaveAccessToDevice(deviceOptions.DeviceID, userResult.Value))
            {
                return Unauthorized();
            }

        }

        var deviceResult = await _dataService.UpdateDevice(deviceOptions, orgId);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return BadRequest();
        }
        return Created(Request.GetDisplayUrl(), deviceResult.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DeviceSetupOptions deviceOptions)
    {
        var result = await _dataService.CreateDevice(deviceOptions);
        _logger.LogResult(result);

        if (!result.IsSuccess)
        {
            return BadRequest("Device already exists.  Use Put with authorization to update the device.");
        }
        return Created(Request.GetDisplayUrl(), result.Value);
    }

    [HttpDelete("{deviceId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Delete(string deviceId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // 验证设备存在且属于该组织
        var deviceResult = await _dataService.GetDevice(orgId, deviceId);
        if (!deviceResult.IsSuccess)
        {
            return NotFound();
        }

        // 验证用户权限（如果是认证用户）
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess || !_dataService.DoesUserHaveAccessToDevice(deviceId, userResult.Value))
            {
                return Unauthorized();
            }
        }

        _dataService.RemoveDevices(new[] { deviceId });
        return NoContent();
    }

    [HttpDelete("{deviceId}/uninstall")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Uninstall(string deviceId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        // 验证设备存在且属于该组织
        var deviceResult = await _dataService.GetDevice(orgId, deviceId);
        if (!deviceResult.IsSuccess)
        {
            return NotFound();
        }

        var device = deviceResult.Value;

        // 验证用户权限（如果是认证用户）
        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess || !_dataService.DoesUserHaveAccessToDevice(deviceId, userResult.Value))
            {
                return Unauthorized();
            }
        }

        // 检查设备是否在线
        if (!device.IsOnline)
        {
            return BadRequest("Device is not online. Cannot send uninstall command.");
        }

        // 获取设备的 SignalR 连接 ID
        var connectionIds = _serviceSessionCache.GetConnectionIdsByDeviceIds(new[] { deviceId });
        if (!connectionIds.Any())
        {
            return BadRequest("Device connection not found.");
        }

        // 发送卸载命令
        await _agentHubContext.Clients.Clients(connectionIds).UninstallAgent();

        // 从数据库删除设备记录
        _dataService.RemoveDevices(new[] { deviceId });

        return NoContent();
    }
}
