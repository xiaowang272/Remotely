using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.API;
using Remotely.Server.Hubs;
using Remotely.Server.Services;
using Remotely.Server.Tests.Mocks;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

/// <summary>
/// Regression tests to verify existing API endpoints continue to work correctly
/// after adding new API endpoints.
/// </summary>
[TestClass]
public class ApiRegressionTests
{
    private IDataService _dataService = null!;
    private TestData _testData = null!;
    private Mock<ILogger<DevicesController>> _devicesLoggerMock = null!;
    private HubContextFixture<AgentHub, IAgentHubClient> _hubContextFixture = null!;
    private Mock<IAgentHubSessionCache> _sessionCacheMock = null!;

    private Mock<ILogger<OrganizationManagementController>> _orgManagementLoggerMock = null!;
    private Mock<ILogger<SavedScriptsController>> _savedScriptsLoggerMock = null!;
    private Mock<IEmailSenderEx> _emailSenderMock = null!;

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
        _devicesLoggerMock = new Mock<ILogger<DevicesController>>();
        _hubContextFixture = new HubContextFixture<AgentHub, IAgentHubClient>();
        _sessionCacheMock = new Mock<IAgentHubSessionCache>();
        _orgManagementLoggerMock = new Mock<ILogger<OrganizationManagementController>>();
        _savedScriptsLoggerMock = new Mock<ILogger<SavedScriptsController>>();
        _emailSenderMock = new Mock<IEmailSenderEx>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    #region DevicesController Regression Tests (Task 10.1)

    [TestMethod]
    [Description("GET /api/Devices - Verify getting device list returns all devices for organization")]
    public void Devices_Get_ReturnsAllDevicesForOrganization()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = controller.Get();

        // Assert
        var devices = result.ToList();
        Assert.AreEqual(2, devices.Count);
        Assert.IsTrue(devices.Any(d => d.ID == _testData.Org1Device1.ID));
        Assert.IsTrue(devices.Any(d => d.ID == _testData.Org1Device2.ID));
    }

    [TestMethod]
    [Description("GET /api/Devices - Verify organization isolation")]
    public void Devices_Get_ReturnsOnlyDevicesForRequestingOrganization()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = controller.Get();

        // Assert
        var devices = result.ToList();
        Assert.IsFalse(devices.Any(d => d.ID == _testData.Org2Device1.ID));
        Assert.IsFalse(devices.Any(d => d.ID == _testData.Org2Device2.ID));
    }

    [TestMethod]
    [Description("GET /api/Devices/{id} - Verify getting single device by ID")]
    public async Task Devices_GetById_ReturnsDevice()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = await controller.Get(_testData.Org1Device1.ID);

        // Assert
        // When returning a value directly, ActionResult<T>.Value contains the result
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(_testData.Org1Device1.ID, result.Value.ID);
    }

    [TestMethod]
    [Description("GET /api/Devices/{id} - Verify 404 for non-existent device")]
    public async Task Devices_GetById_ReturnsNotFoundForNonExistentDevice()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = await controller.Get("non-existent-device-id");

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(NotFoundResult));
    }

    [TestMethod]
    [Description("GET /api/Devices/{id} - Verify organization isolation for single device")]
    public async Task Devices_GetById_ReturnsNotFoundForDeviceInDifferentOrg()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act - Try to get Org2's device with Org1's credentials
        var result = await controller.Get(_testData.Org2Device1.ID);

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(NotFoundResult));
    }

    [TestMethod]
    [Description("PUT /api/Devices - Verify updating device")]
    public async Task Devices_Update_UpdatesDevice()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);
        var deviceOptions = new DeviceSetupOptions
        {
            DeviceID = _testData.Org1Device1.ID,
            DeviceAlias = "Updated Alias"
        };

        // Act
        var result = await controller.Update(deviceOptions);

        // Assert
        Assert.IsInstanceOfType(result, typeof(CreatedResult));
    }

    [TestMethod]
    [Description("PUT /api/Devices - Verify BadRequest for missing DeviceID")]
    public async Task Devices_Update_ReturnsBadRequestForMissingDeviceId()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);
        var deviceOptions = new DeviceSetupOptions
        {
            DeviceID = null
        };

        // Act
        var result = await controller.Update(deviceOptions);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }

    [TestMethod]
    [Description("POST /api/Devices - Verify creating new device")]
    public async Task Devices_Create_CreatesNewDevice()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithFullUrl(controller, _testData.Org1Id);
        var newDeviceId = Guid.NewGuid().ToString();
        var deviceOptions = new DeviceSetupOptions
        {
            DeviceID = newDeviceId,
            DeviceAlias = "New Test Device",
            OrganizationID = _testData.Org1Id
        };

        // Act
        var result = await controller.Create(deviceOptions);

        // Assert
        Assert.IsInstanceOfType(result, typeof(CreatedResult));
        
        // Verify device was created
        var createdDevice = await _dataService.GetDevice(newDeviceId);
        Assert.IsTrue(createdDevice.IsSuccess);
        Assert.AreEqual(newDeviceId, createdDevice.Value!.ID);
    }

    [TestMethod]
    [Description("POST /api/Devices - Verify BadRequest for duplicate device")]
    public async Task Devices_Create_ReturnsBadRequestForDuplicateDevice()
    {
        // Arrange
        var controller = CreateDevicesController();
        SetupRequestWithFullUrl(controller, _testData.Org1Id);
        var deviceOptions = new DeviceSetupOptions
        {
            DeviceID = _testData.Org1Device1.ID, // Already exists
            DeviceAlias = "Duplicate Device",
            OrganizationID = _testData.Org1Id
        };

        // Act
        var result = await controller.Create(deviceOptions);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult));
    }

    #endregion

    #region SavedScriptsController Regression Tests (Task 10.2)

    [TestMethod]
    [Description("GET /api/SavedScripts/{scriptId} - Verify getting script by ID returns script")]
    public async Task SavedScripts_GetScript_ReturnsScript()
    {
        // Arrange
        var savedScript = await CreateTestSavedScript();
        var controller = CreateSavedScriptsController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = await controller.GetScript(savedScript.Id);

        // Assert
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(savedScript.Id, result.Value.Id);
        Assert.AreEqual(savedScript.Name, result.Value.Name);
    }

    [TestMethod]
    [Description("GET /api/SavedScripts/{scriptId} - Verify 404 for non-existent script")]
    public async Task SavedScripts_GetScript_ReturnsNotFoundForNonExistentScript()
    {
        // Arrange
        var controller = CreateSavedScriptsController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = await controller.GetScript(Guid.NewGuid());

        // Assert
        Assert.IsInstanceOfType(result.Result, typeof(NotFoundResult));
    }

    #endregion

    #region OrganizationManagementController Regression Tests (Task 10.3)

    [TestMethod]
    [Description("POST /api/OrganizationManagement/ChangeIsAdmin/{userID} - Verify changing admin status")]
    public async Task OrganizationManagement_ChangeIsAdmin_ChangesAdminStatus()
    {
        // Arrange
        var controller = CreateOrganizationManagementController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act - Make Org1User1 an admin
        var result = await controller.ChangeIsAdmin(_testData.Org1User1.Id, true);

        // Assert
        Assert.IsInstanceOfType(result, typeof(NoContentResult));
    }

    [TestMethod]
    [Description("DELETE /api/OrganizationManagement/DeleteUser/{userID} - Verify deleting user")]
    public async Task OrganizationManagement_DeleteUser_DeletesUser()
    {
        // Arrange
        var controller = CreateOrganizationManagementController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act - Delete Org1User2
        var result = await controller.DeleteUser(_testData.Org1User2.Id);

        // Assert
        Assert.IsInstanceOfType(result, typeof(NoContentResult));
        
        // Verify user was deleted
        var deletedUser = await _dataService.GetUserById(_testData.Org1User2.Id);
        Assert.IsFalse(deletedUser.IsSuccess);
    }

    [TestMethod]
    [Description("GET /api/OrganizationManagement/DeviceGroup - Verify getting device groups")]
    public void OrganizationManagement_GetDeviceGroups_ReturnsDeviceGroups()
    {
        // Arrange
        var controller = CreateOrganizationManagementController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = controller.DeviceGroup();

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        var okResult = (OkObjectResult)result;
        var deviceGroups = (DeviceGroup[])okResult.Value!;
        Assert.AreEqual(2, deviceGroups.Length);
        Assert.IsTrue(deviceGroups.Any(g => g.Name == _testData.Org1Group1.Name));
        Assert.IsTrue(deviceGroups.Any(g => g.Name == _testData.Org1Group2.Name));
    }

    [TestMethod]
    [Description("GET /api/OrganizationManagement/DeviceGroup - Verify organization isolation")]
    public void OrganizationManagement_GetDeviceGroups_ReturnsOnlyOrgDeviceGroups()
    {
        // Arrange
        var controller = CreateOrganizationManagementController();
        SetupRequestWithOrgId(controller, _testData.Org1Id);

        // Act
        var result = controller.DeviceGroup();

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));
        var okResult = (OkObjectResult)result;
        var deviceGroups = (DeviceGroup[])okResult.Value!;
        Assert.IsFalse(deviceGroups.Any(g => g.Name == _testData.Org2Group1.Name));
        Assert.IsFalse(deviceGroups.Any(g => g.Name == _testData.Org2Group2.Name));
    }

    #endregion

    #region Helper Methods

    private DevicesController CreateDevicesController()
    {
        return new DevicesController(
            _dataService,
            _devicesLoggerMock.Object,
            _hubContextFixture.HubContextMock.Object,
            _sessionCacheMock.Object);
    }

    private SavedScriptsController CreateSavedScriptsController()
    {
        return new SavedScriptsController(_dataService, _savedScriptsLoggerMock.Object);
    }

    private OrganizationManagementController CreateOrganizationManagementController()
    {
        var userManager = IoCActivator.ServiceProvider.GetRequiredService<UserManager<RemotelyUser>>();
        return new OrganizationManagementController(
            userManager,
            _dataService,
            _emailSenderMock.Object,
            _orgManagementLoggerMock.Object);
    }

    private async Task<SavedScript> CreateTestSavedScript()
    {
        var savedScript = new SavedScript()
        {
            Content = "Get-ChildItem",
            Creator = _testData.Org1Admin1,
            CreatorId = _testData.Org1Admin1.Id,
            Name = "Test Script",
            Organization = _testData.Org1Admin1.Organization,
            OrganizationID = _testData.Org1Id,
            Shell = Remotely.Shared.Enums.ScriptingShell.PSCore,
            IsPublic = true
        };

        await _dataService.AddOrUpdateSavedScript(savedScript, _testData.Org1Admin1.Id);
        return savedScript;
    }

    private void SetupRequestWithOrgId(ControllerBase controller, string orgId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["OrganizationID"] = orgId;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private void SetupRequestWithFullUrl(ControllerBase controller, string orgId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["OrganizationID"] = orgId;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/api/Devices";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #endregion
}
